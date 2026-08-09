using System.Windows;
using System.Windows.Controls;
using NishizumiPitLink.Services;
using NishizumiPitLink.ViewModels;

namespace NishizumiPitLink.Views;

public partial class ProfilePickerWindow : Window
{
    private readonly List<PitHousePresetSummary> _allPresets;

    public ProfilePickResult? Result { get; private set; }

    public ProfilePickerWindow()
    {
        InitializeComponent();

        _allPresets = PitHousePresetImporter.ListPresets();
        PresetList.ItemsSource = _allPresets;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var term = SearchBox.Text?.Trim() ?? string.Empty;
        PresetList.ItemsSource = string.IsNullOrEmpty(term)
            ? _allPresets
            : _allPresets.Where(p =>
                p.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                p.Devices.Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private void PresetList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PresetList.SelectedItem is PitHousePresetSummary) Use_Click(sender, e);
    }

    private void Use_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedItem is PitHousePresetSummary preset)
        {
            Result = new ProfilePickResult(preset.FilePath, preset.Name);
            DialogResult = true;
        }
        else
        {
            System.Windows.MessageBox.Show(this, "Select a Pit House preset first.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
