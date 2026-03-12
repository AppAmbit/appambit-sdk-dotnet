using System.Diagnostics;
using AppAmbit.Models.Analytics;
using AppAmbit.Models.Responses;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using AppAmbit.Enums;
using static AppAmbit.AppConstants;

namespace AppAmbit;

public static class Analytics
{
    internal static bool _isManualSessionEnabled = false;
    private static IAPIService? _apiService;
    private static IStorageService? _storageService;

    internal static void Initialize(IAPIService? apiService, IStorageService? storageService)
    {
        _apiService = apiService;
        _storageService = storageService;
    }

    public static void EnableManualSession()
    {
        _isManualSessionEnabled = true;
        Debug.WriteLine("Manual Session enabled");
    }

    public static async Task StartSession()
    {
        try
        {
            await SessionManager.StartSession();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during StartSession: {ex}");
        }

    }

    public static async Task EndSession()
    {
        try
        {
            await SessionManager.EndSession();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during EndSession: {ex}");
        }
    }

    public static async void SetUserId(string userId)
    {
        try
        {
            await _storageService.SetUserId(userId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during SetUserId: {ex}");
        }
    }

    public static async Task<string?> GetUserId()
    {
        try
        {
            return await _storageService.GetUserId();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during GetUserId: {ex}");
            return null;
        }
    }

    public static async void SetUserEmail(string userEmail)
    {
        try
        {
            await _storageService.SetUserEmail(userEmail);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during SetUserEmail: {ex}");
        }
    }

    public static async Task<string?> GetUserEmail()
    {
        try
        {
            return await _storageService.GetUserEmail();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during GetUserEmail: {ex}");
            return null;
        }
    }

    public static async Task GenerateTestEvent()
    {
        try
        {
            await SendOrSaveEvent("Test Event", new Dictionary<string, string>()
            {
                { "Event", "Custom event" }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during GenerateTestEvent: {ex}");
        }
    }

    public static async Task TrackEvent(string eventTitle, Dictionary<string, string>? data = null)
    {
        try
        {
            await SendOrSaveEvent(eventTitle, data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during TrackEvent: {ex}");
        }
    }

    private static async Task SendOrSaveEvent(string eventTitle, Dictionary<string, string>? data = null)
    {
        data = (data ?? new Dictionary<string, string>())
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
            .GroupBy(kvp => Truncate(kvp.Key, TrackEventPropertyMaxCharacters))
            .Take(TrackEventMaxPropertyLimit)
            .ToDictionary(
                g => Truncate(g.Key, TrackEventPropertyMaxCharacters),
                g => Truncate(g.First().Value, TrackEventPropertyMaxCharacters)
            );

        eventTitle = Truncate(eventTitle, TrackEventNameMaxLimit);

        var eventRequest = new Event()
        {
            Name = eventTitle,
            Data = data
        };

        var response = _apiService != null
            ? await _apiService.ExecuteRequest<object>(new SendEventEndpoint(eventRequest))
            : null;

        if (response?.ErrorType != ApiErrorType.None)
        {
            var eventEntity = new EventEntity()
            {
                Id = Guid.NewGuid(),
                Name = eventTitle,
                Data = data,
                CreatedAt = DateTime.UtcNow,
                SessionId = SessionManager.SessionId
            };

            if (_storageService != null)
            {
                await _storageService.LogAnalyticsEventAsync(eventEntity);
            }
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    internal static async Task SendBatchEvents()
    {
        try
        {
            Debug.WriteLine("Send Batch Events");
            var eventEntityList = await _storageService.GetOldest100EventsAsync();
            if (eventEntityList?.Count == 0)
            {
                Debug.WriteLine($"No events to send");
                return;
            }

            var endpoint = new EventBatchEndpoint(eventEntityList);
            var eventsBatchResponse = await _apiService?.ExecuteRequest<EventsBatchResponse>(endpoint);
            if (eventsBatchResponse?.ErrorType == ApiErrorType.NetworkUnavailable)
            {
                Debug.WriteLine("Batch of unsent events");
                return;
            }

            await _storageService.DeleteEventList(eventEntityList);
            Debug.WriteLine("Finished Events Batch");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during SendBatchEvents: {ex}");
        }
    }

    public static void ClearToken()
    {
        try
        {
           _apiService?.SetToken("");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during ClearToken: {ex}");
        }
    }

    public async static Task RequestToken()
    {
        try
        {
           await _apiService.GetNewToken();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Analytics] Exception during RequestToken: {ex}");
        }
    }
}