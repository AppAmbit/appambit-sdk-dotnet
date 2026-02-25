using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace AppAmbitAvalonia;

public static partial class AppAmbitSdk
{
    static partial void PlatformStartHooks()
    {
        try
        {
            var app = Avalonia.Application.Current;
            if (app == null) return;

            if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownRequested += (_, __) =>
                {
                    try { AppAmbit.AppAmbitSdk.End(); } catch { }
                };

                if (desktop.MainWindow != null)
                    AttachToWindowClosed(desktop.MainWindow);
                else
                    _ = AttachWhenMainWindowAvailableAsync(desktop, TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit.Avalonia] Error hooking lifecycle: {ex}");
        }
    }

    private static void AttachToWindowClosed(Window? window)
    {
        if (window == null) return;

        try { window.Closed -= OnMainWindowClosed; } catch { }
        window.Closed += OnMainWindowClosed;
    }

    private static void OnMainWindowClosed(object? sender, EventArgs e)
    {
        try { AppAmbit.AppAmbitSdk.End(); }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit.Avalonia] Error on MainWindow.Closed: {ex}");
        }
    }

    private static async Task AttachWhenMainWindowAvailableAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                if (desktop.MainWindow != null)
                {
                    AttachToWindowClosed(desktop.MainWindow);
                    return;
                }
            }
            catch { }

            await Task.Delay(100);
        }
    }
}