using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
        tracerProviderBuilder
            .AddJaegerExporter(options =>
            {
                options.AgentHost = Environment.GetEnvironmentVariable("JAEGER_AGENT_HOST") ?? "localhost";
                options.AgentPort = int.Parse(Environment.GetEnvironmentVariable("JAEGER_AGENT_PORT") ?? "14250");
            }));

// Add the worker service
builder.Services.AddHostedService<ControllerManagerWorker>();

var host = builder.Build();

await host.RunAsync();

public class ControllerManagerWorker : BackgroundService
{
    private readonly ILogger<ControllerManagerWorker> _logger;

    public ControllerManagerWorker(ILogger<ControllerManagerWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Controller Manager Worker starting at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Controller Manager applying configurations at: {time}", DateTimeOffset.Now);
            
            // Simulate configuration management work
            await SimulateConfigurationWork();
            
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task SimulateConfigurationWork()
    {
        // Simulate configuration application logic
        _logger.LogInformation("Checking for configuration updates...");
        await Task.Delay(1000);
        
        _logger.LogInformation("Applying configuration changes...");
        await Task.Delay(2000);
        
        _logger.LogInformation("Configuration management cycle completed");
    }
} 