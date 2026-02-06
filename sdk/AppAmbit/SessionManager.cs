using System.Diagnostics;
using AppAmbit.Models.Responses;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using AppAmbit.Models.Analytics;
using static AppAmbit.FileUtils;
using AppAmbit.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace AppAmbit;

public static class SessionManager
{
    private static IAPIService? _apiService;
    private static IStorageService? _storageService;
    private static string? _sessionId { set; get; } = null;
    public static string? SessionId { get => _sessionId; }
    private static bool _isSessionActive = false;
    public static bool IsSessionActive { get => _isSessionActive; }
    private static readonly SemaphoreSlim _batchLock = new(1, 1);


    internal static void Initialize(IAPIService? apiService, IStorageService? storageService)
    {
        _apiService = apiService;
        _storageService = storageService;
    }

    public static async Task StartSession()
    {
        Debug.WriteLine("StartSession called");
        if (_isSessionActive)
        {
            Debug.WriteLine("There is already an active session");
            return;
        }

        _isSessionActive = true;
        var sessionLocalId = Guid.NewGuid().ToString();
        _sessionId = sessionLocalId;

        var sessionData = new SessionData()
        {
            Id = sessionLocalId,
            SessionId = sessionLocalId,
            SessionType = SessionType.Start,
            Timestamp = DateTime.UtcNow,
        };

        await SendSession(sessionData);
    }

    public static async Task EndSession()
    {
        if (!_isSessionActive)
        {
            Debug.WriteLine("There is no active session to end");
            return;
        }

        _isSessionActive = false;

        SessionData? endSession = new()
        {
            Id = Guid.NewGuid().ToString(),
            SessionType = SessionType.End,
            SessionId = _sessionId,
            Timestamp = DateTime.UtcNow
        };

        _sessionId = null;

        await SendSessionEndOrSaveLocally(endSession);
    }

    public static async Task SendSessionEndOrSaveLocally(SessionData sessionData)
    {
        sessionData.SessionId = sessionData.SessionId.IsUIntNumber() ? sessionData.SessionId : null;

        await SendSession(sessionData);
    }

