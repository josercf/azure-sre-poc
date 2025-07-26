using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System.Text.Json;

namespace AdminPanel.Services;

public class ServiceBusManagementService : IServiceBusManagementService
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ServiceBusManagementService> _logger;
    private const string TopicName = "championship-events";
    private const string ProvisioningTopicName = "client-provisioning";

    public ServiceBusManagementService(IConfiguration configuration, ILogger<ServiceBusManagementService> logger)
    {
        _logger = logger;
        
        var connectionString = configuration.GetConnectionString("ServiceBus") 
                             ?? configuration["AzureServiceBus:ConnectionString"];
                             
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Service Bus connection string not configured");
        }

        _adminClient = new ServiceBusAdministrationClient(connectionString);
        _serviceBusClient = new ServiceBusClient(connectionString);
    }

    public async Task<List<string>> CreateClientSubscriptionsAsync(string clientName, List<string> serviceTypes, List<int> championshipIds)
    {
        var createdSubscriptions = new List<string>();
        var clientNameLower = clientName.ToLowerInvariant().Replace(" ", "-");

        try
        {
            foreach (var serviceType in serviceTypes)
            {
                var subscriptionName = $"{clientNameLower}-{serviceType}";
                
                // Create subscription if it doesn't exist
                if (!await _adminClient.SubscriptionExistsAsync(TopicName, subscriptionName))
                {
                    var subscriptionOptions = new CreateSubscriptionOptions(TopicName, subscriptionName)
                    {
                        DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                        MaxDeliveryCount = 10
                    };

                    await _adminClient.CreateSubscriptionAsync(subscriptionOptions);
                    _logger.LogInformation("Created subscription: {SubscriptionName}", subscriptionName);
                }

                // Create filters for this subscription
                await CreateSubscriptionFiltersAsync(subscriptionName, serviceType, championshipIds);
                createdSubscriptions.Add(subscriptionName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscriptions for client: {ClientName}", clientName);
            throw;
        }

        return createdSubscriptions;
    }

    private async Task CreateSubscriptionFiltersAsync(string subscriptionName, string serviceType, List<int> championshipIds)
    {
        try
        {
            // Remove default filter
            if (await _adminClient.RuleExistsAsync(TopicName, subscriptionName, "$Default"))
            {
                await _adminClient.DeleteRuleAsync(TopicName, subscriptionName, "$Default");
            }

            // Create championship filter
            var championshipFilter = string.Join(" OR ", championshipIds.Select(id => $"idChampionship = {id}"));
            
            var filterExpression = serviceType switch
            {
                "coleta" => $"({championshipFilter}) AND (eventType = 'ChampionshipData' OR eventType = 'SkillEvent')",
                "finalizacao" => $"({championshipFilter}) AND (eventType = 'ChampionshipData' OR eventType = 'SkillEvent') AND (idSkill IN (3, 8, 18))", // Shot, Goal, Goal Against
                "periodo-partida" => $"({championshipFilter}) AND eventType = 'GamePeriod'",
                "escalacao" => $"({championshipFilter}) AND eventType = 'Lineup'",
                "finalizacao-xg" => $"({championshipFilter}) AND eventType = 'xGEvent'",
                _ => $"({championshipFilter}) AND eventType = 'ChampionshipData'"
            };

            var ruleName = $"{serviceType}-filter";
            var ruleOptions = new CreateRuleOptions(ruleName, new SqlRuleFilter(filterExpression));
            
            await _adminClient.CreateRuleAsync(TopicName, subscriptionName, ruleOptions);
            _logger.LogInformation("Created filter for subscription {SubscriptionName}: {FilterExpression}", 
                subscriptionName, filterExpression);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating filters for subscription: {SubscriptionName}", subscriptionName);
            throw;
        }
    }

    public async Task<bool> DeleteClientSubscriptionsAsync(string clientName, List<string> serviceTypes)
    {
        var clientNameLower = clientName.ToLowerInvariant().Replace(" ", "-");

        try
        {
            foreach (var serviceType in serviceTypes)
            {
                var subscriptionName = $"{clientNameLower}-{serviceType}";
                
                if (await _adminClient.SubscriptionExistsAsync(TopicName, subscriptionName))
                {
                    await _adminClient.DeleteSubscriptionAsync(TopicName, subscriptionName);
                    _logger.LogInformation("Deleted subscription: {SubscriptionName}", subscriptionName);
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subscriptions for client: {ClientName}", clientName);
            return false;
        }
    }

    public async Task<bool> PublishProvisioningMessageAsync(int clientId, string keyVaultCredentialsKey)
    {
        try
        {
            var sender = _serviceBusClient.CreateSender(ProvisioningTopicName);
            
            var provisioningMessage = new
            {
                ClientId = clientId,
                KeyVaultCredentialsKey = keyVaultCredentialsKey,
                Action = "provision",
                Timestamp = DateTime.UtcNow
            };

            var messageBody = JsonSerializer.Serialize(provisioningMessage);
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                Subject = "ClientProvisioning",
                ApplicationProperties =
                {
                    ["clientId"] = clientId,
                    ["action"] = "provision",
                    ["keyVaultKey"] = keyVaultCredentialsKey
                }
            };

            await sender.SendMessageAsync(serviceBusMessage);
            _logger.LogInformation("Published provisioning message for client: {ClientId}", clientId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing provisioning message for client: {ClientId}", clientId);
            return false;
        }
    }
}