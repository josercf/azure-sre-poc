using System.Text.Json;

namespace Collector.Services;

public class MockServiceBusService : IServiceBusService
{
    private readonly ILogger<MockServiceBusService> _logger;

    public MockServiceBusService(ILogger<MockServiceBusService> logger)
    {
        _logger = logger;
        _logger.LogWarning("Using MockServiceBusService - Azure Service Bus functionality is disabled");
    }

    public async Task PublishChampionshipDataAsync(ChampionshipData data, string? traceId = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken); // Simulate some processing time
        
        var messageBody = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        _logger.LogInformation(
            "MOCK: Championship data would be published to Service Bus. TraceId: {TraceId}, Championship: {Championship}, Match: {Match}, Skill: {Skill}",
            traceId, data.IdChampionship, data.IdMatch, data.IdSkill);
            
        _logger.LogDebug("MOCK: Message body would be: {MessageBody}", messageBody);
        
        // Simulate success - in a real scenario this would be sent to Azure Service Bus
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("MockServiceBusService disposed");
        return ValueTask.CompletedTask;
    }
} 