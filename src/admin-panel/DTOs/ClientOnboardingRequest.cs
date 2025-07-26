using System.ComponentModel.DataAnnotations;

namespace AdminPanel.DTOs;

public class ClientOnboardingRequest
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Company { get; set; } = string.Empty;
    
    [Required]
    [Url]
    [MaxLength(500)]
    public string BaseUrl { get; set; } = string.Empty;
    
    [Required]
    public AuthenticationConfig Authentication { get; set; } = new();
    
    [Required]
    [MinLength(1)]
    public List<ServiceEndpoint> Endpoints { get; set; } = new();
    
    [Required]
    [MinLength(1)]
    public List<int> ChampionshipIds { get; set; } = new();
}

public class AuthenticationConfig
{
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // basic, oauth2, apikey
    
    [MaxLength(500)]
    public string? AuthEndpoint { get; set; }
    
    // Basic Authentication
    [MaxLength(255)]
    public string? Username { get; set; }
    
    [MaxLength(255)]
    public string? Password { get; set; }
    
    // OAuth2 Authentication
    [MaxLength(255)]
    public string? ClientId { get; set; }
    
    [MaxLength(500)]
    public string? ClientSecret { get; set; }
    
    [MaxLength(1000)]
    public string? Scope { get; set; }
    
    [MaxLength(500)]
    public string? TokenEndpoint { get; set; }
    
    // API Key Authentication
    [MaxLength(255)]
    public string? ApiKey { get; set; }
    
    [MaxLength(100)]
    public string? ApiKeyHeader { get; set; } = "X-API-Key";
    
    // Bearer Token Authentication
    [MaxLength(1000)]
    public string? BearerToken { get; set; }
    
    // Custom Headers (JSON string)
    [MaxLength(2000)]
    public string? CustomHeaders { get; set; }
}

public class ServiceEndpoint
{
    [Required]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty; // coleta, finalizacao, periodo-partida, escalacao, finalizacao-xg
    
    [Required]
    [MaxLength(255)]
    public string Endpoint { get; set; } = string.Empty;
}