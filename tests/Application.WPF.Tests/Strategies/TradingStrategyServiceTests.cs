using Application.WPF.Infrastructure.Data;
using Application.WPF.Services.Strategies;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.WPF.Tests.Strategies;

/// <summary>
/// Usa SQLite in-memory con conexiÃ³n compartida porque UpdateAsync ejecuta SQL raw
/// que no es compatible con el proveedor EF Core InMemory.
/// </summary>
public class TradingStrategyServiceTests : IDisposable
{
    private readonly SqliteConnection        _connection;
    private readonly TradingJournalDbContext _db;
    private readonly TradingStrategyService  _sut;

    public TradingStrategyServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var opts = new DbContextOptionsBuilder<TradingJournalDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TradingJournalDbContext(opts);
        _db.Database.EnsureCreated();

        // SQLite real exige FK. Usuarios semilla para los tests.
        _db.Users.AddRange(
            new Models.Entities.User { Id = 1, FullName = "User One", Email = "u1@t.com", Username = "user1", PasswordHash = "h", CreatedAt = DateTime.UtcNow },
            new Models.Entities.User { Id = 2, FullName = "User Two", Email = "u2@t.com", Username = "user2", PasswordHash = "h", CreatedAt = DateTime.UtcNow }
        );
        _db.SaveChanges();

        _sut = new TradingStrategyService(_db, NullLogger<TradingStrategyService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // â”€â”€ CreateAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Create_PersistsStrategy()
    {
        await _sut.CreateAsync(1, "Ruptura de rangos", "Desc", null, null, Array.Empty<string>(), Array.Empty<string>());
        Assert.True(await _db.TradingStrategies.AnyAsync());
    }

    [Fact]
    public async Task Create_TrimsTitle()
    {
        var s = await _sut.CreateAsync(1, "  Scalping  ", null, null, null, Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal("Scalping", s.Title);
    }

    [Fact]
    public async Task Create_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;
        var s = await _sut.CreateAsync(1, "S1", null, null, null, Array.Empty<string>(), Array.Empty<string>());
        Assert.True(s.CreatedAt >= before);
    }

    [Fact]
    public async Task Create_WithRules_PersistsRules()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null,
                                       new[] { "Regla 1", "Regla 2", "Regla 3" }, Array.Empty<string>());
        Assert.Equal(3, s.Rules.Count);
    }

    [Fact]
    public async Task Create_IgnoresBlankRules()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null,
                                       new[] { "Regla 1", "  ", "" }, Array.Empty<string>());
        Assert.Single(s.Rules);
    }

    [Fact]
    public async Task Create_SetsRuleOrderIndex()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null,
                                       new[] { "R1", "R2", "R3" }, Array.Empty<string>());
        var ordered = s.Rules.OrderBy(r => r.OrderIndex).ToList();
        Assert.Equal(0, ordered[0].OrderIndex);
        Assert.Equal(1, ordered[1].OrderIndex);
        Assert.Equal(2, ordered[2].OrderIndex);
    }

    [Fact]
    public async Task Create_WithImage_StoresImageData()
    {
        var img = new byte[] { 1, 2, 3 };
        var s   = await _sut.CreateAsync(1, "S1", null, img, "image/png", Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal(img, s.ImageData);
        Assert.Equal("image/png", s.ImageMimeType);
    }

    [Fact]
    public async Task Create_EmptyDescription_StoresNull()
    {
        var s = await _sut.CreateAsync(1, "S1", "", null, null, Array.Empty<string>(), Array.Empty<string>());
        Assert.Null(s.Description);
    }

    // â”€â”€ GetAllByUserIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetAll_WhenNone_ReturnsEmpty()
    {
        Assert.Empty(await _sut.GetAllByUserIdAsync(1));
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyStrategiesOfUser()
    {
        await _sut.CreateAsync(1, "S1", null, null, null, Array.Empty<string>(), Array.Empty<string>());
        await _sut.CreateAsync(1, "S2", null, null, null, Array.Empty<string>(), Array.Empty<string>());
        await _sut.CreateAsync(2, "S3", null, null, null, Array.Empty<string>(), Array.Empty<string>());

        var result = await _sut.GetAllByUserIdAsync(1);
        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(1, s.UserId));
    }

    [Fact]
    public async Task GetAll_IncludesRules()
    {
        await _sut.CreateAsync(1, "S1", null, null, null, new[] { "R1", "R2" }, Array.Empty<string>());
        var result = await _sut.GetAllByUserIdAsync(1);
        Assert.Equal(2, result[0].Rules.Count);
    }

    // â”€â”€ GetByIdAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNull()
    {
        Assert.Null(await _sut.GetByIdAsync(9999));
    }

    [Fact]
    public async Task GetById_ReturnsWithRules()
    {
        var created = await _sut.CreateAsync(1, "S1", null, null, null, new[] { "R1" }, Array.Empty<string>());
        var found   = await _sut.GetByIdAsync(created.Id);
        Assert.NotNull(found);
        Assert.Single(found!.Rules);
    }

    // â”€â”€ UpdateAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Update_ChangesTitle()
    {
        var s = await _sut.CreateAsync(1, "Old", null, null, null, Array.Empty<string>(), Array.Empty<string>());
        var u = await _sut.UpdateAsync(s.Id, "New", null, null, null, Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal("New", u.Title);
    }

    [Fact]
    public async Task Update_ReplacesRules()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null, new[] { "R1", "R2" }, Array.Empty<string>());
        var u = await _sut.UpdateAsync(s.Id, "S1", null, null, null, new[] { "R3" }, Array.Empty<string>());
        Assert.Single(u.Rules);
        Assert.Equal("R3", u.Rules.First().Description);
    }

    [Fact]
    public async Task Update_SetsUpdatedAt()
    {
        var s      = await _sut.CreateAsync(1, "S1", null, null, null, Array.Empty<string>(), Array.Empty<string>());
        var before = DateTime.UtcNow;
        var u      = await _sut.UpdateAsync(s.Id, "S1", null, null, null, Array.Empty<string>(), Array.Empty<string>());
        Assert.NotNull(u.UpdatedAt);
        Assert.True(u.UpdatedAt >= before);
    }

    [Fact]
    public async Task Update_EmptyDescription_StoresNull()
    {
        var s = await _sut.CreateAsync(1, "S1", "Desc original", null, null, Array.Empty<string>(), Array.Empty<string>());
        var u = await _sut.UpdateAsync(s.Id, "S1", "", null, null, Array.Empty<string>(), Array.Empty<string>());
        Assert.Null(u.Description);
    }

    [Fact]
    public async Task Update_WithInvalidId_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(9999, "T", null, null, null, Array.Empty<string>(), Array.Empty<string>()));
    }

    // â”€â”€ DeleteAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Delete_RemovesStrategy()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null, Array.Empty<string>(), Array.Empty<string>());
        await _sut.DeleteAsync(s.Id);
        Assert.False(await _db.TradingStrategies.AnyAsync(x => x.Id == s.Id));
    }

    [Fact]
    public async Task Delete_WithInvalidId_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(9999));
    }
}
