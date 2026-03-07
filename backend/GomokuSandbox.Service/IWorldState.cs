using GomokuSandbox.Service.Data;
using GomokuSandbox.Spec.Models;

namespace GomokuSandbox.Service;

public interface IWorldState
{
    WorldSnapshotDto GetSnapshot();
    WorldRulesDto GetRules();
    void SetRules(WorldRulesDto rules);
    AiNextTurnDto GetAiNextTurn(bool afterRefereeCheck = false, bool refereeRequested = false);
    (bool Ok, string Error) PlacePiece(string color, int x, int y);
    string? CheckResult();
    void EnsureGameExists();
    Player EnsurePlayer(string color, int intelligence = 50);
    void UpdatePlayerScore(int playerId, int delta);
    void SetGameOver(string status);
    void ResetWorld();
}
