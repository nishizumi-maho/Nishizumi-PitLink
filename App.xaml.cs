using System.Windows;
using NishizumiPitLink.ViewModels;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace NishizumiPitLink;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private MainViewModel? _viewModel;
    private Views.MainWindow? _mainWindow;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            System.Windows.MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception}",
                "Nishizumi PitLink — error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _viewModel = new MainViewModel();
        _mainWindow = new Views.MainWindow(_viewModel);
        _mainWindow.Closing += (_, args) =>
        {
            if (_isExiting) return;
            args.Cancel = true;
            _mainWindow.Hide();
        };

        SetupTrayIcon();

        var startMinimized = e.Args.Contains("--minimized");
        if (!startMinimized)
            _mainWindow.Show();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = "Nishizumi PitLink",
        };

        var menu = new Forms.ContextMenuStrip();
        var showItem = menu.Items.Add("Open");
        showItem.Click += (_, _) => ShowMainWindow();

        var toggleItem = menu.Items.Add("Toggle automatic switching");
        toggleItem.Click += (_, _) =>
        {
            if (_viewModel is not null) _viewModel.GlobalEnabled = !_viewModel.GlobalEnabled;
        };

        menu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = menu.Items.Add("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static Drawing.Icon LoadAppIcon()
    {
        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        return Drawing.Icon.ExtractAssociatedIcon(exePath) ?? Drawing.SystemIcons.Application;
    }

    private void ShowMainWindow()
    {
        _mainWindow?.Show();
        _mainWindow?.Activate();
        if (_mainWindow?.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _trayIcon?.Dispose();
        _viewModel?.Dispose();
        _mainWindow?.Close();
        Shutdown();
    }
}
