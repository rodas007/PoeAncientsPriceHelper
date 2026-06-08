using System.IO;
using System.Net;
using System.Net.Http;
using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class PriceRepositoryTests
{
    // Real API shape: items[] has id+name, lines[] has id+primaryValue, core.rates.exalted
    private const string FakeApiResponse = """
        {
          "items": [
            { "id": "chilling-flux",            "name": "Chilling Flux" },
            { "id": "support-scattering-flame",  "name": "Support: Scattering Flame" }
          ],
          "lines": [
            { "id": "chilling-flux",           "primaryValue": 0.5 },
            { "id": "support-scattering-flame", "primaryValue": 1.2 }
          ],
          "core": { "primary": "divine", "rates": { "exalted": 80.0 } }
        }
        """;

    private static AppConfig DefaultConfig(string tempDir) => new()
    {
        LeagueName = "Test League",
        CustomPricesPath = Path.Combine(tempDir, "custom_prices.json")
    };

    [Fact]
    public async Task FetchPopulatesDict_WithNormalizedKeys()
    {
        using var http = FakeHttp(FakeApiResponse);
        using var dir = new TempDir();
        var repo = new PriceRepository(http);
        await repo.InitialFetchAsync(DefaultConfig(dir.Path));

        Assert.True(repo.Prices.ContainsKey("chilling flux"));
        Assert.True(repo.Prices.ContainsKey("support scattering flame"));
        Assert.Equal(0.5m, repo.Prices["chilling flux"].DivineValue);
        Assert.Equal(40.0m, repo.Prices["chilling flux"].ExaltedValue); // 0.5 * 80
    }

    [Fact]
    public async Task CustomOverride_ReplacesPoENinjaEntry()
    {
        using var http = FakeHttp(FakeApiResponse);
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "custom_prices.json"),
            """{"chilling flux":{"divineValue":2.0,"exaltedValue":160.0}}""");

        var repo = new PriceRepository(http);
        await repo.InitialFetchAsync(DefaultConfig(dir.Path));

        Assert.Equal(2.0m, repo.Prices["chilling flux"].DivineValue);
    }

    [Fact]
    public async Task CustomOverride_InsertsNewEntry()
    {
        using var http = FakeHttp("""{"items":[],"lines":[],"core":{"rates":{"exalted":80}}}""");
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "custom_prices.json"),
            """{"support scattering flame":{"divineValue":1.5,"exaltedValue":120.0}}""");

        var repo = new PriceRepository(http);
        await repo.InitialFetchAsync(DefaultConfig(dir.Path));

        Assert.True(repo.Prices.ContainsKey("support scattering flame"));
        Assert.Equal(1.5m, repo.Prices["support scattering flame"].DivineValue);
    }

    [Fact]
    public async Task MissingCustomFile_IsIgnoredSilently()
    {
        using var http = FakeHttp(FakeApiResponse);
        var config = new AppConfig
        {
            LeagueName = "Test League",
            CustomPricesPath = "/nonexistent/path/custom_prices.json"
        };
        var repo = new PriceRepository(http);
        await repo.InitialFetchAsync(config);
        Assert.True(repo.Prices.ContainsKey("chilling flux"));
    }

    [Theory]
    [InlineData("Support: Scattering Flame", "support scattering flame")]
    [InlineData("CHILLING FLUX", "chilling flux")]
    [InlineData("  Grip's Edge  ", "grip s edge")]
    [InlineData("Rune-of-Aldur", "rune of aldur")]
    public void NormalizeName_ProducesConsistentKey(string input, string expected)
    {
        Assert.Equal(expected, PriceRepository.NormalizeName(input));
    }

    private static HttpClient FakeHttp(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        return new HttpClient(handler);
    }
}
