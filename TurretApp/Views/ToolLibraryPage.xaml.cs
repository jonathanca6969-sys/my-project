using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TurretApp.Models;

namespace TurretApp.Views;

public partial class ToolLibraryPage : Page
{
    private List<InventoryTool> _allTools = [];
    private InventoryTool? _selectedTool;

    public ToolLibraryPage()
    {
        InitializeComponent();
        _allTools = App.State.ToolInventory;
        CboStation.SelectedIndex = 0;
        CboCategory.SelectedIndex = 0;
        CboCondition.SelectedIndex = 0;
        ApplyFilters();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (CboStation == null || CboCategory == null || CboCondition == null) return;

        var station = (CboStation.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        var category = (CboCategory.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        var condition = (CboCondition.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        var search = TxtSearch.Text?.Trim().ToLowerInvariant() ?? "";

        var filtered = _allTools.Where(t =>
        {
            if (station != "All" && t.Station != station) return false;
            if (category != "All" && t.Category != category) return false;
            if (condition != "All" && !t.Condition.Equals(condition, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(search))
            {
                var haystack = $"{t.Station} {t.ToolType} {t.Shape} {t.Size} {t.Condition} {t.Notes}".ToLowerInvariant();
                if (!haystack.Contains(search)) return false;
            }
            return true;
        }).ToList();

        DgTools.ItemsSource = filtered;
        TxtResultCount.Text = $"{filtered.Count} tools";
    }

    private void OnToolSelected(object sender, SelectionChangedEventArgs e)
    {
        _selectedTool = DgTools.SelectedItem as InventoryTool;
        if (_selectedTool == null)
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            return;
        }

        DetailPanel.Visibility = Visibility.Visible;
        TxtDetailTitle.Text = $"SELECTED: {_selectedTool.DisplayName}";
        TxtEditNotes.Text = _selectedTool.Notes;

        // Set condition combo to match
        for (int i = 0; i < CboEditCondition.Items.Count; i++)
        {
            if (CboEditCondition.Items[i] is ComboBoxItem item &&
                item.Content?.ToString() == _selectedTool.Condition)
            {
                CboEditCondition.SelectedIndex = i;
                break;
            }
        }
    }

    private void OnSaveDetail(object sender, RoutedEventArgs e)
    {
        if (_selectedTool == null) return;

        var condItem = CboEditCondition.SelectedItem as ComboBoxItem;
        if (condItem != null)
            _selectedTool.Condition = condItem.Content?.ToString() ?? _selectedTool.Condition;

        _selectedTool.Notes = TxtEditNotes.Text;
        App.SaveState();
        ApplyFilters();
    }

    private void OnDeleteTool(object sender, RoutedEventArgs e)
    {
        if (_selectedTool == null) return;

        var result = MessageBox.Show(
            $"Delete {_selectedTool.DisplayName}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _allTools.Remove(_selectedTool);
            _selectedTool = null;
            DetailPanel.Visibility = Visibility.Collapsed;
            App.SaveState();
            ApplyFilters();
        }
    }
}
