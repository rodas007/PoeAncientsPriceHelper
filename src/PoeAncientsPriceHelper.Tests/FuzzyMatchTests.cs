using PoeAncientsPriceHelper;

namespace PoeAncientsPriceHelper.Tests;

public class FuzzyMatchTests
{
    [Theory]
    [InlineData("", "", 0)]
    [InlineData("abc", "abc", 0)]
    [InlineData("abc", "abd", 1)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("vision", "viswn", 2)]
    public void Levenshtein_ComputesEditDistance(string a, string b, int expected)
    {
        Assert.Equal(expected, ScanEngine.Levenshtein(a, b));
    }

    // Real misreads from the scan log should clear the 0.84 fuzzy threshold against the
    // correct key, while an unrelated item should not.
    [Theory]
    [InlineData("greater viswn rune", "greater vision rune", true)]
    [InlineData("greater reblrth rune", "greater rebirth rune", true)]
    [InlineData("grgater inspiration rune", "greater inspiration rune", true)]
    [InlineData("greater vision rune", "greater rebirth rune", false)] // different item, must NOT match
    public void Similarity_AbsorbsMisreadsButNotWrongItems(string ocr, string key, bool shouldMatch)
    {
        int dist = ScanEngine.Levenshtein(ocr, key);
        double score = 1.0 - (double)dist / System.Math.Max(ocr.Length, key.Length);
        Assert.Equal(shouldMatch, score > 0.84);
    }
}
