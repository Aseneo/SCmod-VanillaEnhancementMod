// 模组加载器入口: 继承 ModLoader, 在模组加载时注册钩子和 Harmony 补丁
using System;
using System.Collections.Generic;
using Engine;
using HarmonyLib;

namespace Game {
    public class VanillaEnhancementModLoader : ModLoader {
        // __ModInitialize() 在模组实例化时被调用, 是最早的执行入口
        public override void __ModInitialize() {
            // 注册游戏加载完成钩子
            ModsManager.RegisterHook("OnLoadingFinished", this);
            // 通过 HarmonyX 注入所有标记了 [HarmonyPatch] 的补丁类
            Harmony harmony = new Harmony("com.vanillaenhancement.test");
            harmony.PatchAll();
        }

        // 游戏所有资源加载完毕后触发
        public override void OnLoadingFinished(List<Action> actions) {
            TimeDisplayConfig.Load();
            MusketCooldownTracker.CooldownEnabled = TimeDisplayConfig.EnableReloadCooldown;
            if (MusketCooldownTracker.CooldownEnabled) ComponentMusketAutoReload.ScanForModWeapons();
            Log.Information("Vanilla Enhancement Mod: Game Loaded.");
        }
    }
}
