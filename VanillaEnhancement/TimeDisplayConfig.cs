using Engine;

namespace Game {
    public static class TimeDisplayConfig {
        public static WidgetAlignment HorizontalAlignment = WidgetAlignment.Near;
        public static WidgetAlignment VerticalAlignment = WidgetAlignment.Far;
        public static float MarginLeft = 10f;
        public static float MarginBottom = 10f;
        public static float FontScale = 1.1f;
        public static bool DropShadow = true;
        public static Color DawnSegmentColor = new Color(255, 200, 128);
        public static Color DaySegmentColor = Color.White;
        public static Color DuskSegmentColor = new Color(255, 140, 60);
        public static Color NightSegmentColor = new Color(140, 180, 255);
        public static Color FullMoonNightColor = new Color(255, 210, 80);
        public static bool EnableReloadCooldown = true;
        public static bool EnableTimeDisplay = true;
        public static bool EnableLongPressReload = true;
        public static bool EnableClothingWear = true;
        public static bool EnableEating = true;
        public static bool EnableModWeaponCompat = true;
        // 是否已检测到模组武器(由 MarkModWeapon 设置), 用于配置界面锁定冷却开关
        public static bool ModWeaponsDetected = false;
    }
}
