using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Collector.Services;
using Microsoft.AspNetCore.Http.Extensions;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register environment mapping and tracing services
builder.Services.AddSingleton<IEnvironmentMapper, EnvironmentMapper>();
builder.Services.AddSingleton<IServiceBusTracing, ServiceBusTracing>();

// Register Service Bus service with fallback to mock
builder.Services.AddSingleton<IServiceBusService>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<ServiceBusService>>();
    var tracing = serviceProvider.GetRequiredService<IServiceBusTracing>();
    var environmentMapper = serviceProvider.GetRequiredService<IEnvironmentMapper>();
    
    try
    {
        // Try to create the real service
        return new ServiceBusService(configuration, logger, tracing, environmentMapper);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Failed to initialize ServiceBusService: {ex.Message}");
        Console.WriteLine("Falling back to MockServiceBusService for development.");
        
        var mockLogger = serviceProvider.GetRequiredService<ILogger<MockServiceBusService>>();
        return new MockServiceBusService(mockLogger);
    }
});

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
            .AddSource("Collector.ServiceBus")
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    activity.SetTag("http.request.method", request.Method);
                    activity.SetTag("http.request.url", request.GetDisplayUrl());
                };
                options.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity.SetTag("http.response.status_code", response.StatusCode);
                };
            });

        // Add OTLP exporter
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
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
                Console.WriteLine("Continuing without OTLP tracing...");
            }
        }
        else
        {
            Console.WriteLine("OTEL_EXPORTER_OTLP_ENDPOINT not set, using console exporter for tracing");
            tracerProviderBuilder.AddConsoleExporter();
        }
    })
    .WithMetrics(meterProviderBuilder =>
        meterProviderBuilder
            .AddMeter("Collector.ServiceBus")
            .AddAspNetCoreInstrumentation()
            .AddPrometheusExporter());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

// Prometheus metrics endpoint
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "collector" }));

// Root endpoint
app.MapGet("/", () => "Collector - Data Collection and Service Bus Publisher");

// Championship data collection endpoint
app.MapPost("/api/collect", async (ChampionshipData data, IServiceBusService serviceBusService, ILogger<Program> logger, HttpContext context) => 
{
    using var activity = Activity.Current;
    var traceId = activity?.TraceId.ToString() ?? context.TraceIdentifier;
    var requestId = Guid.NewGuid().ToString();
    
    // Adicionar informações de contexto ao span atual
    activity?.SetTag("operation.name", "collect-championship-data");
    activity?.SetTag("request.id", requestId);
    activity?.SetTag("championship.id", data.IdChampionship);
    activity?.SetTag("match.id", data.IdMatch);
    activity?.SetTag("skill.id", data.IdSkill);
    
    try
    {
        // Validate input data
        if (data.IdChampionship <= 0 || data.IdMatch <= 0 || data.IdSkill <= 0)
        {
            activity?.SetTag("validation.result", "failed");
            activity?.SetTag("error.type", "ValidationError");
            
            logger.LogWarning(
                "Invalid championship data received. TraceId: {TraceId}, RequestId: {RequestId}, Championship: {Championship}, Match: {Match}, Skill: {Skill}",
                traceId, requestId, data.IdChampionship, data.IdMatch, data.IdSkill);
                
            return Results.BadRequest(new { 
                error = "Invalid field values", 
                message = "idChampionship, idMatch, and idSkill must be positive integers",
                traceId = traceId,
                requestId = requestId
            });
        }

        activity?.SetTag("validation.result", "passed");

        // Log the collected data
        logger.LogInformation(
            "Collected championship data: TraceId={TraceId}, RequestId={RequestId}, Championship={Championship}, Match={Match}, Skill={Skill}, Timestamp={Timestamp}",
            traceId, requestId, data.IdChampionship, data.IdMatch, data.IdSkill, data.Timestamp);
        
        // Publish to Service Bus with message properties for filtering and trace information
        await serviceBusService.PublishChampionshipDataAsync(data, traceId);
        
        activity?.SetTag("publish.result", "success");
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        return Results.Ok(new { 
            id = requestId, 
            message = "Championship data collected and published to Service Bus successfully",
            traceId = traceId,
            data = new {
                championship = data.IdChampionship,
                match = data.IdMatch,
                skill = data.IdSkill,
                timestamp = data.Timestamp,
                published = DateTime.UtcNow
            }
        });
    }
    catch (Exception ex)
    {
        activity?.SetTag("publish.result", "failed");
        activity?.SetTag("error.type", ex.GetType().Name);
        activity?.SetTag("error.message", ex.Message);
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        
        logger.LogError(ex, 
            "Error processing championship data: TraceId={TraceId}, RequestId={RequestId}, Championship={Championship}, Match={Match}, Skill={Skill}", 
            traceId, requestId, data.IdChampionship, data.IdMatch, data.IdSkill);
            
        return Results.Problem(
            title: "Error processing championship data",
            detail: ex.Message,
            statusCode: 500,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = traceId,
                ["requestId"] = requestId
            });
    }
});

app.MapControllers();

app.Run();

public record ChampionshipData(int IdChampionship, int IdMatch, int IdSkill, DateTime Timestamp); 