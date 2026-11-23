using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NEREA.App.ViewModels;
using NEREA.Core.Models;

namespace NEREA.App.Views;

/// <summary>
/// The premium aquarium configuration panel: saved aquariums, worlds,
/// multi-species populations and atmosphere.
/// </summary>
public sealed partial class AquariumPanel : UserControl
{
    private AquariumViewModel? _vm;

    public AquariumPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as AquariumViewModel;
        if (_vm is not null && _vm.SelectedWorld is not null)
            WorldsList.SelectedItem = _vm.SelectedWorld;
        MarkAtmosphere();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if ((Application.Current as App)?.Services.GetService(typeof(MainViewModel)) is MainViewModel main)
            main.ClosePanelsPublic();
    }

    // ── Presets ───────────────────────────────────────────────────────────────

    private async void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var name = await PromptAsync("Save aquarium", "Name this aquarium");
        if (!string.IsNullOrWhiteSpace(name)) _vm.SavePreset(name);
    }

    private void ApplyFirstPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (PresetsList.SelectedItem is AquariumPreset preset)
            _vm.ApplyPreset(preset);
        else if (_vm.Presets.Count > 0)
            _vm.ApplyPreset(_vm.Presets[0]);
    }

    private void Preset_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_vm is null) return;
        if ((e.OriginalSource as FrameworkElement)?.DataContext is AquariumPreset preset)
            _vm.ApplyPreset(preset);
    }

    private void Preset_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_vm is null) return;
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not AquariumPreset preset) return;
        var menu = new MenuFlyout();
        menu.Items.Add(MakeItem("Apply", _ => _vm.ApplyPreset(preset)));
        menu.Items.Add(MakeItem("Delete", _ => _vm.DeletePreset(preset)));
        menu.ShowAt(PanelRoot, e.GetPosition(PanelRoot));
        e.Handled = true;
    }

    // ── Worlds ────────────────────────────────────────────────────────────────

    private void Category_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        _vm.WorldCategoryFilter = CategoryCombo.SelectedIndex switch
        {
            1 => "Freshwater",
            2 => "Ocean",
            3 => "Exotic",
            _ => "All"
        };
    }

    private void World_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        if (WorldsList.SelectedItem is WorldDefinition world && world.Id != _vm.SelectedWorld?.Id)
            _vm.SelectWorld(world);
    }

    // ── Population ────────────────────────────────────────────────────────────

    private static SpeciesRow? RowFrom(object sender) =>
        ((sender as FrameworkElement)?.DataContext as FrameworkElement)?.DataContext as SpeciesRow
        ?? (sender as FrameworkElement)?.DataContext as SpeciesRow;

    private void IncrementSpecies_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.IncrementSpecies(RowFrom(sender));
    }

    private void DecrementSpecies_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.DecrementSpecies(RowFrom(sender));
    }

    private void RemoveSpecies_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.RemoveSpecies(RowFrom(sender));
    }

    private void AddSpecies_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (AddSpeciesCombo.SelectedItem is SpeciesDefinition def)
            _vm.AddSpecies(def.Id);
    }

    // ── Atmosphere ────────────────────────────────────────────────────────────

    private void Atmosphere_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (int.TryParse((sender as FrameworkElement)?.Tag as string, out int idx))
            _vm.SetAtmosphere(idx);
        MarkAtmosphere();
    }

    private void MarkAtmosphere()
    {
        int idx = _vm?.AtmosphereIndex ?? 1;
        SetActive(CalmButton, idx == 0);
        SetActive(BalancedButton, idx == 1);
        SetActive(VividButton, idx == 2);
    }

    private static void SetActive(Button b, bool active) =>
        b.BorderBrush = active
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightBlue)
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 0x26, 0xFF, 0xFF));

    // ── Dialogs / helpers ─────────────────────────────────────────────────────

    private async Task<string?> PromptAsync(string title, string placeholder)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            XamlRoot = XamlRoot,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        var box = new TextBox { PlaceholderText = placeholder };
        dialog.Content = box;
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? box.Text.Trim() : null;
    }

    private static MenuFlyoutItem MakeItem(string text, Action<RoutedEventArgs> action)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, e) => action(e);
        return item;
    }
}
