using Foxnet.Desktop.Services;
using Foxnet.Desktop.ViewModels;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace Foxnet.Desktop;

public partial class App : Application
{
    private Mutex? _instance;
    private bool _ownsInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var data = AppPaths.DataDirectory;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToUpperInvariant())));
        _instance = new Mutex(false, @"Local\Foxnet.Desktop." + hash);
        try
        {
            _ownsInstance = _instance.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            _ownsInstance = true;
        }

        if (!_ownsInstance)
        {
            MessageBox.Show("FOXNET Desktop уже открыт.", "FOXNET Desktop",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var shell = new ShellViewModel(Path.Combine(data, "session.db"), data, new WindowsConsoleHost());
        MainWindow = new MainWindow { DataContext = shell };
        MainWindow.Show();
        shell.InitializeCommand.Execute(null);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (MainWindow?.DataContext is ShellViewModel shell)
            shell.Dispose();
        if (_ownsInstance)
            _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
