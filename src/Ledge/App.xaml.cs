using System.IO;
using System.Windows;
using Ledge.Core.Abstractions;
using Ledge.Core.Models;
using Ledge.Core.Services;
using Ledge.Services;
using Ledge.ViewModels;
using Ledge.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ledge;

/// <summary>
/// Application entry point: makes sure only one copy runs, loads the tasks,
/// wires up all services (dependency injection), then shows the pill.
/// </summary>
/// <remarks>
/// Dependency injection (DI) in one sentence: instead of each class doing
/// <c>new SomeService()</c> itself, every class lists what it needs in its
/// constructor, and the "container" below builds and hands those over.
/// https://learn.microsoft.com/dotnet/core/extensions/dependency-injection
/// </remarks>
public sealed partial class App : Application
{
    /// <summary>Folder under %LOCALAPPDATA% that holds tasks.json (ARCHITECTURE.md §5).</summary>
    private const string DataFolderName = "Ledge";

    /// <summary>ADR-0005: the longest we wait on exit for the last save.</summary>
    private static readonly TimeSpan ExitSaveTimeout = TimeSpan.FromSeconds(2);

    private SingleInstance? _singleInstance;
    private IHost? _host;

    /// <summary>The open Settings window, or null.</summary>
    private SettingsWindow? _settingsWindow;

