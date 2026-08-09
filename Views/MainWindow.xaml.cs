using System.Windows;
using NishizumiPitLink.ViewModels;

namespace NishizumiPitLink.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestProfilePick += OnRequestProfilePick;
    }

    private ProfilePickResult? OnRequestProfilePick()
    {
        var dialog = new ProfilePickerWindow { Owner = this };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
}
