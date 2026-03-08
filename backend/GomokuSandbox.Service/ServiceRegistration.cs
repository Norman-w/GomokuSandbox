using System.IO;
using GomokuSandbox.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GomokuSandbox.Service;

/// <summary>CLI 与 UI (Api) 共用：注册所有 Service 与 DbContext，统一落库/读库。</summary>
public static class ServiceRegistration
{
    /// <summary>默认数据库路径：与运行目录无关，保证本机 API 与 CLI 共用同一库（统领者发言、叙事等一致）。优先级：GOMOKU_DB > 传入 connectionString > 此默认路径。</summary>
    private static string DefaultDbPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GomokuSandbox");
        try { Directory.CreateDirectory(dir); } catch { /* 忽略 */ }
        return Path.Combine(dir, "gomoku.db");
    }

    /// <summary>连接串优先级：环境变量 GOMOKU_DB > 传入的 connectionString > 默认 gomoku.db。若最终为相对路径 Data Source=gomoku.db 则改为本机共享路径，保证 API 与 CLI 共用。</summary>
    public static IServiceCollection AddGomokuServices(this IServiceCollection services, string? connectionString = null)
    {
        var conn = Environment.GetEnvironmentVariable("GOMOKU_DB") ?? connectionString ?? "Data Source=gomoku.db";
        if (string.IsNullOrWhiteSpace(conn) || conn.Trim().Equals("Data Source=gomoku.db", StringComparison.OrdinalIgnoreCase))
            conn = "Data Source=" + DefaultDbPath();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(conn));
        services.AddSingleton<IWorldState, WorldStateService>();
        services.AddSingleton<INarrativeService, NarrativeService>();
        services.AddScoped<IAiActionService, AiActionService>();
        services.AddScoped<IWorldViewService, WorldViewService>();
        return services;
    }
}
