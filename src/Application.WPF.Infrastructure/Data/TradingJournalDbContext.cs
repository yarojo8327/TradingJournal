using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.WPF.Infrastructure.Data;

public class TradingJournalDbContext : DbContext
{
    public TradingJournalDbContext(DbContextOptions<TradingJournalDbContext> options) : base(options) { }

    public DbSet<User>             Users             => Set<User>();
    public DbSet<TradingAccount>   TradingAccounts   => Set<TradingAccount>();
    public DbSet<TradingStrategy>  TradingStrategies => Set<TradingStrategy>();
    public DbSet<StrategyRule>     StrategyRules     => Set<StrategyRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.FullName).IsRequired().HasMaxLength(150);
            e.Property(u => u.Email).IsRequired().HasMaxLength(200);
            e.Property(u => u.Username).IsRequired().HasMaxLength(50);
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<TradingStrategy>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId);
            e.Property(s => s.Title).IsRequired().HasMaxLength(200);
            e.Property(s => s.Description).HasMaxLength(2000);
            e.Property(s => s.ImageData).HasColumnType("BLOB");
            e.Property(s => s.ImageMimeType).HasMaxLength(50);
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Rules).WithOne(r => r.Strategy)
                .HasForeignKey(r => r.StrategyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StrategyRule>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.StrategyId);
            e.Property(r => r.Description).IsRequired().HasMaxLength(2000);
        });

        modelBuilder.Entity<TradingAccount>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.UserId);
            e.Property(a => a.Broker).IsRequired().HasMaxLength(100);
            e.Property(a => a.AccountNumber).IsRequired().HasMaxLength(50);
            e.Property(a => a.AccountType).IsRequired()
                .HasConversion<string>();
            e.Property(a => a.InitialCapital).IsRequired()
                .HasColumnType("decimal(18,2)");
            e.Property(a => a.BaseCurrency).IsRequired().HasMaxLength(10);
            e.Property(a => a.Leverage).IsRequired().HasMaxLength(20);
            e.Property(a => a.StartDate).IsRequired();
            e.Property(a => a.CreatedAt).IsRequired();
            e.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
