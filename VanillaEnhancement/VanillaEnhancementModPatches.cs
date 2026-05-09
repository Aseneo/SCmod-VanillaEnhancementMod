// Harmony 补丁集合: 冷却覆盖层、武器发射检测、秒杀矛、右键吃食物拦截
// 所有补丁通过 VanillaEnhancementModLoader 中的 harmony.PatchAll() 自动注入
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using HarmonyLib;

namespace Game {
    // 武器装填冷却追踪器: 以 (IInventory + SlotIndex) 为键, 每个物品栏槽位独立计时
    public static class MusketCooldownTracker {
        // 槽位键: 组合物品栏引用和槽位索引, 支持 Equals/HashCode 以用作 Dictionary 键
        public struct SlotKey {
            public IInventory Inventory;
            public int SlotIndex;

            public SlotKey(IInventory inventory, int slotIndex) { Inventory = inventory; SlotIndex = slotIndex; }

            public override bool Equals(object obj) {
                return obj is SlotKey other && ReferenceEquals(Inventory, other.Inventory) && SlotIndex == other.SlotIndex;
            }

            public override int GetHashCode() {
                int hash = Inventory != null ? Inventory.GetHashCode() : 0;
                return hash ^ SlotIndex;
            }
        }

        public static Dictionary<SlotKey, double> FireTimes = new();
        public static Dictionary<SlotKey, float> FullCooldowns = new();

        // 由配置文件 + 模组武器检测控制. 为false时RecordFire无操作, GetCooldownRemaining永远返回0
        public static bool CooldownEnabled = true;

        // 记录发射时间, 冷却由调用方计算
        public static void RecordFire(IInventory inventory, int slotIndex, float cooldown) {
            if (!CooldownEnabled) return;
            SlotKey key = new SlotKey(inventory, slotIndex);
            FireTimes[key] = Time.FrameStartTime;
            FullCooldowns[key] = cooldown;
        }

        // 查询剩余冷却秒数, 已结束时返回 0
        public static float GetCooldownRemaining(IInventory inventory, int slotIndex) {
            if (!CooldownEnabled) return 0f;
            SlotKey key = new SlotKey(inventory, slotIndex);
            if (FireTimes.TryGetValue(key, out double fireTime)) {
                float full = GetFullCooldown(inventory, slotIndex);
                float elapsed = (float)(Time.FrameStartTime - fireTime);
                float remaining = full - elapsed;
                return remaining > 0f ? remaining : 0f;
            }
            return 0f;
        }

        public static float GetFullCooldown(IInventory inventory, int slotIndex) {
            SlotKey key = new SlotKey(inventory, slotIndex);
            if (FullCooldowns.TryGetValue(key, out float full)) { return full; }
            return 2.5f;
        }

        public static bool IsReloadableWeapon(int contents) {
            if (contents == 0 || contents >= BlocksManager.Blocks.Length) { return false; }
            if (ComponentMusketAutoReload.s_patternCache.TryGetValue(contents, out ComponentMusketAutoReload.ReloadPattern pattern)) {
                return pattern != ComponentMusketAutoReload.ReloadPattern.None;
            }
            Block block = BlocksManager.Blocks[contents];
            return block is MusketBlock || block is CrossbowBlock || block is BowBlock;
        }
    }

    // 冷却覆盖层: 在每个 InventorySlotWidget 上叠加一个 LabelWidget 显示冷却倒计时数字
    [HarmonyPatch(typeof(InventorySlotWidget), MethodType.Constructor)]
    static class InventorySlotCooldownOverlayPatch {
        public static Dictionary<InventorySlotWidget, LabelWidget> Labels = new();

        static void Postfix(InventorySlotWidget __instance) {
            LabelWidget label = new LabelWidget();
            label.IsVisible = false;
            label.IsHitTestVisible = false;
            label.FontScale = 0.9f;
            label.DropShadow = true;
            label.TextAnchor = TextAnchor.Center;
            label.HorizontalAlignment = WidgetAlignment.Center;
            label.VerticalAlignment = WidgetAlignment.Center;
            __instance.Children.Add(label);
            Labels[__instance] = label;
        }
    }

