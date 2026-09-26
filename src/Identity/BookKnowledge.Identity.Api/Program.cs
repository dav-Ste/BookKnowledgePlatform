using BookKnowledge.ServiceDefaults;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit = 60;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();

app.MapGet("/healthz", () => Results.Ok(new { service = "Identity", status = "ok" }))
   .RequireRateLimiting("api");

app.MapGet("/api/identity/ping", () => Results.Ok(new { service = "identity" })).RequireRateLimiting("api");

app.Run();
public partial class Program { }
