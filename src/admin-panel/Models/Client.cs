using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminPanel.Models;

[Table("clients")]
public class Client
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Company { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string BaseUrl { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string AuthType { get; set; } = string.Empty; // basic, oauth2, apikey
    
    [MaxLength(500)]
    public string? AuthEndpoint { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string KeyVaultCredentialsKey { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<ClientEndpoint> Endpoints { get; set; } = new List<ClientEndpoint>();
    public virtual ICollection<ClientChampionship> Championships { get; set; } = new List<ClientChampionship>();
}