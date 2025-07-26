using System.Diagnostics.Metrics;

namespace EventsPusher.Services;

public interface IMetricsService
{
    void RecordMessageReceived(int count, string subscriptionName);
    void RecordMessageProcessed(int count, double durationMs, bool success);
    void RecordWebhookCall(double durationMs, int statusCode, bool success);
    void RecordError(string operationType, string errorType);
}

public class MetricsService : IMetricsService
{
    private static readonly Meter Meter = new("EventsPusher.ServiceBus");
    
    // Counters
    private static readonly Counter<int> MessagesReceivedCounter = 
        Meter.CreateCounter<int>("events_messages_received_total", "Total number of messages received from Service Bus");
    
    private static readonly Counter<int> MessagesProcessedCounter = 
        Meter.CreateCounter<int>("events_messages_processed_total", "Total number of messages processed");
    
    private static readonly Counter<int> WebhookCallsCounter = 
        Meter.CreateCounter<int>("events_webhook_calls_total", "Total number of webhook calls made");
    
    private static readonly Counter<int> ErrorsCounter = 
        Meter.CreateCounter<int>("events_errors_total", "Total number of errors");
    
    // Histograms
    private static readonly Histogram<double> ProcessingDurationHistogram = 
        Meter.CreateHistogram<double>("events_processing_duration_seconds", "Duration of message processing in seconds");
    
    private static readonly Histogram<double> WebhookDurationHistogram = 
        Meter.CreateHistogram<double>("events_webhook_duration_seconds", "Duration of webhook calls in seconds");

    // Gauges
    private static readonly UpDownCounter<int> ActiveProcessingGauge = 
        Meter.CreateUpDownCounter<int>("events_active_processing", "Number of messages currently being processed");

    public void RecordMessageReceived(int count, string subscriptionName)
    {
        MessagesReceivedCounter.Add(count, 
            new KeyValuePair<string, object?>("subscription", subscriptionName),
            new KeyValuePair<string, object?>("service", "events-pusher"));
    }

    public void RecordMessageProcessed(int count, double durationMs, bool success)
    {
        MessagesProcessedCounter.Add(count, 
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"),
            new KeyValuePair<string, object?>("service", "events-pusher"));
        
        ProcessingDurationHistogram.Record(durationMs / 1000.0, 
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));
    }

    public void RecordWebhookCall(double durationMs, int statusCode, bool success)
    {
        WebhookCallsCounter.Add(1, 
            new KeyValuePair<string, object?>("status_code", statusCode),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"),
            new KeyValuePair<string, object?>("service", "events-pusher"));
        
        WebhookDurationHistogram.Record(durationMs / 1000.0, 
            new KeyValuePair<string, object?>("status_code", statusCode),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));
    }

    public void RecordError(string operationType, string errorType)
    {
        ErrorsCounter.Add(1, 
            new KeyValuePair<string, object?>("operation", operationType),
            new KeyValuePair<string, object?>("error_type", errorType),
            new KeyValuePair<string, object?>("service", "events-pusher"));
    }

    public void IncrementActiveProcessing()
    {
        ActiveProcessingGauge.Add(1);
    }

    public void DecrementActiveProcessing()
    {
        ActiveProcessingGauge.Add(-1);
    }
}