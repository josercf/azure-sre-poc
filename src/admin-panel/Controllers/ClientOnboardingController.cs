using Microsoft.AspNetCore.Mvc;
using AdminPanel.Services;
using AdminPanel.DTOs;
using AdminPanel.Models;
using System.Diagnostics;

namespace AdminPanel.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientOnboardingController : ControllerBase
{
    private readonly IClientOnboardingService _onboardingService;
    private readonly ILogger<ClientOnboardingController> _logger;

    public ClientOnboardingController(
        IClientOnboardingService onboardingService,
        ILogger<ClientOnboardingController> logger)
    {
        _onboardingService = onboardingService;
        _logger = logger;
    }

    [HttpPost("onboard")]
    public async Task<ActionResult<ClientOnboardingResponse>> OnboardClient([FromBody] ClientOnboardingRequest request)
    {
        using var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
        
        try
        {
            activity?.SetTag("operation.name", "client-onboarding");
            activity?.SetTag("client.company", request.Company);
            activity?.SetTag("client.name", request.Name);
            activity?.SetTag("endpoints.count", request.Endpoints.Count);
            activity?.SetTag("championships.count", request.ChampionshipIds.Count);

            _logger.LogInformation(
                "Starting client onboarding for {Company} - {Name}. TraceId: {TraceId}",
                request.Company, request.Name, traceId);

            var response = await _onboardingService.OnboardClientAsync(request);
            
            activity?.SetTag("client.id", response.ClientId);
            activity?.SetTag("subscriptions.count", response.CreatedSubscriptions.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation(
                "Client onboarding completed successfully. ClientId: {ClientId}, TraceId: {TraceId}",
                response.ClientId, traceId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex,
                "Error during client onboarding for {Company} - {Name}. TraceId: {TraceId}",
                request.Company, request.Name, traceId);

            return StatusCode(500, new
            {
                error = "Internal server error during onboarding",
                message = ex.Message,
                traceId = traceId
            });
        }
    }

    [HttpDelete("{clientId}")]
    public async Task<ActionResult> DeactivateClient(int clientId)
    {
        using var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

        try
        {
            activity?.SetTag("operation.name", "client-deactivation");
            activity?.SetTag("client.id", clientId);

            _logger.LogInformation(
                "Starting client deactivation for ClientId: {ClientId}. TraceId: {TraceId}",
                clientId, traceId);

            var result = await _onboardingService.DeactivateClientAsync(clientId);

            if (!result)
            {
                activity?.SetTag("result", "not_found");
                return NotFound(new { message = "Client not found", clientId = clientId });
            }

            activity?.SetTag("result", "success");
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation(
                "Client deactivated successfully. ClientId: {ClientId}, TraceId: {TraceId}",
                clientId, traceId);

            return Ok(new { message = "Client deactivated successfully", clientId = clientId });
        }
        catch (Exception ex)
        {
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex,
                "Error during client deactivation for ClientId: {ClientId}. TraceId: {TraceId}",
                clientId, traceId);

            return StatusCode(500, new
            {
                error = "Internal server error during deactivation",
                message = ex.Message,
                traceId = traceId,
                clientId = clientId
            });
        }
    }

    [HttpGet("championships")]
    public async Task<ActionResult<List<Championship>>> GetAvailableChampionships()
    {
        try
        {
            var championships = await _onboardingService.GetAvailableChampionshipsAsync();
            return Ok(championships);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available championships");
            return StatusCode(500, new { error = "Error retrieving championships" });
        }
    }
}