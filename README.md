# VanillaEnhancement - 原版内容辅助增强

Survivalcraft API 1.9.0.2 增强型模组，为原版游戏添加一系列 QoL 辅助功能和测试工具。

## 目录结构

```
VanillaEnhancement/
├── VanillaEnhancement.sln                  # 解决方案文件
└── VanillaEnhancement/
    ├── modinfo.json                         # 模组元数据
    ├── VanillaEnhancement.csproj            # .NET 10 项目文件
    ├── VanillaEnhancementModLoader.cs       # 模组入口: 注册钩子 + Harmony 激活
    ├── VanillaEnhancementModPatches.cs      # 全部 Harmony 补丁 (七大功能)
    ├── ComponentTimeDisplay.cs              # 时间显示组件 (注入 GUI)
    ├── TimeDisplayWidget.cs                 # 昼夜/月相倒计时控件
    ├── ComponentMusketAutoReload.cs         # 武器 R 键快速装填组件
    ├── ComponentEating.cs                   # 右键长按进食状态机
    ├── InstantKillSpearBlock.cs             # 秒杀测试矛 (方块注册)
    └── Assets/
        ├── VanillaEnhancementDatabase.xdb   # 数据库: 组件注册
        ├── InstantKillSpearBlocksData.csv   # 方块表: 秒杀矛注册
        ├── Widgets/
        │   └── TimeDisplayWidget.xml        # 时间控件布局
        └── Lang/
            ├── zh-CN.json
            └── en-US.json
```

## 功能详解

### 1. 昼夜/月圆时间显示

- **文件**: [ComponentTimeDisplay.cs](VanillaEnhancement/ComponentTimeDisplay.cs) + [TimeDisplayWidget.cs](VanillaEnhancement/TimeDisplayWidget.cs)
- **效果**: 屏幕顶部居中显示时间倒计时
- **白天**: `距离天黑: X:XX`（天黑前 15% 变红）
- **夜晚（月圆）**: `距月圆之夜结束还剩: X:XX`（金色，MoonPhase 0 或 4）
- **夜晚（普通）**: `距离天亮: X:XX`（淡蓝色）

### 2. R 键快速装填武器

- **文件**: [ComponentMusketAutoReload.cs](VanillaEnhancement/ComponentMusketAutoReload.cs)
- **支持武器**: 火枪、弩、弓
- **火枪**: R 键一次性消耗火药+棉花+子弹（优先级: 大子弹 > 霰弹 > 小子弹）
- **弩**: R 键分两步（拉弦 → 装螺栓），螺栓优先级: 钻石弩箭 > 铁弩箭 > 爆炸弩箭
- **弓**: R 键装填一支箭（优先级: 钻石箭 > 铁箭 > 火箭 > 铜箭 > 石箭 > 木箭）
- **冷却**: 发射后进入冷却（物品栏图标显示白色倒计时数字）
- **提示**: 缺失材料提示 `没有可用的 XX`、已装填提示 `XX 已装填`、冷却中提示 `装填冷却中！`

### 3. 装填冷却系统

| 武器 | 基础冷却 | 等级缩放 | 最低冷却 |
|------|---------|---------|---------|
| 火枪 | 2.5s | 每级 -20% | 1.0s |
| 弩 | 1.5s | 每级 -20% | 0.5s |
| 弓 | 0.8s | 每级 -20% | 0.3s |

- 冷却自动在发射后触发（Harmony 监听 `OnAim(Completed)`）
- 每个物品栏槽位独立计时（以 `(IInventory, SlotIndex)` 为键）

### 4. 右键长按吃食物

- **文件**: [ComponentEating.cs](VanillaEnhancement/ComponentEating.cs)
- **触发**: 手持食物 → 长按右键
- **动画**: 手部上下起伏 + 轻微抖动
- **音效**: 每 0.35s 播放咀嚼音，松手立即停止
- **时间**: 1.1s 起，每级 -20%，最低 0.5s（LV 4）
- **打断**: 松右键 / 换物品 / 切换槽位 → 自动取消
- **兼容**: 原版拖拽衣服界面吃食物方式保留不变

### 5. 秒杀测试矛

- **文件**: [InstantKillSpearBlock.cs](VanillaEnhancement/InstantKillSpearBlock.cs)
- **属性**: 继承 `SpearBlock`，使用铁矛纹理和模型，攻击力 9999，无投掷
- **效果**: 左键击中生物时秒杀 + 显示该生物的有效血量上限

### 6. 缺失材料提示

- **文件**: [ComponentMusketAutoReload.cs](VanillaEnhancement/ComponentMusketAutoReload.cs) `ShowMissingMessage()`
- 按 R 装填时逐项检查所需材料，首次缺失即闪烁提示 `没有可用的 XX`

### 7. 已装填状态提示

- **文件**: [ComponentMusketAutoReload.cs](VanillaEnhancement/ComponentMusketAutoReload.cs) `ShowLoadedMessage()`
- 武器已装填时按 R 显示 `XX 已装填`，名称通过 `LanguageControl.Get()` 读取原版语言文件

## 架构设计

### 组件注入 (Component + xdb)

3 个自定义 Component 通过 `VanillaEnhancementDatabase.xdb` 注册到 Player 实体：

| 组件 | LoadOrder | 功能 |
|------|-----------|------|
| ComponentTimeDisplay | 2147483647 | 注入 TimeDisplayWidget 到 GUI |
| ComponentMusketAutoReload | 2147483646 | 武器 R 键装填 + 冷却更新 |
| ComponentEating | 2147483645 | 右键长按进食状态机 |

### Harmony 补丁注入

全部补丁定义在 `VanillaEnhancementModPatches.cs`，由 `VanillaEnhancementModLoader.__ModInitialize()` 中的 `harmony.PatchAll()` 一次性注入：

| 补丁 | 目标方法 | 功能 |
|------|---------|------|
| InventorySlotCooldownOverlayPatch | InventorySlotWidget.ctor | 添加冷却数字 Label |
| InventorySlotCooldownUpdatePatch | InventorySlotWidget.Update | 更新冷却数字显示 |
| MusketFireDetectionPatch | SubsystemMusketBlockBehavior.OnAim | 火枪发射→记录冷却 |
| CrossbowFireDetectionPatch | SubsystemCrossbowBlockBehavior.OnAim | 弩发射→记录冷却 |
| BowFireDetectionPatch | SubsystemBowBlockBehavior.OnAim | 弓发射→记录冷却 |
| InstantKillSpearHitPatch | ComponentMiner.Hit | 秒杀矛命中→秒杀+显示血量 |
| RightClickEatPatch | ComponentMiner.Use | 右键手持食物→启动进食 |

### 方块注册 (csv)

秒杀矛通过 `InstantKillSpearBlocksData.csv` 注册为动态方块（Class Name 匹配 `InstantKillSpearBlock` 类）。

## 构建与安装

```powershell
dotnet build "VanillaEnhancement\VanillaEnhancement.csproj" -c Debug
```

输出: `VanillaEnhancement\bin\Debug\VanillaEnhancement.scmod`

将该文件复制到游戏 `Mods/` 目录即可。
