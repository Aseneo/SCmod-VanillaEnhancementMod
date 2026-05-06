// 昼夜时间显示控件: 在屏幕上方显示天黑/天亮/月圆之夜倒计时
using System.Xml.Linq;
using Engine;
using GameEntitySystem;

namespace Game {
    public class TimeDisplayWidget : CanvasWidget {
        public LabelWidget m_label;
        public SubsystemTimeOfDay m_subsystemTimeOfDay;
        public SubsystemSky m_subsystemSky;

        public TimeDisplayWidget() {
            // 从 XML 布局文件加载控件结构
            LoadContents(this, ContentManager.Get<XElement>("Widgets/TimeDisplayWidget"));
            m_label = Children.Find<LabelWidget>("TimeLabel");
            Size = new Vector2(-1f, -1f);
        }

        public override void Update() {
            // 延迟初始化: Widget 可能在 Project 尚未就绪时被创建
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

            // 白天阶段: 黎明→黄昏, 显示距离天黑倒计时
            if (timeOfDay >= dawnStart && timeOfDay < duskStart) {
                float interval = duskStart - timeOfDay;
                float secondsLeft = interval * dayDuration;
                int hours = (int)(secondsLeft / 60f);
                int minutes = (int)(secondsLeft % 60f);
                m_label.Text = string.Format("距离天黑: {0}:{1:D2}", hours, minutes);
                // 天黑前 15% 变红警告
                m_label.Color = interval < 0.15f ? new Color(255, 80, 80) : Color.White;
            }
            else {
                // 夜晚阶段: 根据月相判断是否为月圆之夜
                int moonPhase = m_subsystemSky != null ? m_subsystemSky.MoonPhase : -1;
                // MoonPhase 0 和 4 为月圆之夜(新月/满月), 狼人出没
                if (moonPhase == 0 || moonPhase == 4) {
                    float interval = dawnStart - timeOfDay;
                    if (interval < 0f) {
                        interval += 1f;
                    }
                    float secondsLeft = interval * dayDuration;
                    int hours = (int)(secondsLeft / 60f);
                    int minutes = (int)(secondsLeft % 60f);
                    m_label.Text = string.Format("距月圆之夜结束还剩: {0}:{1:D2}", hours, minutes);
                    m_label.Color = new Color(255, 200, 80);
                }
                else {
                    // 普通夜晚: 显示距离天亮倒计时
                    float interval = dawnStart - timeOfDay;
                    if (interval < 0f) {
                        interval += 1f;
                    }
                    float secondsLeft = interval * dayDuration;
                    int hours = (int)(secondsLeft / 60f);
                    int minutes = (int)(secondsLeft % 60f);
                    m_label.Text = string.Format("距离天亮: {0}:{1:D2}", hours, minutes);
                    m_label.Color = interval < 0.15f ? new Color(80, 160, 255) : new Color(180, 200, 255);
                }
            }
        }
    }
}
