using System;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Engine.Input;

namespace Game {
    /// <summary>
    /// VanillaEnhancement 模组配置界面: 提供时间显示位置/颜色、武器装填冷却、右键功能等开关的编辑,
    /// 修改即时写回 TimeDisplayConfig
    /// </summary>
    public class VanillaEnhancementConfigScreen : Screen {
        // 语言表类名, LanguageControl.Get 需要
        const string N = "VanillaEnhancementConfig";
        // 从语言文件获取本地化字符串
        static string T(int k) => LanguageControl.Get(N, k);
        // 开关文字
        static string On => T(22);
        static string Off => T(23);
        // 对齐值 → 本地化名称 (左上/居中/右下/拉伸铺满)
        static string AlignLabel(WidgetAlignment a) => a switch {
            WidgetAlignment.Near => T(25),
            WidgetAlignment.Center => T(26),
            WidgetAlignment.Far => T(27),
            _ => T(28)
        };

        // 动态行容器
        public StackPanelWidget m_contentStack;
        // 各行按钮引用
        public BevelledButtonWidget m_alignHBtn, m_alignVBtn, m_shadowBtn, m_cooldownBtn;
        public BevelledButtonWidget m_timeDisplayBtn, m_longReloadBtn, m_clothingBtn, m_eatingBtn, m_modCompatBtn;
        public BevelledButtonWidget m_dawnBtn, m_dayBtn, m_duskBtn, m_nightBtn, m_fullMoonBtn;
        public RectangleWidget m_dawnPrev, m_dayPrev, m_duskPrev, m_nightPrev, m_fullMoonPrev;
        // 当前编辑中的配置值
        public WidgetAlignment m_alignH, m_alignV;
        public bool m_shadow, m_cooldown, m_timeDisplay, m_longReload, m_clothing, m_eating, m_modCompat;
        public bool m_cooldownLocked;
        public Color m_dawnC, m_dayC, m_duskC, m_nightC, m_fullMoonC;

        /// <summary>从 XML 加载 UI 布局, 获取 ContentStack 容器引用</summary>
        public VanillaEnhancementConfigScreen() {
            var node = ContentManager.Get<XElement>("Screens/VanillaEnhancementConfigScreen");
            LoadContents(this, node);
            m_contentStack = Children.Find<StackPanelWidget>("ContentStack");
        }

        // 每次进入配置界面: 从静态配置读入编辑副本, 重建 UI
        public override void Enter(object[] parameters) {
            m_alignH = TimeDisplayConfig.HorizontalAlignment;
            m_alignV = TimeDisplayConfig.VerticalAlignment;
            m_shadow = TimeDisplayConfig.DropShadow;
            m_cooldown = TimeDisplayConfig.EnableReloadCooldown;
            m_timeDisplay = TimeDisplayConfig.EnableTimeDisplay;
            m_longReload = TimeDisplayConfig.EnableLongPressReload;
            m_clothing = TimeDisplayConfig.EnableClothingWear;
            m_eating = TimeDisplayConfig.EnableEating;
            m_modCompat = TimeDisplayConfig.EnableModWeaponCompat;
            m_cooldownLocked = TimeDisplayConfig.ModWeaponsDetected && m_modCompat;
            m_dawnC = TimeDisplayConfig.DawnSegmentColor;
            m_dayC = TimeDisplayConfig.DaySegmentColor;
            m_duskC = TimeDisplayConfig.DuskSegmentColor;
            m_nightC = TimeDisplayConfig.NightSegmentColor;
            m_fullMoonC = TimeDisplayConfig.FullMoonNightColor;

            m_contentStack.Children.Clear();
            AddHeadline(T(1));       // 时间显示
            m_timeDisplayBtn = AddToggleRow(T(8), m_timeDisplay);
            AddHeadline(T(2));       // 显示位置
            m_alignHBtn = AddCycleRow(T(9), m_alignH);
            m_alignVBtn = AddCycleRow(T(10), m_alignV);
            AddHeadline(T(3));       // 显示样式
            m_shadowBtn = AddToggleRow(T(11), m_shadow);
            AddHeadline(T(4));       // 时段颜色
            (m_dawnPrev, m_dawnBtn) = AddColorRow(T(12), m_dawnC);
            (m_dayPrev, m_dayBtn) = AddColorRow(T(13), m_dayC);
            (m_duskPrev, m_duskBtn) = AddColorRow(T(14), m_duskC);
            (m_nightPrev, m_nightBtn) = AddColorRow(T(15), m_nightC);
            (m_fullMoonPrev, m_fullMoonBtn) = AddColorRow(T(16), m_fullMoonC);
            AddHeadline(T(5));       // 武器装填
            m_longReloadBtn = AddToggleRow(T(17), m_longReload);
            m_cooldownBtn = AddToggleRow(T(18), m_cooldown);
            if (m_cooldownLocked) LockButton(m_cooldownBtn, T(24));
            AddHeadline(T(6));       // 模组兼容
            m_modCompatBtn = AddToggleRow(T(19), m_modCompat);
            AddHeadline(T(7));       // 右键功能
            m_clothingBtn = AddToggleRow(T(20), m_clothing);
            m_eatingBtn = AddToggleRow(T(21), m_eating);
        }

