using GomokuSandbox.Spec.Models;

namespace GomokuSandbox.Spec;

/// <summary>
/// 仅用于反射生成 info/help，不包含实现；实现见 Api 的 AiActionService。
/// </summary>
public static class AiActionSpecs
{
    [ActionSpec("Black", "Place",
        Description = "黑方落子。",
        PayloadType = typeof(PlacePayload),
        PayloadSchema = "x (0-14 整数), y (0-14 整数); claimWin (可选 bool)：true 表示自认已赢、请求裁判，下一动为 Referee，否则下一动为对方。",
        ExampleInvoke = "act Black Place '{\"x\":7,\"y\":7}' 或带 claimWin:true 请求裁判")]
    [ActionSpec("White", "Place",
        Description = "白方落子。",
        PayloadType = typeof(PlacePayload),
        PayloadSchema = "同 Black：x, y；可选 claimWin。",
        ExampleInvoke = "act White Place '{\"x\":7,\"y\":7}'")]
    public static void Place() { }

    [ActionSpec("Referee", "Check",
        Description = "裁判判定是否五连。仅当棋手落子时传了 claimWin:true 后才会轮到裁判；不是每步都裁判。",
        ExampleInvoke = "act Referee Check")]
    public static void RefereeCheck() { }

    [ActionSpec("Commander", "SetRules", "盘活",
        Description = "统领者发话/定规则。统领者说的话由你生成并提交，后端存下后所有人拿到。",
        PayloadType = typeof(CommanderRulesPayload),
        PayloadSchema = "direction (必填 字符串，统领者发言); 可选 minMovesBeforeWin (整数), blackAdvantage (数)。",
        ExampleInvoke = "act Commander 盘活 '{\"direction\":\"公平对局，先五连者胜。\"}'")]
    public static void CommanderSetRules() { }

    [ActionSpec("Commander", "开新局", "NewGame",
        Description = "开局。需先统领者发话+造人者造人。",
        ExampleInvoke = "act Commander 开新局")]
    public static void CommanderNewGame() { }

    [ActionSpec("Creator", "造人", "CreatePlayer",
        Description = "造人者造人。color 大小写不限。",
        PayloadType = typeof(CreatePlayerPayload),
        PayloadSchema = "color (必填 \"Black\" 或 \"White\"); intelligence (0-100 整数，默认 50); thought (可选 字符串)。",
        ExampleInvoke = "act Creator 造人 '{\"color\":\"Black\",\"intelligence\":50}'")]
    public static void CreatorCreatePlayer() { }
}
