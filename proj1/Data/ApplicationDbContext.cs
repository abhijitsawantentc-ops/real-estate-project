using Microsoft.EntityFrameworkCore;
using proj1.Models;
using System.Text.RegularExpressions;

namespace proj1.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<PlatformProperty> Properties => Set<PlatformProperty>();
    public DbSet<PropertyImageRecord> PropertyImages => Set<PropertyImageRecord>();
    public DbSet<CustomerInterest> CustomerInterests => Set<CustomerInterest>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<PropertyTransaction> Transactions => Set<PropertyTransaction>();
    public DbSet<PlatformNotification> Notifications => Set<PlatformNotification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasOne(x => x.Agent).WithOne(x => x.Profile).HasForeignKey<Agent>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.ToTable("agents");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasMany(x => x.Properties).WithOne(x => x.Agent).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlatformProperty>(entity =>
        {
            entity.ToTable("properties");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Price).HasPrecision(14, 2);
            entity.Property(x => x.Area).HasPrecision(12, 2);
            entity.Property(x => x.Latitude).HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasPrecision(9, 6);
            entity.HasIndex(x => new { x.AdminApproved, x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.City, x.ListingType, x.Price });
        });

        modelBuilder.Entity<PropertyImageRecord>(entity =>
        {
            entity.ToTable("property_images");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Property).WithMany(x => x.Images).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.PropertyId, x.DisplayOrder });
        });

        modelBuilder.Entity<CustomerInterest>(entity =>
        {
            entity.ToTable("customer_interests");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Customer).WithMany(x => x.Interests).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Property).WithMany(x => x.Interests).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.CustomerId, x.PropertyId }).IsUnique();
        });

        modelBuilder.Entity<Bid>(entity =>
        {
            entity.ToTable("bids");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(14, 2);
            entity.HasOne(x => x.Property).WithMany(x => x.Bids).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CustomerInterest).WithMany(x => x.Bids).HasForeignKey(x => x.CustomerInterestId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PropertyTransaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AgreedPrice).HasPrecision(14, 2);
            entity.HasOne(x => x.Property).WithMany(x => x.Transactions).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Agent).WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Interest).WithMany(x => x.Transactions).HasForeignKey(x => x.InterestId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Bid).WithMany(x => x.Transactions).HasForeignKey(x => x.BidId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlatformNotification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Property).WithMany(x => x.Notifications).HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.User).WithMany(x => x.AuditLogs).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name) =>
        Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
}
