using Newtonsoft.Json;

namespace PoeAncientsPriceHelper;

internal static class ConfigStore
{
    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            var json = File.ReadAllText(ConfigPath);
            return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
        }
        catch { return new AppConfig(); }
    }

    public static void Save(AppConfig config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(ConfigPath, json);
    }
}