    // 冷却覆盖层更新: 每帧检查该槽位是否需要装填且有冷却, 显示 X.X 倒计时数字
    [HarmonyPatch(typeof(InventorySlotWidget), nameof(InventorySlotWidget.Update))]
    static class InventorySlotCooldownUpdatePatch {
        static void Postfix(InventorySlotWidget __instance) {
            if (__instance.m_inventory == null) { return; }
            int slotIndex = __instance.m_slotIndex;
            int slotValue = __instance.m_inventory.GetSlotValue(slotIndex);
            int contents = Terrain.ExtractContents(slotValue);
            if (!MusketCooldownTracker.IsReloadableWeapon(contents)) {
                if (InventorySlotCooldownOverlayPatch.Labels.TryGetValue(__instance, out LabelWidget lb)) { lb.IsVisible = false; }
                return;
            }
            int data = Terrain.ExtractData(slotValue);
            Block block = BlocksManager.Blocks[contents];
            bool needsReload = false;
            if (block is MusketBlock) {
                needsReload = MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Empty;
            }
            else if (block is CrossbowBlock) {
                needsReload = CrossbowBlock.GetDraw(data) < 15 || !CrossbowBlock.GetArrowType(data).HasValue;
            }
            else if (block is BowBlock) {
                needsReload = !BowBlock.GetArrowType(data).HasValue;
            }
            if (!needsReload) {
                if (InventorySlotCooldownOverlayPatch.Labels.TryGetValue(__instance, out LabelWidget lb)) { lb.IsVisible = false; }
                return;
            }
            float remaining = MusketCooldownTracker.GetCooldownRemaining(__instance.m_inventory, slotIndex);
            if (InventorySlotCooldownOverlayPatch.Labels.TryGetValue(__instance, out LabelWidget label)) {
                if (remaining > 0f) {
                    label.Text = remaining.ToString("F1");
                    label.Color = new Color(255, 255, 255);
                    label.IsVisible = true;
                }
                else { label.IsVisible = false; }
            }
        }
    }

    // 火枪发射检测: 拦截 SubsystemMusketBlockBehavior.OnAim(AimState.Completed)
    // Prefix 在发射前计算冷却, Postfix 在发射后记录(仅当 LoadState 变为 Empty 时)
    [HarmonyPatch(typeof(SubsystemMusketBlockBehavior), nameof(SubsystemMusketBlockBehavior.OnAim))]
    static class MusketFireDetectionPatch {
        static float s_cooldown;

        static bool Prefix(SubsystemMusketBlockBehavior __instance, ComponentMiner componentMiner, AimState state) {
            if (state != AimState.Completed) { return true; }
            if (componentMiner == null || componentMiner.Inventory == null) { return true; }
            IInventory inventory = componentMiner.Inventory;
            int slotIndex = inventory.ActiveSlotIndex;
            if (slotIndex < 0) { return true; }
            int slotValue = inventory.GetSlotValue(slotIndex);
            if (Terrain.ExtractContents(slotValue) != __instance.m_MusketBlockIndex) { return true; }
            int data = Terrain.ExtractData(slotValue);
            MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
            if (loadState == MusketBlock.LoadState.Empty) { return true; }
            if (!MusketBlock.GetHammerState(data)) { return true; }
            // 火枪冷却: 2.5s 起, 每级-20%, 最低 1.0s
            ComponentPlayer player = componentMiner.Entity.FindComponent<ComponentPlayer>();
            if (player != null) {
                float level = player.PlayerData.Level;
                s_cooldown = 2.5f * (1f - 0.2f * (level - 1f));
                s_cooldown = MathUtils.Max(s_cooldown, 1f);
            }
            else { s_cooldown = 2.5f; }
            return true;
        }

        static void Postfix(SubsystemMusketBlockBehavior __instance, ComponentMiner componentMiner, AimState state) {
            if (state != AimState.Completed || s_cooldown <= 0f) { s_cooldown = 0f; return; }
            if (componentMiner == null || componentMiner.Inventory == null) { s_cooldown = 0f; return; }
            IInventory inventory = componentMiner.Inventory;
            int slotIndex = inventory.ActiveSlotIndex;
            if (slotIndex < 0) { s_cooldown = 0f; return; }
            int slotValue = inventory.GetSlotValue(slotIndex);
            if (Terrain.ExtractContents(slotValue) != __instance.m_MusketBlockIndex) { s_cooldown = 0f; return; }
            // 仅当发射成功(LoadState 变为 Empty)时记录冷却
            int data = Terrain.ExtractData(slotValue);
            if (MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Empty) {
                MusketCooldownTracker.RecordFire(inventory, slotIndex, s_cooldown);
            }
            s_cooldown = 0f;
        }
    }

    // 弩发射检测: 弩冷却 1.5s 起, 每级-20%, 最低 0.5s
    [HarmonyPatch(typeof(SubsystemCrossbowBlockBehavior), nameof(SubsystemCrossbowBlockBehavior.OnAim))]
    static class CrossbowFireDetectionPatch {
        static float s_cooldown;

