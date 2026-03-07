using GomokuSandbox.Service.Models;

namespace GomokuSandbox.Service;

public interface IWorldViewService
{
    Task<WorldViewDto> GetViewAsync(CancellationToken ct = default);
}
