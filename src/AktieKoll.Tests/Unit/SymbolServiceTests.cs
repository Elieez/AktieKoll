using AktieKoll.Models;
using AktieKoll.Tests.Shared.TestHelpers;

namespace AktieKoll.Tests.Unit;

public class SymbolServiceTests
{
    // ── ISIN exact ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveSymbol_IsinExact_VolvoB()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: "SE0000115446", Code: "VOLV-B", Name: "Volvo B"),
            (Isin: "SE0000115420", Code: "VOLV-A", Name: "Volvo A"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        var symbol = await svc.ResolveSymbolAsync("AB Volvo (publ)", "SE0000115446");

        Assert.Equal("VOLV-B", symbol);
    }

    [Fact]
    public async Task ResolveSymbol_IsinExact_VolvoCar()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: "SE0021628898", Code: "VOLCAR-B", Name: "Volvo Car B"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        var symbol = await svc.ResolveSymbolAsync("Volvo Car", "SE0021628898");

        Assert.Equal("VOLCAR-B", symbol);
    }

    // ── ISIN fuzzy (strip spaces/dashes) ──────────────────────────────────────

    [Fact]
    public async Task ResolveSymbol_IsinFuzzy_NormalisedMatch()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: "SE0000115446", Code: "VOLV-B", Name: "Volvo B"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        // Trade ISIN has a stray space — fuzzy normalisation should still match
        var symbol = await svc.ResolveSymbolAsync("Volvo", "SE 0000115446");

        Assert.Equal("VOLV-B", symbol);
    }

    // ── Name fuzzy (Jaccard) ──────────────────────────────────────────────────

    [Fact]
    public async Task ResolveSymbol_NameFuzzy_AktiebolagetVolvo_ResolvesViaJaccard()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: "SE0000115420", Code: "VOLV-A", Name: "AB Volvo (publ)"),
            (Isin: "SE0000115446", Code: "VOLV-B", Name: "AB Volvo (publ)"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        var symbol = await svc.ResolveSymbolAsync("Aktiebolaget Volvo", null);

        // Should resolve to VOLV-A or VOLV-B (B preferred)
        Assert.NotNull(symbol);
        Assert.True(symbol == "VOLV-A" || symbol == "VOLV-B");
    }

    // ── Unresolved ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveSymbol_UnknownNameAndWrongIsin_ReturnsNull()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: "SE0000115446", Code: "VOLV-B", Name: "Volvo B"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        var symbol = await svc.ResolveSymbolAsync("XyzUnknownCorp Ltd", "XX9999999999");

        Assert.Null(symbol);
    }

    // ── B share preferred over A share ────────────────────────────────────────

    [Fact]
    public async Task ResolveSymbol_BSharePreferredOverAShare()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: null, Code: "ERIC-A", Name: "Telefonaktiebolaget LM Ericsson (publ)"),
            (Isin: null, Code: "ERIC-B", Name: "Telefonaktiebolaget LM Ericsson (publ)"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        // Both ERIC-A and ERIC-B normalise to "ericsson" — B must win
        var symbol = await svc.ResolveSymbolAsync("Telefonaktiebolaget LM Ericsson (publ)", null);

        Assert.Equal("ERIC-B", symbol);
    }

    // ── Batch resolve ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveSymbolsAsync_StampsSymbolOnTrades()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: "SE0000115446", Code: "VOLV-B", Name: "Volvo B"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        var trades = new List<InsiderTrade>
        {
            new()
            {
                CompanyName = "Volvo",
                InsiderName = "Test",
                TransactionType = "Förvärv",
                Shares = 100,
                Price = 50m,
                Currency = "SEK",
                Status = "Aktuell",
                Isin = "SE0000115446",
                PublishingDate = DateTime.Today,
                TransactionDate = DateTime.Today
            }
        };

        await svc.ResolveSymbolsAsync(trades);

        Assert.Equal("VOLV-B", trades[0].Symbol);
    }

    // ── Cache invalidation ────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveSymbol_AfterInvalidate_PicksUpNewCompany()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        // First call with empty DB
        var before = await svc.ResolveSymbolAsync("Foo Corp", "SE0001");
        Assert.Null(before);

        // Add company, invalidate, retry
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: "SE0001", Code: "FOO", Name: "Foo Corp"));
        svc.InvalidateCache();

        var after = await svc.ResolveSymbolAsync("Foo Corp", "SE0001");
        Assert.Equal("FOO", after);
    }

    [Fact]
    public async Task ResolveSymbol_IsinFuzzy_DashInIsin_StillMatches()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: "SE0000115446", Code: "VOLV-B", Name: "Volvo B"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        // Trade ISIN has dashes — fuzzy normalisation should still match
        var symbol = await svc.ResolveSymbolAsync("Volvo", "SE-0000-115446");

        Assert.Equal("VOLV-B", symbol);
    }

    [Fact]
    public async Task ResolveSymbol_NullInputs_ReturnsNullWithoutThrowing()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        var symbol = await svc.ResolveSymbolAsync(null, null);

        Assert.Null(symbol);
    }

    [Fact]
    public async Task ResolveSymbol_NameExact_StripsPublAndMatches()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: null, Code: "HM-B", Name: "Hennes & Mauritz AB (publ)"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        // Input has no ISIN, slightly different format — should hit name_exact after normalise
        var symbol = await svc.ResolveSymbolAsync("Hennes & Mauritz AB", null);

        Assert.Equal("HM-B", symbol);
    }

    [Fact]
    public async Task ResolveSymbol_IsinExact_WinsEvenWithWrongName()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        await ServiceTestHelpers.SeedCompanies(ctx,
            (Isin: "SE0000115446", Code: "VOLV-B", Name: "AB Volvo (publ)"));

        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        // Name is wrong but ISIN is correct — should still resolve
        var symbol = await svc.ResolveSymbolAsync("Completely Wrong Name", "SE0000115446");

        Assert.Equal("VOLV-B", symbol);
    }

    // ── Already-resolved trades are skipped by batch method ──────────────────

    [Fact]
    public async Task ResolveSymbolsAsync_SkipsTradesWithExistingSymbol()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var svc = ServiceTestHelpers.CreateSymbolService(ctx);

        var trade = new InsiderTrade
        {
            CompanyName = "SomeCompany",
            InsiderName = "Test",
            TransactionType = "Förvärv",
            Shares = 1,
            Price = 1m,
            Currency = "SEK",
            Status = "Aktuell",
            Symbol = "ALREADY",
            PublishingDate = DateTime.Today,
            TransactionDate = DateTime.Today
        };

        await svc.ResolveSymbolsAsync([trade]);

        Assert.Equal("ALREADY", trade.Symbol);
    }
}
