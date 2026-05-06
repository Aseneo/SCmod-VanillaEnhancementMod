// 武器 R 键快速装填组件: 支持火枪/弩/弓的 R 键装填, 含冷却、装填检测、弹药优先级选择
using Engine;
using Engine.Input;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game {
    public class ComponentMusketAutoReload : Component, IUpdateable {
        public ComponentPlayer m_componentPlayer;
        public SubsystemTerrain m_subsystemTerrain;
        public SubsystemTime m_subsystemTime;
        // 各武器/弹药的 BlockIndex 缓存
        public int m_musketBlockIndex;
        public int m_gunpowderBlockIndex;
        public int m_cottonWadBlockIndex;
        public int m_bulletBlockIndex;
        public int m_crossbowBlockIndex;
        public int m_bowBlockIndex;
        public int m_arrowBlockIndex;
        public const string fName = "ComponentMusketAutoReload";

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) {
            m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
            m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
            m_musketBlockIndex = BlocksManager.GetBlockIndex<MusketBlock>();
            m_gunpowderBlockIndex = BlocksManager.GetBlockIndex<GunpowderBlock>();
            m_cottonWadBlockIndex = BlocksManager.GetBlockIndex<CottonWadBlock>();
            m_bulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>();
            m_crossbowBlockIndex = BlocksManager.GetBlockIndex<CrossbowBlock>();
            m_bowBlockIndex = BlocksManager.GetBlockIndex<BowBlock>();
            m_arrowBlockIndex = BlocksManager.GetBlockIndex<ArrowBlock>();
        }

        public void Update(float dt) {
            // 只响应 R 键单次按下
            if (m_componentPlayer == null || !Keyboard.IsKeyDownOnce(Key.R)) {
                return;
            }
            ComponentMiner componentMiner = m_componentPlayer.ComponentMiner;
            if (componentMiner == null) {
                return;
            }
            IInventory inventory = componentMiner.Inventory;
            if (inventory == null) {
                return;
            }
            int activeSlotIndex = inventory.ActiveSlotIndex;
            if (activeSlotIndex < 0) {
                return;
            }
            int slotValue = inventory.GetSlotValue(activeSlotIndex);
            int contents = Terrain.ExtractContents(slotValue);
            int data = Terrain.ExtractData(slotValue);
            // 查询当前槽位的冷却剩余时间
            float remaining = MusketCooldownTracker.GetCooldownRemaining(inventory, activeSlotIndex);

            // --- 火枪装填逻辑 ---
            if (contents == m_musketBlockIndex) {
                MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
                // 已装弹完毕 → 显示当前弹种名称
                if (loadState == MusketBlock.LoadState.Loaded) {
                    BulletBlock.BulletType? bulletType = MusketBlock.GetBulletType(data);
                    if (bulletType.HasValue) {
                        ShowLoadedMessage(LanguageControl.Get("BulletBlock", (int)bulletType.Value));
                    }
                    return;
                }
                // 冷却中
                if (remaining > 0f) {
                    ShowCooldownMessage();
                    return;
                }
                // 一次性装填: 火药 + 棉花 + 子弹
                TryReloadMusket(inventory, activeSlotIndex, data);
            }
            // --- 弩装填逻辑: 分两步(拉弦→装螺栓), 共享一个冷却窗口 ---
            else if (contents == m_crossbowBlockIndex) {
                ArrowBlock.ArrowType? boltType = CrossbowBlock.GetArrowType(data);
                int draw = CrossbowBlock.GetDraw(data);
                // 已拉弦 + 已装螺栓 → 显示当前螺栓名称
                if (draw == 15 && boltType.HasValue) {
                    ShowLoadedMessage(LanguageControl.Get("ArrowBlock", (int)boltType.Value));
                    return;
                }
                if (remaining > 0f) {
                    ShowCooldownMessage();
                    return;
                }
                // 第一步: 拉弦(draw→15) 或 第二步: 装填螺栓
                TryReloadCrossbow(inventory, activeSlotIndex, data);
            }
            // --- 弓装填逻辑 ---
            else if (contents == m_bowBlockIndex) {
                ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);
                // 已装箭 → 显示当前箭名
                if (arrowType.HasValue) {
                    ShowLoadedMessage(LanguageControl.Get("ArrowBlock", (int)arrowType.Value));
                    return;
                }
                if (remaining > 0f) {
                    ShowCooldownMessage();
                    return;
                }
                TryReloadBow(inventory, activeSlotIndex, data);
            }
        }

        // 显示冷却提示(闪烁消息)
        public void ShowCooldownMessage() {
            m_componentPlayer.ComponentGui.DisplaySmallMessage("装填冷却中！", Color.White, true, false);
        }

        // 显示缺失材料提示
        public void ShowMissingMessage(string itemName) {
            m_componentPlayer.ComponentGui.DisplaySmallMessage(string.Format("没有可用的 {0}", itemName), Color.White, true, false);
        }

        // 显示已装填提示
        public void ShowLoadedMessage(string ammoName) {
            m_componentPlayer.ComponentGui.DisplaySmallMessage(string.Format("{0} 已装填", ammoName), Color.White, true, false);
        }

        // 火枪装填: 消耗火药+棉花+子弹 → LoadState.Empty→Loaded
        // 子弹优先级: MusketBall(大子弹) > Buckshot(霰弹) > BuckshotBall(小子弹)
        public bool TryReloadMusket(IInventory inventory, int activeSlotIndex, int data) {
            int gunpowderSlot = FindItemInInventory(inventory, m_gunpowderBlockIndex);
            int cottonWadSlot = FindItemInInventory(inventory, m_cottonWadBlockIndex);
            int bulletSlot = FindBestBulletSlot(inventory);
            if (gunpowderSlot < 0) { ShowMissingMessage("火药"); return false; }
            if (cottonWadSlot < 0) { ShowMissingMessage("棉花"); return false; }
            if (bulletSlot < 0) { ShowMissingMessage("子弹"); return false; }
            int bulletValue = inventory.GetSlotValue(bulletSlot);
            BulletBlock.BulletType? bulletType = BulletBlock.GetBulletType(Terrain.ExtractData(bulletValue));
            inventory.RemoveSlotItems(gunpowderSlot, 1);
            inventory.RemoveSlotItems(cottonWadSlot, 1);
            inventory.RemoveSlotItems(bulletSlot, 1);
            int newData = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
            newData = MusketBlock.SetBulletType(newData, bulletType);
            inventory.RemoveSlotItems(activeSlotIndex, 1);
            inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(m_musketBlockIndex, 0, newData), 1);
            return true;
        }

        // 弩装填: 第一步 R=拉弦(draw→15), 第二步 R=装螺栓(draw=15且无螺栓时)
        // 螺栓优先级: DiamondBolt(钻石弩箭) > IronBolt(铁弩箭) > ExplosiveBolt(爆炸弩箭)
        public bool TryReloadCrossbow(IInventory inventory, int activeSlotIndex, int data) {
            int draw = CrossbowBlock.GetDraw(data);
            if (draw < 15) {
                // 拉弦到满
                int newData = CrossbowBlock.SetDraw(data, 15);
                inventory.RemoveSlotItems(activeSlotIndex, 1);
                inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(m_crossbowBlockIndex, 0, newData), 1);
                return true;
            }
            if (!CrossbowBlock.GetArrowType(data).HasValue) {
                // 装填螺栓
                int boltSlot = FindBestBoltSlot(inventory);
                if (boltSlot < 0) { ShowMissingMessage("弩箭"); return false; }
                int boltValue = inventory.GetSlotValue(boltSlot);
                ArrowBlock.ArrowType boltType = ArrowBlock.GetArrowType(Terrain.ExtractData(boltValue));
                inventory.RemoveSlotItems(boltSlot, 1);
                int newData = CrossbowBlock.SetArrowType(data, boltType);
                inventory.RemoveSlotItems(activeSlotIndex, 1);
                inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(m_crossbowBlockIndex, 0, newData), 1);
                return true;
            }
            return false;
        }

        // 弓装填: 消耗一支箭
        // 箭优先级: DiamondArrow > IronArrow > FireArrow > CopperArrow > StoneArrow > WoodenArrow
        public bool TryReloadBow(IInventory inventory, int activeSlotIndex, int data) {
            int arrowSlot = FindBestArrowSlot(inventory);
            if (arrowSlot < 0) { ShowMissingMessage("箭"); return false; }
            int arrowValue = inventory.GetSlotValue(arrowSlot);
            ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(arrowValue));
            inventory.RemoveSlotItems(arrowSlot, 1);
            int newData = BowBlock.SetArrowType(data, arrowType);
            inventory.RemoveSlotItems(activeSlotIndex, 1);
            inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(m_bowBlockIndex, 0, newData), 1);
            return true;
        }

        // 在物品栏中查找指定 blockIndex 的物品(排除手持槽位)
        public int FindItemInInventory(IInventory inventory, int blockIndex) {
            for (int i = 0; i < inventory.VisibleSlotsCount; i++) {
                if (i == inventory.ActiveSlotIndex) { continue; }
                int slotValue = inventory.GetSlotValue(i);
                if (Terrain.ExtractContents(slotValue) == blockIndex && inventory.GetSlotCount(i) > 0) {
                    return i;
                }
            }
            return -1;
        }

        // 查找最优子弹槽位: 大子弹 > 霰弹 > 小子弹
        public int FindBestBulletSlot(IInventory inventory) {
            int musketBallSlot = -1, buckshotSlot = -1, buckshotBallSlot = -1;
            for (int i = 0; i < inventory.VisibleSlotsCount; i++) {
                if (i == inventory.ActiveSlotIndex) { continue; }
                int slotValue = inventory.GetSlotValue(i);
                if (Terrain.ExtractContents(slotValue) != m_bulletBlockIndex || inventory.GetSlotCount(i) <= 0) { continue; }
                BulletBlock.BulletType bulletType = BulletBlock.GetBulletType(Terrain.ExtractData(slotValue));
                switch (bulletType) {
                    case BulletBlock.BulletType.MusketBall: musketBallSlot = i; break;
                    case BulletBlock.BulletType.Buckshot: buckshotSlot = i; break;
                    case BulletBlock.BulletType.BuckshotBall: if (buckshotBallSlot < 0) { buckshotBallSlot = i; } break;
                }
            }
            if (musketBallSlot >= 0) { return musketBallSlot; }
            if (buckshotSlot >= 0) { return buckshotSlot; }
            return buckshotBallSlot;
        }

        // 查找最优弩箭槽位: 钻石弩箭 > 铁弩箭 > 爆炸弩箭
        public int FindBestBoltSlot(IInventory inventory) {
            int diamondBoltSlot = -1, ironBoltSlot = -1, explosiveBoltSlot = -1;
            for (int i = 0; i < inventory.VisibleSlotsCount; i++) {
                if (i == inventory.ActiveSlotIndex) { continue; }
                int slotValue = inventory.GetSlotValue(i);
                if (Terrain.ExtractContents(slotValue) != m_arrowBlockIndex || inventory.GetSlotCount(i) <= 0) { continue; }
                ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(slotValue));
                switch (arrowType) {
                    case ArrowBlock.ArrowType.DiamondBolt: diamondBoltSlot = i; break;
                    case ArrowBlock.ArrowType.IronBolt: ironBoltSlot = i; break;
                    case ArrowBlock.ArrowType.ExplosiveBolt: if (explosiveBoltSlot < 0) { explosiveBoltSlot = i; } break;
                }
            }
            if (diamondBoltSlot >= 0) { return diamondBoltSlot; }
            if (ironBoltSlot >= 0) { return ironBoltSlot; }
            return explosiveBoltSlot;
        }

        // 查找最优箭矢槽位: DiamondArrow > IronArrow > FireArrow > CopperArrow > StoneArrow > WoodenArrow
        public int FindBestArrowSlot(IInventory inventory) {
            int diamondArrowSlot = -1, ironArrowSlot = -1, fireArrowSlot = -1;
            int copperArrowSlot = -1, stoneArrowSlot = -1, woodenArrowSlot = -1;
            for (int i = 0; i < inventory.VisibleSlotsCount; i++) {
                if (i == inventory.ActiveSlotIndex) { continue; }
                int slotValue = inventory.GetSlotValue(i);
                if (Terrain.ExtractContents(slotValue) != m_arrowBlockIndex || inventory.GetSlotCount(i) <= 0) { continue; }
                ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(slotValue));
                switch (arrowType) {
                    case ArrowBlock.ArrowType.DiamondArrow: diamondArrowSlot = i; break;
                    case ArrowBlock.ArrowType.IronArrow: ironArrowSlot = i; break;
                    case ArrowBlock.ArrowType.FireArrow: fireArrowSlot = i; break;
                    case ArrowBlock.ArrowType.CopperArrow: if (copperArrowSlot < 0) { copperArrowSlot = i; } break;
                    case ArrowBlock.ArrowType.StoneArrow: if (stoneArrowSlot < 0) { stoneArrowSlot = i; } break;
                    case ArrowBlock.ArrowType.WoodenArrow: if (woodenArrowSlot < 0) { woodenArrowSlot = i; } break;
                }
            }
            if (diamondArrowSlot >= 0) { return diamondArrowSlot; }
            if (ironArrowSlot >= 0) { return ironArrowSlot; }
            if (fireArrowSlot >= 0) { return fireArrowSlot; }
            if (copperArrowSlot >= 0) { return copperArrowSlot; }
            if (stoneArrowSlot >= 0) { return stoneArrowSlot; }
            return woodenArrowSlot;
        }
    }
}
