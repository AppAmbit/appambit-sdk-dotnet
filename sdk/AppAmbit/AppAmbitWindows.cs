using System;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace AppAmbit
{
    internal static class AppAmbitWindows
    {
        private static string? _appKey;
        private static bool _initialized = false;
        private static bool _sessionStarted = false;

        public static void Register(string appKey)
        {
            try
            {
                if (_initialized) return;
                _initialized = true;
                _appKey = appKey;

                AppDomain.CurrentDomain.ProcessExit += OnAppExit;

                RegisterNetworkEvents();

                StartAppSessionSafe(_appKey);
            }catch (Exception ex)
            {
                Log($"Error during AppAmbit registration: {ex.Message}");
            }
        }

        private static void StartAppSessionSafe(string appKey)
        {
            if (_sessionStarted) return;

            _sessionStarted = true;
            AppAmbitSdk.InternalStart(appKey);
        }

        private static void OnAppExit(object? sender, EventArgs e)
        {
            AppAmbitSdk.InternalEnd();
        }

        private static void RegisterNetworkEvents()
        {
            NetworkChange.NetworkAvailabilityChanged += async (s, e) =>
            {
                bool isOnline = e.IsAvailable;
                Log(isOnline ? "Network: Online" : "Network: Offline");

                await BreadcrumbManager.AddAsync(
                    isOnline ? BreadcrumbsConstants.online : BreadcrumbsConstants.offline
                );
            };

            NetworkChange.NetworkAddressChanged += (s, e) =>
            {
                Log("Network Address Changed");
            };
        }

        private static void Log(string message)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}