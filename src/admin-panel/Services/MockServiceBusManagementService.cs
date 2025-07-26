using System.Collections.Concurrent;

namespace AdminPanel.Services;

public class MockServiceBusManagementService : IServiceBusManagementService
{
    private readonly ConcurrentDictionary<string, List<string>> _subscriptions = new();
    private readonly ILogger<MockServiceBusManagementService> _logger;

    public MockServiceBusManagementService(ILogger<MockServiceBusManagementService> logger)
    {
        _logger = logger;
    }

    public Task<List<string>> CreateClientSubscriptionsAsync(string clientName, List<string> serviceTypes, List<int> championshipIds)
    {
        var createdSubscriptions = new List<string>();
        var clientNameLower = clientName.ToLowerInvariant().Replace(" ", "-");

        try
        {
            foreach (var serviceType in serviceTypes)
            {
                var subscriptionName = $"{clientNameLower}-{serviceType}";
                
                // Mock subscription creation
                _subscriptions.AddOrUpdate(subscriptionName, 
                    championshipIds.Select(id => $"filter-championship-{id}").ToList(),
                    (key, existing) => championshipIds.Select(id => $"filter-championship-{id}").ToList());
                
                _logger.LogInformation("Mock: Created subscription: {SubscriptionName} with filters for championships: {Championships}", 
                    subscriptionName, string.Join(", ", championshipIds));
                
                createdSubscriptions.Add(subscriptionName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock: Error creating subscriptions for client: {ClientName}", clientName);
            throw;
        }

        return Task.FromResult(createdSubscriptions);
    }

    public Task<bool> DeleteClientSubscriptionsAsync(string clientName, List<string> serviceTypes)
    {
        var clientNameLower = clientName.ToLowerInvariant().Replace(" ", "-");

        try
        {
            foreach (var serviceType in serviceTypes)
            {
                var subscriptionName = $"{clientNameLower}-{serviceType}";
                
                if (_subscriptions.TryRemove(subscriptionName, out _))
                {
                    _logger.LogInformation("Mock: Deleted subscription: {SubscriptionName}", subscriptionName);
                }
            }
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock: Error deleting subscriptions for client: {ClientName}", clientName);
            return Task.FromResult(false);
        }
    }

    public Task<bool> PublishProvisioningMessageAsync(int clientId, string keyVaultCredentialsKey)
    {
        try
        {
            _logger.LogInformation("Mock: Published provisioning message for client: {ClientId} with KeyVault key: {KeyVaultKey}", 
                clientId, keyVaultCredentialsKey);
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock: Error publishing provisioning message for client: {ClientId}", clientId);
            return Task.FromResult(false);
        }
    }
}