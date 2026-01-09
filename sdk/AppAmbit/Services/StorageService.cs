using System.Diagnostics;
using AppAmbit.Enums;
using AppAmbit.Models;
using AppAmbit.Models.Analytics;
using AppAmbit.Models.App;
using AppAmbit.Models.Breadcrumbs;
using AppAmbit.Models.Logs;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using SQLite;

namespace AppAmbit.Services;

public class StorageService : IStorageService
{
    private SQLiteAsyncConnection _database;

    public async Task InitializeAsync()
    {
        if (_database is not null)
        {
            return;
        }

        Debug.WriteLine($"DatabasePath: {AppConstants.DatabasePath}");
        _database = new SQLiteAsyncConnection(AppConstants.DatabasePath, AppConstants.Flags, storeDateTimeAsTicks: false);
        await _database.CreateTableAsync<AppSecrets>();
        await _database.CreateTableAsync<LogEntity>();
        await _database.CreateTableAsync<EventEntity>();
        await _database.CreateTableAsync<SessionBatch>();
        await _database.CreateTableAsync<BreadcrumbsEntity>();
        await EnsureAppSecretsColumns();
    }

    private async Task EnsureAppSecretsColumns()
    {
        // SQLite-net does not add columns on CreateTable when the table already exists.
        // These migrations are idempotent and will no-op after the first run.
        var alterCommands = new[]
        {
            "ALTER TABLE AppSecrets ADD COLUMN DeviceToken TEXT",
            "ALTER TABLE AppSecrets ADD COLUMN PushEnabled INTEGER"
        };

        foreach (var sql in alterCommands)
        {
            try
            {
                await _database.ExecuteAsync(sql);
            }
            catch (SQLiteException)
            {
                // Column already exists or table missing; ignore to keep migration idempotent.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StorageService] Error running migration '{sql}': {ex.Message}");
            }
        }
    }

    public async Task SetDeviceId(string? deviceId)
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();

