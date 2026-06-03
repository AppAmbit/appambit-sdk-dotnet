using System;
using System.IO;
#if MACCATALYST
using Foundation;
#endif

namespace AppAmbit;

internal class AppConstants
{
    private const string DatabaseFileName = "AppAmbit.db3";

    internal const SQLite.SQLiteOpenFlags Flags =
        SQLite.SQLiteOpenFlags.ReadWrite | SQLite.SQLiteOpenFlags.Create | SQLite.SQLiteOpenFlags.SharedCache;

    internal static string DatabasePath => AppPaths.GetDatabaseFilePath(DatabaseFileName);
    internal const string DidCrashFileName = "did_app_crash.json";
    internal const string UnknownFileName = nameof(UnknownFileName);
    internal const string UnknownClass = nameof(UnknownClass);
    internal const string NoStackTraceAvailable = nameof(NoStackTraceAvailable);
    internal const int TrackEventNameMaxLimit = 80;
    internal const int TrackEventMaxPropertyLimit = 20;
    internal const int TrackEventPropertyMaxCharacters = 80;
    internal const string LiveSessionStreaming = "live_session_streaming";
    internal const string CmsBaseUrl = "https://cms.staging-appambit.com/api/v1";
    internal const string BaseUrlSdk = "https://staging-appambit.com/api";
}