        // 每帧轮询按钮点击 + Esc/返回键退出
        public override void Update() {
            if (Input.Back || Input.IsKeyDownOnce(Key.Escape)
                || Children.Find<ButtonWidget>("TopBar.Back").IsClicked) {
                ScreensManager.GoBack();
                return;
            }
            if (m_alignHBtn != null && m_alignHBtn.IsClicked) { m_alignH = Cycle(m_alignH); m_alignHBtn.Text = AlignLabel(m_alignH); }
            if (m_alignVBtn != null && m_alignVBtn.IsClicked) { m_alignV = Cycle(m_alignV); m_alignVBtn.Text = AlignLabel(m_alignV); }
            if (m_shadowBtn != null && m_shadowBtn.IsClicked) { m_shadow = !m_shadow; m_shadowBtn.Text = m_shadow ? On : Off; }
            if (m_cooldownBtn != null && !m_cooldownLocked && m_cooldownBtn.IsClicked) { m_cooldown = !m_cooldown; m_cooldownBtn.Text = m_cooldown ? On : Off; }
            if (m_timeDisplayBtn != null && m_timeDisplayBtn.IsClicked) { m_timeDisplay = !m_timeDisplay; m_timeDisplayBtn.Text = m_timeDisplay ? On : Off; }
            if (m_longReloadBtn != null && m_longReloadBtn.IsClicked) { m_longReload = !m_longReload; m_longReloadBtn.Text = m_longReload ? On : Off; }
            if (m_clothingBtn != null && m_clothingBtn.IsClicked) { m_clothing = !m_clothing; m_clothingBtn.Text = m_clothing ? On : Off; }
            if (m_eatingBtn != null && m_eatingBtn.IsClicked) { m_eating = !m_eating; m_eatingBtn.Text = m_eating ? On : Off; }
            if (m_modCompatBtn != null && m_modCompatBtn.IsClicked) { m_modCompat = !m_modCompat; m_modCompatBtn.Text = m_modCompat ? On : Off; }
            TryColor(m_dawnBtn, m_dawnPrev, m_dawnC, c => m_dawnC = c);
            TryColor(m_dayBtn, m_dayPrev, m_dayC, c => m_dayC = c);
            TryColor(m_duskBtn, m_duskPrev, m_duskC, c => m_duskC = c);
            TryColor(m_nightBtn, m_nightPrev, m_nightC, c => m_nightC = c);
            TryColor(m_fullMoonBtn, m_fullMoonPrev, m_fullMoonC, c => m_fullMoonC = c);
        }

        // 退出配置界面: 一次性写回所有静态配置 + 清空武器缓存
        public override void Leave() {
            TimeDisplayConfig.HorizontalAlignment = m_alignH;
            TimeDisplayConfig.VerticalAlignment = m_alignV;
            TimeDisplayConfig.DropShadow = m_shadow;
            TimeDisplayConfig.DawnSegmentColor = m_dawnC;
            TimeDisplayConfig.DaySegmentColor = m_dayC;
            TimeDisplayConfig.DuskSegmentColor = m_duskC;
            TimeDisplayConfig.NightSegmentColor = m_nightC;
            TimeDisplayConfig.FullMoonNightColor = m_fullMoonC;
            TimeDisplayConfig.EnableReloadCooldown = m_cooldown;
            TimeDisplayConfig.EnableTimeDisplay = m_timeDisplay;
            TimeDisplayConfig.EnableLongPressReload = m_longReload;
            TimeDisplayConfig.EnableClothingWear = m_clothing;
            TimeDisplayConfig.EnableEating = m_eating;
            TimeDisplayConfig.EnableModWeaponCompat = m_modCompat;
            MusketCooldownTracker.CooldownEnabled = m_cooldown;
            // 兼容开关变更后清除武器类型缓存, 强制下次 R 键时重新检测
            ComponentMusketAutoReload.s_patternCache.Clear();
        }

