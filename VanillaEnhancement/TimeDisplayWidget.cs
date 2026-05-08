// 昼夜时间显示控件: 在屏幕指定位置显示当前时间段+结束倒计时, 位置和颜色可通过配置文件自定义
using Engine;
using GameEntitySystem;

namespace Game {
    public class TimeDisplayWidget : CanvasWidget {
        public LabelWidget m_label;
        public SubsystemTimeOfDay m_subsystemTimeOfDay;
        public SubsystemSky m_subsystemSky;

        public TimeDisplayWidget() {
            HorizontalAlignment = TimeDisplayConfig.HorizontalAlignment;
            VerticalAlignment = TimeDisplayConfig.VerticalAlignment;
            MarginLeft = TimeDisplayConfig.MarginLeft;
            MarginBottom = TimeDisplayConfig.MarginBottom;
            Size = new Vector2(-1f, -1f);
            m_label = new LabelWidget {
                FontScale = TimeDisplayConfig.FontScale,
                DropShadow = TimeDisplayConfig.DropShadow
            };
            Children.Add(m_label);
        }

        public override void Update() {
            if (m_subsystemTimeOfDay == null) {
                Project project = GameManager.Project;
                if (project == null) return;
                m_subsystemTimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>();
                m_subsystemSky = project.FindSubsystem<SubsystemSky>();
                if (m_subsystemTimeOfDay == null) return;
            }
            float t = m_subsystemTimeOfDay.TimeOfDay;
            float ds = m_subsystemTimeOfDay.DayStart;
            float dus = m_subsystemTimeOfDay.DuskStart;
            float ns = m_subsystemTimeOfDay.NightStart;
            float das = m_subsystemTimeOfDay.DawnStart;
            float dd = m_subsystemTimeOfDay.DayDuration;

            string segment;
            float nextStart;
            Color color;

            if (t >= das && t < ds) {
                segment = "黎明";
                nextStart = ds;
                color = TimeDisplayConfig.DawnSegmentColor;
            } else if (t >= ds && t < dus) {
                segment = "白昼";
                nextStart = dus;
                color = TimeDisplayConfig.DaySegmentColor;
            } else if (t >= dus && t < ns) {
                segment = "黄昏";
                nextStart = ns;
                color = TimeDisplayConfig.DuskSegmentColor;
            } else {
                int mp = m_subsystemSky != null ? m_subsystemSky.MoonPhase : -1;
                if (mp == 0 || mp == 4) {
                    segment = "月圆之夜";
                    color = TimeDisplayConfig.FullMoonNightColor;
                } else {
                    segment = "夜晚";
                    color = TimeDisplayConfig.NightSegmentColor;
                }
                nextStart = das;
            }

            float remain = nextStart - t;
            if (remain < 0f) remain += 1f;
            float secs = remain * dd;
            int h = (int)(secs / 60f);
            int m = (int)(secs % 60f);
            m_label.Text = string.Format("{0} {1}:{2:D2}", segment, h, m);
            m_label.Color = color;
        }
    }
}
