using System.Diagnostics.Metrics;

namespace ShotsPusher.Services;

public interface IMetricsService
{
    void RecordMessageReceived(int count, string subscriptionName);
    void RecordMessageProcessed(int count, double durationMs, bool success);
    void RecordWebhookCall(double durationMs, int statusCode, bool success);
    void RecordError(string operationType, string errorType);
    void RecordShotsProcessed(int shotCount, double durationMs);
}

public class MetricsService : IMetricsService
{
    private static readonly Meter Meter = new("ShotsPusher.ServiceBus");
    
    // Counters
    private static readonly Counter<int> MessagesReceivedCounter = 
        Meter.CreateCounter<int>("shots_messages_received_total", "Total number of messages received from Service Bus");
    
    private static readonly Counter<int> MessagesProcessedCounter = 
        Meter.CreateCounter<int>("shots_messages_processed_total", "Total number of messages processed");
    
    private static readonly Counter<int> WebhookCallsCounter = 
        Meter.CreateCounter<int>("shots_webhook_calls_total", "Total number of webhook calls made");
    
    private static readonly Counter<int> ErrorsCounter = 
        Meter.CreateCounter<int>("shots_errors_total", "Total number of errors");
        
    private static readonly Counter<int> ShotsProcessedCounter = 
        Meter.CreateCounter<int>("shots_total_processed", "Total number of shots processed");
    
    // Histograms
    private static readonly Histogram<double> ProcessingDurationHistogram = 
        Meter.CreateHistogram<double>("shots_processing_duration_seconds", "Duration of message processing in seconds");
    
    private static readonly Histogram<double> WebhookDurationHistogram = 
        Meter.CreateHistogram<double>("shots_webhook_duration_seconds", "Duration of webhook calls in seconds");
        
    private static readonly Histogram<double> ShotsProcessingHistogram = 
        Meter.CreateHistogram<double>("shots_analysis_duration_seconds", "Duration of shots analysis in seconds");

    // Gauges
    private static readonly UpDownCounter<int> ActiveProcessingGauge = 
        Meter.CreateUpDownCounter<int>("shots_active_processing", "Number of messages currently being processed");

    public void RecordMessageReceived(int count, string subscriptionName)
    {
        MessagesReceivedCounter.Add(count, 
            new KeyValuePair<string, object?>("subscription", subscriptionName),
            new KeyValuePair<string, object?>("service", "shots-pusher"),
            new KeyValuePair<string, object?>("skill_id", "4"));
    }

    public void RecordMessageProcessed(int count, double durationMs, bool success)
    {
        MessagesProcessedCounter.Add(count, 
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"),
            new KeyValuePair<string, object?>("service", "shots-pusher"),
            new KeyValuePair<string, object?>("skill_id", "4"));
        
        ProcessingDurationHistogram.Record(durationMs / 1000.0, 
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));
    }

    public void RecordWebhookCall(double durationMs, int statusCode, bool success)
    {
        WebhookCallsCounter.Add(1, 
            new KeyValuePair<string, object?>("status_code", statusCode),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"),
            new KeyValuePair<string, object?>("service", "shots-pusher"),
            new KeyValuePair<string, object?>("webhook_type", "shots"));
        
        WebhookDurationHistogram.Record(durationMs / 1000.0, 
            new KeyValuePair<string, object?>("status_code", statusCode),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));
    }

    public void RecordError(string operationType, string errorType)
    {
        ErrorsCounter.Add(1, 
            new KeyValuePair<string, object?>("operation", operationType),
            new KeyValuePair<string, object?>("error_type", errorType),
            new KeyValuePair<string, object?>("service", "shots-pusher"),
            new KeyValuePair<string, object?>("skill_id", "4"));
    }

    public void RecordShotsProcessed(int shotCount, double durationMs)
    {
        ShotsProcessedCounter.Add(shotCount, 
            new KeyValuePair<string, object?>("service", "shots-pusher"));
        
        ShotsProcessingHistogram.Record(durationMs / 1000.0, 
            new KeyValuePair<string, object?>("shot_count", shotCount));
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