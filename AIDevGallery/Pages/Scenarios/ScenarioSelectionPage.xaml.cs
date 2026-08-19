// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Models;
using AIDevGallery.Samples;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;

namespace AIDevGallery.Pages;

internal sealed partial class ScenarioSelectionPage : Page
{
    public ScenarioSelectionPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        SetUpScenarios();

        NavigatedToPageEvent.Log(nameof(ScenarioSelectionPage));

        this.NavView.Loaded += (sender, args) =>
        {
            HandleNavigation(e.Parameter);
        };
        base.OnNavigatedTo(e);
    }

    public void HandleNavigation(object? obj)
    {
        Scenario? scenario = null;
        SampleNavigationArgs? sampleArgs = null;

        if (obj is Scenario sc)
        {
            scenario = sc;
        }
        else if (obj is MostRecentlyUsedItem mru)
        {
            scenario = App.FindScenarioById(mru.ItemId);
        }
        else if (obj is Sample sample)
        {
            scenario = ScenarioCategoryHelpers.AllScenarioCategories.SelectMany(sc => sc.Scenarios).FirstOrDefault(s => s.ScenarioType == sample.Scenario);
        }
        else if (obj is SampleNavigationArgs args)
        {
            sampleArgs = args;
            scenario = ScenarioCategoryHelpers.AllScenarioCategories.SelectMany(sc => sc.Scenarios).FirstOrDefault(s => s.ScenarioType == args.Sample.Scenario);
        }

        if (scenario == null)
        {
            SelectAndNavigate(NavView.MenuItems.OfType<NavigationViewItem>().First(), null);
            return;
        }

        var category = ScenarioCategoryHelpers.AllScenarioCategories.First(category => category.Scenarios.Contains(scenario));
        var categoryItem = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .First(item => ReferenceEquals(item.Tag, category));

        NavView.SelectedItem = categoryItem;
        NavigateToScenario(scenario, sampleArgs);
    }

    private void SelectAndNavigate(NavigationViewItem item, ScenarioCategory? category)
    {
        NavView.SelectedItem = item;
        NavFrame.Navigate(typeof(ScenarioOverviewPage), category);
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item)
        {
            return;
        }

        if (item.Tag is ScenarioCategory category)
        {
            SelectAndNavigate(item, category);
        }
        else if (item.Tag is string tag && tag == "Overview")
        {
            SelectAndNavigate(item, null);
        }
    }

    public void ShowHideNavPane()
    {
        NavView.OpenPaneLength = NavView.OpenPaneLength == 0 ? 276 : 0;
    }

    private void SetUpScenarios()
    {
        NavView.MenuItems.Clear();
        NavView.MenuItems.Add(new NavigationViewItem() { Content = "Overview", Icon = new FontIcon() { Glyph = "\uF0E2" }, Tag = "Overview" });
        NavView.MenuItems.Add(new NavigationViewItemSeparator());
        foreach (var scenarioCategory in ScenarioCategoryHelpers.AllScenarioCategories)
        {
            var categoryMenu = new NavigationViewItem() { Content = scenarioCategory.Name, Icon = new FontIcon() { Glyph = scenarioCategory.Icon }, Tag = scenarioCategory };
            ToolTip categoryToolTip = new() { Content = scenarioCategory.Name };
            ToolTipService.SetToolTip(categoryMenu, categoryToolTip);
            NavView.MenuItems.Add(categoryMenu);
        }
    }

    private void NavigateToScenario(Scenario scenario, SampleNavigationArgs? sampleArgs = null)
    {
        if (sampleArgs != null)
        {
            NavFrame.Navigate(typeof(ScenarioPage), sampleArgs);
        }
        else
        {
            NavFrame.Navigate(typeof(ScenarioPage), scenario);
        }
    }
}