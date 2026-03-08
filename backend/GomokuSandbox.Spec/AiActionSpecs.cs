using GomokuSandbox.Spec.Models;

namespace GomokuSandbox.Spec;

/// <summary>
/// 仅用于反射生成 info/help，不包含实现；实现见 Api 的 AiActionService。
/// </summary>
public static class AiActionSpecs
{
    [ActionSpec("Black", "Place",
        Description = "黑方落子。是否出现五连由后端算法在落子后计算，若算出有人赢则下一步会指向 Referee，无需 AI 传 claimWin。",
        PayloadType = typeof(PlacePayload),
        PayloadSchema = "x (0-14 整数), y (0-14 整数)。",
        ExampleInvoke = "act Black Place '{\"x\":7,\"y\":7}'")]
    [ActionSpec("White", "Place",
        Description = "白方落子。同 Black，后端算是否五连并决定下一步是否轮到裁判。",
        PayloadType = typeof(PlacePayload),
        PayloadSchema = "同 Black：x, y。",
        ExampleInvoke = "act White Place '{\"x\":7,\"y\":7}'")]
    public static void Place() { }

    [ActionSpec("Referee", "Check",
        Description = "裁判判定是否五连。后端用固定算法校验，同时需 AI 提供判定：winner 为 \"Black\"|\"White\" 表示你确认该方赢，null 表示未赢。若确认赢则裁判宣布结果并结束对局；否则下一步切回该下棋的棋手。",
        PayloadType = typeof(RefereeCheckPayload),
        PayloadSchema = "winner (必填): \"Black\" | \"White\" | null，表示 AI 判定谁赢或未赢。",
        ExampleInvoke = "act Referee Check '{\"winner\":\"Black\"}' 或 '{\"winner\":null}'")]
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
