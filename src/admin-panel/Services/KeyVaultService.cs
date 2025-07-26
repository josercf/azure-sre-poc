using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using System.Text.Json;

namespace AdminPanel.Services;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<KeyVaultService> _logger;

    public KeyVaultService(IConfiguration configuration, ILogger<KeyVaultService> logger)
    {
        _logger = logger;
        
        var keyVaultUrl = configuration["AzureKeyVault:VaultUrl"];
        if (string.IsNullOrEmpty(keyVaultUrl))
        {
            throw new InvalidOperationException("Azure Key Vault URL not configured");
        }

        _secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
    }

    public async Task<string> StoreCredentialsAsync(string keyName, Dictionary<string, string> credentials)
    {
        try
        {
            var credentialsJson = JsonSerializer.Serialize(credentials);
            var secretName = $"client-{keyName}-credentials";
            
            await _secretClient.SetSecretAsync(secretName, credentialsJson);
            
            _logger.LogInformation("Credentials stored successfully for key: {SecretName}", secretName);
            return secretName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing credentials for key: {KeyName}", keyName);
            throw;
        }
    }

    public async Task<Dictionary<string, string>?> GetCredentialsAsync(string keyName)
    {
        try
        {
            var response = await _secretClient.GetSecretAsync(keyName);
            var credentialsJson = response.Value.Value;
            
            return JsonSerializer.Deserialize<Dictionary<string, string>>(credentialsJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving credentials for key: {KeyName}", keyName);
            return null;
        }
    }

    public async Task<bool> DeleteCredentialsAsync(string keyName)
    {
        try
        {
            await _secretClient.StartDeleteSecretAsync(keyName);
            _logger.LogInformation("Credentials deleted successfully for key: {KeyName}", keyName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting credentials for key: {KeyName}", keyName);
            return false;
        }
    }
}