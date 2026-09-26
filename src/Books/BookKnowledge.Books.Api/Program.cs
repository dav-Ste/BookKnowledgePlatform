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

app.MapGet("/healthz", () => Results.Ok(new { service = "Books", status = "ok" }))
   .RequireRateLimiting("api");

app.MapGet("/api/books/search", (string? q) => Results.Ok(new { query = q ?? "", results = Array.Empty<object>() })).RequireRateLimiting("api");

app.Run();
public partial class Program { }
