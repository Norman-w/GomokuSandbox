namespace GomokuSandbox.Spec;

public static class CliCommandSpecs
{
    [CommandSpec("next", Api = "GET /api/UI/next", Description = "获取下一步角色+快照+规则；delaySeconds>0 时先等待再输出", Example = "GomokuSandbox.Cli next", Returns = "nextAct 结构（见 returnValues）")]
    public static void Next() { }

    [CommandSpec("act", Api = "POST /api/UI/act", Args = "role action [payloadJson]", Description = "以角色执行动作；返回即下一轮 next", Example = "GomokuSandbox.Cli act Black Place \"{\\\"x\\\":7,\\\"y\\\":7}\"", Returns = "nextAct 结构")]
    public static void Act() { }

    [CommandSpec("ensure", Api = "POST /api/UI/ensure", Description = "确保有一局；需先统领者发话+造人者造人", Example = "GomokuSandbox.Cli ensure", Returns = "快照 JSON")]
    public static void Ensure() { }

    [CommandSpec("state", Api = "GET /api/UI/state", Description = "当前对局快照", Example = "GomokuSandbox.Cli state", Returns = "WorldSnapshotDto")]
    public static void State() { }

    [CommandSpec("snapshot", Api = "GET /api/UI/snapshot", Description = "世界快照", Example = "GomokuSandbox.Cli snapshot", Returns = "WorldSnapshotDto")]
    public static void Snapshot() { }

    [CommandSpec("view", Api = "GET /api/UI/view", Description = "聚合：快照+规则+棋手+叙事", Example = "GomokuSandbox.Cli view", Returns = "WorldViewDto")]
    public static void View() { }

    [CommandSpec("rules", Api = "GET /api/UI/rules", Description = "世界规则", Example = "GomokuSandbox.Cli rules", Returns = "WorldRulesDto")]
    public static void Rules() { }

    [CommandSpec("direction", Api = "GET /api/UI/direction", Description = "统领者方向/寄语", Example = "GomokuSandbox.Cli direction", Returns = "字符串")]
    public static void Direction() { }

    [CommandSpec("place", Api = "POST /api/UI/place", Args = "color x y", Description = "直接落子；AI 轮转请用 act", Example = "GomokuSandbox.Cli place Black 7 7", Returns = "{ success, snapshot, result }")]
    public static void Place() { }

    [CommandSpec("referee-check", Api = "POST /api/UI/referee/check", Description = "裁判判定五连", Example = "GomokuSandbox.Cli referee-check", Returns = "{ result, snapshot }")]
    public static void RefereeCheck() { }

    [CommandSpec("players", Api = "GET /api/UI/players", Description = "棋手列表", Example = "GomokuSandbox.Cli players", Returns = "PlayerDto[]")]
    public static void Players() { }

    [CommandSpec("info", Api = "-", Description = "列出规范（本地反射 [ActionSpec]/[CommandSpec]，不依赖 API）", Example = "GomokuSandbox.Cli info", Returns = "本 JSON")]
    public static void Info() { }
}