        // 分区标题: 灰色大字
        void AddHeadline(string s) => m_contentStack.Children.Add(new LabelWidget {
            Text = s, FontScale = 1f, Color = Color.LightGray, MarginTop = 8, MarginBottom = 2
        });

        // 循环切换行: Near→Center→Far→Stretch, 显示本地化文字
        BevelledButtonWidget AddCycleRow(string label, WidgetAlignment cur) {
            var p = new UniformSpacingPanelWidget { Direction = LayoutDirection.Horizontal, Margin = new Vector2(0, 3) };
            p.Children.Add(new LabelWidget { Text = label, HorizontalAlignment = WidgetAlignment.Far, VerticalAlignment = WidgetAlignment.Center, Margin = new Vector2(20, 0) });
            var b = new BevelledButtonWidget { Text = AlignLabel(cur), Style = ContentManager.Get<XElement>("Styles/ButtonStyle_310x60"), VerticalAlignment = WidgetAlignment.Center, Margin = new Vector2(20, 0) };
            p.Children.Add(b);
            m_contentStack.Children.Add(p);
            return b;
        }

        // 开关行: 开启/关闭
        BevelledButtonWidget AddToggleRow(string label, bool cur) {
            var p = new UniformSpacingPanelWidget { Direction = LayoutDirection.Horizontal, Margin = new Vector2(0, 3) };
            p.Children.Add(new LabelWidget { Text = label, HorizontalAlignment = WidgetAlignment.Far, VerticalAlignment = WidgetAlignment.Center, Margin = new Vector2(20, 0) });
            var b = new BevelledButtonWidget { Text = cur ? On : Off, Style = ContentManager.Get<XElement>("Styles/ButtonStyle_310x60"), VerticalAlignment = WidgetAlignment.Center, Margin = new Vector2(20, 0) };
            p.Children.Add(b);
            m_contentStack.Children.Add(p);
            return b;
        }

        // 锁定按钮: 灰色 + 禁止交互 + 提示文字
        void LockButton(BevelledButtonWidget btn, string text) {
            btn.IsEnabled = false;
            btn.Text = text;
            btn.Color = new Color(128, 128, 128);
        }

        // 颜色行: 预览方块 + RGB 按钮 → 点击弹出 EditColorDialog
        (RectangleWidget, BevelledButtonWidget) AddColorRow(string label, Color c) {
            var p = new UniformSpacingPanelWidget { Direction = LayoutDirection.Horizontal, Margin = new Vector2(0, 3) };
            p.Children.Add(new LabelWidget { Text = label, HorizontalAlignment = WidgetAlignment.Far, VerticalAlignment = WidgetAlignment.Center, Margin = new Vector2(20, 0) });
            var row2 = new StackPanelWidget { Direction = LayoutDirection.Horizontal, VerticalAlignment = WidgetAlignment.Center, Margin = new Vector2(20, 0) };
            var prev = new RectangleWidget { Size = new Vector2(32, 32), FillColor = c, MarginRight = 6, VerticalAlignment = WidgetAlignment.Center };
            row2.Children.Add(prev);
            var b = new BevelledButtonWidget { Text = $"{c.R},{c.G},{c.B}", Style = ContentManager.Get<XElement>("Styles/ButtonStyle_310x60"), VerticalAlignment = WidgetAlignment.Center };
            row2.Children.Add(b);
            p.Children.Add(row2);
            m_contentStack.Children.Add(p);
            return (prev, b);
        }

        // 检测颜色按钮点击 → 弹出颜色选择对话框
        void TryColor(BevelledButtonWidget btn, RectangleWidget prev, Color cur, Action<Color> setter) {
            if (btn == null || !btn.IsClicked) return;
            DialogsManager.ShowDialog(this, new EditColorDialog(cur, result => {
                if (result.HasValue) { setter(result.Value); prev.FillColor = result.Value; btn.Text = $"{result.Value.R},{result.Value.G},{result.Value.B}"; }
            }));
        }

        // 对齐值循环切换
        static WidgetAlignment Cycle(WidgetAlignment a) => a switch {
            WidgetAlignment.Near => WidgetAlignment.Center,
            WidgetAlignment.Center => WidgetAlignment.Far,
            WidgetAlignment.Far => WidgetAlignment.Stretch,
            _ => WidgetAlignment.Near
        };
    }
}
