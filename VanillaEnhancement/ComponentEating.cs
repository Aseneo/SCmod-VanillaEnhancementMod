// 右键长按吃食物组件: 模拟 Minecraft 式进食, 含手部动画、咀嚼音效、等级加速
using Engine;
using Engine.Audio;
using Engine.Input;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game {
    public class ComponentEating : Component, IUpdateable {
        public ComponentPlayer m_componentPlayer;
        public ComponentVitalStats m_componentVitalStats;
        public SubsystemAudio m_subsystemAudio;
        public SubsystemTime m_subsystemTime;
        public Random m_random = new();
        /// <summary>当前正在进食的食物物品值(slotValue), 0 表示未在进食</summary>
        public int m_eatingFoodValue;
        /// <summary>已进食累计时间(秒)</summary>
        public float m_eatingTime;
        /// <summary>完成进食所需总时间(秒), 受玩家等级影响</summary>
        public float m_requiredTime;
        /// <summary>咀嚼音效播放计时器</summary>
        public float m_chewSoundTimer;
        /// <summary>当前播放的咀嚼音效实例</summary>
        public Sound m_currentChewSound;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) {
            m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
            m_componentVitalStats = Entity.FindComponent<ComponentVitalStats>(true);
            m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
            m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
        }

        // 开始进食: 记录食物 value, 计算所需时间(等级缩放: 1.1s 起, 每级-20%, 最低 0.5s)
        public void StartEating(int foodValue) {
            m_eatingFoodValue = foodValue;
            m_eatingTime = 0f;
            m_chewSoundTimer = 0f;
            float level = m_componentPlayer.PlayerData.Level;
            m_requiredTime = 1.1f * (1f - 0.2f * (level - 1f));
            m_requiredTime = MathUtils.Max(m_requiredTime, 0.5f);
        }

        public void Update(float dt) {
            if (m_eatingFoodValue == 0) {
                return;
            }
            ComponentMiner componentMiner = m_componentPlayer.ComponentMiner;
            // 物品栏变更 → 打断
            if (componentMiner == null || componentMiner.Inventory == null) { CancelEating(); return; }
            int activeSlotIndex = componentMiner.Inventory.ActiveSlotIndex;
            if (activeSlotIndex < 0) { CancelEating(); return; }
            int slotValue = componentMiner.Inventory.GetSlotValue(activeSlotIndex);
            // 切换物品 → 打断
            if (slotValue != m_eatingFoodValue) { CancelEating(); return; }
            // 松右键 → 打断, 不降低移动速度
            if (!Mouse.IsMouseButtonDown(MouseButton.Right)) { CancelEating(); return; }

            m_eatingTime += dt;

            // 手部动画: 上下节奏起伏(大振幅) + 水平微抖动(小振幅), 越接近吃完越稳定
            ComponentFirstPersonModel componentFirstPersonModel = m_componentPlayer.Entity.FindComponent<ComponentFirstPersonModel>();
            if (componentFirstPersonModel != null) {
                float phase = m_eatingTime / m_requiredTime;
                float num = (float)m_subsystemTime.GameTime * 24f;
                float bobY = -0.22f * (float)System.Math.Sin(phase * (float)System.Math.PI * 2f);
                componentFirstPersonModel.ItemOffsetOrder = new Vector3(
                    0.03f * (float)System.Math.Sin(num * 1.5f) * (1f - phase),
                    bobY + 0.10f + 0.08f * (float)System.Math.Cos(num * 1.7f),
                    0.05f + 0.03f * (float)System.Math.Sin(num * 1.9f)
                );
                componentFirstPersonModel.ItemRotationOrder = new Vector3(
                    0.3f + 0.06f * (float)System.Math.Sin(num * 2.0f),
                    0.04f * (float)System.Math.Cos(num * 1.8f),
                    0.04f * (float)System.Math.Sin(num * 2.3f)
                );
            }

            // 咀嚼音效: 每 0.35 秒播放一次, 松右键立即停止
            m_chewSoundTimer += dt;
            if (m_chewSoundTimer >= 0.35f) {
                m_chewSoundTimer -= 0.35f;
                if (m_currentChewSound != null) {
                    m_currentChewSound.Dispose();
                    m_currentChewSound = null;
                }
                m_currentChewSound = m_subsystemAudio.CreateSound("Audio/Creatures/HumanEat/HumanEat1");
                if (m_currentChewSound != null) {
                    m_currentChewSound.Volume = 1f;
                    m_currentChewSound.Pitch = m_random.Float(0.9f, 1.1f);
                    m_currentChewSound.Play();
                }
            }

            // 进食完毕: 消耗食物 → 调用原版 Eat() 补充饱食度
            if (m_eatingTime >= m_requiredTime) {
                componentMiner.Inventory.RemoveSlotItems(activeSlotIndex, 1);
                m_componentVitalStats.Eat(m_eatingFoodValue);
                CancelEating();
            }
        }

        // 取消进食: 清理状态 + 停止音效 + 复位手部
        public void CancelEating() {
            m_eatingFoodValue = 0;
            m_eatingTime = 0f;
            if (m_currentChewSound != null) {
                m_currentChewSound.Dispose();
                m_currentChewSound = null;
            }
            ComponentFirstPersonModel componentFirstPersonModel = m_componentPlayer.Entity.FindComponent<ComponentFirstPersonModel>();
            if (componentFirstPersonModel != null) {
                componentFirstPersonModel.ItemOffsetOrder = Vector3.Zero;
                componentFirstPersonModel.ItemRotationOrder = Vector3.Zero;
            }
        }
    }
}
