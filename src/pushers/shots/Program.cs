using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using ShotsPusher.Services;
using System.Diagnostics;
using System.Diagnostics.Metrics;

var builder = Host.CreateApplicationBuilder(args);

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Register environment mapping, tracing and metrics services
builder.Services.AddSingleton<IEnvironmentMapper, EnvironmentMapper>();
builder.Services.AddSingleton<ITraceContextPropagator, TraceContextPropagator>();
builder.Services.AddSingleton<IServiceBusTracing, ServiceBusTracing>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();

// Register HTTP client and services
builder.Services.AddHttpClient<IWebhookService, WebhookService>();
builder.Services.AddSingleton<IServiceBusConsumerService, ServiceBusConsumerService>();

// Add OpenTelemetry with dynamic resource configuration
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => 
    {
        var environmentMapper = new EnvironmentMapper(builder.Configuration);
        var envTags = environmentMapper.GetEnvironmentTags();
        
        resource.AddService(
            serviceName: environmentMapper.GetServiceName(),
            serviceVersion: environmentMapper.GetServiceVersion(),
            serviceNamespace: envTags.GetValueOrDefault("service.namespace")?.ToString());
            
        // Adicionar todas as tags de ambiente como atributos do recurso
        foreach (var tag in envTags)
        {
            resource.AddAttributes(new[] { new KeyValuePair<string, object>(tag.Key, tag.Value ?? "unknown") });
        }
    })
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("ShotsPusher.ServiceBus")
            .SetSampler(new AlwaysOnSampler());

        // Configure OTLP exporter for Jaeger
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";
        try
        {
            Console.WriteLine($"Configuring OTLP exporter with endpoint: {otlpEndpoint}");
            tracerProviderBuilder.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to configure OTLP exporter: {ex.Message}");
            Console.WriteLine("Using console exporter for tracing...");
            tracerProviderBuilder.AddConsoleExporter();
        }
    })
    .WithMetrics(meterProviderBuilder =>
    {
        meterProviderBuilder
            .AddMeter("ShotsPusher.ServiceBus")
            .AddMeter("ShotsPusher.Processing")
            .AddMeter("ShotsPusher.Webhook")
            .AddRuntimeInstrumentation();

        // Configure OTLP exporter for metrics (Prometheus)
        var metricsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT") ?? "http://localhost:4317";
        try
        {
            Console.WriteLine($"Configuring metrics OTLP exporter with endpoint: {metricsEndpoint}");
            meterProviderBuilder.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(metricsEndpoint);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to configure metrics OTLP exporter: {ex.Message}");
            Console.WriteLine("Using console exporter for metrics...");
            meterProviderBuilder.AddConsoleExporter();
        }
    });

// Add the worker service
builder.Services.AddHostedService<ShotsPusherWorker>();

var host = builder.Build();

await host.RunAsync();

public class ShotsPusherWorker : BackgroundService
{
    private readonly ILogger<ShotsPusherWorker> _logger;
    private readonly IServiceBusConsumerService _consumerService;

    public ShotsPusherWorker(ILogger<ShotsPusherWorker> logger, IServiceBusConsumerService consumerService)
    {
        _logger = logger;
        _consumerService = consumerService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Shots Pusher Worker starting at: {time}", DateTimeOffset.Now);

        try
        {
            // Start the Service Bus consumer
            await _consumerService.StartProcessingAsync(stoppingToken);
            _logger.LogInformation("Service Bus consumer started successfully");

            // Keep the service running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shots Pusher Worker stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Shots Pusher Worker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shots Pusher Worker stopping...");
        
        try
        {
            await _consumerService.StopProcessingAsync(cancellationToken);
            _logger.LogInformation("Service Bus consumer stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Service Bus consumer");
        }

        await base.StopAsync(cancellationToken);
    }


} 