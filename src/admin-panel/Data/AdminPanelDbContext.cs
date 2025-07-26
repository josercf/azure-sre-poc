using Microsoft.EntityFrameworkCore;
using AdminPanel.Models;

namespace AdminPanel.Data;

public class AdminPanelDbContext : DbContext
{
    public AdminPanelDbContext(DbContextOptions<AdminPanelDbContext> options) : base(options)
    {
    }
    
    public DbSet<Client> Clients { get; set; }
    public DbSet<ClientEndpoint> ClientEndpoints { get; set; }
    public DbSet<ClientChampionship> ClientChampionships { get; set; }
    public DbSet<Championship> Championships { get; set; }
    

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure relationships
        modelBuilder.Entity<ClientEndpoint>()
            .HasOne(ce => ce.Client)
            .WithMany(c => c.Endpoints)
            .HasForeignKey(ce => ce.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<ClientChampionship>()
            .HasOne(cc => cc.Client)
            .WithMany(c => c.Championships)
            .HasForeignKey(cc => cc.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Configure unique constraints
        modelBuilder.Entity<ClientChampionship>()
            .HasIndex(cc => new { cc.ClientId, cc.IdChampionship })
            .IsUnique();
            
        modelBuilder.Entity<ClientEndpoint>()
            .HasIndex(ce => new { ce.ClientId, ce.ServiceType })
            .IsUnique();
    }
}