using PoeAncientsPriceHelper;
using Xunit;

namespace PoeAncientsPriceHelper.Tests;

public class LeagueFetchTests
{
    [Fact]
    public async Task FetchAvailableLeaguesAsync_ReturnsFallback_OnFailure()
    {
        // Use a fake HTTP client that always fails
        using var http = new HttpClient();
        http.BaseAddress = new Uri("http://localhost:1"); // will fail to connect

        var leagues = await PriceRepository.FetchAvailableLeaguesAsync(http, CancellationToken.None);

        Assert.NotNull(leagues);
        Assert.True(leagues.Count > 0); // fallback list
        Assert.Contains("Runes of Aldur", leagues);
    }

    [Fact]
    public void PriceEntry_ChaosValue_IsStored()
    {
        var entry = new PriceEntry(1.0m, 188m, 10m);
        Assert.Equal(10m, entry.ChaosValue);
    }

    [Fact]
    public void PriceEntry_RecordEquality()
    {
        var a = new PriceEntry(1.0m, 188m, 10m);
        var b = new PriceEntry(1.0m, 188m, 10m);
        Assert.Equal(a, b);
    }
}
