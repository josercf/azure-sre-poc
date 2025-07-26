namespace AdminPanel.Services;

public interface IServiceBusManagementService
{
    Task<List<string>> CreateClientSubscriptionsAsync(string clientName, List<string> serviceTypes, List<int> championshipIds);
    Task<bool> DeleteClientSubscriptionsAsync(string clientName, List<string> serviceTypes);
    Task<bool> PublishProvisioningMessageAsync(int clientId, string keyVaultCredentialsKey);
}