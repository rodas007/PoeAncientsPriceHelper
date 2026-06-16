using System.Collections.Concurrent;

namespace PoeAncientsPriceHelper;

/// <summary>
/// Tracks recent price snapshots per item to detect snipe opportunities.
/// An item is a "snipe" if its current price is significantly below its recent average.
/// </summary>
internal sealed class PriceHistory
{
    private readonly ConcurrentDictionary<string,decimal[]> _history = new();
    private const int MaxSnapshots = 10;           // keep last N price observations
    private const double SnipeThreshold = 0.5;     // current price < 50% of avg → snipe

    /// <summary>
    /// Record the current prices for all items. Called after each fetch.
    /// </summary>
    public void RecordSnapshot(IReadOnlyDictionary<string, PriceEntry> prices)
    {
        foreach (var (name, entry) in prices)
        {
            if (entry.DivineValue <= 0) continue;
            _history.AddOrUpdate(name,
                _ => new[] { entry.DivineValue },
                (_, arr) =>
                {
                    var ring = new decimal[MaxSnapshots];
                    Array.Copy(arr, 1, ring, 0, Math.Min(arr.Length, MaxSnapshots - 1));
                    ring[Math.Min(arr.Length, MaxSnapshots - 1)] = entry.DivineValue;
                    return ring;
                });
        }
    }

    /// <summary>
    /// Check if an item is a snipe: current price is less than SnipeThreshold of its average.
    /// Returns true + the average if it is; false otherwise.
    /// </summary>
    public bool IsSnipe(string name, decimal currentPrice, out decimal avgPrice)
    {
        avgPrice = 0;
        if (!_history.TryGetValue(name, out var snapshots) || snapshots.Length < 3)
            return false;  // not enough data

        decimal sum = 0;
        int count = 0;
        foreach (var v in snapshots)
        {
            if (v > 0) { sum += v; count++; }
        }
        if (count < 3) return false;

        avgPrice = sum / count;
        if (avgPrice <= 0) return false;

        // Current price is less than threshold of average → snipe
        return (double)(currentPrice / avgPrice) < SnipeThreshold;
    }

    /// <summary>
    /// Clear all history (e.g. on league change).
    /// </summary>
    public void Clear() => _history.Clear();
}
