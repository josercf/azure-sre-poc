using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ShotsPusher.Services;

public interface IServiceBusTracing
{
    Activity? StartReceiveActivity(string operationName);
    Activity? StartProcessActivity(string operationName, ServiceBusReceivedMessage? message);
    Activity? StartWebhookActivity(string operationName, object data);
    void EnrichReceiveActivity(Activity? activity, ServiceBusReceivedMessage message);
    void EnrichProcessActivity(Activity? activity, object processedData);
    void EnrichWebhookActivity(Activity? activity, string webhookUrl, object payload);
    void RecordReceiveSuccess(Activity? activity, TimeSpan duration, int messageCount);
    void RecordProcessSuccess(Activity? activity, TimeSpan duration, object data);
    void RecordWebhookSuccess(Activity? activity, TimeSpan duration, int statusCode);
    void RecordError(Activity? activity, Exception exception, TimeSpan duration, string operation);
}

public class ServiceBusTracing : IServiceBusTracing
{
    private readonly IEnvironmentMapper _environmentMapper;
    private readonly ILogger<ServiceBusTracing> _logger;
    private static readonly ActivitySource ActivitySource = new("ShotsPusher.ServiceBus");

    public ServiceBusTracing(IEnvironmentMapper environmentMapper, ILogger<ServiceBusTracing> logger)
    {
        _environmentMapper = environmentMapper;
        _logger = logger;
    }

    public Activity? StartReceiveActivity(string operationName)
    {
        var activity = ActivitySource.StartActivity($"ServiceBus.{operationName}");
        if (activity == null) return null;

        // Enriquecer com informações do ambiente
        _environmentMapper.EnrichActivity(activity, operationName);

        // Adicionar informações específicas do Service Bus para shots
        activity.SetTag("messaging.system", "azureservicebus");
        activity.SetTag("messaging.operation", "receive");
        activity.SetTag("messaging.destination.kind", "subscription");
        activity.SetTag("messaging.destination.name", "shots-subscription");
        activity.SetTag("shots.filter", "skill_id=4");

        return activity;
    }

    public Activity? StartProcessActivity(string operationName, ServiceBusReceivedMessage? message)
    {
        var activity = ActivitySource.StartActivity($"Process.{operationName}");
        if (activity == null) return null;

        // Enriquecer com informações do ambiente
        _environmentMapper.EnrichActivity(activity, operationName);

        // Se temos uma mensagem real, adicionar informações dela
        if (message != null)
        {
            // Propagar contexto de trace da mensagem recebida
            if (message.ApplicationProperties.TryGetValue("traceparent", out var traceparent))
            {
                activity.SetTag("parent.trace.id", traceparent.ToString());
            }

            // Informações da mensagem
            activity.SetTag("messaging.message.id", message.MessageId);
            activity.SetTag("messaging.message.conversation_id", message.CorrelationId);
            activity.SetTag("messaging.message.payload_size_bytes", message.Body.ToArray().Length);
        }
        else
        {
            // Para simulação, adicionar tags indicativas
            activity.SetTag("simulation.mode", "true");
            activity.SetTag("messaging.message.id", "simulated-" + Guid.NewGuid().ToString("N")[..8]);
        }
        
        activity.SetTag("shots.processing", "true");

        return activity;
    }

    public Activity? StartWebhookActivity(string operationName, object data)
    {
        var activity = ActivitySource.StartActivity($"Webhook.{operationName}");
        if (activity == null) return null;

        // Enriquecer com informações do ambiente
        _environmentMapper.EnrichActivity(activity, operationName);

        // Informações do webhook específico para shots
        activity.SetTag("http.method", "POST");
        activity.SetTag("webhook.type", "championship_shots");
        activity.SetTag("shots.webhook", "true");

        return activity;
    }

