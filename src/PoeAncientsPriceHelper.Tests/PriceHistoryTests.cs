using PoeAncientsPriceHelper;
using Xunit;

namespace PoeAncientsPriceHelper.Tests;

public class PriceHistoryTests
{
    [Fact]
    public void IsSnipe_ReturnsFalse_WithInsufficientHistory()
    {
        var history = new PriceHistory();
        var prices = new Dictionary<string, PriceEntry>
        {
            ["ancient rune"] = new(0.1m, 19m, 1m)
        };
        // Only 1 snapshot — need at least 3
        history.RecordSnapshot(prices);
        Assert.False(history.IsSnipe("ancient rune", 0.01m, out _));
    }

    [Fact]
    public void IsSnipe_DetectsPriceDrop()
    {
        var history = new PriceHistory();
        // Record 5 snapshots at 0.1 divine
        for (int i = 0; i < 5; i++)
        {
            history.RecordSnapshot(new Dictionary<string, PriceEntry>
            {
                ["test item"] = new(0.1m, 19m, 1m)
            });
        }
        // Now price drops to 0.01 (10% of avg) → should be snipe
        Assert.True(history.IsSnipe("test item", 0.01m, out var avg));
        Assert.Equal(0.1m, avg);
    }

    [Fact]
    public void IsSnipe_ReturnsFalse_WhenPriceIsStable()
    {
        var history = new PriceHistory();
        for (int i = 0; i < 5; i++)
        {
            history.RecordSnapshot(new Dictionary<string, PriceEntry>
            {
                ["stable item"] = new(1.0m, 188m, 10m)
            });
        }
        // Price stays at 1.0 — not a snipe
        Assert.False(history.IsSnipe("stable item", 1.0m, out _));
    }

    [Fact]
    public void IsSnipe_ReturnsFalse_ForUnknownItem()
    {
        var history = new PriceHistory();
        Assert.False(history.IsSnipe("nonexistent", 0.01m, out _));
    }

    [Fact]
    public void Clear_ResetsHistory()
    {
        var history = new PriceHistory();
        for (int i = 0; i < 5; i++)
        {
            history.RecordSnapshot(new Dictionary<string, PriceEntry>
            {
                ["item"] = new(0.1m, 19m, 1m)
            });
        }
        history.Clear();
        Assert.False(history.IsSnipe("item", 0.01m, out _));
    }
}
