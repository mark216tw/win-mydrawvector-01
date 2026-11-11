using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using MyDrawVector.Services;
using MyDrawVector.ViewModels;
using WinRT.Interop;

namespace MyDrawVector;

public partial class App : Application
{
    private Window? _window;
    private ILogger<App>? _logger;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mainInstance = AppInstance.FindOrRegisterForKey("MyDrawVector");
        if (!mainInstance.IsCurrent)
        {
            try
            {
                await mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            }
            finally
            {
                Exit();
            }

            return;
        }

        mainInstance.Activated += OnActivated;

        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyDrawVector",
            "Logs");

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(logFolder));
        });
        services.AddSingleton<SettingsService>();
        services.AddSingleton<FileAssociationService>();
        services.AddSingleton<FileService>();
        services.AddSingleton<ImageAssetStore>();
        services.AddSingleton<AutosaveService>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();
        _logger = Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("MyDrawVector starting");

        var association = Services.GetRequiredService<FileAssociationService>();
        var requestedUnregister = false;
        var cliActionRequested = false;
        foreach (var argument in Environment.GetCommandLineArgs().Skip(1))
        {
            if (string.Equals(argument, "--unregister", StringComparison.OrdinalIgnoreCase))
            {
                requestedUnregister = true;
                cliActionRequested = true;
                TryAssociationAction(association.Unregister, "已移除檔案關聯");
            }
            else if (string.Equals(argument, "--register", StringComparison.OrdinalIgnoreCase))
            {
                cliActionRequested = true;
                TryAssociationAction(association.Register, "已建立檔案關聯");
            }
        }

        if (cliActionRequested)
        {
            Exit();
            return;
        }

        if (!requestedUnregister &&
            !string.Equals(
                Environment.GetEnvironmentVariable("MYDRAWVECTOR_DISABLE_ASSOC"),
                "1",
                StringComparison.Ordinal))
        {
            try
            {
                if (!association.IsRegisteredForCurrentExe())
                {
                    association.Register();
                    _logger.LogInformation("Registered .mydraw file association");
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to register file association");
            }
        }

        MainWindow window;
        try
        {
            _logger.LogInformation("Creating main window");
            window = Services.GetRequiredService<MainWindow>();
            _window = window;
            window.Activate();
            _logger.LogInformation("Main window activated");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Unable to create or activate the main window");
            throw;
        }

        var startupFile = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault(argument =>
                !argument.StartsWith('-') &&
                File.Exists(argument));
        if (!string.IsNullOrWhiteSpace(startupFile))
        {
            _ = window.OpenFileFromStartupAsync(startupFile);
        }
    }

    private void OnActivated(object? sender, AppActivationArguments args)
    {
        if (_window is not MainWindow main)
        {
            return;
        }

        var path = GetActivationFilePath(args);
        main.DispatcherQueue.TryEnqueue(() =>
        {
            if (main.AppWindow.Presenter is OverlappedPresenter presenter &&
                presenter.State == OverlappedPresenterState.Minimized)
            {
                presenter.Restore();
            }

            main.Activate();
            SetForegroundWindow(WindowNative.GetWindowHandle(main));
            if (!string.IsNullOrWhiteSpace(path))
            {
                _ = main.OpenFileFromActivationAsync(path);
            }
        });
    }

    private static string? GetActivationFilePath(AppActivationArguments args)
    {
        if (args.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch &&
            !string.IsNullOrWhiteSpace(launch.Arguments))
        {
            foreach (var part in launch.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = part.Trim('"');
                if (FileAssociationService.IsSupportedFile(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void TryAssociationAction(Action action, string successMessage)
    {
        try
        {
            action();
            _logger?.LogInformation("{Message}", successMessage);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "File association action failed");
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled UI exception");
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.ExceptionObject as Exception, "Unhandled domain exception");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
