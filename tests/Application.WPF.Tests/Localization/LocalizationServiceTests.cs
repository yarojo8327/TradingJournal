using Application.WPF.Common.Localization;
using Application.WPF.Services.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Application.WPF.Tests.Localization;

public class LocalizationServiceTests
{
    private LocalizationService CreateSut(string culture = "en-US") =>
        new(NullLogger<LocalizationService>.Instance, culture);

    [Fact]
    public void Default_Culture_Is_EnUS()
    {
        var sut = CreateSut("en-US");
        Assert.Equal("en-US", sut.CurrentCulture);
    }

    [Fact]
    public void Get_EnUS_Returns_EnglishString()
    {
        var sut = CreateSut("en-US");
        Assert.Equal("Dashboard", sut["Nav_Dashboard"]);
    }

    [Fact]
    public void Get_EsCO_Returns_SpanishString()
    {
        var sut = CreateSut("es-CO");
        Assert.Equal("Panel", sut["Nav_Dashboard"]);
    }

    [Fact]
    public void ChangeLanguage_Updates_CurrentCulture()
    {
        var sut = CreateSut("en-US");
        sut.ChangeLanguage("es-CO");
        Assert.Equal("es-CO", sut.CurrentCulture);
    }

    [Fact]
    public void ChangeLanguage_Updates_Translated_Strings()
    {
        var sut = CreateSut("en-US");
        Assert.Equal("Dashboard", sut["Nav_Dashboard"]);

        sut.ChangeLanguage("es-CO");
        Assert.Equal("Panel", sut["Nav_Dashboard"]);
    }

    [Fact]
    public void ChangeLanguage_Raises_PropertyChanged()
    {
        var sut = CreateSut("en-US");
        var raised = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == string.Empty || e.PropertyName == nameof(sut.CurrentCulture))
                raised = true;
        };

        sut.ChangeLanguage("es-CO");
        Assert.True(raised);
    }

    [Fact]
    public void ChangeLanguage_SameCulture_DoesNotRaisePropertyChanged()
    {
        var sut = CreateSut("en-US");
        var count = 0;
        sut.PropertyChanged += (_, _) => count++;

        sut.ChangeLanguage("en-US");
        Assert.Equal(0, count);
    }

    [Fact]
    public void Get_MissingKey_Returns_BracketedKey()
    {
        var sut = CreateSut("en-US");
        Assert.Equal("[NonExistentKey_XYZ]", sut["NonExistentKey_XYZ"]);
    }

    [Fact]
    public void AvailableLanguages_Contains_EnUS_And_EsCO()
    {
        var sut = CreateSut();
        var codes = sut.AvailableLanguages.Select(l => l.CultureCode).ToList();
        Assert.Contains("en-US", codes);
        Assert.Contains("es-CO", codes);
    }

    [Theory]
    [InlineData("en-US", "Refresh")]
    [InlineData("es-CO", "Actualizar")]
    public void Get_Returns_Correct_Translation_Per_Culture(string culture, string expected)
    {
        var sut = CreateSut(culture);
        Assert.Equal(expected, sut["Dashboard_Refresh"]);
    }

    [Theory]
    [InlineData("en-US", "TOTAL TRADES")]
    [InlineData("es-CO", "TOTAL OPERACIONES")]
    public void KPI_Keys_Are_Correctly_Translated(string culture, string expected)
    {
        var sut = CreateSut(culture);
        Assert.Equal(expected, sut["Kpi_TotalTrades"]);
    }
}
