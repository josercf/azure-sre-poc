using System.Diagnostics;
using Azure.Messaging.ServiceBus;

namespace Collector.Services;

public interface IServiceBusTracing
{
    Activity? StartPublishActivity(string operationName, ChampionshipData data);
    void EnrichPublishActivity(Activity? activity, ServiceBusMessage message, string? traceId);
    void RecordPublishSuccess(Activity? activity, TimeSpan duration, ChampionshipData data);
    void RecordPublishError(Activity? activity, Exception exception, TimeSpan duration, ChampionshipData data);
}

public class ServiceBusTracing : IServiceBusTracing
{
    private readonly IEnvironmentMapper _environmentMapper;
    private readonly ILogger<ServiceBusTracing> _logger;
    private static readonly ActivitySource ActivitySource = new("Collector.ServiceBus");

    public ServiceBusTracing(IEnvironmentMapper environmentMapper, ILogger<ServiceBusTracing> logger)
    {
        _environmentMapper = environmentMapper;
        _logger = logger;
    }

    public Activity? StartPublishActivity(string operationName, ChampionshipData data)
    {
        var activity = ActivitySource.StartActivity($"ServiceBus.{operationName}");
        if (activity == null) return null;

        // Enriquecer com informações do ambiente
        _environmentMapper.EnrichActivity(activity, operationName);

        // Adicionar informações específicas dos dados
        activity.SetTag("messaging.system", "azureservicebus");
        activity.SetTag("messaging.operation", "publish");
        activity.SetTag("messaging.destination.kind", "topic");
        activity.SetTag("messaging.destination.name", "championship-events");
        
        // Dados do campeonato
        activity.SetTag("championship.id", data.IdChampionship);
        activity.SetTag("match.id", data.IdMatch);
        activity.SetTag("skill.id", data.IdSkill);
        activity.SetTag("championship.timestamp", data.Timestamp.ToString("O"));

        return activity;
    }

    public void EnrichPublishActivity(Activity? activity, ServiceBusMessage message, string? traceId)
    {
        if (activity == null) return;

        // Informações da mensagem
        activity.SetTag("messaging.message.id", message.MessageId);
        activity.SetTag("messaging.message.conversation_id", message.CorrelationId);
        activity.SetTag("messaging.message.payload_size_bytes", message.Body.ToArray().Length);
        activity.SetTag("messaging.message.content_type", message.ContentType);

        // Informações de rastreamento
        if (!string.IsNullOrEmpty(traceId))
        {
            activity.SetTag("trace.id", traceId);
        }

        // Propagar trace context para a mensagem
        var propagationContext = new ActivityContext(
            activity.TraceId,
            activity.SpanId,
            activity.ActivityTraceFlags,
            activity.TraceStateString);

        // Adicionar informações de trace context às propriedades da mensagem
        message.ApplicationProperties["traceparent"] = activity.Id;
        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            message.ApplicationProperties["tracestate"] = activity.TraceStateString;
        }

        // Adicionar timestamp de envio
        activity.SetTag("messaging.publish.timestamp", DateTimeOffset.UtcNow.ToString("O"));
    }

    public void RecordPublishSuccess(Activity? activity, TimeSpan duration, ChampionshipData data)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("publish.result", "success");
        activity?.SetTag("publish.duration_ms", duration.TotalMilliseconds);

        _logger.LogInformation(
            "ServiceBus publish successful - Championship: {Championship}, Match: {Match}, Skill: {Skill}, Duration: {Duration}ms, TraceId: {TraceId}",
            data.IdChampionship, data.IdMatch, data.IdSkill, duration.TotalMilliseconds, activity?.TraceId);
    }

    public void RecordPublishError(Activity? activity, Exception exception, TimeSpan duration, ChampionshipData data)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("publish.result", "error");
        activity?.SetTag("publish.duration_ms", duration.TotalMilliseconds);
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetTag("error.message", exception.Message);
        activity?.SetTag("error.stack", exception.StackTrace);

        _logger.LogError(exception,
            "ServiceBus publish failed - Championship: {Championship}, Match: {Match}, Skill: {Skill}, Duration: {Duration}ms, TraceId: {TraceId}",
            data.IdChampionship, data.IdMatch, data.IdSkill, duration.TotalMilliseconds, activity?.TraceId);
    }
}