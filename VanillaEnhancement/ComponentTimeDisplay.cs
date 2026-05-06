// 时间显示组件: 负责在玩家实体加载时将 TimeDisplayWidget 注入 GUI
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game {
    public class ComponentTimeDisplay : Component, IUpdateable {
        public ComponentPlayer m_componentPlayer;
        public bool m_widgetInserted;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) {
            m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
        }

        public void Update(float dt) {
            // 仅在首次 Update 时插入控件, 确保 GuiWidget 已就绪
            if (!m_widgetInserted && m_componentPlayer != null && m_componentPlayer.GuiWidget != null) {
                m_componentPlayer.GuiWidget.AddChildren(new TimeDisplayWidget());
                m_widgetInserted = true;
            }
        }
    }
}
