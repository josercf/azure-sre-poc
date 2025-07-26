using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace EventsPusher.Services;

public interface ITraceContextPropagator
{
    ActivityContext? ExtractTraceContext(ServiceBusReceivedMessage message);
    Activity? StartActivityWithParent(string activityName, ActivityContext? parentContext, ActivitySource activitySource);
}

public class TraceContextPropagator : ITraceContextPropagator
{
    private readonly ILogger<TraceContextPropagator> _logger;

    public TraceContextPropagator(ILogger<TraceContextPropagator> logger)
    {
        _logger = logger;
    }

    public ActivityContext? ExtractTraceContext(ServiceBusReceivedMessage message)
    {
        try
        {
            // Tentar extrair traceparent no formato W3C
            if (message.ApplicationProperties.TryGetValue("traceparent", out var traceparentObj) && 
                traceparentObj is string traceparent)
            {
                _logger.LogDebug("Found traceparent in message: {Traceparent}", traceparent);
                
                // Parse W3C traceparent format: 00-{trace-id}-{span-id}-{flags}
                var parts = traceparent.Split('-');
                if (parts.Length == 4 && parts[0] == "00")
                {
                    var traceIdString = parts[1];
                    var spanIdString = parts[2];
                    var flagsString = parts[3];

                    if (traceIdString.Length == 32 && spanIdString.Length == 16 && 
                        byte.TryParse(flagsString, System.Globalization.NumberStyles.HexNumber, null, out var flagsByte))
                    {
                        try
                        {
                            var traceId = ActivityTraceId.CreateFromString(traceIdString.AsSpan());
                            var spanId = ActivitySpanId.CreateFromString(spanIdString.AsSpan());
                            var flags = (ActivityTraceFlags)flagsByte;
                            
                            // Extrair tracestate se disponível
                            string? traceState = null;
                            if (message.ApplicationProperties.TryGetValue("tracestate", out var traceStateObj) && 
                                traceStateObj is string ts)
                            {
                                traceState = ts;
                            }

                            var context = new ActivityContext(traceId, spanId, flags, traceState);
                            
                            _logger.LogInformation("Successfully extracted trace context - TraceId: {TraceId}, SpanId: {SpanId}", 
                                traceId, spanId);
                                
                            return context;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse W3C trace context {Traceparent} for message {MessageId}", 
                                traceparent, message.MessageId);
                        }
                    }
                }
            }
            
            // Fallback: tentar extrair usando propriedades legacy
            if (message.ApplicationProperties.TryGetValue("trace_id", out var traceIdObj) && 
                traceIdObj is string legacyTraceId && legacyTraceId.Length == 32)
            {
                try
                {
                    var fallbackTraceId = ActivityTraceId.CreateFromString(legacyTraceId.AsSpan());
                    _logger.LogWarning("Using legacy trace_id extraction for message {MessageId}", message.MessageId);
                    
                    // Criar um novo span ID para este serviço
                    var newSpanId = ActivitySpanId.CreateRandom();
                    var context = new ActivityContext(fallbackTraceId, newSpanId, ActivityTraceFlags.Recorded);
                    
                    return context;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse legacy trace_id {TraceId} for message {MessageId}", 
                        legacyTraceId, message.MessageId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract trace context from message {MessageId}", message.MessageId);
        }

        _logger.LogWarning("No valid trace context found in message {MessageId}", message.MessageId);
        return null;
    }

    public Activity? StartActivityWithParent(string activityName, ActivityContext? parentContext, ActivitySource activitySource)
    {
        try
        {
            Activity? activity;
            
            if (parentContext.HasValue)
            {
                // Criar atividade como filho do contexto pai
                activity = activitySource.CreateActivity(activityName, ActivityKind.Consumer, parentContext.Value);
                
                if (activity != null)
                {
                    activity.Start();
                    _logger.LogDebug("Started child activity {ActivityName} with parent trace {TraceId}", 
                        activityName, parentContext.Value.TraceId);
                }
            }
            else
            {
                // Criar nova atividade raiz
                activity = activitySource.StartActivity(activityName, ActivityKind.Consumer);
                _logger.LogDebug("Started root activity {ActivityName} (no parent context)", activityName);
            }

            return activity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start activity {ActivityName}", activityName);
            return null;
        }
    }
}