using Azure.Messaging.ServiceBus;
using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;

namespace Collector.Services;

public interface IServiceBusService
{
    Task PublishChampionshipDataAsync(ChampionshipData data, string? traceId = null, CancellationToken cancellationToken = default);
}

public class ServiceBusService : IServiceBusService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _topicSender;
    private readonly ILogger<ServiceBusService> _logger;
    private readonly IServiceBusTracing _tracing;
    private readonly IEnvironmentMapper _environmentMapper;
    private readonly string _topicName;
    
    // Métricas customizadas
    private static readonly ActivitySource ActivitySource = new("Collector.ServiceBus");
    private static readonly Meter Meter = new("Collector.ServiceBus");
    private static readonly Counter<int> MessagesPublishedCounter = Meter.CreateCounter<int>("servicebus_messages_published_total", "Total number of messages published to Service Bus");
    private static readonly Counter<int> PublishErrorsCounter = Meter.CreateCounter<int>("servicebus_publish_errors_total", "Total number of Service Bus publish errors");
    private static readonly Histogram<double> PublishDurationHistogram = Meter.CreateHistogram<double>("servicebus_publish_duration_seconds", "Duration of Service Bus publish operations in seconds");

    public ServiceBusService(IConfiguration configuration, ILogger<ServiceBusService> logger, IServiceBusTracing tracing, IEnvironmentMapper environmentMapper)
    {
        _logger = logger;
        _tracing = tracing;
        _environmentMapper = environmentMapper;
        _topicName = configuration["AzureServiceBus:TopicName"] ?? "championship-events";
        
        // Try multiple configuration sources for connection string
        var connectionString = configuration["AzureServiceBus:ConnectionString"];
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Service Bus connection string is not configured. " +
                "Please set AzureServiceBus:ConnectionString in appsettings.json or AZURE_SERVICE_BUS_CONNECTION_STRING environment variable.");
        }

        // Expand environment variables in connection string if needed
        connectionString = Environment.ExpandEnvironmentVariables(connectionString);

        try
        {
            _client = new ServiceBusClient(connectionString);
            _topicSender = _client.CreateSender(_topicName);
            
            _logger.LogInformation("ServiceBusService initialized with topic: {TopicName}", _topicName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ServiceBusService with connection string. Topic: {TopicName}", _topicName);
            throw new InvalidOperationException($"Failed to connect to Azure Service Bus. Please verify your connection string and network connectivity. Topic: {_topicName}", ex);
        }
    }

    public async Task PublishChampionshipDataAsync(ChampionshipData data, string? traceId = null, CancellationToken cancellationToken = default)
    {
        using var activity = _tracing.StartPublishActivity("PublishChampionshipData", data);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {

            // Serialize the data to JSON
            var messageBody = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Create the Service Bus message
            var messageId = Guid.NewGuid().ToString();
            var message = new ServiceBusMessage(messageBody)
            {
                ContentType = "application/json",
                MessageId = messageId,
                TimeToLive = TimeSpan.FromHours(24) // Message will expire after 24 hours
            };

            // Add custom properties for Service Bus message filtering
            message.ApplicationProperties.Add("idChampionship", data.IdChampionship);
            message.ApplicationProperties.Add("idMatch", data.IdMatch);
            message.ApplicationProperties.Add("idSkill", data.IdSkill);
            message.ApplicationProperties.Add("timestamp", data.Timestamp.ToString("O")); // ISO 8601 format
            message.ApplicationProperties.Add("eventType", "ChampionshipData");
            
            // Add correlation properties for tracking
            message.ApplicationProperties.Add("source", _environmentMapper.GetServiceName());
            message.ApplicationProperties.Add("version", _environmentMapper.GetServiceVersion());
            message.ApplicationProperties.Add("environment", _environmentMapper.GetEnvironmentName());
            message.ApplicationProperties.Add("host", _environmentMapper.GetHostName());
            
            // Enriquecer atividade com informações da mensagem e tracing
            _tracing.EnrichPublishActivity(activity, message, traceId);

            // Send the message to the topic
            await _topicSender.SendMessageAsync(message, cancellationToken);
            
            stopwatch.Stop();
            
            // Record metrics
            MessagesPublishedCounter.Add(1, new KeyValuePair<string, object?>("championship_id", data.IdChampionship),
                                              new KeyValuePair<string, object?>("skill_id", data.IdSkill),
                                              new KeyValuePair<string, object?>("environment", _environmentMapper.GetEnvironmentName()));
            PublishDurationHistogram.Record(stopwatch.Elapsed.TotalSeconds);
            
            _tracing.RecordPublishSuccess(activity, stopwatch.Elapsed, data);

            _logger.LogInformation(
                "Championship data published successfully. MessageId: {MessageId}, TraceId: {TraceId}, Championship: {Championship}, Match: {Match}, Skill: {Skill}, Duration: {Duration}ms",
                messageId, traceId, data.IdChampionship, data.IdMatch, data.IdSkill, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Record error metrics
            PublishErrorsCounter.Add(1, new KeyValuePair<string, object?>("championship_id", data.IdChampionship),
                                         new KeyValuePair<string, object?>("error_type", ex.GetType().Name),
                                         new KeyValuePair<string, object?>("environment", _environmentMapper.GetEnvironmentName()));
            PublishDurationHistogram.Record(stopwatch.Elapsed.TotalSeconds);
            
            _tracing.RecordPublishError(activity, ex, stopwatch.Elapsed, data);

            _logger.LogError(ex, 
                "Failed to publish championship data. TraceId: {TraceId}, Championship: {Championship}, Match: {Match}, Skill: {Skill}, Duration: {Duration}ms",
                traceId, data.IdChampionship, data.IdMatch, data.IdSkill, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _topicSender.DisposeAsync();
            await _client.DisposeAsync();
            _logger.LogInformation("ServiceBusService disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing ServiceBusService");
        }
    }
} 