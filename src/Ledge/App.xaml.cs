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
    /// <summary>
    /// "Local\" scopes the name to the current Windows user session, so two
    /// people signed in on the same PC can each run their own Ledge.
    /// https://learn.microsoft.com/windows/win32/termserv/kernel-object-namespaces
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\Ledge-SingleInstance";

    /// <summary>Folder under %LOCALAPPDATA% that holds tasks.json (ARCHITECTURE.md §5).</summary>
    private const string DataFolderName = "Ledge";

    /// <summary>ADR-0005: the longest we wait on exit for the last save.</summary>
    private static readonly TimeSpan ExitSaveTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Kept in a field on purpose: if it were a local variable, the garbage
    /// collector could free it and release the lock while the app is still running.
    /// </summary>
    private Mutex? _singleInstanceMutex;

    private IHost? _host;

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

        // Try to create (and own) the named mutex. If it already exists,
        // another Ledge is running in this session.
        // https://learn.microsoft.com/dotnet/api/system.threading.mutex.-ctor#system-threading-mutex-ctor(system-boolean-system-string-system-boolean@)
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);

        if (!createdNew)
        {
            // M4: tell the running copy to open its panel before exiting.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        DisableXamlAnimationsIfTurnedOff();

        // The tasks must be loaded before the board (and everything showing it)
        // exists, so load first, then register the result in the container.
        var time = TimeProvider.System;
        var store = new JsonTaskStore(DataFolder, time);
        var loaded = await store.LoadAsync();

        _host = BuildHost(time, store, loaded);
        await _host.StartAsync();

        WatchForSaveFailures();
        _host.Services.GetRequiredService<PillWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        FlushPendingSave();

        // Disposes every singleton that is IDisposable (hotkey, screen events, autosaver).
        _host?.Dispose();

        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    /// <summary>
    /// The service registrations: "when a class asks for X, give it this".
    /// Singleton = one shared instance for the whole app.
    /// </summary>
    private static IHost BuildHost(TimeProvider time, ITaskStore store, TaskLoadResult loaded)
    {
        // https://learn.microsoft.com/dotnet/core/extensions/generic-host
        var builder = Host.CreateApplicationBuilder();
        var services = builder.Services;

        // Core
        services.AddSingleton(time);
        services.AddSingleton(store);
        services.AddSingleton(loaded);
        services.AddSingleton(_ => new TaskBoard(time, loaded.Document));
        services.AddSingleton<TaskAutoSaver>();

        // UI services
        services.AddSingleton<ScreenService>();
        services.AddSingleton<HotkeyService>();

        // ViewModels and windows
        services.AddSingleton<TaskListViewModel>();
        services.AddSingleton<PillWindow>();

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
