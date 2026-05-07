// 昼夜时间显示控件: 在屏幕指定位置显示天黑/天亮/月圆之夜倒计时, 位置和颜色可通过配置文件自定义
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
                if (project == null) {
                    return;
                }
                m_subsystemTimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>();
                m_subsystemSky = project.FindSubsystem<SubsystemSky>();
                if (m_subsystemTimeOfDay == null) {
                    return;
                }
            }
            float timeOfDay = m_subsystemTimeOfDay.TimeOfDay;
            float duskStart = m_subsystemTimeOfDay.DuskStart;
            float dawnStart = m_subsystemTimeOfDay.DawnStart;
            float dayStart = m_subsystemTimeOfDay.DayStart;
            float dayDuration = m_subsystemTimeOfDay.DayDuration;

            if (timeOfDay >= dawnStart && timeOfDay < duskStart) {
                float interval = duskStart - timeOfDay;
                float secondsLeft = interval * dayDuration;
                int hours = (int)(secondsLeft / 60f);
                int minutes = (int)(secondsLeft % 60f);
                m_label.Text = string.Format("距离天黑: {0}:{1:D2}", hours, minutes);
                m_label.Color = interval < 0.15f ? TimeDisplayConfig.DuskWarningColor : TimeDisplayConfig.DuskCountdownColor;
            }
            else {
                int moonPhase = m_subsystemSky != null ? m_subsystemSky.MoonPhase : -1;
                if (moonPhase == 0 || moonPhase == 4) {
                    float interval = dawnStart - timeOfDay;
                    if (interval < 0f) {
                        interval += 1f;
                    }
                    float secondsLeft = interval * dayDuration;
                    int hours = (int)(secondsLeft / 60f);
                    int minutes = (int)(secondsLeft % 60f);
                    m_label.Text = string.Format("距月圆之夜结束还剩: {0}:{1:D2}", hours, minutes);
                    m_label.Color = TimeDisplayConfig.FullMoonCountdownColor;
                }
                else {
                    float interval = dawnStart - timeOfDay;
                    if (interval < 0f) {
                        interval += 1f;
                    }
                    float secondsLeft = interval * dayDuration;
                    int hours = (int)(secondsLeft / 60f);
                    int minutes = (int)(secondsLeft % 60f);
                    m_label.Text = string.Format("距离天亮: {0}:{1:D2}", hours, minutes);
                    m_label.Color = interval < 0.15f ? TimeDisplayConfig.DawnCountdownNightColor : TimeDisplayConfig.DawnCountdownDayColor;
                }
            }
        }
    }
}
