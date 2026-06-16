using PoeAncientsPriceHelper;
using Xunit;

namespace PoeAncientsPriceHelper.Tests;

public class ScanEngineTests
{
    [Theory]
    [InlineData("hello", "hello", 0)]       // identical
    [InlineData("hello", "hallo", 1)]       // one substitution
    [InlineData("hello", "helo", 1)]        // one deletion
    [InlineData("hello", "helloo", 1)]      // one insertion
    [InlineData("kitten", "sitting", 3)]    // classic example
    [InlineData("", "abc", 3)]              // empty vs non-empty
    [InlineData("abc", "", 3)]              // non-empty vs empty
    [InlineData("", "", 0)]                // both empty
    public void Levenshtein_ComputesCorrectDistance(string a, string b, int expected)
    {
        Assert.Equal(expected, ScanEngine.Levenshtein(a, b));
    }

    [Fact]
    public void TryResolveGemKey_DetectsSkillGem()
    {
        Assert.True(ScanEngine.TryResolveGemKey("uncut skill gem level 19", out var key));
        Assert.Equal("uncut skill gem level 19", key);
    }

    [Fact]
    public void TryResolveGemKey_DetectsSpiritGem()
    {
        Assert.True(ScanEngine.TryResolveGemKey("uncut spirit gem level 5", out var key));
        Assert.Equal("uncut spirit gem level 5", key);
    }

    [Fact]
    public void TryResolveGemKey_DetectsSupportGem()
    {
        Assert.True(ScanEngine.TryResolveGemKey("uncut support gem level 20", out var key));
        Assert.Equal("uncut support gem level 20", key);
    }

    [Fact]
    public void TryResolveGemKey_ReturnsNullKey_WhenLevelMissing()
    {
        // Recognised as gem but no level number → key is null (shows '?' in overlay)
        Assert.True(ScanEngine.TryResolveGemKey("uncut skill gem", out var key));
        Assert.Null(key);
    }

    [Fact]
    public void TryResolveGemKey_ReturnsFalse_ForNonGem()
    {
        Assert.False(ScanEngine.TryResolveGemKey("ancient rune of decay", out _));
    }

    [Fact]
    public void TryResolveGemKey_IgnoresMisspelledType()
    {
        // "skil" instead of "skill" — the regex looks for exact type words
        Assert.False(ScanEngine.TryResolveGemKey("uncut skil gem level 10", out _));
    }
}
