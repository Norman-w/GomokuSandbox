using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using GomokuSandbox.Spec.Models;
using System.ComponentModel;

namespace GomokuSandbox.Spec;

/// <summary>
/// 仅通过反射 [CommandSpec] 与 [ActionSpec] 生成 info/help，不依赖 API；CLI 本地调用即可。
/// </summary>
public static class SpecBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions JsonOptionsCompact = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>从 [ActionSpec] 反射生成完整 info JSON，面向 AI 调用 CLI。无参数即返回本说明。</summary>
    public static string Build(Type? actionSpecsType = null)
    {
        actionSpecsType ??= typeof(AiActionSpecs);
        var spec = new
        {
            schemaVersion = "1.0",
            usage = "无参数：返回本说明（类型、谁能干啥、咋干、返回啥）。有参数：<role> <action> [payload...]，执行该动作；payload 为 JSON 字符串（可多词以空格连接）。返回含 success、下一步谁/干啥/咋干、当前世界。",
            invocation = new
            {
                noArgs = "无参数运行 dotnet run --project GomokuSandbox.Cli 即返回本 info（类型、roles、models、returnValues）。",
                withArgs = "<role> <action> [payload]",
                description = "第 1 个参数=谁(role)，第 2 个=干啥(action)，后续=该动作的 payload（JSON 字符串，见 roles 中各 action 的 payloadSchema）。",
                example = new[] { "Commander", "SetRules", "{\"direction\":\"公平对局，先五连者胜。\"}" }
            },
            roles = ReflectActionSpecs(actionSpecsType),
            models = ReflectModelSchemas(),
            returnValues = BuildReturnValues()
        };
        return JsonSerializer.Serialize(spec, JsonOptions);
    }

    /// <summary>根据同一套反射生成 CLI help（面向人类）；action 以外见文档。</summary>
    public static string BuildHelpText(Type? actionSpecsType = null, Type? commandSpecsType = null)
    {
        actionSpecsType ??= typeof(AiActionSpecs);
        commandSpecsType ??= typeof(CliCommandSpecs);
        var sb = new StringBuilder();
        sb.AppendLine("GomokuSandbox.Cli — next/act 供 AI 轮转，详见 dotnet run --project GomokuSandbox.Cli -- info");
        sb.AppendLine();
        sb.AppendLine("命令:");
        foreach (var method in commandSpecsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var a = method.GetCustomAttribute<CommandSpecAttribute>();
            if (a == null) continue;
            sb.AppendLine($"  {a.Name,-14} {a.Description}");
        }
        sb.AppendLine();
        sb.AppendLine("角色与动作（详见 info 的 roles）:");
        foreach (dynamic r in ReflectActionSpecs(actionSpecsType))
            sb.AppendLine($"  {r.role} {r.action}: {r.description}");
        sb.AppendLine();
        sb.AppendLine("环境变量: GOMOKU_API_URL 默认 http://localhost:5244");
        sb.AppendLine("action 以外见 docs/AI-AGENT-GUIDE.md");
        return sb.ToString();
    }

    private static List<object> ReflectCommandSpecs(Type type)
    {
        var list = new List<object>();
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var a = method.GetCustomAttribute<CommandSpecAttribute>();
            if (a == null) continue;
            var exampleArgs = GetExampleArgs(a.Name);
            list.Add(new
            {
                name = a.Name,
                description = a.Description,
                returns = a.Returns,
                exampleArgs
            });
        }
        return list.OrderBy(x => ((dynamic)x).name).Cast<object>().ToList();
    }

    /// <summary>供 AI 直接使用的参数数组，无转义歧义。payload 以单独字符串元素出现，内容为合法 JSON。</summary>
    private static List<string> GetExampleArgs(string commandName)
    {
        return commandName switch
        {
            "next" => new List<string> { "next" },
            "act" => new List<string> { "act", "Black", "Place", "{\"x\":7,\"y\":7}" },
            "ensure" => new List<string> { "ensure" },
            "state" => new List<string> { "state" },
            "snapshot" => new List<string> { "snapshot" },
            "view" => new List<string> { "view" },
            "rules" => new List<string> { "rules" },
            "direction" => new List<string> { "direction" },
            "place" => new List<string> { "place", "Black", "7", "7" },
            "referee-check" => new List<string> { "referee-check" },
            "players" => new List<string> { "players" },
            "info" => new List<string> { "info" },
            "help" => new List<string> { "help" },
            _ => new List<string> { commandName }
        };
    }

    private static List<object> ReflectActionSpecs(Type type)
    {
        var list = new List<object>();
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (ActionSpecAttribute a in method.GetCustomAttributes(typeof(ActionSpecAttribute), false))
            {
                var payloadSchema = a.PayloadSchema;
                if (string.IsNullOrEmpty(payloadSchema) && a.PayloadType != null)
                    payloadSchema = ReflectPayloadSchema(a.PayloadType);
                object? examplePayload = GetExamplePayloadForAction(a);
                var exampleArgs = GetExampleArgsForRoleAction(a.Role, a.ActionNames.Length > 0 ? a.ActionNames[0] : "", examplePayload);
                list.Add(new
                {
                    role = a.Role,
                    action = a.ActionNames.Length > 0 ? a.ActionNames[0] : "",
                    aliases = a.ActionNames,
                    description = a.Description,
                    payloadRequired = a.PayloadType != null,
                    payloadSchema,
                    payloadTypeName = a.PayloadType?.Name,
                    exampleArgs
                });
            }
        }
        return list;
    }

    private static object? GetExamplePayloadForAction(ActionSpecAttribute a)
    {
        if (a.PayloadType == typeof(PlacePayload)) return new { x = 7, y = 7 };
        if (a.PayloadType == typeof(RefereeCheckPayload)) return new { winner = (string?)null };
        if (a.PayloadType == typeof(CommanderRulesPayload)) return new { direction = "公平对局，先五连者胜。" };
        if (a.PayloadType == typeof(CreatePlayerPayload)) return new { color = "Black", intelligence = 50 };
        return null;
    }

    private static List<string> GetExampleArgsForRoleAction(string role, string action, object? examplePayload)
    {
        var args = new List<string> { role, action };
        if (examplePayload != null)
            args.Add(JsonSerializer.Serialize(examplePayload, JsonOptionsCompact));
        return args;
    }

    private static string ReflectPayloadSchema(Type payloadType)
    {
        var parts = new List<string>();
        foreach (var p in payloadType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var desc = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? p.Name;
            parts.Add($"{p.Name} ({GetTypeName(p.PropertyType)}): {desc}");
        }
        return string.Join("; ", parts);
    }

    private static string GetTypeName(Type t)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            return GetTypeName(t.GetGenericArguments()[0]) + "?";
        if (t == typeof(int)) return "int";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(string)) return "string";
        if (t == typeof(double)) return "double";
        return t.Name;
    }

    /// <summary>反射公共 model 类型，输出字段名、类型、摘要，供 AI 理解序列化格式。</summary>
    private static object ReflectModelSchemas()
    {
        var types = new[] { typeof(AiNextTurnDto), typeof(WorldSnapshotDto), typeof(WorldRulesDto), typeof(PlacePayload), typeof(RefereeCheckPayload), typeof(CommanderRulesPayload), typeof(CreatePlayerPayload) };
        var result = new Dictionary<string, object>();
        foreach (var t in types)
        {
            var fields = new List<object>();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var summary = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var typeName = GetTypeName(p.PropertyType);
                if (p.PropertyType.IsClass && p.PropertyType != typeof(string) && !p.PropertyType.IsArray)
                    typeName = p.PropertyType.Name;
                fields.Add(new { name = p.Name, type = typeName, summary });
            }
            result[t.Name] = new { type = t.Name, fields };
        }
        return result;
    }

    private static object BuildReturnValues()
    {
        return new
        {
            stepResponse = new
            {
                description = "有参数执行一步后，stdout 为单条 JSON，包含：本步是否成功、当前世界、下一步谁/干啥/咋干。",
                fields = new[]
                {
                    new { name = "success", type = "bool", meaning = "本步是否执行成功。" },
                    new { name = "error", type = "string?", meaning = "失败时的错误信息。" },
                    new { name = "world", type = "WorldViewDto", meaning = "执行完本步后的完整世界（快照、规则、棋手、叙事）。" },
                    new { name = "nextWho", type = "string", meaning = "下一步你要代理的角色（下一轮第 1 个参数 role）。" },
                    new { name = "nextWhat", type = "string", meaning = "下一步建议执行的动作（下一轮第 2 个参数 action）。" },
                    new { name = "nextHow", type = "object", meaning = "咋干的提示（如 roleContext）；具体 payload 格式见本 info 的 roles 中对应 action。" }
                }
            },
            worldView = new
            {
                description = "world 字段即 WorldViewDto：snapshot、rules、direction、blackPlayer、whitePlayer、narrative。",
                type = "WorldViewDto"
            }
        };
    }
}
