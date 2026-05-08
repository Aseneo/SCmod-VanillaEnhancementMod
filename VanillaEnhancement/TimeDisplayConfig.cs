using System;
using System.Text.Json;
using Engine;

namespace Game {
    public class TimeDisplayConfigData {
        public string HorizontalAlignment { get; set; } = "Near";
        public string VerticalAlignment { get; set; } = "Far";
        public float MarginLeft { get; set; } = 10f;
        public float MarginBottom { get; set; } = 10f;
        public float FontScale { get; set; } = 1.1f;
        public bool DropShadow { get; set; } = true;
        public string DuskCountdownColor { get; set; } = "255,255,255";
        public string DuskWarningColor { get; set; } = "255,80,80";
        public string DawnCountdownDayColor { get; set; } = "180,200,255";
        public string DawnCountdownNightColor { get; set; } = "80,160,255";
        public string FullMoonCountdownColor { get; set; } = "255,200,80";
        public bool EnableReloadCooldown { get; set; } = true;
    }

    public static class TimeDisplayConfig {
        public static WidgetAlignment HorizontalAlignment = WidgetAlignment.Near;
        public static WidgetAlignment VerticalAlignment = WidgetAlignment.Far;
        public static float MarginLeft = 10f;
        public static float MarginBottom = 10f;
        public static float FontScale = 1.1f;
        public static bool DropShadow = true;
        public static Color DuskCountdownColor = Color.White;
        public static Color DuskWarningColor = new Color(255, 80, 80);
        public static Color DawnCountdownDayColor = new Color(180, 200, 255);
        public static Color DawnCountdownNightColor = new Color(80, 160, 255);
        public static Color FullMoonCountdownColor = new Color(255, 200, 80);
        public static bool EnableReloadCooldown = true;

        public static string ConfigPath => Storage.CombinePaths(ModsManager.ModsPath, "VanillaEnhancementConfig.json");

        public static void Load() {
            TimeDisplayConfigData data = new();
            if (Storage.FileExists(ConfigPath)) {
                try {
                    string json = Storage.ReadAllText(ConfigPath);
                    data = JsonSerializer.Deserialize<TimeDisplayConfigData>(json) ?? new();
                }
                catch (Exception e) {
                    Log.Warning($"VanillaEnhancement: Failed to load config, using defaults. {e.Message}");
                }
            }
            ApplyData(data);
            Save(data);
        }

        static void Save(TimeDisplayConfigData data) {
            try {
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                Storage.WriteAllText(ConfigPath, json);
            }
            catch (Exception e) {
                Log.Warning($"VanillaEnhancement: Failed to save config. {e.Message}");
            }
        }

        public static void ApplyData(TimeDisplayConfigData data) {
            HorizontalAlignment = ParseAlignment(data.HorizontalAlignment, WidgetAlignment.Near);
            VerticalAlignment = ParseAlignment(data.VerticalAlignment, WidgetAlignment.Far);
            MarginLeft = data.MarginLeft;
            MarginBottom = data.MarginBottom;
            FontScale = data.FontScale;
            DropShadow = data.DropShadow;
            DuskCountdownColor = ParseColor(data.DuskCountdownColor, Color.White);
            DuskWarningColor = ParseColor(data.DuskWarningColor, new Color(255, 80, 80));
            DawnCountdownDayColor = ParseColor(data.DawnCountdownDayColor, new Color(180, 200, 255));
            DawnCountdownNightColor = ParseColor(data.DawnCountdownNightColor, new Color(80, 160, 255));
            FullMoonCountdownColor = ParseColor(data.FullMoonCountdownColor, new Color(255, 200, 80));
            EnableReloadCooldown = data.EnableReloadCooldown;
        }

        public static WidgetAlignment ParseAlignment(string value, WidgetAlignment fallback) {
            if (string.IsNullOrEmpty(value)) { return fallback; }
            if (int.TryParse(value, out int intVal)) {
                return intVal switch {
                    0 => WidgetAlignment.Near,
                    1 => WidgetAlignment.Center,
                    2 => WidgetAlignment.Far,
                    3 => WidgetAlignment.Stretch,
                    _ => fallback
                };
            }
            return value.ToLowerInvariant() switch {
                "near" => WidgetAlignment.Near,
                "center" => WidgetAlignment.Center,
                "far" => WidgetAlignment.Far,
                "stretch" => WidgetAlignment.Stretch,
                _ => fallback
            };
        }

        public static Color ParseColor(string value, Color fallback) {
            if (string.IsNullOrEmpty(value)) { return fallback; }
            string[] parts = value.Split(',');
            if (parts.Length < 3) { return fallback; }
            try {
                int r = int.Parse(parts[0].Trim());
                int g = int.Parse(parts[1].Trim());
                int b = int.Parse(parts[2].Trim());
                return new Color(r, g, b);
            }
            catch {
                return fallback;
            }
        }
    }
}
