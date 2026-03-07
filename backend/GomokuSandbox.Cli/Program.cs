using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using GomokuSandbox.Service;
using GomokuSandbox.Service.Data;
using GomokuSandbox.Service.Models;
using GomokuSandbox.Spec;
using GomokuSandbox.Spec.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddGomokuServices(null);
var sp = services.BuildServiceProvider();

using (var scope = sp.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

// 无参数：返回 info（类型、谁能干啥、咋干、返回啥）
if (args.Length == 0)
{
    var specJson = SpecBuilder.Build();
    var specNode = JsonNode.Parse(specJson) as JsonObject ?? new JsonObject();
    specNode["suggestedNext"] = new JsonObject
    {
        ["nextWho"] = "Commander",
        ["nextWhat"] = "SetRules",
        ["nextHow"] = "统领者发言。payload 格式见本 JSON 的 roles 中 Commander SetRules 的 payloadSchema，必填 direction（字符串）。"
    };
    Console.WriteLine(specNode.ToJsonString(jsonOptions));
    return 0;
}

// 有参数：第 1 个=谁(role)，第 2 个=干啥(action)，后续=payload（JSON 字符串，可多词连接）
if (args.Length < 2)
{
    Console.Error.WriteLine("有参数时至少需要 role 与 action，例如: Commander SetRules \"{\\\"direction\\\":\\\"...\\\"}\"");
    await WriteStepErrorAsync("有参数时至少需要 role 与 action", sp, jsonOptions);
    return 1;
}

var role = args[0];
var action = args[1];
var payloadJson = args.Length > 2 ? string.Join(" ", args[2..]) : null;

JsonElement? payload = null;
if (!string.IsNullOrWhiteSpace(payloadJson))
{
    try
    {
        payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("payload 非合法 JSON: " + ex.Message);
        await WriteStepErrorAsync("payload 非合法 JSON: " + ex.Message, sp, jsonOptions);
        return 1;
    }
}

try
{
    using var scope = sp.CreateScope();
    var actionService = scope.ServiceProvider.GetRequiredService<IAiActionService>();
    var (result, error) = await actionService.ExecuteAsync(role, action, payload);

    WorldViewDto world;
    using (var scope2 = sp.CreateScope())
    {
        var viewService = scope2.ServiceProvider.GetRequiredService<IWorldViewService>();
        world = await viewService.GetViewAsync();
    }

    if (error != null)
    {
        Console.Error.WriteLine(error);
        var nextHint = sp.GetRequiredService<IWorldState>().GetAiNextTurn();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            success = false,
            error,
            world,
            nextWho = nextHint.NextRole,
            nextWhat = InferNextWhat(nextHint),
            nextHow = nextHint.RoleContext
        }, jsonOptions));
        return 1;
    }

    var delay = result!.DelaySeconds;
    if (delay > 0)
    {
        Console.Error.WriteLine($"[CLI] 等待 {delay}s 后再返回…");
        Thread.Sleep(TimeSpan.FromSeconds(delay));
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        success = true,
        world,
        nextWho = result.NextRole,
        nextWhat = InferNextWhat(result),
        nextHow = result.RoleContext
    }, jsonOptions));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("错误: " + ex.Message);
    await WriteStepErrorAsync(ex.Message, sp, jsonOptions);
    return 1;
}

static string InferNextWhat(AiNextTurnDto next)
{
    var role = next.NextRole;
    var msg = GetRoleContextMessage(next.RoleContext);
    if (role == "Commander")
    {
        if (msg != null && (msg.Contains("开新局") || msg.Contains("造人者造人"))) return "开新局";
        return "SetRules";
    }
    if (role == "Creator") return "CreatePlayer";
    if (role == "Black" || role == "White") return "Place";
    if (role == "Referee") return "Check";
    return "";
}

static string? GetRoleContextMessage(object? ctx)
{
    if (ctx == null) return null;
    try
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(ctx));
        return node?["message"]?.GetValue<string>();
    }
    catch { return null; }
}

static async Task WriteStepErrorAsync(string error, IServiceProvider sp, JsonSerializerOptions jsonOptions)
{
    try
    {
        WorldViewDto? world = null;
        using (var scope = sp.CreateScope())
            world = await scope.ServiceProvider.GetRequiredService<IWorldViewService>().GetViewAsync();
        var next = sp.GetRequiredService<IWorldState>().GetAiNextTurn();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            success = false,
            error,
            world,
            nextWho = next.NextRole,
            nextWhat = InferNextWhat(next),
            nextHow = next.RoleContext
        }, jsonOptions));
    }
    catch
    {
        Console.WriteLine(JsonSerializer.Serialize(new { success = false, error, world = (WorldViewDto?)null, nextWho = (string?)null, nextWhat = (string?)null, nextHow = (object?)null }, jsonOptions));
    }
}
