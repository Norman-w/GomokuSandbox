using GomokuSandbox.Service;
using GomokuSandbox.Service.Data;
using Microsoft.EntityFrameworkCore; // for Migrate()

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGomokuServices(builder.Configuration.GetConnectionString("Default") ?? "Data Source=gomoku.db");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyMethod().AllowAnyHeader().SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "GomokuSandbox API", Version = "v1" });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GomokuSandbox API v1"));
app.UseCors();
app.MapControllers();

app.Run();
