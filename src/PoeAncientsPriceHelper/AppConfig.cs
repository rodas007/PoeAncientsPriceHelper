using System.Drawing;

namespace PoeAncientsPriceHelper;

internal sealed class AppConfig
{
    public string LeagueName { get; set; } = "Runes of Aldur";
    // Leagues offered in the settings dropdown. Add more here as new leagues launch.
    public List<string> AvailableLeagues { get; set; } = ["Runes of Aldur"];
    public int RegionX { get; set; } = 0;
    public int RegionY { get; set; } = 0;
    public int RegionWidth { get; set; } = 0;
    public int RegionHeight { get; set; } = 0;
    public int OverlayXOffset { get; set; } = 8;
    public string ReferencePixelColor { get; set; } = "#000000"; // kept for JSON backwards compat, unused
    public string CustomPricesPath { get; set; } = "custom_prices.json";

    public Rectangle RegionRect
    {
        get => new(RegionX, RegionY, RegionWidth, RegionHeight);
        set { RegionX = value.X; RegionY = value.Y; RegionWidth = value.Width; RegionHeight = value.Height; }
    }

    public bool IsCalibrated => RegionWidth > 0 && RegionHeight > 0;
}
