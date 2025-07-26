using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ShotsPusher.Services;

public interface IWebhookService
{
    Task<bool> SendChampionshipDataAsync(ChampionshipData data, string webhookUrl, int shotCount = 0, CancellationToken cancellationToken = default);
}

public class WebhookService : IWebhookService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public WebhookService(HttpClient httpClient, ILogger<WebhookService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Configure HttpClient timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<bool> SendChampionshipDataAsync(ChampionshipData data, string webhookUrl, int shotCount = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create payload for the webhook with shots-specific data
            var payload = new
            {
                eventType = "championship.shots",
                timestamp = DateTime.UtcNow,
                data = new
                {
                    championshipId = data.IdChampionship,
                    matchId = data.IdMatch,
                    skillId = data.IdSkill,
                    eventTimestamp = data.Timestamp,
                    shotCount = shotCount,
                    analysisType = "shots_detection",
                    source = "shots-pusher"
                }
            };

            var jsonContent = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending shots data to webhook: {WebhookUrl}, ShotCount: {ShotCount}", webhookUrl, shotCount);
            _logger.LogDebug("Webhook payload: {Payload}", jsonContent);

            var response = await _httpClient.PostAsync(webhookUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent shots data to webhook. StatusCode: {StatusCode}, Championship: {Championship}, Match: {Match}, Skill: {Skill}, ShotCount: {ShotCount}",
                    response.StatusCode, data.IdChampionship, data.IdMatch, data.IdSkill, shotCount);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Webhook call failed. StatusCode: {StatusCode}, Response: {Response}, Championship: {Championship}, Match: {Match}, Skill: {Skill}",
                    response.StatusCode, responseBody, data.IdChampionship, data.IdMatch, data.IdSkill);
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending shots data to webhook: {WebhookUrl}, Championship: {Championship}, Match: {Match}, Skill: {Skill}",
                webhookUrl, data.IdChampionship, data.IdMatch, data.IdSkill);
            return false;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout sending shots data to webhook: {WebhookUrl}, Championship: {Championship}, Match: {Match}, Skill: {Skill}",
                webhookUrl, data.IdChampionship, data.IdMatch, data.IdSkill);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending shots data to webhook: {WebhookUrl}, Championship: {Championship}, Match: {Match}, Skill: {Skill}",
                webhookUrl, data.IdChampionship, data.IdMatch, data.IdSkill);
            return false;
        }
    }
} 