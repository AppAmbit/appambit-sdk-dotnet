using System.Diagnostics;
using AppAmbit.Enums;
using AppAmbit.Models.Breadcrumbs;
using AppAmbit.Models.Responses;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using static AppAmbit.FileUtils;

namespace AppAmbit;

public static class BreadcrumbManager
{
    private static IAPIService? _api;
    private static IStorageService? _storage;
    private static readonly object _lastLock = new();
    private static string? _lastBreadcrumb;
    private static long _lastBreadcrumbAtMs;

    public static void Initialize(IAPIService api, IStorageService storage)
    {
        _api = api;
        _storage = storage;
    }

    public static async Task AddAsync(string name)
    {
        if (IsDuplicate(name)) return;
        var entity = CreateBreadcrumb(name);
        await SendBreadcumbs(entity);
    }

    public static void SaveFile(string name)
    {
        if (IsDuplicate(name)) return;
        var breadcrumb = CreateBreadcrumb(name);
        var data = breadcrumb.ToData(sessionId: SessionManager.SessionId);
        GetSaveJsonArray(BreadcrumbsConstants.nameFile, data);
    }

    public static void LoadBreadcrumbsFromFile()
    {
        try
        {
            var files = GetSaveJsonArray<BreadcrumbData>(BreadcrumbsConstants.nameFile, null);
            var notSent = new List<BreadcrumbData>();

            if (files == null || files.Count == 0) return;

            string? lastName = null;
            long lastMs = 0;

            foreach (var item in files)
            {
                if (item == null) continue;
                try
                {
                    var entity = item.ToEntity();
                    var name = entity.Name ?? string.Empty;
                    var createdMs = new DateTimeOffset(entity.CreatedAt).ToUnixTimeMilliseconds();

                    if (lastName != null && name == lastName && (createdMs - lastMs) < 2000) continue;

                    AsyncHelpers.RunSync(() => _storage!.AddBreadcrumbAsync(entity));
                    lastName = name;
                    lastMs = createdMs;
                }
                catch
                {
                    notSent.Add(item);
                }
            }

            UpdateJsonArray(BreadcrumbsConstants.nameFile, notSent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }
    }

    internal static BreadcrumbsEntity CreateBreadcrumb(string name)
    {
        return new BreadcrumbsEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            SessionId = SessionManager.SessionId ?? "",
            CreatedAt = DateTime.UtcNow
        };
    }

    public static async Task SendBatchBreadcrumbs()
    {
        try
        {
            var items = await _storage.GetOldest100BreadcrumbsAsync();
            if (items == null || items.Count == 0) return;

            var itemsData = items
                .Select(b => b.ToData(sessionId: b.SessionId ?? string.Empty))
                .ToList();

            var endpoint = new BreadcrumbsBatchEndpoint(itemsData ?? []);
            var responseBatch = await _api.ExecuteRequest<Response>(endpoint);

            if (responseBatch?.ErrorType != ApiErrorType.None) return;

            await _storage.DeleteBreadcrumbs(items);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("SendBatchBreadcrumbs error: " + ex);
        }
    }

    private static async Task<bool> TrySendAsync(BreadcrumbsEntity entity)
    {
        if (_api == null) return false;
        try
        {
            var ep = new BreadcrumbEndpoint(entity);
            var result = await _api.ExecuteRequest<Response>(ep);
            return result.ErrorType == ApiErrorType.None;
        }
        catch
        {
            return false;
        }
    }

    private static async Task SendBreadcumbs(BreadcrumbsEntity entity)
    {
        try
        {
            var sent = await TrySendAsync(entity);
            if (!sent && _storage != null)
            {
                await _storage.AddBreadcrumbAsync(entity);
            }
        }catch (Exception ex)
        {
            Debug.WriteLine("SendBreadcumbs error: " + ex);
        }
    }

    private static bool IsDuplicate(string name)
    {
        lock (_lastLock)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var dup = _lastBreadcrumb == name && (now - _lastBreadcrumbAtMs) < 2000;
            if (!dup)
            {
                _lastBreadcrumb = name;
                _lastBreadcrumbAtMs = now;
            }
            return dup;
        }
    }
}
