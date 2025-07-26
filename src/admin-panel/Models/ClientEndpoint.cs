using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminPanel.Models;

[Table("client_endpoints")]
public class ClientEndpoint
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ClientId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty; // coleta, finalizacao, periodo-partida, escalacao, finalizacao-xg
    
    [Required]
    [MaxLength(255)]
    public string Endpoint { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    [ForeignKey("ClientId")]
    public virtual Client Client { get; set; } = null!;
}