        if (appSecrets != null)
        {
            appSecrets.DeviceId = deviceId;
            await _database.UpdateAsync(appSecrets);
        }
        else
        {
            appSecrets = new AppSecrets()
            {
                DeviceId = deviceId
            };

            await _database.InsertAsync(appSecrets);
        }
    }

    public async Task<string?> GetDeviceId()
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();
        return appSecrets?.DeviceId;
    }

    public async Task SetAppId(string? appId)
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();
        if (appSecrets != null)
        {
            appSecrets.AppId = appId;
            await _database.UpdateAsync(appSecrets);
        }
        else
        {
            appSecrets = new AppSecrets
            {
                AppId = appId
            };
            await _database.InsertAsync(appSecrets);
        }
    }

    public async Task<string?> GetAppId()
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();
        return appSecrets?.AppId;
    }

    public async Task SetUserId(string userId)
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();

        if (appSecrets != null)
        {
            appSecrets.UserId = userId;
            await _database.UpdateAsync(appSecrets);
        }
        else
        {
            appSecrets = new AppSecrets { UserId = userId };
            await _database.InsertAsync(appSecrets);
        }
    }

    public async Task<string?> GetUserId()
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();
        return appSecrets?.UserId;
    }

    public async Task SetUserEmail(string? userEmail)
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();

        if (appSecrets != null)
        {
            appSecrets.UserEmail = userEmail;
            await _database.UpdateAsync(appSecrets);
        }
        else
        {
            appSecrets = new AppSecrets() { UserEmail = userEmail };
            await _database.InsertAsync(appSecrets);
        }
    }

    public async Task<string?> GetUserEmail()
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();
        return appSecrets?.UserEmail;
    }

    public async Task<string?> GetConsumerId()
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();
        return appSecrets?.ConsumerId ?? "";
    }

    public async Task SetConsumerId(string consumerId)
    {

        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();

        if (appSecrets != null)
        {
            appSecrets.ConsumerId = consumerId ?? "";
            await _database.UpdateAsync(appSecrets);
        }
        else
        {
            appSecrets = new AppSecrets
            {
                ConsumerId = consumerId
            };
            await _database.InsertAsync(appSecrets);
        }
    }

    public async Task<string?> GetPushDeviceToken()
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();
        return appSecrets?.DeviceToken;
    }

    public async Task SetPushDeviceToken(string? token)
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();

        if (appSecrets != null)
        {
            appSecrets.DeviceToken = token;
            await _database.UpdateAsync(appSecrets);
        }
        else
        {
            appSecrets = new AppSecrets
            {
                DeviceToken = token
            };
            await _database.InsertAsync(appSecrets);
        }
    }

    public async Task<bool?> GetPushEnabled()
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();
        return appSecrets?.PushEnabled;
    }

    public async Task SetPushEnabled(bool enabled)
    {
        var appSecrets = await _database.Table<AppSecrets>().FirstOrDefaultAsync();

        if (appSecrets != null)
        {
            appSecrets.PushEnabled = enabled;
            await _database.UpdateAsync(appSecrets);
        }
        else
        {
            appSecrets = new AppSecrets
            {
                PushEnabled = enabled
            };
            await _database.InsertAsync(appSecrets);
        }
    }

    public async Task LogEventAsync(LogEntity logEntity)
    {
        await _database.InsertAsync(logEntity);
    }

    public async Task LogAnalyticsEventAsync(EventEntity analyticsLog)
    {
        await _database.InsertAsync(analyticsLog);
    }

    public async Task<List<LogEntity>> GetOldest100LogsAsync()
    {
        return await _database.QueryAsync<LogEntity>(
            @"SELECT * FROM LogEntity
                WHERE SessionId IS NOT NULL
                    AND TRIM(SessionId) <> ''
                    AND SessionId GLOB '[1-9][0-9]*'
                ORDER BY CreatedAt
                LIMIT 100;");
    }

    public async Task DeleteLogList(List<LogEntity> logs)
    {
        var ids = logs.Select(log => log.Id).ToList();
        await _database.RunInTransactionAsync(tran =>
        {
            foreach (var id in ids)
            {
                tran.Execute("DELETE FROM LogEntity WHERE Id = ?", id);
            }
        });
    }

    public async Task DeleteAllLogs()
    {
        await _database.DeleteAllAsync<Log>();
    }

    public async Task<List<EventEntity>> GetOldest100EventsAsync()
    {
        return await _database.QueryAsync<EventEntity>(
            @"SELECT * FROM EventEntity
            WHERE SessionId IS NOT NULL
                AND TRIM(SessionId) <> ''
                AND SessionId GLOB '[1-9][0-9]*'
            ORDER BY CreatedAt
            LIMIT 100;");
    }

    public async Task DeleteEventList(List<EventEntity> logs)
    {
        var ids = logs.Select(log => log.Id).ToList();
        await _database.RunInTransactionAsync(tran =>
        {
            foreach (var id in ids)
            {
                tran.Execute("DELETE FROM EventEntity WHERE Id = ?", id);
            }
        });
    }

    public async Task SessionData(SessionData sessionData)
    {
        var endSession = await _database.Table<SessionBatch>()
                .Where(sb => sb.EndedAt == null)
                .FirstOrDefaultAsync();

        if (endSession != null)
        {
            try
            {
                endSession.EndedAt = sessionData.Timestamp;
                await _database.UpdateAsync(endSession);
                Debug.WriteLine("Session end updated");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex.Message}");
            }
        }

        if (sessionData?.SessionType == SessionType.Start)
        {
            var sessionBatch = new SessionBatch
            {
                Id = sessionData.Id,
                SessionId = sessionData.SessionId,
                StartedAt = sessionData.Timestamp
            };

            await _database.InsertAsync(sessionBatch);
            Debug.WriteLine("Session start created");
            return;
        }


        if (endSession == null)
        {
            var sessionEnd = new SessionBatch
            {
                Id = sessionData.Id,
                SessionId = sessionData.SessionId,
                EndedAt = sessionData.Timestamp
            };

            await _database.InsertAsync(sessionEnd);
            Debug.WriteLine("Session end created");
        }
    }

    public async Task<List<SessionBatch>> GetOldest100SessionsAsync()
    {
        return await _database.Table<SessionBatch>()
            .Where(session => session.StartedAt != null && session.EndedAt != null)
            .OrderBy(session => session.StartedAt)
            .Take(100)
            .ToListAsync();
    }

    public async Task DeleteSessionsList(List<SessionBatch> sessions)
    {
        var ids = sessions.Select(session => session.Id).ToList();

        await _database.RunInTransactionAsync(tran =>
        {
            foreach (var id in ids)
            {
                tran.Execute("DELETE FROM SessionEntity WHERE Id = ?", id);
            }
        });
    }

    public async Task<SessionData?> GetUnpairedSessionStart()
    {
        var session = await _database.Table<SessionBatch>()
        .Where(s => s.StartedAt != null && s.EndedAt == null)
        .FirstOrDefaultAsync();

        if (session == null)
        {
            return null;
        }

        return new SessionData
        {
            Id = session.Id!,
            SessionId = session.SessionId,
            SessionType = SessionType.Start,
            Timestamp = session.StartedAt!.Value,
        };
    }

    public async Task<SessionData?> GetUnpairedSessionEnd()
    {
        var session = await _database.Table<SessionBatch>()
        .Where(s => s.StartedAt == null && s.EndedAt != null)
        .FirstOrDefaultAsync();

        if (session == null)
        {
            return null;
        }

        return new SessionData
        {
            Id = session.Id!,
            SessionId = session.SessionId,
            SessionType = SessionType.End,
            Timestamp = session.EndedAt!.Value,
        };

    }

    public async Task DeleteSessionById(string id)
    {
        await _database.Table<SessionBatch>()
            .Where(s => s.Id == id)
            .DeleteAsync();
    }

    public async Task UpdateSessionIdsForAllTrackingData(List<SessionBatch> sessions)
    {
        if (sessions == null || sessions.Count == 0) return;

        const string sqlLogs = @"
        UPDATE LogEntity
        SET sessionId = TRIM(?)
        WHERE TRIM(sessionId) = TRIM(?) COLLATE NOCASE;";

        const string sqlEvents = @"
        UPDATE EventEntity
        SET sessionId = TRIM(?)
        WHERE TRIM(sessionId) = TRIM(?) COLLATE NOCASE;";

        const string sqlBreadcrumbs = @"
        UPDATE BreadcrumbsEntity
        SET sessionId = TRIM(?)
        WHERE TRIM(sessionId) = TRIM(?) COLLATE NOCASE;";        

        await _database.RunInTransactionAsync(tran =>
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in sessions)
            {
                var oldRaw = (s.Id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(oldRaw)) continue;

                var newRaw = (s.SessionId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(newRaw)) continue;

                if (string.Equals(oldRaw, newRaw, StringComparison.OrdinalIgnoreCase))
                    continue;

                var key = $"{oldRaw}\u001F{newRaw}";
                if (!seen.Add(key)) continue;

                tran.Execute(sqlLogs, newRaw, oldRaw);
                tran.Execute(sqlEvents, newRaw, oldRaw);
                tran.Execute(sqlBreadcrumbs, newRaw, oldRaw);
            }
        });
    }

    public async Task<List<BreadcrumbsEntity>> GetOldest100BreadcrumbsAsync()
    {
        return await _database.Table<BreadcrumbsEntity>()
            .OrderBy(b => b.CreatedAt)
            .Take(100)
            .ToListAsync();
    }

    public async Task AddBreadcrumbAsync(BreadcrumbsEntity breadcrumb)
    {
        await _database.InsertAsync(breadcrumb);
    }

    public Task DeleteBreadcrumbs(List<BreadcrumbsEntity> breadcrumbs)
    {
        var ids = breadcrumbs.Select(b => b.Id).ToList();
        return _database.RunInTransactionAsync(tran =>
        {
            foreach (var id in ids)
            {
                tran.Execute("DELETE FROM BreadcrumbsEntity WHERE Id = ?", id);
            }
        });
    }
}
