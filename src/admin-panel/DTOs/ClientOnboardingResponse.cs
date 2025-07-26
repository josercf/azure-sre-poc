namespace AdminPanel.DTOs;

public class ClientOnboardingResponse
{
    public int ClientId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string KeyVaultCredentialsKey { get; set; } = string.Empty;
    public List<string> CreatedSubscriptions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}