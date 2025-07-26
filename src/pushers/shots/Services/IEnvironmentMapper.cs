using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace ShotsPusher.Services;

public interface IEnvironmentMapper
{
    void EnrichActivity(Activity? activity, string operation);
    Dictionary<string, object?> GetEnvironmentTags();
    string GetServiceName();
    string GetServiceVersion();
    string GetEnvironmentName();
    string GetHostName();
}

public class EnvironmentMapper : IEnvironmentMapper
{
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, object?> _environmentTags;

    public EnvironmentMapper(IConfiguration configuration)
    {
        _configuration = configuration;
        _environmentTags = BuildEnvironmentTags();
    }

    public void EnrichActivity(Activity? activity, string operation)
    {
        if (activity == null) return;

        // Adicionar tags padrão do ambiente
        foreach (var tag in _environmentTags)
        {
            activity.SetTag(tag.Key, tag.Value);
        }

        // Adicionar informações específicas da operação
        activity.SetTag("operation.name", operation);
        activity.SetTag("operation.timestamp", DateTimeOffset.UtcNow.ToString("O"));
    }

    public Dictionary<string, object?> GetEnvironmentTags()
    {
        return new Dictionary<string, object?>(_environmentTags);
    }

    public string GetServiceName()
    {
        return _environmentTags["service.name"]?.ToString() ?? "shots-pusher";
    }

    public string GetServiceVersion()
    {
        return _environmentTags["service.version"]?.ToString() ?? "1.0.0";
    }

    public string GetEnvironmentName()
    {
        return _environmentTags["deployment.environment"]?.ToString() ?? "unknown";
    }

    public string GetHostName()
    {
        return _environmentTags["host.name"]?.ToString() ?? "unknown";
    }

    private Dictionary<string, object?> BuildEnvironmentTags()
    {
        var tags = new Dictionary<string, object?>();

        // Service information
        tags["service.name"] = _configuration["OpenTelemetry:ServiceName"] ?? "shots-pusher";
        tags["service.version"] = _configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
        tags["service.namespace"] = _configuration["OpenTelemetry:ServiceNamespace"] ?? "azure-sre-poc";

        // Environment information
        tags["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        tags["deployment.region"] = _configuration["Azure:Region"] ?? Environment.GetEnvironmentVariable("AZURE_REGION");
        tags["deployment.zone"] = _configuration["Azure:Zone"] ?? Environment.GetEnvironmentVariable("AZURE_ZONE");

        // Host information
        tags["host.name"] = Environment.MachineName;
        tags["host.type"] = Environment.GetEnvironmentVariable("HOST_TYPE") ?? "virtual";
        tags["host.arch"] = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? RuntimeInformation.ProcessArchitecture.ToString();

        // Container information (if running in container)
        var containerName = Environment.GetEnvironmentVariable("CONTAINER_NAME");
        if (!string.IsNullOrEmpty(containerName))
        {
            tags["container.name"] = containerName;
            tags["container.id"] = Environment.GetEnvironmentVariable("HOSTNAME"); // Docker container ID
        }

        // Kubernetes information (if running in k8s)
        var podName = Environment.GetEnvironmentVariable("POD_NAME");
        if (!string.IsNullOrEmpty(podName))
        {
            tags["k8s.pod.name"] = podName;
            tags["k8s.namespace.name"] = Environment.GetEnvironmentVariable("POD_NAMESPACE");
            tags["k8s.node.name"] = Environment.GetEnvironmentVariable("NODE_NAME");
            tags["k8s.cluster.name"] = Environment.GetEnvironmentVariable("CLUSTER_NAME");
        }

        // Azure specific information
        tags["cloud.provider"] = "azure";
        tags["cloud.platform"] = "azure_app_service"; // or azure_container_instances, azure_kubernetes_service
        tags["cloud.region"] = _configuration["Azure:Region"] ?? Environment.GetEnvironmentVariable("AZURE_REGION");
        tags["cloud.subscription.id"] = _configuration["Azure:SubscriptionId"] ?? Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        tags["cloud.resource_group"] = _configuration["Azure:ResourceGroup"] ?? Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP");

        // Application insights correlation
        var appInsightsKey = _configuration["ApplicationInsights:InstrumentationKey"] ?? Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
        if (!string.IsNullOrEmpty(appInsightsKey))
        {
            tags["ai.instrumentation_key"] = appInsightsKey;
        }

        // Build information
        tags["build.version"] = _configuration["Build:Version"] ?? Environment.GetEnvironmentVariable("BUILD_VERSION");
        tags["build.commit"] = _configuration["Build:Commit"] ?? Environment.GetEnvironmentVariable("BUILD_COMMIT");
        tags["build.branch"] = _configuration["Build:Branch"] ?? Environment.GetEnvironmentVariable("BUILD_BRANCH");

        // Service Bus específico para pusher de shots
        tags["messaging.system"] = "azureservicebus";
        tags["messaging.operation"] = "receive";
        tags["messaging.destination.kind"] = "subscription";
        tags["messaging.destination.name"] = "shots-subscription";

        // Remove null values
        return tags.Where(kvp => kvp.Value != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}