        static bool Prefix(SubsystemCrossbowBlockBehavior __instance, ComponentMiner componentMiner, AimState state) {
            if (state != AimState.Completed) { return true; }
            if (componentMiner == null || componentMiner.Inventory == null) { return true; }
            IInventory inventory = componentMiner.Inventory;
            int slotIndex = inventory.ActiveSlotIndex;
            if (slotIndex < 0) { return true; }
            int slotValue = inventory.GetSlotValue(slotIndex);
            if (Terrain.ExtractContents(slotValue) != __instance.m_CrossbowBlockIndex) { return true; }
            int data = Terrain.ExtractData(slotValue);
            if (CrossbowBlock.GetDraw(data) != 15 || !CrossbowBlock.GetArrowType(data).HasValue) { return true; }
            ComponentPlayer player = componentMiner.Entity.FindComponent<ComponentPlayer>();
            if (player != null) {
                float level = player.PlayerData.Level;
                s_cooldown = 1.5f * (1f - 0.2f * (level - 1f));
                s_cooldown = MathUtils.Max(s_cooldown, 0.5f);
            }
            else { s_cooldown = 1.5f; }
            return true;
        }

        static void Postfix(SubsystemCrossbowBlockBehavior __instance, ComponentMiner componentMiner, AimState state) {
            if (state != AimState.Completed || s_cooldown <= 0f) { s_cooldown = 0f; return; }
            if (componentMiner == null || componentMiner.Inventory == null) { s_cooldown = 0f; return; }
            IInventory inventory = componentMiner.Inventory;
            int slotIndex = inventory.ActiveSlotIndex;
            if (slotIndex < 0) { s_cooldown = 0f; return; }
            MusketCooldownTracker.RecordFire(inventory, slotIndex, s_cooldown);
            s_cooldown = 0f;
        }
    }

    // 弓发射检测: 弓冷却 0.8s 起, 每级-20%, 最低 0.3s
    [HarmonyPatch(typeof(SubsystemBowBlockBehavior), nameof(SubsystemBowBlockBehavior.OnAim))]
    static class BowFireDetectionPatch {
        static float s_cooldown;

        static bool Prefix(SubsystemBowBlockBehavior __instance, ComponentMiner componentMiner, AimState state) {
            if (state != AimState.Completed) { return true; }
            if (componentMiner == null || componentMiner.Inventory == null) { return true; }
            IInventory inventory = componentMiner.Inventory;
            int slotIndex = inventory.ActiveSlotIndex;
            if (slotIndex < 0) { return true; }
            int slotValue = inventory.GetSlotValue(slotIndex);
            int contents = Terrain.ExtractContents(slotValue);
            if (contents == 0 || contents >= BlocksManager.Blocks.Length) { return true; }
            if (!(BlocksManager.Blocks[contents] is BowBlock)) { return true; }
            int data = Terrain.ExtractData(slotValue);
            if (!BowBlock.GetArrowType(data).HasValue) { return true; }
            ComponentPlayer player = componentMiner.Entity.FindComponent<ComponentPlayer>();
            if (player != null) {
                float level = player.PlayerData.Level;
                s_cooldown = 0.8f * (1f - 0.2f * (level - 1f));
                s_cooldown = MathUtils.Max(s_cooldown, 0.3f);
            }
            else { s_cooldown = 0.8f; }
            return true;
        }

        static void Postfix(SubsystemBowBlockBehavior __instance, ComponentMiner componentMiner, AimState state) {
            if (state != AimState.Completed || s_cooldown <= 0f) { s_cooldown = 0f; return; }
            if (componentMiner == null || componentMiner.Inventory == null) { s_cooldown = 0f; return; }
            IInventory inventory = componentMiner.Inventory;
            int slotIndex = inventory.ActiveSlotIndex;
            if (slotIndex < 0) { s_cooldown = 0f; return; }
            MusketCooldownTracker.RecordFire(inventory, slotIndex, s_cooldown);
            s_cooldown = 0f;
        }
    }

    // 秒杀矛命中检测: 拦截 ComponentMiner.Hit()
    // Prefix 捕获命中前血量, Postfix 将血量归零并在命中后显示有效血量(Health * AttackResilience)
    [HarmonyPatch(typeof(ComponentMiner), nameof(ComponentMiner.Hit))]
    static class InstantKillSpearHitPatch {
        static float s_capturedHealth;
        static float s_attackResilience;
        static bool s_shouldInstantKill;

        static void Prefix(ComponentMiner __instance, ComponentBody componentBody) {
            s_shouldInstantKill = false;
            int instantKillIndex = BlocksManager.GetBlockIndex<InstantKillSpearBlock>();
            if (instantKillIndex < 0 || Terrain.ExtractContents(__instance.ActiveBlockValue) != instantKillIndex) { return; }
            ComponentHealth health = componentBody.Entity.FindComponent<ComponentHealth>();
            if (health == null) { return; }
            s_capturedHealth = health.Health;
            s_attackResilience = health.AttackResilience;
            s_shouldInstantKill = true;
        }

