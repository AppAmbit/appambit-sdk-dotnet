using System.Diagnostics;
using AppAmbit.Enums;
using AppAmbit.Models.Logs;
using AppAmbit.Services.Endpoints;
using AppAmbit.Services.Interfaces;
using System.Runtime.CompilerServices;
using AppAmbit.Services;

namespace AppAmbit
{
    internal static class Logging
    {
        private static IAPIService? _apiService;
        private static IStorageService? _storageService;
        private static string? _deviceId;

        public static void Initialize(IAPIService? apiService, IStorageService? storageService, string deviceId)
        {
            _apiService = apiService;
            _storageService = storageService;
            _deviceId = deviceId;
        }

        public static Task LogEvent(
            string? message,
            LogType logType,
            Exception? exception = null,
            Dictionary<string, string>? properties = null,
            string? classFqn = null,
            string? fileName = null,
            int? lineNumber = null)
        {
            var exInfo = (exception != null) ? ExceptionInfo.FromException(exception, _deviceId) : null;
            return BuildAndSend(message, logType, exInfo, properties, classFqn, fileName, lineNumber);
        }

        public static Task LogEvent(
            string? message,
            LogType logType,
            ExceptionInfo? exception = null,
            Dictionary<string, string>? properties = null,
            string? classFqn = null,
            string? fileName = null,
            int? lineNumber = null)
        {
            return BuildAndSend(message, logType, exception, properties, classFqn, fileName, lineNumber);
        }

        internal static Task LogEventFromExceptionInfo(
            string? message,
            LogType logType,
            ExceptionInfo? exception = null,
            Dictionary<string, string>? properties = null,
            string? classFqn = null,
            string? fileName = null,
            int? lineNumber = null)
        {
            return BuildAndSend(message, logType, exception, properties, classFqn, fileName, lineNumber);
        }

        private static Task BuildAndSend(
            string? message,
            LogType logType,
            ExceptionInfo? exInfo,
            Dictionary<string, string>? properties,
            string? classFqn,
            string? fileName,
            int? lineNumber)
        {
            if (!SessionManager.IsSessionActive)
                return Task.CompletedTask;

            var stackTrace = exInfo?.StackTrace;
            stackTrace = string.IsNullOrEmpty(stackTrace) ? AppConstants.NoStackTraceAvailable : stackTrace;
            var file = exInfo?.CrashLogFile;

            var cleanedProperties = (properties ?? new Dictionary<string, string>())
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var log = new LogEntity
            {
                AppVersion = new AppInfoService().AppVersion,
                ClassFQN = exInfo?.ClassFullName ?? classFqn ?? AppConstants.UnknownClass,
                FileName = exInfo?.FileNameFromStackTrace ?? fileName ?? AppConstants.UnknownFileName,
                LineNumber = exInfo?.LineNumberFromStackTrace ?? lineNumber ?? 0,
                Message = exInfo?.Message ?? (string.IsNullOrEmpty(message) ? "" : message),
                StackTrace = stackTrace,
                Context = cleanedProperties,
                Type = logType,
                File = (logType == LogType.Crash && exInfo != null) ? file : null,
                CreatedAt = DateTime.UtcNow,
                SessionId = string.IsNullOrEmpty(exInfo?.SessionId) ? SessionManager.SessionId : exInfo?.SessionId
            };

            return SendOrSaveLogEventAsync(log);
        }


        private static Task SendOrSaveLogEventAsync(Log log)
        {
            try
            {
                var endpoint = new LogEndpoint(log);
                var reqTask = _apiService?.ExecuteRequest<LogResponse>(endpoint);
                if (reqTask == null)
                    return StoreLogInDb(log);

                return reqTask
                    .ContinueWith(static (tr, state) =>
                    {
                        var lg = (Log)state!;
                        if (tr.IsFaulted || tr.IsCanceled)
                            return StoreLogInDb(lg);

                        var resp = tr.Result;
                        if (resp == null || resp.ErrorType != ApiErrorType.None)
                            return StoreLogInDb(lg);

                        return Task.CompletedTask;
                    }, log, TaskScheduler.Default)
                    .Unwrap();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logging] Error {ex.Message}");
                return StoreLogInDb(log);
            }
        }

        private static Task StoreLogInDb(Log log)
        {
            var storage = _storageService;
            var logEntity = log.ConvertTo<LogEntity>();
            logEntity.Id = Guid.NewGuid();
            var t = storage?.LogEventAsync(logEntity);
            return t ?? Task.CompletedTask;
        }
    }
}
