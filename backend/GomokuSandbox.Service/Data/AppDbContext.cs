using Microsoft.EntityFrameworkCore;

namespace GomokuSandbox.Service.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameMove> GameMoves => Set<GameMove>();
    public DbSet<NarrativeEntry> NarrativeEntries => Set<NarrativeEntry>();
    public DbSet<WorldRules> WorldRules => Set<WorldRules>();
}
