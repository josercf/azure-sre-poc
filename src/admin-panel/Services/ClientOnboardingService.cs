using Microsoft.EntityFrameworkCore;
using AdminPanel.Data;
using AdminPanel.Models;
using AdminPanel.DTOs;

namespace AdminPanel.Services;

public class ClientOnboardingService : IClientOnboardingService
{
    private readonly AdminPanelDbContext _context;
    private readonly IKeyVaultService _keyVaultService;
    private readonly IServiceBusManagementService _serviceBusService;
    private readonly ILogger<ClientOnboardingService> _logger;

    public ClientOnboardingService(
        AdminPanelDbContext context,
        IKeyVaultService keyVaultService,
        IServiceBusManagementService serviceBusService,
        ILogger<ClientOnboardingService> logger)
    {
        _context = context;
        _keyVaultService = keyVaultService;
        _serviceBusService = serviceBusService;
        _logger = logger;
    }

    public async Task<ClientOnboardingResponse> OnboardClientAsync(ClientOnboardingRequest request)
    {
        try
        {
            // 1. Create client record
            var client = new Client
            {
                Name = request.Name,
                Email = request.Email,
                Company = request.Company,
                BaseUrl = request.BaseUrl.TrimEnd('/'),
                AuthType = request.Authentication.Type,
                AuthEndpoint = request.Authentication.AuthEndpoint,
                KeyVaultCredentialsKey = GenerateKeyVaultKey(request.Company, request.Name)
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created client record with ID: {ClientId}", client.Id);

            // 2. Store credentials in Key Vault
            var credentials = BuildCredentialsDictionary(request.Authentication);
            var keyVaultKey = await _keyVaultService.StoreCredentialsAsync(
                client.KeyVaultCredentialsKey, 
                credentials);

            client.KeyVaultCredentialsKey = keyVaultKey;

            // 3. Create client endpoints
            foreach (var endpoint in request.Endpoints)
            {
                var clientEndpoint = new ClientEndpoint
                {
                    ClientId = client.Id,
                    ServiceType = endpoint.ServiceType,
                    Endpoint = endpoint.Endpoint.TrimStart('/')
                };
                _context.ClientEndpoints.Add(clientEndpoint);
            }

            // 4. Create client championships
            foreach (var championshipId in request.ChampionshipIds)
            {
                var clientChampionship = new ClientChampionship
                {
                    ClientId = client.Id,
                    IdChampionship = championshipId
                };
                _context.ClientChampionships.Add(clientChampionship);
            }

            await _context.SaveChangesAsync();

            // 5. Create Service Bus subscriptions
            var serviceTypes = request.Endpoints.Select(e => e.ServiceType).ToList();
            var createdSubscriptions = await _serviceBusService.CreateClientSubscriptionsAsync(
                client.Company, 
                serviceTypes, 
                request.ChampionshipIds);

            // 6. Publish provisioning message
            await _serviceBusService.PublishProvisioningMessageAsync(client.Id, keyVaultKey);

            _logger.LogInformation("Successfully onboarded client: {ClientId} - {Company}", 
                client.Id, client.Company);

            return new ClientOnboardingResponse
            {
                ClientId = client.Id,
                Message = "Client onboarded successfully",
                KeyVaultCredentialsKey = keyVaultKey,
                CreatedSubscriptions = createdSubscriptions,
                CreatedAt = client.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error onboarding client: {Company}", request.Company);
            throw;
        }
    }

    public async Task<bool> DeactivateClientAsync(int clientId)
    {
        try
        {
            var client = await _context.Clients
                .Include(c => c.Endpoints)
                .FirstOrDefaultAsync(c => c.Id == clientId);

            if (client == null)
            {
                return false;
            }

            client.IsActive = false;
            client.UpdatedAt = DateTime.UtcNow;

            // Delete Service Bus subscriptions
            var serviceTypes = client.Endpoints.Select(e => e.ServiceType).ToList();
            await _serviceBusService.DeleteClientSubscriptionsAsync(client.Company, serviceTypes);

            // Delete Key Vault credentials
            await _keyVaultService.DeleteCredentialsAsync(client.KeyVaultCredentialsKey);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deactivated client: {ClientId}", clientId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating client: {ClientId}", clientId);
            return false;
        }
    }

    public async Task<List<Championship>> GetAvailableChampionshipsAsync()
    {
        return await _context.Championships
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    private static Dictionary<string, string> BuildCredentialsDictionary(AuthenticationConfig auth)
    {
        var credentials = new Dictionary<string, string>
        {
            ["type"] = auth.Type
        };

        if (!string.IsNullOrEmpty(auth.AuthEndpoint))
            credentials["authEndpoint"] = auth.AuthEndpoint;

        switch (auth.Type.ToLowerInvariant())
        {
            case "basic":
                if (!string.IsNullOrEmpty(auth.Username))
                    credentials["username"] = auth.Username;
                if (!string.IsNullOrEmpty(auth.Password))
                    credentials["password"] = auth.Password;
                break;

            case "oauth2":
                if (!string.IsNullOrEmpty(auth.ClientId))
                    credentials["clientId"] = auth.ClientId;
                if (!string.IsNullOrEmpty(auth.ClientSecret))
                    credentials["clientSecret"] = auth.ClientSecret;
                if (!string.IsNullOrEmpty(auth.Scope))
                    credentials["scope"] = auth.Scope;
                if (!string.IsNullOrEmpty(auth.TokenEndpoint))
                    credentials["tokenEndpoint"] = auth.TokenEndpoint;
                break;

            case "apikey":
                if (!string.IsNullOrEmpty(auth.ApiKey))
                    credentials["apiKey"] = auth.ApiKey;
                if (!string.IsNullOrEmpty(auth.ApiKeyHeader))
                    credentials["apiKeyHeader"] = auth.ApiKeyHeader;
                break;

            case "bearer":
                if (!string.IsNullOrEmpty(auth.BearerToken))
                    credentials["bearerToken"] = auth.BearerToken;
                break;
        }

        if (!string.IsNullOrEmpty(auth.CustomHeaders))
            credentials["customHeaders"] = auth.CustomHeaders;

        return credentials;
    }

    private static string GenerateKeyVaultKey(string company, string clientName)
    {
        var sanitized = $"{company}-{clientName}"
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "-")
            .Replace("_", "-");
        
        return $"{sanitized}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
    }
}