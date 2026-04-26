using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var config = Config.Load();

builder.Services.AddSingleton(config);

builder.Services.AddSingleton<FilePathBuilder>();
builder.Services.AddSingleton<HistoryService>();
builder.Services.AddSingleton<GraphAuthService>();
builder.Services.AddSingleton<FormsService>();

builder.Services.AddSingleton<IJob, FormsJob>();
builder.Services.AddSingleton<IJob, OneDriveJob>();

builder.Services.AddSingleton<JobRunner>();
builder.Services.AddHostedService<DailyWorker>();

var app = builder.Build();

app.MapGet("/health", () => "OK");

app.MapPost("/run/{job}", async (HttpContext ctx, string job, JobRunner runner) =>
{
    var cfg = ctx.RequestServices.GetRequiredService<Config>();

    if (ctx.Request.Headers["x-api-key"] != cfg.Security.ApiKey)
        return Results.Unauthorized();

    await runner.RunSingle(job);
    return Results.Ok($"Executed: {job}");
});

app.MapPost("/run-all", async (JobRunner runner) =>
{
    await runner.RunAll();
    return Results.Ok("All jobs executed");
});

app.Run();