        static void Postfix(ComponentMiner __instance, ComponentBody componentBody) {
            if (!s_shouldInstantKill) { return; }
            s_shouldInstantKill = false;
            ComponentHealth health = componentBody.Entity.FindComponent<ComponentHealth>();
            if (health == null) { return; }
            // 确认命中(血量下降)后秒杀并显示有效血量
            if (health.Health < s_capturedHealth) {
                float displayHealth = s_capturedHealth * s_attackResilience;
                health.Health = 0f;
                ComponentPlayer player = __instance.Entity.FindComponent<ComponentPlayer>();
                if (player != null) {
                    player.ComponentGui.DisplaySmallMessage(string.Format("生物血量: {0:F0}", displayHealth), Color.White, false, false);
                }
            }
        }
    }

    // 右键自动穿衣: 拦截 ComponentMiner.Use(), 手持衣物时自动穿戴到对应槽位
    [HarmonyPatch(typeof(ComponentMiner), nameof(ComponentMiner.Use))]
    [HarmonyPriority(Priority.HigherThanNormal)]
    static class RightClickWearClothingPatch {
        static bool Prefix(ComponentMiner __instance, Ray3 ray, ref bool __result) {
            if (!TimeDisplayConfig.EnableClothingWear) return true;
            if (__instance.Inventory == null) { return true; }
            int activeSlotIndex = __instance.Inventory.ActiveSlotIndex;
            if (activeSlotIndex < 0) { return true; }
            int slotValue = __instance.Inventory.GetSlotValue(activeSlotIndex);
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
            ClothingData clothingData = block.GetClothingData(slotValue);
            if (clothingData == null) { return true; }
            ComponentClothing componentClothing = __instance.Entity.FindComponent<ComponentClothing>();
            if (componentClothing == null) { return true; }
            if (!componentClothing.CanWearClothing(slotValue)) {
                __instance.ComponentPlayer?.ComponentGui.DisplaySmallMessage(
                    "无法穿戴此衣物", Color.White, true, false);
                __result = true;
                return false;
            }
            if (componentClothing.EnableDressLimit
                && clothingData.PlayerLevelRequired > __instance.ComponentPlayer?.PlayerData.Level) {
                __instance.ComponentPlayer.ComponentGui.DisplaySmallMessage(
                    string.Format(LanguageControl.Get(ComponentClothing.fName, 1),
                        clothingData.PlayerLevelRequired, clothingData.DisplayName),
                    Color.White, true, false);
                __result = true;
                return false;
            }
            clothingData.Mount?.Invoke(slotValue, componentClothing);
            List<int> list = [..componentClothing.GetClothes(clothingData.Slot), slotValue];
            componentClothing.SetClothes(clothingData.Slot, list);
            __instance.Inventory.RemoveSlotItems(activeSlotIndex, 1);
            __result = true;
            return false;
        }
    }

    // 右键吃食物拦截: 拦截 ComponentMiner.Use(), 手持食物时启动长按进食状态机
    [HarmonyPatch(typeof(ComponentMiner), nameof(ComponentMiner.Use))]
    static class RightClickEatPatch {
        static bool Prefix(ComponentMiner __instance, Ray3 ray, ref bool __result) {
            if (!TimeDisplayConfig.EnableEating) return true;
            if (__instance.Inventory == null) { return true; }
            int activeSlotIndex = __instance.Inventory.ActiveSlotIndex;
            if (activeSlotIndex < 0) { return true; }
            int slotValue = __instance.Inventory.GetSlotValue(activeSlotIndex);
            int contents = Terrain.ExtractContents(slotValue);
            Block block = BlocksManager.Blocks[contents];
            float nutrition = block.GetNutritionalValue(slotValue);
            if (nutrition <= 0f) { return true; }
            ComponentVitalStats vitalStats = __instance.Entity.FindComponent<ComponentVitalStats>();
            if (vitalStats == null) { return true; }
            // 饱食度已满 → 不拦截, 允许原版逻辑
            if (vitalStats.Food >= 0.98f) { return true; }
            ComponentEating eating = __instance.Entity.FindComponent<ComponentEating>();
            if (eating != null) { eating.StartEating(slotValue); }
            __result = true;
            return false;
        }
    }

    // 主菜单右下角配置按钮: 轮询 IsClicked → 弹出 Dialog
    [HarmonyPatch(typeof(MainMenuScreen), nameof(MainMenuScreen.Update))]
    static class MainMenuConfigButtonPatch {
        static void Postfix(MainMenuScreen __instance) {
            var rightBar = __instance.Children.Find<StackPanelWidget>("RightBottomBar", false);
            if (rightBar == null) return;
            var btn = rightBar.Children.Find<BevelledButtonWidget>("VanillaEnhancementConfigButton", false);
            if (btn != null && btn.IsClicked) {
                ScreensManager.SwitchScreen("VanillaEnhancementConfig");
            }
        }
    }
}

