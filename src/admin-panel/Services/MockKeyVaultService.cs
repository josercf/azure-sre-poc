using System.Collections.Concurrent;
using System.Text.Json;

namespace AdminPanel.Services;

public class MockKeyVaultService : IKeyVaultService
{
    private readonly ConcurrentDictionary<string, string> _secrets = new();
    private readonly ILogger<MockKeyVaultService> _logger;

    public MockKeyVaultService(ILogger<MockKeyVaultService> logger)
    {
        _logger = logger;
    }

    public Task<string> StoreCredentialsAsync(string keyName, Dictionary<string, string> credentials)
    {
        try
        {
            var credentialsJson = JsonSerializer.Serialize(credentials);
            var secretName = $"client-{keyName}-credentials";
            
            _secrets[secretName] = credentialsJson;
            
            _logger.LogInformation("Mock: Credentials stored successfully for key: {SecretName}", secretName);
            return Task.FromResult(secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock: Error storing credentials for key: {KeyName}", keyName);
            throw;
        }
    }

    public Task<Dictionary<string, string>?> GetCredentialsAsync(string keyName)
    {
        try
        {
            if (_secrets.TryGetValue(keyName, out var credentialsJson))
            {
                var credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(credentialsJson);
                return Task.FromResult(credentials);
            }
            
            _logger.LogWarning("Mock: Credentials not found for key: {KeyName}", keyName);
            return Task.FromResult<Dictionary<string, string>?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock: Error retrieving credentials for key: {KeyName}", keyName);
            return Task.FromResult<Dictionary<string, string>?>(null);
        }
    }

    public Task<bool> DeleteCredentialsAsync(string keyName)
    {
        try
        {
            var removed = _secrets.TryRemove(keyName, out _);
            if (removed)
            {
                _logger.LogInformation("Mock: Credentials deleted successfully for key: {KeyName}", keyName);
            }
            return Task.FromResult(removed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock: Error deleting credentials for key: {KeyName}", keyName);
            return Task.FromResult(false);
        }
    }
}