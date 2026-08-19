// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Linq;

namespace AIDevGallery.ViewModels;

internal sealed class OnnxModelPickerRow : ObservableObject
{
    private bool _isSelected;

    public ModelDetails ModelDetails { get; }
    public DownloadableModel Download { get; }
    public bool IsInstalled { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                RefreshState();
            }
        }
    }

    public float Progress => Download.Progress;
    public string? VerificationFailureMessage => Download.VerificationFailureMessage;
    public bool IsDownloading => !IsInstalled &&
        !Download.CanDownload &&
        Download.Status is DownloadStatus.Waiting or DownloadStatus.InProgress or DownloadStatus.Verifying;
    public bool CanRetry => !IsInstalled && Download.Status == DownloadStatus.VerificationFailed;
    public bool CanDownload => !IsInstalled &&
        !IsDownloading &&
        !CanRetry &&
        Download.Status != DownloadStatus.Completed;
    public bool CanSelect => IsInstalled && !IsSelected;
    public bool ShowSelected => IsInstalled && IsSelected;
    public bool IsDownloadEnabled => Download.IsDownloadEnabled;
    public string SelectAccessibleName => $"Select {ModelDetails.Name}";
    public string DownloadAccessibleName => $"Download {ModelDetails.Name}";
    public string CancelAccessibleName => $"Cancel download of {ModelDetails.Name}";
    public string RetryAccessibleName => $"Retry download of {ModelDetails.Name}";

    public string DeviceText => string.Join(
        ", ",
        ModelDetails.HardwareAccelerators.Select(AppUtils.GetHardwareAcceleratorString));

    public string SizeText => AppUtils.FileSizeToString(ModelDetails.Size);

    public string StateText
    {
        get
        {
            if (IsInstalled || Download.Status == DownloadStatus.Completed)
            {
                return IsSelected ? "Selected" : "Installed";
            }

            if (Download.Status == DownloadStatus.VerificationFailed)
            {
                return "Download failed";
            }

            if (IsDownloading)
            {
                return Download.Status switch
                {
                    DownloadStatus.Verifying => "Verifying",
                    DownloadStatus.Waiting => "Pending",
                    _ => $"{Progress:0}%"
                };
            }

            return "Not installed";
        }
    }

    public string AccessibleName => $"{ModelDetails.Name}, {DeviceText}, {SizeText}, {StateText}";

    public OnnxModelPickerRow(ModelDetails modelDetails, bool isInstalled)
    {
        ModelDetails = modelDetails;
        IsInstalled = isInstalled;
        Download = new DownloadableModel(modelDetails);
        Download.PropertyChanged += Download_PropertyChanged;
    }

    public void StartDownload() => Download.StartDownload();

    public void CancelDownload() => Download.CancelDownload();

    private void Download_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshState();
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(VerificationFailureMessage));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanSelect));
        OnPropertyChanged(nameof(ShowSelected));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(AccessibleName));
    }
}