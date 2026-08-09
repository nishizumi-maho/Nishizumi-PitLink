using System.IO;
using System.Threading;
using System.Windows;
using NishizumiPitLink.ViewModels;
using Velopack;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace NishizumiPitLink;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\NishizumiPitLink.SingleInstance";

    private Forms.NotifyIcon? _trayIcon;
    private MainViewModel? _viewModel;
    private Views.MainWindow? _mainWindow;
    private Mutex? _singleInstanceMutex;
    private bool _isExiting;

    /// <summary>
    /// Custom entry point (see StartupObject in the csproj) so Velopack gets first crack at argv: on
    /// the install/update/uninstall hooks it invokes the exe with, this handles the hook and exits
    /// before any UI is touched.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Two copies would fight over the wheelbase and stack up tray icons - easy to hit when the
        // app is set to start with Windows and then also launched by hand.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isOnlyInstance);
        if (!isOnlyInstance)
        {
            System.Windows.MessageBox.Show(
                "Nishizumi PitLink is already running — look for it in the system tray, next to the clock.",
                "Nishizumi PitLink",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            ReportUnhandledException(args.Exception);
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

        var updateItem = menu.Items.Add("Check for updates");
        updateItem.Click += (_, _) => _viewModel?.CheckForUpdatesCommand.Execute(null);

        menu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = menu.Items.Add("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    /// <summary>
    /// Shows something a human can act on and puts the full stack trace in a log file, rather than
    /// filling a message box with an unreadable wall of frames.
    /// </summary>
    private static void ReportUnhandledException(Exception ex)
    {
        string? logPath = null;
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NishizumiPitLink");
            Directory.CreateDirectory(dir);
            logPath = Path.Combine(dir, "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            logPath = null; // logging is best effort - never let it mask the original error
        }

        var details = logPath is null ? string.Empty : $"\n\nFull details were saved to:\n{logPath}";
        System.Windows.MessageBox.Show(
            $"{ex.Message}{details}\n\nThe app is still running.",
            "Nishizumi PitLink — error",
            MessageBoxButton.OK, MessageBoxImage.Error);
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

        if (_singleInstanceMutex is not null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch { /* not held - nothing to release */ }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        Shutdown();
    }
}
