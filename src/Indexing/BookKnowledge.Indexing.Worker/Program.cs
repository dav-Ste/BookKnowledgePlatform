using BookKnowledge.ServiceDefaults;

namespace BookKnowledge.Indexing.Worker;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.AddServiceDefaults();
        builder.Services.AddHostedService<Worker>();

        await builder.Build().RunAsync();
    }
}

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> WorkerStarted = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(1000, nameof(WorkerStarted)),
        "Book indexing worker started.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WorkerStarted(logger, null);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
