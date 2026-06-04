using Application.WPF.Infrastructure.Data;
using Application.WPF.Services.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.WPF.Tests.Strategies;

public class TradingStrategyServiceTests : IDisposable
{
    private readonly TradingJournalDbContext  _db;
    private readonly TradingStrategyService  _sut;

    public TradingStrategyServiceTests()
    {
        var opts = new DbContextOptionsBuilder<TradingJournalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db  = new TradingJournalDbContext(opts);
        _sut = new TradingStrategyService(_db, NullLogger<TradingStrategyService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── CreateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsStrategy()
    {
        await _sut.CreateAsync(1, "Ruptura de rangos", "Desc", null, null, Array.Empty<string>());
        Assert.True(await _db.TradingStrategies.AnyAsync());
    }

    [Fact]
    public async Task Create_TrimsTitle()
    {
        var s = await _sut.CreateAsync(1, "  Scalping  ", null, null, null, Array.Empty<string>());
        Assert.Equal("Scalping", s.Title);
    }

    [Fact]
    public async Task Create_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;
        var s = await _sut.CreateAsync(1, "S1", null, null, null, Array.Empty<string>());
        Assert.True(s.CreatedAt >= before);
    }

    [Fact]
    public async Task Create_WithRules_PersistsRules()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null,
                                       new[] { "Regla 1", "Regla 2", "Regla 3" });
        Assert.Equal(3, s.Rules.Count);
    }

    [Fact]
    public async Task Create_IgnoresBlankRules()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null,
                                       new[] { "Regla 1", "  ", "" });
        Assert.Single(s.Rules);
    }

    [Fact]
    public async Task Create_SetsRuleOrderIndex()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null,
                                       new[] { "R1", "R2", "R3" });
        var ordered = s.Rules.OrderBy(r => r.OrderIndex).ToList();
        Assert.Equal(0, ordered[0].OrderIndex);
        Assert.Equal(1, ordered[1].OrderIndex);
        Assert.Equal(2, ordered[2].OrderIndex);
    }

    [Fact]
    public async Task Create_WithImage_StoresImageData()
    {
        var img = new byte[] { 1, 2, 3 };
        var s   = await _sut.CreateAsync(1, "S1", null, img, "image/png", Array.Empty<string>());
        Assert.Equal(img, s.ImageData);
        Assert.Equal("image/png", s.ImageMimeType);
    }

    // ── GetAllByUserIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WhenNone_ReturnsEmpty()
    {
        Assert.Empty(await _sut.GetAllByUserIdAsync(1));
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyStrategiesOfUser()
    {
        await _sut.CreateAsync(1, "S1", null, null, null, Array.Empty<string>());
        await _sut.CreateAsync(1, "S2", null, null, null, Array.Empty<string>());
        await _sut.CreateAsync(2, "S3", null, null, null, Array.Empty<string>());

        var result = await _sut.GetAllByUserIdAsync(1);
        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(1, s.UserId));
    }

    [Fact]
    public async Task GetAll_IncludesRules()
    {
        await _sut.CreateAsync(1, "S1", null, null, null, new[] { "R1", "R2" });
        var result = await _sut.GetAllByUserIdAsync(1);
        Assert.Equal(2, result[0].Rules.Count);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNull()
    {
        Assert.Null(await _sut.GetByIdAsync(9999));
    }

    [Fact]
    public async Task GetById_ReturnsWithRules()
    {
        var created = await _sut.CreateAsync(1, "S1", null, null, null, new[] { "R1" });
        var found   = await _sut.GetByIdAsync(created.Id);
        Assert.NotNull(found);
        Assert.Single(found!.Rules);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesTitle()
    {
        var s = await _sut.CreateAsync(1, "Old", null, null, null, Array.Empty<string>());
        var u = await _sut.UpdateAsync(s.Id, "New", null, null, null, Array.Empty<string>());
        Assert.Equal("New", u.Title);
    }

    [Fact]
    public async Task Update_ReplacesRules()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null, new[] { "R1", "R2" });
        var u = await _sut.UpdateAsync(s.Id, "S1", null, null, null, new[] { "R3" });
        Assert.Single(u.Rules);
        Assert.Equal("R3", u.Rules.First().Description);
    }

    [Fact]
    public async Task Update_SetsUpdatedAt()
    {
        var s      = await _sut.CreateAsync(1, "S1", null, null, null, Array.Empty<string>());
        var before = DateTime.UtcNow;
        var u      = await _sut.UpdateAsync(s.Id, "S1", null, null, null, Array.Empty<string>());
        Assert.NotNull(u.UpdatedAt);
        Assert.True(u.UpdatedAt >= before);
    }

    [Fact]
    public async Task Update_WithInvalidId_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(9999, "T", null, null, null, Array.Empty<string>()));
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesStrategy()
    {
        var s = await _sut.CreateAsync(1, "S1", null, null, null, Array.Empty<string>());
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
