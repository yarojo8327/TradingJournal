using Application.WPF.Models.Entities;
using Application.WPF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.WPF.Infrastructure.Data;

public class TradingJournalDbContext : DbContext
{
    public TradingJournalDbContext(DbContextOptions<TradingJournalDbContext> options) : base(options) { }

    public DbSet<User>                     Users                    => Set<User>();
    public DbSet<TradingAccount>           TradingAccounts          => Set<TradingAccount>();
    public DbSet<TradingStrategy>          TradingStrategies        => Set<TradingStrategy>();
    public DbSet<StrategyRule>             StrategyRules            => Set<StrategyRule>();
    public DbSet<StrategyConfluence>       StrategyConfluences      => Set<StrategyConfluence>();
    public DbSet<TradeEntry>               TradeEntries             => Set<TradeEntry>();
    public DbSet<PlaybookEntry>            PlaybookEntries          => Set<PlaybookEntry>();
    public DbSet<PlaybookConfluenceRating> PlaybookConfluenceRatings => Set<PlaybookConfluenceRating>();

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
            e.HasMany(s => s.Confluences).WithOne(c => c.Strategy)
                .HasForeignKey(c => c.StrategyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Ignore(s => s.AverageRating);
            e.Ignore(s => s.HasAverageRating);
            e.Ignore(s => s.IsQualifiedSetup);
        });

        modelBuilder.Entity<StrategyConfluence>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.StrategyId);
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
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

        modelBuilder.Entity<PlaybookEntry>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.UserId);
            // Symbol se persiste en la columna "Title" (nombre original) para compatibilidad de schema
            e.Property(p => p.Symbol).HasColumnName("Title").IsRequired().HasMaxLength(50);
            e.Property(p => p.Notes).HasMaxLength(4000);
            e.Property(p => p.ImageData).HasColumnType("BLOB");
            e.Property(p => p.ImageMimeType).HasMaxLength(50);
            e.Property(p => p.Rating).HasColumnType("REAL");
            e.Property(p => p.ManualRating);
            e.Property(p => p.CreatedAt).IsRequired();
            e.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Strategy).WithMany().HasForeignKey(p => p.StrategyId)
                .OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasMany(p => p.ConfluenceRatings).WithOne(r => r.PlaybookEntry)
                .HasForeignKey(r => r.PlaybookEntryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlaybookConfluenceRating>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.PlaybookEntryId);
            e.Property(r => r.ConfluenceName).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<TradeEntry>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.AccountId);
            e.HasIndex(t => t.StrategyId);
            e.Property(t => t.Symbol).IsRequired().HasMaxLength(20);
            e.Property(t => t.Direction).IsRequired().HasConversion<string>();
            e.Property(t => t.Result).IsRequired().HasConversion<string>();
            e.Property(t => t.Session).HasConversion<string>();
            e.Property(t => t.EmotionalState).HasConversion<string>();
            e.Property(t => t.EntryPrice).HasColumnType("decimal(18,8)");
            e.Property(t => t.ExitPrice).HasColumnType("decimal(18,8)");
            e.Property(t => t.StopLoss).HasColumnType("decimal(18,8)");
            e.Property(t => t.TakeProfit).HasColumnType("decimal(18,8)");
            e.Property(t => t.PositionSizeLots).HasColumnType("decimal(18,4)");
            e.Property(t => t.RiskAmount).HasColumnType("decimal(18,2)");
            e.Property(t => t.ProfitLoss).HasColumnType("decimal(18,2)");
            e.Property(t => t.PipsResult).HasColumnType("decimal(10,1)");
            e.Property(t => t.RiskRewardRatio).HasColumnType("decimal(6,2)");
            e.Property(t => t.Rating);
            e.Property(t => t.Timeframe).HasMaxLength(10);
            e.Property(t => t.MistakeType).HasMaxLength(100);
            e.Property(t => t.Notes).HasMaxLength(2000);
            e.Property(t => t.ScreenshotUrl).HasMaxLength(500);
            e.Property(t => t.CreatedAt).IsRequired();
            e.HasOne(t => t.Account)
                .WithMany()
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Strategy)
                .WithMany()
                .HasForeignKey(t => t.StrategyId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });
    }
}
