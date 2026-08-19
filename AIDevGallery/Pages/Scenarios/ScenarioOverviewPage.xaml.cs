// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Samples;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AIDevGallery.Pages;

internal sealed partial class ScenarioOverviewPage : Page
{
    internal record FilterRecord(string? Tag, string Text);

    private readonly List<FilterRecord> filters =
    [
        new(null, "All"),
        new("npu", "NPU"),
        new("gpu", "GPU"),
        new("windows-ai-api", "Windows AI API")
    ];

    private readonly ObservableCollection<ScenarioCategory> allScenarioCategories;
    private ScenarioCategory? selectedCategory;

    public ScenarioOverviewPage()
    {
        this.InitializeComponent();
        allScenarioCategories = new ObservableCollection<ScenarioCategory>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        allScenarioCategories.Clear();
        selectedCategory = e.Parameter as ScenarioCategory;

        if (selectedCategory == null)
        {
            OverviewHeader.Visibility = Visibility.Visible;
            CategoryFilter.Visibility = Visibility.Collapsed;
            allView.Visibility = Visibility.Visible;
            EmptyStateText.Visibility = Visibility.Collapsed;

            foreach (var category in ScenarioCategoryHelpers.AllScenarioCategories)
            {
                allScenarioCategories.Add(category);
            }

            return;
        }

        OverviewHeader.Visibility = Visibility.Collapsed;
        CategoryFilter.Visibility = Visibility.Visible;
        AutomationProperties.SetName(FilterComboBox, $"Filter {selectedCategory.Name} samples by hardware");
        ApplyFilter(filters[FilterComboBox.SelectedIndex < 0 ? 0 : FilterComboBox.SelectedIndex], false);
    }

    private void ScenarioItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is Scenario scenario)
        {
            App.MainWindow.NavigateToPage(scenario);
        }
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (selectedCategory != null && e.AddedItems.FirstOrDefault() is FilterRecord filter)
        {
            ApplyFilter(filter, true);
        }
    }

    private void ApplyFilter(FilterRecord filter, bool announce)
    {
        var scenarios = selectedCategory!.Scenarios
            .Where(scenario => MatchesFilter(GetModelsForScenario(scenario), filter.Tag))
            .ToList();

        allScenarioCategories.Clear();
        if (scenarios.Count > 0)
        {
            allScenarioCategories.Add(new ScenarioCategory
            {
                Name = $"{selectedCategory.Name} Samples",
                Icon = selectedCategory.Icon,
                Description = selectedCategory.Description,
                Scenarios = scenarios
            });
        }

        allView.Visibility = scenarios.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Visibility = scenarios.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Text = $"No {selectedCategory.Name} samples match the {filter.Text} filter.";

        if (announce)
        {
            var message = scenarios.Count == 0
                ? EmptyStateText.Text
                : $"{scenarios.Count} {selectedCategory.Name} sample{(scenarios.Count == 1 ? string.Empty : "s")} match the {filter.Text} filter.";
            NarratorHelper.Announce(FilterComboBox, message, "scenarioFilterResults");
        }
    }

    internal static bool MatchesFilter(IEnumerable<ModelDetails> models, string? filter)
    {
        return filter switch
        {
            null => true,
            "gpu" => models.Any(model => model.HardwareAccelerators.Contains(HardwareAccelerator.DML)
                || model.HardwareAccelerators.Contains(HardwareAccelerator.GPU)),
            "npu" => models.Any(model => (model.HardwareAccelerators.Contains(HardwareAccelerator.QNN)
                || model.HardwareAccelerators.Contains(HardwareAccelerator.NPU))
                && !model.Url.StartsWith("file", StringComparison.InvariantCultureIgnoreCase)),
            "windows-ai-api" => models.Any(model => model.Url.StartsWith("file", StringComparison.InvariantCultureIgnoreCase)),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown scenario filter.")
        };
    }

    private static List<ModelDetails> GetModelsForScenario(Scenario scenario)
    {
        List<ModelDetails> modelDetails = [];
        foreach (var sample in SampleDetails.Samples.Where(sample => sample.Scenario == scenario.ScenarioType))
        {
            modelDetails.AddRange(ModelDetailsHelper.GetModelDetails(sample)
                .SelectMany(details => details)
                .GroupBy(detail => detail.Key)
                .Select(group => group.First().Value)
                .SelectMany(models => models));
        }

        return modelDetails;
    }
}