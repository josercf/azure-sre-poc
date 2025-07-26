using AdminPanel.Models;

namespace AdminPanel.Data;

public static class SeedData
{
    public static async Task SeedAsync(AdminPanelDbContext context)
    {
        // Seed Championships if they don't exist
        if (!context.Championships.Any())
        {
            var championships = new[]
            {
                new Championship { Id = 1, Name = "Campeonato Brasileiro Série A", Description = "Principal campeonato do futebol brasileiro", IsActive = true },
                new Championship { Id = 2, Name = "Copa Libertadores", Description = "Principal competição sul-americana de clubes", IsActive = true },
                new Championship { Id = 3, Name = "Copa do Brasil", Description = "Copa nacional eliminatória", IsActive = true },
                new Championship { Id = 4, Name = "Campeonato Paulista", Description = "Campeonato estadual de São Paulo", IsActive = true },
                new Championship { Id = 5, Name = "Campeonato Carioca", Description = "Campeonato estadual do Rio de Janeiro", IsActive = true },
                new Championship { Id = 6, Name = "Premier League", Description = "Campeonato inglês", IsActive = true },
                new Championship { Id = 7, Name = "La Liga", Description = "Campeonato espanhol", IsActive = true },
                new Championship { Id = 8, Name = "Serie A", Description = "Campeonato italiano", IsActive = true },
                new Championship { Id = 9, Name = "Bundesliga", Description = "Campeonato alemão", IsActive = true },
                new Championship { Id = 10, Name = "Ligue 1", Description = "Campeonato francês", IsActive = true }
            };

            context.Championships.AddRange(championships);
            await context.SaveChangesAsync();
        }
    }
}