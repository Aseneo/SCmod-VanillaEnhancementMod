using System;
using System.Collections.Generic;
using System.Reflection;
using Engine;
using Engine.Input;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game {
    public class ComponentMusketAutoReload : Component, IUpdateable {
        public ComponentPlayer m_componentPlayer;
        public SubsystemTerrain m_subsystemTerrain;
        public SubsystemBlockBehaviors m_subsystemBlockBehaviors;

        public enum ReloadPattern { None, Musket, Crossbow, Bow }

        public static Dictionary<int, ReloadPattern> s_patternCache = new();
        public static bool s_hasModWeapons;
        public static Dictionary<int, MethodInfo> s_getLoadState = new();
        public static Dictionary<int, MethodInfo> s_getBulletType = new();
        public static Dictionary<int, MethodInfo> s_getDraw = new();
        public static Dictionary<int, MethodInfo> s_getArrowType = new();
        public static Dictionary<int, MethodInfo> s_setDraw = new();
        // 兜底: 任意 Get*Type(int) 方法缓存(用于检测已装填)
        public static Dictionary<int, MethodInfo> s_anyTypeGetter = new();
        // 持久记录已确认装填过的武器类型(wc), 跨按键保持. 条件: behavior 曾成功处理过弹药
        public static HashSet<int> s_loadedOnce = new();

        // 长按持续装填
        public float m_reloadTimer;
        public int m_reloadWeaponSlot = -1;
        public bool m_didProcessThisHold;
        public const float FirstDelay = 0.50f;
        public const float RepeatDelay = 0.04f;

        public static BindingFlags s_sf = BindingFlags.Public | BindingFlags.Static;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap) {
            m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
            m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
        }

        public void Update(float dt) {
            if (m_componentPlayer == null) return;
            var miner = m_componentPlayer.ComponentMiner;
            if (miner == null) return;
            var inv = miner.Inventory;
            if (inv == null) return;
            int slot = inv.ActiveSlotIndex;
            if (slot < 0) return;
            int sv = inv.GetSlotValue(slot);
            int wc = Terrain.ExtractContents(sv);
            if (wc == 0) return;
            Block block = BlocksManager.Blocks[wc];
            var wb = m_subsystemBlockBehaviors.GetBlockBehaviors(wc);
            ReloadPattern p = DetectPattern(wc, block);
            if (p == ReloadPattern.None) { m_reloadTimer = 0f; m_reloadWeaponSlot = -1; return; }

            bool keyDown = Keyboard.IsKeyDown(Key.R);
            bool keyOnce = Keyboard.IsKeyDownOnce(Key.R);

            if (!keyDown && !keyOnce) { m_reloadTimer = 0f; m_reloadWeaponSlot = -1; return; }

            if (keyOnce || (keyDown && m_reloadWeaponSlot != slot)) {
                m_reloadTimer = 0f;
                m_reloadWeaponSlot = slot;
                m_didProcessThisHold = false;
                bool ok = ProcessSingleStep(inv, slot, wc, sv, block, wb, p);
                if (ok) { m_didProcessThisHold = true; return; }
                ShowFinalStatus(inv, slot, wc, sv, block, p);
                return;
            }
            if (keyDown && TimeDisplayConfig.EnableLongPressReload) {
                m_reloadTimer += dt;
                float threshold = m_reloadTimer < FirstDelay ? FirstDelay : RepeatDelay;
                if (m_reloadTimer >= threshold) {
                    m_reloadTimer -= threshold;
                    bool ok = ProcessSingleStep(inv, slot, wc, sv, block, wb, p);
                    if (ok) { m_didProcessThisHold = true; return; }
                    ShowFinalStatus(inv, slot, wc, sv, block, p);
                    m_reloadTimer = -10f;
                }
            }
        }

        // 执行一次装填步骤, 返回 true 表示成功递进了
        bool ProcessSingleStep(IInventory inv, int slot, int wc, int sv, Block block, SubsystemBlockBehavior[] wb, ReloadPattern p) {
            int data = Terrain.ExtractData(sv);
            float cd = MusketCooldownTracker.GetCooldownRemaining(inv, slot);
            bool ok;
            if (p == ReloadPattern.Crossbow) {
                ok = TryProcessAmmo(inv, slot, wc, data, p, wb, cd);
                if (!ok && !IsAlreadyLoaded(wc, data, block)) { TryCrankCrossbow(inv, slot, wc, data, block, cd); }
            } else {
                ok = TryProcessAmmo(inv, slot, wc, data, p, wb, cd);
            }
            if (ok) s_loadedOnce.Add(wc);
            return ok;
        }

        // 停止持续装填时显示最终状态
        void ShowFinalStatus(IInventory inv, int slot, int wc, int sv, Block block, ReloadPattern p) {
            int data = Terrain.ExtractData(sv);
            bool loaded = m_didProcessThisHold || IsAlreadyLoaded(wc, data, block) || s_loadedOnce.Contains(wc);
            if (loaded) {
                ShowLoaded(block.GetDisplayName(m_subsystemTerrain, Terrain.MakeBlockValue(wc, 0, data)));
            }
            else if (p == ReloadPattern.Musket) {
                ShowMissing(GuessNextMusketAmmo(wc, data, block));
            }
            else if (p == ReloadPattern.Crossbow) {
                int draw = block is CrossbowBlock ? CrossbowBlock.GetDraw(data) : (int)s_getDraw[wc].Invoke(null, [data]);
                ShowMissing("弩箭");
            }
            else if (p == ReloadPattern.Bow) {
                ShowMissing("箭");
            }
        }

        // ===== 核心: behavior 递进装填 =====

        bool TryProcessAmmo(IInventory inv, int slot, int wc, int data, ReloadPattern p, SubsystemBlockBehavior[] wb, float cd) {
            int sc = InvSearchCount(inv);
            for (int offset = 1; offset < sc; offset++) {
                int i = (slot + offset) % sc;
                int isv = inv.GetSlotValue(i);
                if (inv.GetSlotCount(i) <= 0) continue;
                foreach (var bh in wb) {
                    if (bh.GetProcessInventoryItemCapacity(inv, slot, isv) > 0) {
                        if (cd > 0f) { ShowCooldown(); return true; }
                        bh.ProcessInventoryItem(inv, slot, isv, 1, 1, out _, out _);
                        // 如果处理装填的 behavior 不是三个原版类之一, 说明是模组自定义武器, 禁用冷却
                        if (bh is not (SubsystemMusketBlockBehavior or SubsystemCrossbowBlockBehavior or SubsystemBowBlockBehavior))
                            MarkModWeapon();
                        return true;
                    }
                }
            }
            return false;
        }

        void TryCrankCrossbow(IInventory inv, int slot, int wc, int data, Block block, float cd) {
            if (cd > 0f) { ShowCooldown(); return; }
            int nd = 15;
            if (block is CrossbowBlock)
                ReplaceSlot(inv, slot, wc, CrossbowBlock.SetDraw(data, nd));
            else
                ReplaceSlot(inv, slot, wc, (int)s_setDraw[wc].Invoke(null, [data, nd]));
        }

        // ===== 装填状态判断 =====

        // 弹药类型检测: GetBulletType/GetArrowType 非null → 已装填
        // 兜底: 遍历块类所有 Get*Type(int) 方法, 任一个返回非null即视为已装填
        bool IsAlreadyLoaded(int wc, int data, Block block) {
            if (block is MusketBlock) return MusketBlock.GetBulletType(data).HasValue;
            if (block is CrossbowBlock) return CrossbowBlock.GetArrowType(data).HasValue && CrossbowBlock.GetDraw(data) >= 15;
            if (block is BowBlock) return BowBlock.GetArrowType(data).HasValue;

            // 已缓存的读取方法
            if (s_getBulletType.TryGetValue(wc, out var mb) && mb != null) return mb.Invoke(null, [data]) != null;
            if (s_getArrowType.TryGetValue(wc, out var ma) && ma != null) {
                object at = ma.Invoke(null, [data]);
                if (at != null) return !s_getDraw.ContainsKey(wc) || (int)s_getDraw[wc].Invoke(null, [data]) >= 15;
            }

            // 兜底扫描
            if (s_anyTypeGetter.TryGetValue(wc, out var mg) && mg != null) return mg.Invoke(null, [data]) != null;

            Type t = block.GetType();
            if (t == typeof(MusketBlock) || t == typeof(CrossbowBlock) || t == typeof(BowBlock)) return false;

            foreach (MethodInfo m in t.GetMethods(s_sf)) {
                if (!m.Name.EndsWith("Type")) continue;
                ParameterInfo[] pr = m.GetParameters();
                if (pr.Length == 1 && pr[0].ParameterType == typeof(int)) {
                    s_anyTypeGetter[wc] = m;
                    return m.Invoke(null, [data]) != null;
                }
            }
            s_anyTypeGetter[wc] = null;
            return false;
        }

        static string GuessNextMusketAmmo(int wc, int data, Block block) {
            if (block is MusketBlock) {
                return MusketBlock.GetLoadState(data) switch {
                    MusketBlock.LoadState.Empty => "火药",
                    MusketBlock.LoadState.Gunpowder => "棉花",
                    _ => "子弹"
                };
            }
            if (s_getLoadState.TryGetValue(wc, out var m) && m != null) {
                int ls = (int)m.Invoke(null, [data]);
                return ls == 0 ? "火药" : ls == 1 ? "棉花" : "子弹";
            }
            return "弹药";
        }

        // ===== 模式检测 =====

        ReloadPattern DetectPattern(int wc, Block block) {
            if (s_patternCache.TryGetValue(wc, out var c)) return c;

            if (TimeDisplayConfig.EnableModWeaponCompat) {
                // 兼容模式: 子类 + behavior 绑定 + 反射方法签名 三级检测
                if (block is MusketBlock) { if (block.GetType() != typeof(MusketBlock)) MarkModWeapon(); EnsureMethods(block, ReloadPattern.Musket); return Cache(wc, ReloadPattern.Musket); }
                if (block is CrossbowBlock) { if (block.GetType() != typeof(CrossbowBlock)) MarkModWeapon(); EnsureMethods(block, ReloadPattern.Crossbow); return Cache(wc, ReloadPattern.Crossbow); }
                if (block is BowBlock) { if (block.GetType() != typeof(BowBlock)) MarkModWeapon(); EnsureMethods(block, ReloadPattern.Bow); return Cache(wc, ReloadPattern.Bow); }

                var wb = m_subsystemBlockBehaviors.GetBlockBehaviors(wc);
                foreach (var bh in wb) {
                    if (bh is SubsystemMusketBlockBehavior) { MarkModWeapon(); EnsureMethods(block, ReloadPattern.Musket); return Cache(wc, ReloadPattern.Musket); }
                    if (bh is SubsystemCrossbowBlockBehavior) { MarkModWeapon(); EnsureMethods(block, ReloadPattern.Crossbow); return Cache(wc, ReloadPattern.Crossbow); }
                    if (bh is SubsystemBowBlockBehavior) { MarkModWeapon(); EnsureMethods(block, ReloadPattern.Bow); return Cache(wc, ReloadPattern.Bow); }
                }

                Type t = block.GetType();
                if (t.GetMethod("GetLoadState", s_sf) != null && t.GetMethod("SetLoadState", s_sf) != null) { MarkModWeapon(); EnsureMethods(block, ReloadPattern.Musket); return Cache(wc, ReloadPattern.Musket); }
                if (t.GetMethod("GetDraw", s_sf) != null && t.GetMethod("SetDraw", s_sf) != null && t.GetMethod("GetArrowType", s_sf) != null && t.GetMethod("SetArrowType", s_sf) != null) { MarkModWeapon(); EnsureMethods(block, ReloadPattern.Crossbow); return Cache(wc, ReloadPattern.Crossbow); }
                if (t.GetMethod("GetArrowType", s_sf) != null && t.GetMethod("SetArrowType", s_sf) != null && t.GetMethod("GetDraw", s_sf) == null) { MarkModWeapon(); EnsureMethods(block, ReloadPattern.Bow); return Cache(wc, ReloadPattern.Bow); }
            }
            else {
                // 纯原版模式: 仅精确匹配三个原版类型
                if (block.GetType() == typeof(MusketBlock)) { EnsureMethods(block, ReloadPattern.Musket); return Cache(wc, ReloadPattern.Musket); }
                if (block.GetType() == typeof(CrossbowBlock)) { EnsureMethods(block, ReloadPattern.Crossbow); return Cache(wc, ReloadPattern.Crossbow); }
                if (block.GetType() == typeof(BowBlock)) { EnsureMethods(block, ReloadPattern.Bow); return Cache(wc, ReloadPattern.Bow); }
            }
            return Cache(wc, ReloadPattern.None);
        }

        // 在 OnLoadingFinished 时调用: 遍历所有方块检测模组武器, 有则预禁用冷却
        // 比运行时检测更提前, 无需玩家先碰一下模组武器
        public static void ScanForModWeapons() {
            if (s_hasModWeapons) return;
            foreach (Block block in BlocksManager.Blocks) {
                if (block == null) continue;
                Type t = block.GetType();
                if (t == typeof(MusketBlock) || t == typeof(CrossbowBlock) || t == typeof(BowBlock)) continue;
                if (block is MusketBlock || block is CrossbowBlock || block is BowBlock) {
                    MarkModWeapon();
                    return;
                }
            }
        }

        static ReloadPattern Cache(int wc, ReloadPattern p) { s_patternCache[wc] = p; return p; }

        // 标记检测到模组武器: 首次命中时写入标记并全局禁用装填冷却
        // 触发点有三处:
        //   1. OnLoadingFinished 全局方块扫描 — 启动时预判, 无需接触武器
        //   2. DetectPattern 的 behavior/反射路径 — 按R时预判
        //   3. TryProcessAmmo 装填成功时检测 behavior 非原版 — 最终兜底
        static void MarkModWeapon() {
            if (!TimeDisplayConfig.EnableModWeaponCompat) return;
            if (!s_hasModWeapons) {
                s_hasModWeapons = true;
                TimeDisplayConfig.ModWeaponsDetected = true;
                MusketCooldownTracker.CooldownEnabled = false;
                TimeDisplayConfig.EnableReloadCooldown = false;
            }
        }

        static void EnsureMethods(Block block, ReloadPattern p) {
            Type t = block.GetType(); int wc = block.BlockIndex;
            if (p == ReloadPattern.Musket && !s_getLoadState.ContainsKey(wc)) {
                s_getLoadState[wc] = t.GetMethod("GetLoadState", s_sf);
                s_getBulletType[wc] = t.GetMethod("GetBulletType", s_sf);
            } else if (p == ReloadPattern.Crossbow && !s_getDraw.ContainsKey(wc)) {
                s_getDraw[wc] = t.GetMethod("GetDraw", s_sf);
                s_setDraw[wc] = t.GetMethod("SetDraw", s_sf);
                s_getArrowType[wc] = t.GetMethod("GetArrowType", s_sf);
            } else if (p == ReloadPattern.Bow && !s_getArrowType.ContainsKey(wc)) {
                s_getArrowType[wc] = t.GetMethod("GetArrowType", s_sf);
            }
        }

        // ===== 提示 =====

        void ShowCooldown() => m_componentPlayer.ComponentGui.DisplaySmallMessage("装填冷却中！", Color.White, true, false);
        void ShowMissing(string s) => m_componentPlayer.ComponentGui.DisplaySmallMessage(string.Format("没有可用的 {0}", s), Color.White, true, false);
        void ShowLoaded(string s) => m_componentPlayer.ComponentGui.DisplaySmallMessage(string.Format("{0} 已装填", s), Color.White, true, false);

        // ===== 工具 =====

        static void ReplaceSlot(IInventory inv, int slot, int contents, int data) {
            inv.RemoveSlotItems(slot, 1);
            int cc = Terrain.ExtractContents(inv.GetSlotValue(slot));
            if (cc == 0) cc = contents;
            inv.AddSlotItems(slot, Terrain.MakeBlockValue(cc, 0, data), 1);
        }

        static int InvSearchCount(IInventory inv) {
            if (inv is ComponentCreativeInventory) return inv.VisibleSlotsCount;
            return inv.SlotsCount;
        }
    }
}
