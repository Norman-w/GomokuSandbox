using GomokuSandbox.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GomokuSandbox.Service;

/// <summary>CLI 与 UI (Api) 共用：注册所有 Service 与 DbContext，统一落库/读库。</summary>
public static class ServiceRegistration
{
    /// <summary>连接串优先级：环境变量 GOMOKU_DB > 传入的 connectionString > 默认 gomoku.db。API 与 CLI 设置相同 GOMOKU_DB 可共用同一数据库。</summary>
    public static IServiceCollection AddGomokuServices(this IServiceCollection services, string? connectionString = null)
    {
        var conn = Environment.GetEnvironmentVariable("GOMOKU_DB") ?? connectionString ?? "Data Source=gomoku.db";
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(conn));
        services.AddSingleton<IWorldState, WorldStateService>();
        services.AddSingleton<INarrativeService, NarrativeService>();
        services.AddScoped<IAiActionService, AiActionService>();
        services.AddScoped<IWorldViewService, WorldViewService>();
        return services;
    }
}
