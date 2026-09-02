// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Utils;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AIDevGallery.Samples.SharedCode;

internal static class WinMLHelpers
{
    // ponytail: the AMD GPU EP (Name "MIGraphXExecutionProvider", backed by amdgpu-ep.dll) access-violates
    // on load (0xc0000005 in amdgpu_ep!CreateEpFactories) inside EnsureReadyAsync/TryRegister with WinML AMD
    // GPU EP 1.8.60.0 on this machine. A native AV can't be caught in managed code, so registering it takes
    // the whole process down when a sample loads a model. Name is the only identity populated before
    // EnsureReadyAsync (PackageId/LibraryPath stay empty until then), so skip on Name. CPU/DML/QNN/OpenVINO
    // and the AMD NPU (VitisAI) still register. Upgrade path: delete the MIGraphX skip and call
    // catalog.EnsureAndRegisterCertifiedAsync() once the AMD GPU EP no longer crashes on load.
    private const string AmdGpuProviderName = "MIGraphXExecutionProvider";

    public static async System.Threading.Tasks.Task EnsureAndRegisterCertifiedEpsAsync(Microsoft.Windows.AI.MachineLearning.ExecutionProviderCatalog catalog)
    {
        foreach (var provider in catalog.FindAllProviders())
        {
            if (provider.Certification != Microsoft.Windows.AI.MachineLearning.ExecutionProviderCertification.Certified)
            {
                continue;
            }

            if (string.Equals(provider.Name, AmdGpuProviderName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"Skipping AMD GPU execution provider '{provider.Name}' (known amdgpu-ep.dll load crash).");
                continue;
            }

            try
            {
                await provider.EnsureReadyAsync();
                provider.TryRegister();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Skipping execution provider '{provider.Name}': {ex.Message}");
            }
        }
    }

    public static bool AppendExecutionProviderFromEpName(this SessionOptions sessionOptions, string epName, string? deviceType)
    {
        if (epName == "CPU")
        {
            // No need to append CPU execution provider
            return true;
        }

        var environment = OrtEnv.Instance();
        var epDeviceMap = GetEpDeviceMap();

        if (epDeviceMap.TryGetValue(epName, out var devices))
        {
            Dictionary<string, string> epOptions = new(StringComparer.OrdinalIgnoreCase);
            switch (epName)
            {
                case "DmlExecutionProvider":
                    // Configure performance mode for Dml EP
                    // Dml some times have multiple devices which cause exception, we pick the first one here
                    sessionOptions.AppendExecutionProvider(environment, [devices[0]], epOptions);
                    return true;
                case "OpenVINOExecutionProvider":
                    var device = devices.Where(d => d.HardwareDevice.Type.ToString().Equals(deviceType, StringComparison.Ordinal)).FirstOrDefault();
                    sessionOptions.AppendExecutionProvider(environment, [device], epOptions);
                    return true;
                case "QNNExecutionProvider":
                    // Configure performance mode for QNN EP
                    epOptions["htp_performance_mode"] = "high_performance";
                    break;
                default:
                    break;
            }

            sessionOptions.AppendExecutionProvider(environment, devices, epOptions);
            return true;
        }

        return false;
    }

    public static string? GetCompiledModel(this SessionOptions sessionOptions, string modelPath, string device)
    {
        if (IsCompileModelSupported(device) == false)
        {
            return null;
        }

        var compiledModelPath = Path.Combine(Path.GetDirectoryName(modelPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelPath)) + $".{device}.onnx";

        if (!File.Exists(compiledModelPath))
        {
            try
            {
                using OrtModelCompilationOptions compilationOptions = new(sessionOptions);
                compilationOptions.SetInputModelPath(modelPath);
                compilationOptions.SetOutputModelPath(compiledModelPath);
                compilationOptions.CompileModel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Model compilation failed for {device}: {ex.Message}");

                // Clean up any empty or corrupted files that may have been created
                if (File.Exists(compiledModelPath))
                {
                    try
                    {
                        File.Delete(compiledModelPath);
                        Debug.WriteLine($"Deleted corrupted compiled model file: {compiledModelPath}");
                    }
                    catch
                    {
                        // Ignore deletion errors
                    }
                }

                return null;
            }
        }

        // Validate that the compiled model file exists and is not empty
        if (File.Exists(compiledModelPath))
        {
            var fileInfo = new FileInfo(compiledModelPath);
            if (fileInfo.Length > 0)
            {
                return compiledModelPath;
            }
        }

        return null;
    }

    public static Dictionary<string, List<OrtEpDevice>> GetEpDeviceMap()
    {
        IReadOnlyList<OrtEpDevice> epDevices = DeviceUtils.GetEpDevices();
        Dictionary<string, List<OrtEpDevice>> epDeviceMap = new(StringComparer.OrdinalIgnoreCase);

        foreach (OrtEpDevice device in epDevices)
        {
            string name = device.EpName;

            if (!epDeviceMap.TryGetValue(name, out List<OrtEpDevice>? value))
            {
                value = [];
                epDeviceMap[name] = value;
            }

            value.Add(device);
        }

        return epDeviceMap;
    }

    /// <summary>
    /// Determines whether model compilation should be surfaced based on device type.
    /// </summary>
    /// <param name="deviceType">Device type string (e.g., "CPU", "GPU", "NPU").</param>
    /// <returns>False for CPU; true for other known accelerator types.</returns>
    public static bool IsCompileModelSupported(string? deviceType)
    {
        if (string.IsNullOrWhiteSpace(deviceType))
        {
            return false;
        }

        // NOTE: Skip compilation for the CPU execution provider.
        // - EPContext is an EP-specific offline-compiled/partitioned graph artifact that requires the
        //   execution provider to implement serialization/deserialization of its optimized graph.
        // - ONNX Runtime's CPU EP does NOT implement EPContext model generation or loading. Invoking
        //   OrtModelCompilationOptions.CompileModel() for CPU attempts to emit a "*.CPU.onnx" EPContext
        //   artifact, which fails (commonly with InvalidProtobuf) because no EPContext is produced/understood
        //   by the CPU EP.
        // Behavior:
        // - For CPU, we return null here so callers fall back to the original ONNX model without attempting
        //   EPContext compilation.
        // - Other EPs (e.g., DirectML, OpenVINO, QNN) may support EPContext depending on the ORT build,
        //   platform drivers, and hardware; for those we allow compilation to proceed.
        if (string.Equals(deviceType, "CPU", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(deviceType, "GPU", StringComparison.OrdinalIgnoreCase)
            || string.Equals(deviceType, "NPU", StringComparison.OrdinalIgnoreCase);
    }
}