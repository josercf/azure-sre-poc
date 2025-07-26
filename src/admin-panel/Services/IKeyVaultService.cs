namespace AdminPanel.Services;

public interface IKeyVaultService
{
    Task<string> StoreCredentialsAsync(string keyName, Dictionary<string, string> credentials);
    Task<Dictionary<string, string>?> GetCredentialsAsync(string keyName);
    Task<bool> DeleteCredentialsAsync(string keyName);
}