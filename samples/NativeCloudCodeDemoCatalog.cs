using AppAmbit.Services.Interfaces;

namespace AppAmbitTestingApp.Shared;

public sealed record NativeCloudCodeDemo(
    string Id,
    string Section,
    string Detail,
    string Prerequisite,
    bool RequiresConfirmation = false);

public sealed record NativeCloudCodeRequest(
    string Slug,
    HttpMethodEnum Method,
    IReadOnlyDictionary<string, string>? Query,
    object? Body);

public static class NativeCloudCodeDemoCatalog
{
    private static readonly HashSet<string> SharedFunctionNames = new(StringComparer.Ordinal)
    {
        "http-inspector",
        "json-values",
        "null-contract",
        "response-shapes",
        "error-response",
        "timeout-10s",
        "runtime-context"
    };

    public static IReadOnlyList<NativeCloudCodeDemo> Demos { get; } = new[]
    {
        new NativeCloudCodeDemo("setup-database", "Database", "Create the tables used by the Database examples without destroying data.", "Existing linked Database"),
        new NativeCloudCodeDemo("create-task", "Database", "Insert a task for the signed-in consumer.", "cloud_demo_tasks"),
        new NativeCloudCodeDemo("list-tasks", "Database", "Read the current consumer's tasks.", "cloud_demo_tasks"),
        new NativeCloudCodeDemo("complete-task", "Database", "Update one task with consumer ownership.", "Task id"),
        new NativeCloudCodeDemo("delete-task", "Database", "Delete one task owned by the consumer.", "Task id + confirmation", true),
        new NativeCloudCodeDemo("create-order", "Database", "Create an order without duplicate idempotency keys.", "cloud_demo_orders"),
        new NativeCloudCodeDemo("dashboard-summary", "Database", "Combine Database and CMS in one typed response.", "Database + CMS"),
        new NativeCloudCodeDemo("publish-post", "CMS", "Create a published CMS entry.", "Confirmation", true),
        new NativeCloudCodeDemo("read-posts", "CMS", "List published entries using only CMS data.", "cloud_code_demo_posts"),
        new NativeCloudCodeDemo("send-push", "Push", "Send a notification to all consumers.", "Permission + push credentials", true),
        new NativeCloudCodeDemo("http-inspector", "HTTP", "Inspect method, query, body and consumer context.", "HTTP trigger"),
        new NativeCloudCodeDemo("json-values", "HTTP", "Return common JSON value types.", "HTTP trigger"),
        new NativeCloudCodeDemo("null-contract", "HTTP", "Compare raw null and an explicit value.", "HTTP trigger"),
        new NativeCloudCodeDemo("response-shapes", "HTTP", "Demonstrate statuses, body and headers.", "HTTP trigger"),
        new NativeCloudCodeDemo("error-response", "HTTP", "Return a safe client error response.", "HTTP trigger"),
        new NativeCloudCodeDemo("timeout-10s", "HTTP", "Observe the configured function timeout.", "Function timeout = 10 s"),
        new NativeCloudCodeDemo("runtime-context", "HTTP", "Use environment values, secrets and logs safely.", "DEMO_REGION + DEMO_SECRET")
    };

    public static string Slug(string id, string platform) =>
        $"cloud-demo-{id}{(SharedFunctionNames.Contains(id) ? string.Empty : $"-{platform}")}";

    public static NativeCloudCodeRequest Configure(
        string id,
        string platform,
        string taskTitle,
        int taskId,
        string? postUuid,
        string publishTitle,
        string publishBody)
    {
        var slug = Slug(id, platform);
        return id switch
        {
            "setup-database" => new(slug, HttpMethodEnum.Post, null, null),
            "create-task" => new(slug, HttpMethodEnum.Post, null, new { title = taskTitle }),
            "list-tasks" => new(slug, HttpMethodEnum.Get, new Dictionary<string, string> { ["limit"] = "20" }, null),
            "complete-task" => new(slug, HttpMethodEnum.Patch, null, new { task_id = taskId }),
            "delete-task" => new(slug, HttpMethodEnum.Delete, null, new { task_id = taskId }),
            "create-order" => new(slug, HttpMethodEnum.Post, null, new { idempotency_key = Guid.NewGuid().ToString(), amount = 100 }),
            "dashboard-summary" => new(slug, HttpMethodEnum.Get, null, null),
            "publish-post" => new(slug, HttpMethodEnum.Post, null, new { title = publishTitle, body = publishBody }),
            "read-posts" => new(slug, HttpMethodEnum.Get, string.IsNullOrWhiteSpace(postUuid) ? null : new Dictionary<string, string> { ["uuid"] = postUuid.Trim() }, null),
            "send-push" => new(slug, HttpMethodEnum.Post, null, new { title = $"Cloud Code {platform} demo", body = $"Push from the .NET {platform} sample." }),
            "http-inspector" => new(slug, HttpMethodEnum.Post, new Dictionary<string, string> { ["source"] = $"dotnet-{platform}" }, new { message = "hello", count = 2 }),
            "json-values" => new(slug, HttpMethodEnum.Post, null, null),
            "null-contract" => new(slug, HttpMethodEnum.Get, null, null),
            "response-shapes" => new(slug, HttpMethodEnum.Post, null, null),
            "error-response" => new(slug, HttpMethodEnum.Post, null, new { invalid = true }),
            "timeout-10s" => new(slug, HttpMethodEnum.Get, null, null),
            "runtime-context" => new(slug, HttpMethodEnum.Get, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
        };
    }
}
