// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Controls;
using AIDevGallery.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIDevGallery.Tests.UnitTests.Controls.ModelPicker;

[TestClass]
public class ModelSelectionItemTests
{
    [TestMethod]
    public void SelectionChangesAreCommittedOrDiscardedExplicitly()
    {
        var firstModel = new ModelDetails { Id = "first", Name = "First model", Url = "first" };
        var secondModel = new ModelDetails { Id = "second", Name = "Second model", Url = "second" };
        var item = new ModelSelectionItem([ModelType.MultimodalModels]);

        item.SetAppliedSelection(firstModel);
        Assert.IsFalse(item.HasPendingChange);

        item.SelectedModel = secondModel;
        Assert.IsTrue(item.HasPendingChange);
        Assert.AreEqual("Pending selection", item.SelectionStatusText);

        item.DiscardPendingSelection();
        Assert.AreSame(firstModel, item.SelectedModel);
        Assert.IsFalse(item.HasPendingChange);

        item.SelectedModel = secondModel;
        item.CommitSelection();
        Assert.AreSame(secondModel, item.SelectedModel);
        Assert.IsFalse(item.HasPendingChange);
    }
}