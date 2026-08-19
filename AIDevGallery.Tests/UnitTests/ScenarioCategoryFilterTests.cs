// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Pages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.Tests.UnitTests;

[TestClass]
public class ScenarioCategoryFilterTests
{
    [TestMethod]
    public void MatchesFilterUsesExistingHardwareRules()
    {
        var cpu = new ModelDetails { Url = "https://example.com/cpu", HardwareAccelerators = [HardwareAccelerator.CPU] };
        var gpu = new ModelDetails { Url = "https://example.com/gpu", HardwareAccelerators = [HardwareAccelerator.DML] };
        var npu = new ModelDetails { Url = "https://example.com/npu", HardwareAccelerators = [HardwareAccelerator.QNN] };
        var windowsAiApi = new ModelDetails { Url = "file:///windows-ai-api", HardwareAccelerators = [HardwareAccelerator.NPU] };

        Assert.IsTrue(ScenarioOverviewPage.MatchesFilter([cpu], null));
        Assert.IsTrue(ScenarioOverviewPage.MatchesFilter([gpu], "gpu"));
        Assert.IsFalse(ScenarioOverviewPage.MatchesFilter([cpu], "gpu"));
        Assert.IsTrue(ScenarioOverviewPage.MatchesFilter([npu], "npu"));
        Assert.IsFalse(ScenarioOverviewPage.MatchesFilter([windowsAiApi], "npu"));
        Assert.IsTrue(ScenarioOverviewPage.MatchesFilter([windowsAiApi], "windows-ai-api"));
        Assert.IsFalse(ScenarioOverviewPage.MatchesFilter([cpu], "windows-ai-api"));
    }
}