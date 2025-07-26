using Azure.Messaging.ServiceBus;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ShotsPusher.Services;

public interface IServiceBusConsumerService : IAsyncDisposable
{
    Task StartProcessingAsync(CancellationToken cancellationToken = default);
    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}

public class ServiceBusConsumerService : IServiceBusConsumerService
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _processor;
    private readonly ILogger<ServiceBusConsumerService> _logger;
    private readonly IServiceBusTracing _tracing;
    private readonly IMetricsService _metricsService;
    private readonly IWebhookService _webhookService;
    private readonly string _subscriptionName;

    public ServiceBusConsumerService(
        IConfiguration configuration,
        ILogger<ServiceBusConsumerService> logger,
        IServiceBusTracing tracing,
        IMetricsService metricsService,
        IWebhookService webhookService)
    {
        _logger = logger;
        _tracing = tracing;
        _metricsService = metricsService;
        _webhookService = webhookService;

        var connectionString = configuration["AzureServiceBus:ConnectionString"];
        var topicName = configuration["AzureServiceBus:TopicName"] ;
        _subscriptionName = configuration["AzureServiceBus:ShotsSubscriptionName"];

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Service Bus connection string is not configured.");
        }

        _logger.LogInformation("Connection string: {ConnectionString}", connectionString);
        _logger.LogInformation("Topic name: {TopicName}", topicName);
        _logger.LogInformation("Subscription name: {SubscriptionName}", _subscriptionName);

        // Expand environment variables in connection string if needed
        connectionString = Environment.ExpandEnvironmentVariables(connectionString);

        try
        {
            _client = new ServiceBusClient(connectionString);
            
            // Create processor for the subscription with filter for shots (skill 4)
            var processorOptions = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                PrefetchCount = 10
            };

            _processor = _client.CreateProcessor(topicName, _subscriptionName, processorOptions);
            
            // Set up event handlers
            _processor.ProcessMessageAsync += ProcessMessageAsync;
            _processor.ProcessErrorAsync += ProcessErrorAsync;

            _logger.LogInformation("ServiceBusConsumerService initialized for topic: {TopicName}, subscription: {SubscriptionName}",
                topicName, _subscriptionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ServiceBusConsumerService");
            throw;
        }
    }

    public async Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _processor.StartProcessingAsync(cancellationToken);
            _logger.LogInformation("Started processing messages from subscription: {SubscriptionName}", _subscriptionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start processing messages");
            throw;
        }
    }

    public async Task StopProcessingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _processor.StopProcessingAsync(cancellationToken);
            _logger.LogInformation("Stopped processing messages from subscription: {SubscriptionName}", _subscriptionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop processing messages");
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        using var receiveActivity = _tracing.StartReceiveActivity("ReceiveShots");
        var receiveStopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var message = args.Message;
            _logger.LogInformation("Received message: {MessageId}", message.MessageId);
            
            // Enrich activity with message information
            _tracing.EnrichReceiveActivity(receiveActivity, message);
            
            receiveStopwatch.Stop();
            _tracing.RecordReceiveSuccess(receiveActivity, receiveStopwatch.Elapsed, 1);
            _metricsService.RecordMessageReceived(1, _subscriptionName);

            // Process the message
            using var processActivity = _tracing.StartProcessActivity("ProcessShots", message);
            var processStopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Extract championship data from message
                var messageBody = message.Body.ToString();
                var championshipData = JsonSerializer.Deserialize<ChampionshipData>(messageBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (championshipData == null)
                {
                    throw new InvalidOperationException("Failed to deserialize championship data");
                }

                _logger.LogInformation("Processing championship data: Championship={Championship}, Match={Match}, Skill={Skill}",
                    championshipData.IdChampionship, championshipData.IdMatch, championshipData.IdSkill);

                // Filter for shots (skill 4)
                if (championshipData.IdSkill == 4)
                {
                    _tracing.EnrichProcessActivity(processActivity, championshipData);
                    
                    // Record shots-specific metrics
                    var shotCount = Random.Shared.Next(10, 25); // Simulate shot count analysis
                    _metricsService.RecordShotsProcessed(shotCount, processStopwatch.Elapsed.TotalMilliseconds);
                    
                    processStopwatch.Stop();
                    _tracing.RecordProcessSuccess(processActivity, processStopwatch.Elapsed, championshipData);
                    _metricsService.RecordMessageProcessed(1, processStopwatch.Elapsed.TotalMilliseconds, true);

                    // Send to webhook
                    using var webhookActivity = _tracing.StartWebhookActivity("PushShotsWebhook", championshipData);
                    var webhookStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        var webhookUrl = "http://demo5566824.mockable.io/events";
                        _tracing.EnrichWebhookActivity(webhookActivity, webhookUrl, championshipData);

                        var success = await _webhookService.SendChampionshipDataAsync(championshipData, webhookUrl, shotCount);
                        
                        webhookStopwatch.Stop();
                        if (success)
                        {
                            _tracing.RecordWebhookSuccess(webhookActivity, webhookStopwatch.Elapsed, 200);
                            _metricsService.RecordWebhookCall(webhookStopwatch.Elapsed.TotalMilliseconds, 200, true);
                        }
                        else
                        {
                            _tracing.RecordWebhookSuccess(webhookActivity, webhookStopwatch.Elapsed, 500);
                            _metricsService.RecordWebhookCall(webhookStopwatch.Elapsed.TotalMilliseconds, 500, false);
                        }

                        // Complete the message
                        await args.CompleteMessageAsync(message);
                        _logger.LogInformation("Message processed and completed successfully: {MessageId}", message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        webhookStopwatch.Stop();
                        _tracing.RecordError(webhookActivity, ex, webhookStopwatch.Elapsed, "webhook");
                        _metricsService.RecordError("webhook", ex.GetType().Name);
                        _metricsService.RecordWebhookCall(webhookStopwatch.Elapsed.TotalMilliseconds, 500, false);
                        throw;
                    }
                }
                else
                {
                    // Not a shots skill, abandon the message
                    await args.AbandonMessageAsync(message);
                    _logger.LogInformation("Message abandoned - not a shots skill (skill={Skill}): {MessageId}", 
                        championshipData.IdSkill, message.MessageId);
                }
            }
            catch (Exception ex)
            {
                processStopwatch.Stop();
                _tracing.RecordError(processActivity, ex, processStopwatch.Elapsed, "process");
                _metricsService.RecordError("process", ex.GetType().Name);
                _metricsService.RecordMessageProcessed(1, processStopwatch.Elapsed.TotalMilliseconds, false);
                throw;
            }
        }
        catch (Exception ex)
        {
            receiveStopwatch.Stop();
            _tracing.RecordError(receiveActivity, ex, receiveStopwatch.Elapsed, "receive");
            _metricsService.RecordError("receive", ex.GetType().Name);
            
            _logger.LogError(ex, "Error processing message: {MessageId}", args.Message.MessageId);
            
            // Dead letter the message after multiple failures
            await args.DeadLetterMessageAsync(args.Message, "ProcessingError", ex.Message);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Error occurred while processing Service Bus message. Source: {Source}, EntityPath: {EntityPath}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_processor != null)
            {
                await _processor.DisposeAsync();
            }
            if (_client != null)
            {
                await _client.DisposeAsync();
            }
            _logger.LogInformation("ServiceBusConsumerService disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing ServiceBusConsumerService");
        }
    }
}

// Data model for championship data
public record ChampionshipData(int IdChampionship, int IdMatch, int IdSkill, DateTime Timestamp); 