using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using HarmonyLib;

namespace Game {
    /// <summary>
    /// VanillaEnhancement 模组加载器: 负责注册事件钩子、注入 Harmony 补丁、创建配置界面入口按钮、
    /// 以及配置的持久化读写
    /// </summary>
    public class VanillaEnhancementModLoader : ModLoader {
        public override void __ModInitialize() {
            ModsManager.RegisterHook("OnLoadingFinished", this);
            ModsManager.RegisterHook("OnMainMenuScreenCreated", this);
            Harmony harmony = new Harmony("com.vanillaenhancement.test");
            harmony.PatchAll();
        }

        public override void OnLoadingFinished(List<Action> actions) {
            if (TimeDisplayConfig.EnableModWeaponCompat && MusketCooldownTracker.CooldownEnabled)
                ComponentMusketAutoReload.ScanForModWeapons();
            ScreensManager.AddScreen("VanillaEnhancementConfig", new VanillaEnhancementConfigScreen());
            Log.Information("Vanilla Enhancement Mod: Game Loaded.");
        }

        public override void OnMainMenuScreenCreated(MainMenuScreen mainMenuScreen,
            StackPanelWidget leftBottomBar, StackPanelWidget rightBottomBar) {
            rightBottomBar.Children.Add(new BevelledButtonWidget {
                Name = "VanillaEnhancementConfigButton",
                Text = LanguageControl.Get("VanillaEnhancementConfig", 29),
                Size = new Vector2(60, 60),
                FontScale = 1.2f
            });
        }

        /// <summary>将 TimeDisplayConfig 中的所有配置项序列化到 XML 属性</summary>
        public override void SaveSettings(XElement xElement) {
            xElement.SetAttributeValue("HorizontalAlignment", TimeDisplayConfig.HorizontalAlignment.ToString());
            xElement.SetAttributeValue("VerticalAlignment", TimeDisplayConfig.VerticalAlignment.ToString());
            xElement.SetAttributeValue("DropShadow", TimeDisplayConfig.DropShadow.ToString());
            xElement.SetAttributeValue("DawnSegmentColor", FormatColor(TimeDisplayConfig.DawnSegmentColor));
            xElement.SetAttributeValue("DaySegmentColor", FormatColor(TimeDisplayConfig.DaySegmentColor));
            xElement.SetAttributeValue("DuskSegmentColor", FormatColor(TimeDisplayConfig.DuskSegmentColor));
            xElement.SetAttributeValue("NightSegmentColor", FormatColor(TimeDisplayConfig.NightSegmentColor));
            xElement.SetAttributeValue("FullMoonNightColor", FormatColor(TimeDisplayConfig.FullMoonNightColor));
            xElement.SetAttributeValue("EnableReloadCooldown", TimeDisplayConfig.EnableReloadCooldown.ToString());
            xElement.SetAttributeValue("EnableTimeDisplay", TimeDisplayConfig.EnableTimeDisplay.ToString());
            xElement.SetAttributeValue("EnableLongPressReload", TimeDisplayConfig.EnableLongPressReload.ToString());
            xElement.SetAttributeValue("EnableClothingWear", TimeDisplayConfig.EnableClothingWear.ToString());
            xElement.SetAttributeValue("EnableEating", TimeDisplayConfig.EnableEating.ToString());
            xElement.SetAttributeValue("EnableModWeaponCompat", TimeDisplayConfig.EnableModWeaponCompat.ToString());
        }

        /// <summary>从 XML 属性反序列化所有配置项到 TimeDisplayConfig, 兼容旧版 EnableAutoReload 键名</summary>
        public override void LoadSettings(XElement xElement) {
            var h = xElement.Attribute("HorizontalAlignment")?.Value;
            if (h != null) TimeDisplayConfig.HorizontalAlignment = Enum.TryParse(h, out WidgetAlignment wh) ? wh : WidgetAlignment.Near;
            var v = xElement.Attribute("VerticalAlignment")?.Value;
            if (v != null) TimeDisplayConfig.VerticalAlignment = Enum.TryParse(v, out WidgetAlignment wv) ? wv : WidgetAlignment.Far;
            var ds = xElement.Attribute("DropShadow")?.Value;
            if (ds != null) TimeDisplayConfig.DropShadow = bool.TryParse(ds, out bool b) ? b : true;
            TimeDisplayConfig.DawnSegmentColor = ParseColorAttr(xElement, "DawnSegmentColor", TimeDisplayConfig.DawnSegmentColor);
            TimeDisplayConfig.DaySegmentColor = ParseColorAttr(xElement, "DaySegmentColor", TimeDisplayConfig.DaySegmentColor);
            TimeDisplayConfig.DuskSegmentColor = ParseColorAttr(xElement, "DuskSegmentColor", TimeDisplayConfig.DuskSegmentColor);
            TimeDisplayConfig.NightSegmentColor = ParseColorAttr(xElement, "NightSegmentColor", TimeDisplayConfig.NightSegmentColor);
            TimeDisplayConfig.FullMoonNightColor = ParseColorAttr(xElement, "FullMoonNightColor", TimeDisplayConfig.FullMoonNightColor);
            var ec = xElement.Attribute("EnableReloadCooldown")?.Value;
            if (ec != null) TimeDisplayConfig.EnableReloadCooldown = bool.TryParse(ec, out bool bc) ? bc : true;
            MusketCooldownTracker.CooldownEnabled = TimeDisplayConfig.EnableReloadCooldown;
            TimeDisplayConfig.EnableTimeDisplay = ParseBoolAttr(xElement, "EnableTimeDisplay", true);
            var longReload = xElement.Attribute("EnableLongPressReload");
            if (longReload != null)
                TimeDisplayConfig.EnableLongPressReload = bool.TryParse(longReload.Value, out var lr) ? lr : true;
            else
                TimeDisplayConfig.EnableLongPressReload = ParseBoolAttr(xElement, "EnableAutoReload", true);
            TimeDisplayConfig.EnableClothingWear = ParseBoolAttr(xElement, "EnableClothingWear", true);
            TimeDisplayConfig.EnableEating = ParseBoolAttr(xElement, "EnableEating", true);
            TimeDisplayConfig.EnableModWeaponCompat = ParseBoolAttr(xElement, "EnableModWeaponCompat", true);
        }

        /// <summary>安全解析布尔属性, 缺失或格式错误时返回默认值</summary>
        static bool ParseBoolAttr(XElement el, string name, bool fallback) {
            var val = el.Attribute(name)?.Value;
            return val != null ? bool.TryParse(val, out bool b) ? b : fallback : fallback;
        }

        /// <summary>将颜色序列化为 "R,G,B" 字符串</summary>
        static string FormatColor(Color c) => $"{c.R},{c.G},{c.B}";
        /// <summary>从 "R,G,B" 字符串反序列化颜色, 格式错误时返回默认值</summary>
        static Color ParseColorAttr(XElement el, string name, Color fallback) {
            var val = el.Attribute(name)?.Value;
            if (string.IsNullOrEmpty(val)) return fallback;
            var parts = val.Split(',');
            if (parts.Length < 3) return fallback;
            if (int.TryParse(parts[0], out int r) && int.TryParse(parts[1], out int g) && int.TryParse(parts[2], out int b))
                return new Color(r, g, b);
            return fallback;
        }
    }
}
