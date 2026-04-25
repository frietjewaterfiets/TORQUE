using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace TORQUE;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppInfo.DataDirectoryName,
        "logs");

    private static readonly string LogFilePath = Path.Combine(LogDirectory, "app.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterGlobalErrorHandlers();
        WriteLog("Application starting.");
        base.OnStartup(e);
    }

    private void RegisterGlobalErrorHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        HandleFatalException("A startup error occurred.", e.Exception);
        e.Handled = true;
        Shutdown(-1);
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            HandleFatalException("An unexpected application error occurred.", exception);
        }
        else
        {
            WriteLog($"Unhandled non-exception object: {e.ExceptionObject}");
        }
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleFatalException("A background task failed.", e.Exception);
        e.SetObserved();
    }

    private static void HandleFatalException(string title, Exception exception)
    {
        WriteLog($"{title}{Environment.NewLine}{exception}");

        try
        {
            MessageBox.Show(
                $"{title}{Environment.NewLine}{Environment.NewLine}Details were written to:{Environment.NewLine}{LogFilePath}",
                $"{AppInfo.DisplayName} Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // If WPF cannot show a dialog, we still keep the log on disk.
        }
    }

    private static void WriteLog(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(
                LogFilePath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Logging should never crash the app.
        }
    }
}

