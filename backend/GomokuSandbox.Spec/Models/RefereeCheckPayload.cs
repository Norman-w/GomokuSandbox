namespace GomokuSandbox.Spec.Models;

/// <summary>裁判 Check 时由 AI 提供的判定结果：确认谁赢或未赢。</summary>
public class RefereeCheckPayload
{
    /// <summary>AI 判定获胜方："Black" | "White"；若判定未有人五连则为 null。</summary>
    public string? Winner { get; set; }
}