    public void EnrichReceiveActivity(Activity? activity, ServiceBusReceivedMessage message)
    {
        if (activity == null) return;

        // Informações da mensagem recebida
        activity.SetTag("messaging.message.id", message.MessageId);
        activity.SetTag("messaging.message.conversation_id", message.CorrelationId);
        activity.SetTag("messaging.message.delivery_count", message.DeliveryCount);
        activity.SetTag("messaging.message.enqueued_time", message.EnqueuedTime.ToString("O"));
        activity.SetTag("messaging.message.size_bytes", message.Body.ToArray().Length);

        // Extrair informações do payload se disponível (específico para shots - skill ID 4)
        if (message.ApplicationProperties.TryGetValue("idChampionship", out var championshipId))
        {
            activity.SetTag("championship.id", championshipId);
        }
        if (message.ApplicationProperties.TryGetValue("idMatch", out var matchId))
        {
            activity.SetTag("match.id", matchId);
        }
        if (message.ApplicationProperties.TryGetValue("idSkill", out var skillId))
        {
            activity.SetTag("skill.id", skillId);
            // Verificar se é realmente skill ID 4 (shots)
            if (skillId.ToString() == "4")
            {
                activity.SetTag("shots.verified", "true");
            }
        }
        if (message.ApplicationProperties.TryGetValue("source", out var source))
        {
            activity.SetTag("message.source", source);
        }

        // Informações de trace correlacionado
        if (message.ApplicationProperties.TryGetValue("traceId", out var traceId))
        {
            activity.SetTag("parent.trace.id", traceId);
        }
    }

    public void EnrichProcessActivity(Activity? activity, object processedData)
    {
        if (activity == null) return;

        activity.SetTag("process.result", "success");
        activity.SetTag("process.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        activity.SetTag("shots.processed", "true");
        
        // Adicionar informações específicas dos dados processados se necessário
        if (processedData != null)
        {
            activity.SetTag("process.data_type", processedData.GetType().Name);
        }
    }

    public void EnrichWebhookActivity(Activity? activity, string webhookUrl, object payload)
    {
        if (activity == null) return;

        activity.SetTag("http.url", webhookUrl);
        activity.SetTag("webhook.payload_type", payload?.GetType().Name ?? "unknown");
        activity.SetTag("webhook.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        activity.SetTag("shots.webhook.sent", "true");
    }

    public void RecordReceiveSuccess(Activity? activity, TimeSpan duration, int messageCount)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("receive.result", "success");
        activity?.SetTag("receive.duration_ms", duration.TotalMilliseconds);
        activity?.SetTag("receive.message_count", messageCount);

        _logger.LogInformation(
            "ServiceBus shots receive successful - MessageCount: {MessageCount}, Duration: {Duration}ms, TraceId: {TraceId}",
            messageCount, duration.TotalMilliseconds, activity?.TraceId);
    }

    public void RecordProcessSuccess(Activity? activity, TimeSpan duration, object data)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("process.result", "success");
        activity?.SetTag("process.duration_ms", duration.TotalMilliseconds);

        _logger.LogInformation(
            "Shots processing successful - Duration: {Duration}ms, TraceId: {TraceId}",
            duration.TotalMilliseconds, activity?.TraceId);
    }

    public void RecordWebhookSuccess(Activity? activity, TimeSpan duration, int statusCode)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("webhook.result", "success");
        activity?.SetTag("webhook.duration_ms", duration.TotalMilliseconds);
        activity?.SetTag("http.status_code", statusCode);

        _logger.LogInformation(
            "Shots webhook call successful - StatusCode: {StatusCode}, Duration: {Duration}ms, TraceId: {TraceId}",
            statusCode, duration.TotalMilliseconds, activity?.TraceId);
    }

    public void RecordError(Activity? activity, Exception exception, TimeSpan duration, string operation)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag($"{operation}.result", "error");
        activity?.SetTag($"{operation}.duration_ms", duration.TotalMilliseconds);
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetTag("error.message", exception.Message);
        activity?.SetTag("error.stack", exception.StackTrace);

        _logger.LogError(exception,
            "Shots {Operation} failed - Duration: {Duration}ms, TraceId: {TraceId}",
            operation, duration.TotalMilliseconds, activity?.TraceId);
    }
}