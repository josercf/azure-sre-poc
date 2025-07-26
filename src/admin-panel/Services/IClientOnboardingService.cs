using AdminPanel.DTOs;
using AdminPanel.Models;

namespace AdminPanel.Services;

public interface IClientOnboardingService
{
    Task<ClientOnboardingResponse> OnboardClientAsync(ClientOnboardingRequest request);
    Task<bool> DeactivateClientAsync(int clientId);
    Task<List<Championship>> GetAvailableChampionshipsAsync();
}