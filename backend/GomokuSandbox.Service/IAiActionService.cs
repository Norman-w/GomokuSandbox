using System.Text.Json;
using GomokuSandbox.Spec.Models;

namespace GomokuSandbox.Service;

public interface IAiActionService
{
    Task<(AiNextTurnDto? Result, string? Error)> ExecuteAsync(string role, string action, JsonElement? payload, CancellationToken ct = default);
}