    public static void SaveEndSession()
    {
        try
        {
            var endSession = new SessionData()
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = _sessionId.IsUIntNumber() ? _sessionId : null,
                Timestamp = DateTime.UtcNow,
                SessionType = SessionType.End
            };

            var json = JsonConvert.SerializeObject(endSession, new JsonSerializerSettings
            {
                Converters = [new StringEnumConverter()],
                Formatting = Formatting.Indented
            });

            SaveToFile<SessionData>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in SaveEndSession: " + ex);
        }
    }

    public static async Task RemoveSavedEndSession()
    {
        await DeleteSingleObject<SessionData>();
    }

    public static async Task SaveSessionEndToDatabaseIfExist()
    {
        var endSession = await GetSavedSingleObject<SessionData>();

        if (endSession is null)
            return;

        await _storageService.SessionData(endSession);
        await DeleteSingleObject<SessionData>();
    }


    public static async Task SendStartSessionIfExist()
    {
        var startSession = await _storageService.GetUnpairedSessionStart();

        if (startSession == null)
        {
            return;
        }

        var resultStart = await _apiService?.ExecuteRequest<SessionResponse>(new StartSessionEndpoint(startSession.Timestamp))!;

        if (resultStart?.ErrorType != ApiErrorType.None)
        {
            Debug.WriteLine("The StartSession could not be sent");
            return;
        }
        _sessionId = resultStart.Data?.SessionId;
        Debug.WriteLine("StartSession sent successfully");

        var session = new List<SessionBatch>
        {
            new() {
                Id = startSession.Id,
                SessionId = _sessionId,
                StartedAt = startSession.Timestamp,
                EndedAt = null
            }
        };

        await _storageService.UpdateSessionIdsForAllTrackingData(session);
        await _storageService.DeleteSessionsList(session);
    }

    public static async Task SendEndSessionFromFile()
    {
        var endSession = await GetSavedSingleObject<SessionData>();

        if (endSession == null)
        {
            return;
        }

        var resultEnd = await _apiService!.ExecuteRequest<EndSessionResponse>(new EndSessionEndpoint(endSession));
        if (resultEnd?.ErrorType == ApiErrorType.None)
        {
            await DeleteSingleObject<SessionData>();
        }
    }

    public static async Task SendEndSessionFromDatabase()
    {
        var endSession = await _storageService.GetUnpairedSessionEnd();

        if (endSession == null)
        {
            return;
        }

        var resultEnd = await _apiService!.ExecuteRequest<EndSessionResponse>(new EndSessionEndpoint(endSession));
        if (resultEnd?.ErrorType == ApiErrorType.None)
        {
            await _storageService.DeleteSessionById(endSession?.Id ?? "");
            Debug.WriteLine("EndSession sent successfully");
        }
    }

    private static async Task SendSession(SessionData sessionData)
    {
        if (sessionData.SessionType == SessionType.Start)
        {
            var resultStart = await _apiService?.ExecuteRequest<SessionResponse>(new StartSessionEndpoint(sessionData.Timestamp))!;

            if (resultStart?.ErrorType != ApiErrorType.None)
            {
                sessionData.SessionId = _sessionId.IsUIntNumber() ? _sessionId : null;
                await _storageService.SessionData(sessionData);
                return;
            }

            Debug.WriteLine("StartSession sent successfully");
            _sessionId = resultStart.Data?.SessionId;
        }
        else
        {
            var resultEnd = await _apiService!.ExecuteRequest<EndSessionResponse>(new EndSessionEndpoint(sessionData));
            if (resultEnd?.ErrorType != ApiErrorType.None)
            {
                await _storageService.SessionData(sessionData);
                Debug.WriteLine("EndSession sent successfully");
            }
        }
    }

    public static async Task SendBatchSessions()
    {
        if (!await _batchLock.WaitAsync(0))
            return;

        try
        {
            Debug.WriteLine("SendBatchSessions started");

            var sessions = await _storageService.GetOldest100SessionsAsync();
            if (sessions.Count == 0)
                return;

            var resolved = await SendBatchAsync(sessions);

            if (resolved.Count > 0)
            {
                await _storageService.UpdateSessionIdsForAllTrackingData(resolved);
                await _storageService.DeleteSessionsList(resolved);
            }

            Debug.WriteLine("SendBatchSessions finished");
        }
        catch (Exception ex)
        {
            Debug.WriteLine("SendBatchSessions error: " + ex);
        }
        finally
        {
            _batchLock.Release();
        }
    }


    private static async Task<List<SessionBatch>> SendBatchAsync(List<SessionBatch> batches)
    {
        if (_apiService == null)
        {
            return [];
        }

        var endpoint = new SessionsPayload { Sessions = batches.ToList() };
        var endpointResult = await _apiService.ExecuteRequest<List<SessionBatch>>(new SessionBatchEndpoint(endpoint));

        if (endpointResult?.ErrorType != ApiErrorType.None)
        {
            return [];
        }

        var serverSessions = endpointResult.Data ?? [];

        var resolved = ResolveSessions(batches, serverSessions);

        return resolved!;
    }

    public static List<SessionBatch?> ResolveSessions(List<SessionBatch> localSessions, List<SessionBatch> serverSessions)
    {
        var localIndex = localSessions
            .Select(local => new
            {
                Finger = FingerPrint(local.StartedAt, local.EndedAt),
                local.Id
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Finger) && !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Finger!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id!, StringComparer.Ordinal);


        var resolved = serverSessions
            .Where(r => !string.IsNullOrWhiteSpace(r.SessionId) &&
                        r.StartedAt != null &&
                        r.EndedAt != null)
            .Select(r =>
            {
                var key = FingerPrint(r.StartedAt, r.EndedAt);

                return localIndex.TryGetValue(key, out var idLocal)
                    ? new SessionBatch
                    {
                        Id = idLocal,
                        SessionId = r.SessionId,
                        StartedAt = r.StartedAt,
                        EndedAt = r.EndedAt
                    }
                    : null;
            })
            .Where(x => x != null)
            .ToList()!;

        return resolved ?? [];
    }

    public static string FingerPrint(DateTime? startedAt, DateTime? endedAt)
    {
        if (startedAt == null || endedAt == null) return "";

        var a = UtcIsoFormatString(NormalizeToUtcSecond(startedAt.Value));
        var b = UtcIsoFormatString(NormalizeToUtcSecond(endedAt.Value));

        return $"{a}-{b}";
    }

    public static DateTime NormalizeToUtcSecond(DateTime date)
    {
        var utc = AsUtcNoDoubleConvert(date);
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
    }

    public static string UtcIsoFormatString(DateTime date)
    {
        var utc = AsUtcNoDoubleConvert(date);
        var trimmed = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
        return trimmed.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static DateTime AsUtcNoDoubleConvert(DateTime dt)
    {
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            _ => dt
        };
    }

}
