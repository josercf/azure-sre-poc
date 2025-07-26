using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdminPanel.Models;

[Table("client_championships")]
public class ClientChampionship
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ClientId { get; set; }
    
    [Required]
    public int IdChampionship { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    [ForeignKey("ClientId")]
    public virtual Client Client { get; set; } = null!;
}