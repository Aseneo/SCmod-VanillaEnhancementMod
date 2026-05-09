// 时间显示组件: 负责在玩家实体加载时将 TimeDisplayWidget 注入 GUI
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game {
    public class ComponentTimeDisplay : Component, IUpdateable {
        public ComponentPlayer m_componentPlayer;
        /// <summary>是否已将 TimeDisplayWidget 注入到玩家 GUI</summary>
        public bool m_widgetInserted;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) {
            m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
        }

        /// <summary>在功能开关开启且玩家 GUI 就绪时, 将 TimeDisplayWidget 添加到 GUI 中</summary>
        public void Update(float dt) {
            if (!TimeDisplayConfig.EnableTimeDisplay) return;
            if (!m_widgetInserted && m_componentPlayer != null && m_componentPlayer.GuiWidget != null) {
                m_componentPlayer.GuiWidget.AddChildren(new TimeDisplayWidget());
                m_widgetInserted = true;
            }
        }
    }
}
