using Engine;

namespace Game {
    public static class TimeDisplayConfig {
        /// <summary>时间控件水平对齐方式: Near=左, Center=中, Far=下, Stretch=拉伸铺满</summary>
        public static WidgetAlignment HorizontalAlignment = WidgetAlignment.Near;
        /// <summary>时间控件垂直对齐方式: Near=上, Center=中, Far=下, Stretch=拉伸铺满</summary>
        public static WidgetAlignment VerticalAlignment = WidgetAlignment.Far;
        public static float MarginLeft = 10f;
        public static float MarginBottom = 10f;
        public static float FontScale = 1.1f;
        /// <summary>时间显示文字是否带阴影</summary>
        public static bool DropShadow = true;
        /// <summary>黎明时段显示颜色 (DawnStart → DayStart)</summary>
        public static Color DawnSegmentColor = new Color(255, 200, 128);
        /// <summary>白昼时段显示颜色 (DayStart → DuskStart)</summary>
        public static Color DaySegmentColor = Color.White;
        /// <summary>黄昏时段显示颜色 (DuskStart → NightStart)</summary>
        public static Color DuskSegmentColor = new Color(255, 140, 60);
        /// <summary>夜晚时段显示颜色 (NightStart → DawnStart, 非月圆)</summary>
        public static Color NightSegmentColor = new Color(140, 180, 255);
        /// <summary>月圆之夜时段显示颜色 (MoonPhase 0 或 4)</summary>
        public static Color FullMoonNightColor = new Color(255, 210, 80);
        /// <summary>武器装填冷却开关; 检测到模组武器时可能被自动禁用并锁定</summary>
        public static bool EnableReloadCooldown = true;
        /// <summary>昼夜时间显示功能总开关; 关闭后屏幕上不再显示时间控件</summary>
        public static bool EnableTimeDisplay = true;
        /// <summary>R键长按连续装填开关; 关闭后单按R仍执行一次, 但长按不再自动循环装填</summary>
        public static bool EnableLongPressReload = true;
        /// <summary>右键自动穿衣功能开关</summary>
        public static bool EnableClothingWear = true;
        /// <summary>右键长按进食功能开关</summary>
        public static bool EnableEating = true;
        /// <summary>
        /// 模组武器兼容模式开关: 开启后三级检测(类型继承/behavior绑定/反射)激活,
        /// 检测到模组武器时自动禁用冷却; 关闭后R键仅对精确类型匹配的原版武器有效
        /// </summary>
        public static bool EnableModWeaponCompat = true;
        /// <summary>是否已检测到模组武器(由 MarkModWeapon 在运行时设置), 用于配置界面锁定冷却开关</summary>
        public static bool ModWeaponsDetected = false;
    }
}
