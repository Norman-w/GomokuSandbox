using System.Linq;
using GomokuSandbox.Service.Data;
using GomokuSandbox.Service.Models;
using Microsoft.Extensions.DependencyInjection;

namespace GomokuSandbox.Service;

public interface INarrativeService
{
    void AppendCommander(string message);
    void AppendCreator(string message);
    IReadOnlyList<NarrativeEntryDto> GetEntries();
    void Clear();
}

public class NarrativeService : INarrativeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();

    public NarrativeService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void AppendCommander(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Append("Commander", message.Trim());
    }

    public void AppendCreator(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Append("Creator", message.Trim());
    }

    private void Append(string role, string message)
    {
        lock (_lock)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.NarrativeEntries.Add(new NarrativeEntry { Role = role, Message = message, At = DateTime.UtcNow });
            db.SaveChanges();
        }
    }

    public IReadOnlyList<NarrativeEntryDto> GetEntries()
    {
        lock (_lock)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return db.NarrativeEntries
                .OrderBy(e => e.Id)
                .Select(e => new NarrativeEntryDto { Role = e.Role, Message = e.Message, At = e.At })
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.NarrativeEntries.RemoveRange(db.NarrativeEntries);
            db.SaveChanges();
        }
    }
}