    private static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolderName);

    /// <remarks>
    /// <c>async void</c> is normally a bad idea (nobody can await it or catch its
    /// exceptions), but it's the accepted pattern for event-style overrides like
    /// this one. WPF keeps running its message loop while we await the load.
    /// </remarks>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstance();
        if (!_singleInstance.IsFirstInstance)
        {
            // Ledge is already running: ask it to open its panel, then leave (PRD F5.2).
            SingleInstance.AskRunningInstanceToOpenPanel();
            Shutdown();
            return;
        }

        var time = TimeProvider.System;
        StartLogging(time);

        DisableXamlAnimationsIfTurnedOff();

        // The tasks must be loaded before the board (and everything showing it)
        // exists, so load first, then register the result in the container.
        var store = new JsonTaskStore(DataFolder, time);
        var loaded = await store.LoadAsync();
        LogLoad(loaded);

        // Settings too: the hotkey is registered as soon as the window exists.
        var settingsStore = new JsonSettingsStore(DataFolder, time);
        var settings = await settingsStore.LoadAsync();

        // Read after DisableXamlAnimationsIfTurnedOff, so it's 0 when animations are off.
        var motion = new TaskListMotion(((Duration)FindResource("TaskRemoveDuration")).TimeSpan);

        _host = BuildHost(time, store, loaded, settingsStore, settings, motion);
        await _host.StartAsync();

        WatchForSaveFailures();
        SyncStartWithWindows();

        // Before any window is created, so everything starts in the right colours.
        _host.Services.GetRequiredService<ThemeService>().Start();

        // Nobody else asks for it, so create it here; from now on it listens.
        _host.Services.GetRequiredService<DayRolloverService>();

        var window = _host.Services.GetRequiredService<PillWindow>();
        window.Show();

        ShowTrayIcon(window);

        // When a focus session ends: notification + panel line (the service), pill pulse (the window).
        _host.Services.GetRequiredService<FocusCompletionService>().Completed += (_, _) => window.PulsePill();

        // Step out of the way of full-screen apps (PRD F1.6).
        var fullscreen = _host.Services.GetRequiredService<FullscreenWatcher>();
        fullscreen.Changed += (_, _) =>
        {
            _log.LogInformation("Full-screen app in front: {Fullscreen}", fullscreen.IsFullscreenAppInFront);
            window.SetHiddenForFullscreen(fullscreen.IsFullscreenAppInFront);
        };
        fullscreen.Start();

        // Later launches of Ledge open this panel. Their signal arrives on a
        // thread-pool thread, so hop to the UI thread first.
        _singleInstance.OpenPanelRequested += (_, _) => Dispatcher.BeginInvoke(window.OpenPanel);
        _singleInstance.ListenForOtherLaunches();
    }

    private void ShowTrayIcon(PillWindow window)
    {
        var tray = _host!.Services.GetRequiredService<TrayService>();
        tray.ToggleRequested += (_, _) => window.TogglePanelFromTray();
        tray.OpenRequested += (_, _) => window.OpenPanel();
        tray.SettingsRequested += (_, _) => ShowSettings();
        tray.NotificationClicked += (_, _) => window.OpenPanel();
        tray.QuitRequested += async (_, _) => await QuitAsync();
        tray.Show();
    }

    /// <summary>Tray → Settings: one Settings window at a time; a second request brings it to the front.</summary>
    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = _host!.Services.GetRequiredService<SettingsWindow>();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Makes the Windows Run entry match settings.json (PRD F5.3). When it's on,
    /// the path is rewritten, so it follows Ledge.exe if it moved.
    /// </summary>
    private void SyncStartWithWindows()
    {
        var startup = _host!.Services.GetRequiredService<IStartupRegistration>();
        var wanted = _host.Services.GetRequiredService<SettingsService>().Current.StartWithWindows;

        if (wanted || startup.IsEnabled)
        {
            startup.SetEnabled(wanted);
        }
    }

    /// <summary>
    /// Tray → Quit, the only way to exit (PRD F5.4). Unlike OnExit, this can
    /// await the final save properly (ADR-0005), so OnExit then has nothing left to do.
    /// </summary>
    private async Task QuitAsync()
    {
        _log.LogInformation("Quit requested");

        if (_host?.Services.GetService<TaskAutoSaver>() is { } saver)
        {
            await saver.FlushAsync();
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        FlushPendingSave();

        // Disposes every singleton that is IDisposable (hotkey, screen events,
        // autosaver, tray icon — which removes it from the tray at once).
        _host?.Dispose();

        _singleInstance?.Dispose();

        // Last, so everything above can still log. Writes out queued lines (ADR-0005).
        if (_logs is not null)
        {
            _log.LogInformation("Ledge stopped");
            _logs.Dispose();
        }

        base.OnExit(e);
    }

    /// <summary>
    /// The service registrations: "when a class asks for X, give it this".
    /// Singleton = one shared instance for the whole app.
    /// </summary>
    private IHost BuildHost(
        TimeProvider time,
        ITaskStore store,
        TaskLoadResult loaded,
        ISettingsStore settingsStore,
        AppSettings settings,
        TaskListMotion motion)
    {
        // https://learn.microsoft.com/dotnet/core/extensions/generic-host
        var builder = Host.CreateApplicationBuilder();
        ConfigureLogging(builder.Logging);
        var services = builder.Services;

        // Core
        services.AddSingleton(time);
        services.AddSingleton(store);
        services.AddSingleton(loaded);
        services.AddSingleton(_ => new TaskBoard(time, loaded.Document));
        services.AddSingleton<TaskAutoSaver>();
        services.AddSingleton<LocalDayWatcher>();
        services.AddSingleton<FocusTimer>();
        services.AddSingleton<IFocusTimer>(sp => sp.GetRequiredService<FocusTimer>());

        // Settings: SettingsService owns the live values and saves every change.
        services.AddSingleton(settingsStore);
        services.AddSingleton(_ => new SettingsService(settingsStore, settings));

        // UI services. One HotkeyService, reachable both as itself (PillWindow
        // listens for Pressed) and as IGlobalHotkey (Settings changes it).
        services.AddSingleton<ScreenService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<IGlobalHotkey>(sp => sp.GetRequiredService<HotkeyService>());
        services.AddSingleton<IStartupRegistration, StartupService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<DayRolloverService>();
        services.AddSingleton<TrayService>();
        services.AddSingleton<FocusCompletionService>();
        services.AddSingleton<FullscreenWatcher>();

        // ViewModels and windows. Settings is "transient": a fresh window (and
        // ViewModel) each time it's opened, since a closed WPF window can't be shown again.
        services.AddSingleton(motion);

        // Resolved while starting up, on the UI thread, so Current is the UI
        // thread's context: timer updates get posted back to it.
        services.AddSingleton(sp => new TimerViewModel(sp.GetRequiredService<IFocusTimer>(), SynchronizationContext.Current));
        services.AddSingleton<TaskListViewModel>();
        services.AddSingleton<PillViewModel>();
        services.AddSingleton<PillWindow>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsWindow>();

        return builder.Build();
    }

    /// <summary>
    /// DESIGN.md §4: with Windows "Animation effects" off, every animation is
    /// instant. Code-behind animations check this themselves (PillWindow.Scale);
    /// XAML storyboards read their Duration from a resource, so we replace that
    /// resource with zero before any window loads. A key added directly to
    /// Application.Resources wins over the same key in a merged dictionary.
    /// (Read once at start-up; changing the setting needs a restart for these.)
    /// https://learn.microsoft.com/dotnet/api/system.windows.systemparameters.clientareaanimation
    /// </summary>
    private void DisableXamlAnimationsIfTurnedOff()
    {
        if (SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        Resources["TaskDoneDuration"] = new Duration(TimeSpan.Zero);
        Resources["InboxChevronDuration"] = new Duration(TimeSpan.Zero);
        Resources["TaskRemoveDuration"] = new Duration(TimeSpan.Zero);
        Resources["PillLabelDuration"] = new Duration(TimeSpan.Zero); // also the pill's width change
    }

    /// <summary>
    /// Creates the autosaver (so it starts listening to the board) and logs
    /// failed saves. Showing the user a message needs copy from DESIGN.md (step 3d).
    /// </summary>
    private void WatchForSaveFailures()
    {
        var services = _host!.Services;
        var logger = services.GetRequiredService<ILogger<App>>();

        services.GetRequiredService<TaskAutoSaver>().SaveFailed += (_, ex) =>
            logger.LogError(ex, "Saving tasks failed; will retry on the next change");

        services.GetRequiredService<SettingsService>().SaveFailed += (_, ex) =>
            logger.LogError(ex, "Saving settings failed; the change applies until Ledge restarts");
    }

    /// <summary>
    /// ADR-0005: the ONLY blocking wait in the app. OnExit can't be async, and a
    /// change made in the last 300 ms would otherwise never reach the disk.
    /// It runs on a thread-pool thread (Task.Run) so it can't deadlock on the UI
    /// thread, and gives up after 2 s; the atomic save means that can't corrupt the file.
    /// </summary>
    private void FlushPendingSave()
    {
        var saver = _host?.Services.GetService<TaskAutoSaver>();
        if (saver is null || !saver.HasUnsavedChanges)
        {
            return;
        }

        Task.Run(saver.FlushAsync).Wait(ExitSaveTimeout);
    }
}
