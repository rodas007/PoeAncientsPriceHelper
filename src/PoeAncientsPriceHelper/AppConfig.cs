using System.Drawing;
using Newtonsoft.Json;

namespace PoeAncientsPriceHelper;

internal sealed class AppConfig
{
    public string LeagueName { get; set; } = "Runes of Aldur";
    // Leagues are fetched dynamically from poe.ninja on startup.
    // Fallback list used when the fetch fails (offline, rate-limited, etc.).
    [JsonIgnore]
    public List<string> AvailableLeagues { get; set; } = ["Runes of Aldur", "HC Runes of Aldur"];

    [JsonIgnore]
    public bool LeaguesFetchedFromApi { get; set; }
    public int RegionX { get; set; } = 0;
    public int RegionY { get; set; } = 0;
    public int RegionWidth { get; set; } = 0;
    public int RegionHeight { get; set; } = 0;
    public int OverlayXOffset { get; set; } = 8;
    // Global hotkeys, each stored as a SharpHook KeyCode name (e.g. "VcF5"). Missing in older configs
    // → fall back to the historical defaults (F5 start/stop, F3 debug, F4 calibrate), preserving prior
    // behaviour. All three live on the same SharpHook hook now. See HotkeyBinding for parse/display.
    public string StartStopHotkey { get; set; } = "VcF5";
    public string DebugHotkey { get; set; } = "VcF3";
    public string CalibrateHotkey { get; set; } = "VcF4";
    public string ReferencePixelColor { get; set; } = "#000000"; // kept for JSON backwards compat, unused
    public string CustomPricesPath { get; set; } = "custom_prices.json";

    public Rectangle RegionRect
    {
        get => new(RegionX, RegionY, RegionWidth, RegionHeight);
        set { RegionX = value.X; RegionY = value.Y; RegionWidth = value.Width; RegionHeight = value.Height; }
    }

    public bool IsCalibrated => RegionWidth > 0 && RegionHeight > 0;
}
