# VanillaEnhancement - 原版内容辅助增强

## 项目介绍

### AI创作警告

- 本项目为 AI 创作，包含人工审查以及部分修改，介意者勿用。
- 本项目仅用于学习和研究，不包含任何商业用途。
- 使用agent工具：Trae CN
- 使用模型：deepseek-v4-pro

## 模组功能

本模组VanillaEnhancement为基于Survivalcraft API 1.9.0.2 创作的增强型模组，为原版游戏添加了一系列 QoL 辅助功能和测试工具。


## 目录结构

```
VanillaEnhancement/
├── VanillaEnhancement.sln                  # 解决方案文件
├── CHANGELOG.md                             # 更新日志
└── VanillaEnhancement/
    ├── modinfo.json                         # 模组元数据
    ├── VanillaEnhancement.csproj            # .NET 10 项目文件
    ├── VanillaEnhancementModLoader.cs       # 模组入口: Hook注册 + Harmony激活 + LoadSettings/SaveSettings
├── VanillaEnhancementModPatches.cs      # 全部 Harmony 补丁 (主导航按钮/冷却追踪/功能钩子)
├── TimeDisplayConfig.cs                 # 全局配置静态字段
├── VanillaEnhancementConfigScreen.cs    # 游戏内配置界面 Screen
├── ComponentTimeDisplay.cs              # 时间显示组件 (注入 GUI)
├── TimeDisplayWidget.cs                 # 昼夜/月相倒计时控件
├── ComponentMusketAutoReload.cs         # 武器 R 键装填组件 (含模组武器兼容)
├── ComponentEating.cs                   # 右键长按进食状态机
├── InstantKillSpearBlock.cs             # 秒杀测试矛 (方块注册)
└── Assets/
    ├── VanillaEnhancementDatabase.xdb   # 数据库: 组件注册
    ├── InstantKillSpearBlocksData.csv   # 方块表: 秒杀矛注册
    ├── Screens/
    │   └── VanillaEnhancementConfigScreen.xml  # 配置界面布局
    └── Lang/
        ├── zh-CN.json                   # 中文本地化
        └── en-US.json                   # 英文本地化
```

## 功能详解

### 1. 昼夜时间显示（当前时段 + 结束倒计时）

- **文件**: [TimeDisplayConfig.cs](VanillaEnhancement/TimeDisplayConfig.cs) + [ComponentTimeDisplay.cs](VanillaEnhancement/ComponentTimeDisplay.cs) + [TimeDisplayWidget.cs](VanillaEnhancement/TimeDisplayWidget.cs)
- **效果**: 屏幕左下角显示当前时间段名称 + 到该时段结束的倒计时（位置、颜色均可通过配置文件调整）
- **黎明**: `黎明 HH:MM`（暖橙色，DawnStart → DayStart）
- **白昼**: `白昼 HH:MM`（白色，DayStart → DuskStart）
- **黄昏**: `黄昏 HH:MM`（橙红色，DuskStart → NightStart）
- **夜晚（普通）**: `夜晚 HH:MM`（淡蓝色，NightStart → DawnStart）
- **夜晚（月圆）**: `月圆之夜 HH:MM`（金色，MoonPhase 0 或 4）

#### 配置系统

配置通过 SCAPI 内建 `LoadSettings`/`SaveSettings` 持久化到 `ModsSettings.xml`，游戏内配置界面即时调整即时生效，无需重启。

**配置界面入口**: 主菜单右下角 "V" 按钮，进入原版风格的 Screen 配置页面。

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `EnableTimeDisplay` | `true` | 时间显示开关 |
| `HorizontalAlignment` | `Near` | 水平位置（左上/居中/右下/拉伸铺满） |
| `VerticalAlignment` | `Far` | 垂直位置（上/中/下） |
| `DropShadow` | `true` | 文字阴影 |
| `DawnSegmentColor` | `255,200,128` | 黎明时段颜色 |
| `DaySegmentColor` | `255,255,255` | 白昼时段颜色 |
| `DuskSegmentColor` | `255,140,60` | 黄昏时段颜色 |
| `NightSegmentColor` | `140,180,255` | 夜晚时段颜色 |
| `FullMoonNightColor` | `255,210,80` | 月圆之夜时段颜色 |
| `EnableLongPressReload` | `true` | R键长按连续装填（单按不受此开关影响） |
| `EnableReloadCooldown` | `true` | 装填冷却（检测到模组武器时自动禁用并锁定） |
| `EnableModWeaponCompat` | `true` | 启用模组武器兼容（三级检测、冷却自动禁用等）；关闭后仅对原版武器有效 |
| `EnableClothingWear` | `true` | 右键自动穿衣 |
| `EnableEating` | `true` | 右键长按进食 |

### 2. R 键快速装填武器

- **文件**: [ComponentMusketAutoReload.cs](VanillaEnhancement/ComponentMusketAutoReload.cs) + [VanillaEnhancementModPatches.cs](VanillaEnhancement/VanillaEnhancementModPatches.cs)
- **支持武器**: 火枪、弩、弓（含原版子类 + 模组武器，通过三级检测自动适配）
- **优先使用官方 Behavior API**: 装填逻辑委托给武器的 `SubsystemBlockBehavior`，通过 `GetProcessInventoryItemCapacity` + `ProcessInventoryItem` 保证与游戏原版行为一致，并自动兼容有自定义 behavior 的模组武器
- **长按 R 键持续装填**: 单按 R 始终执行一次装填步骤；长按 0.5s 后启动连续装填（受 `EnableLongPressReload` 控制），每 0.04s 递进一步
- **弹药搜索顺序**: 从武器槽位右侧开始环绕搜索，同行靠右、同列靠上的弹药优先
- **创造模式优化**: 只搜索快捷栏（VisibleSlotsCount ≤ 10），避免扫描全物品目录
- **状态提示**: 缺失材料 `没有可用的 XX` / 已装填 `XX 已装填` / 冷却中 `装填冷却中！`
- **跨次按键状态记忆**: 满弹后反复按 R 始终正确提示"已装填"

### 3. 武器兼容性系统

#### 武器模式检测（三级，结果缓存）

| 级别 | 检测方式 | 覆盖 |
|------|---------|------|
| 1 | `block is MusketBlock/CrossbowBlock/BowBlock` | 原版 + 所有继承子类 |
| 2 | `behavior is SubsystemMusketBlockBehavior/...` | 任何注册了官方 behavior 的模组武器 |
| 3 | 反射：`GetLoadState+SetLoadState` / `GetDraw+SetDraw+GetArrowType+SetArrowType` | 沿袭原版方法签名的自定义武器 |

#### 弹药兼容（三级）

| 级别 | 检测方式 | 覆盖 |
|------|---------|------|
| 1 | `behavior.GetProcessInventoryItemCapacity(inv, weaponSlot, ammo)` — 官方 API | 标准 behavior 实现 |
| 2 | `ammoBlock is BulletBlock` / `ammoBlock is ArrowBlock` + `IsBoltType` | 原版继承链 |
| 3 | 遍历弹药块所有 `Get*Type(int)` 静态方法 | 完全自定义的模组弹药 |

#### 装填状态检测

- 优先：`GetBulletType(data) != null` / `GetArrowType(data) != null`
- 兜底：遍历武器类所有 `public static *Type(int)` 方法
- 持久记录：首次确认装填状态后跨按键保持

#### 模组武器自动禁用冷却

受 `EnableModWeaponCompat` 开关控制:

- **开启**（默认）: 启动时扫描 + 运行时三级检测(继承/behavior/反射), 检测到模组武器时自动禁用冷却并锁定配置按钮
- **关闭**: R 键仅对精确类型匹配的原版武器(`MusketBlock`/`CrossbowBlock`/`BowBlock`)有效, 不触发冷却禁用

检测到时同步设置 `ModWeaponsDetected` 标记, 配置界面冷却按钮变灰显示"检测到模组武器后已自动禁用"。

### 4. 装填冷却系统

| 武器 | 基础冷却 | 等级缩放 | 最低冷却 |
|------|---------|---------|---------|
| 火枪 | 2.5s | 每级 -20% | 1.0s |
| 弩 | 1.5s | 每级 -20% | 0.5s |
| 弓 | 0.8s | 每级 -20% | 0.3s |

- 通过 `MusketCooldownTracker.CooldownEnabled` 全局开关控制
- 配置项 `EnableReloadCooldown` 可手动关闭
- 检测到模组武器时自动禁用，确保公平

### 5. 右键长按吃食物

- **文件**: [ComponentEating.cs](VanillaEnhancement/ComponentEating.cs)
- **触发**: 手持食物 → 长按右键
- **动画**: 手部上下起伏 + 轻微抖动
- **音效**: 每 0.35s 播放咀嚼音，松手立即停止
- **时间**: 1.1s 起，每级 -20%，最低 0.5s（LV 4）
- **打断**: 松右键 / 换物品 / 切换槽位 → 自动取消
- **兼容**: 原版拖拽衣服界面吃食物方式保留不变

### 6. 右键自动穿衣

- **文件**: [VanillaEnhancementModPatches.cs](VanillaEnhancement/VanillaEnhancementModPatches.cs) `RightClickWearClothingPatch`
- **触发**: 手持衣物（ClothingBlock）→ 点按右键
- **逻辑**: 自动将衣物穿戴到对应的衣物槽位（头部/躯干/腿部/脚部）
- **层级检查**: 外层衣物 Layer 必须大于已穿戴衣物，否则显示"无法穿戴此衣物"
- **等级检查**: 生存模式下等级不足时提示所需等级
- **兼容**: 与右键吃食物功能通过 `HarmonyPriority` 区分优先级，衣物优先检查，互不冲突

### 7. 秒杀测试矛

- **文件**: [InstantKillSpearBlock.cs](VanillaEnhancement/InstantKillSpearBlock.cs)
- **属性**: 继承 `SpearBlock`，使用铁矛纹理和模型，攻击力 9999，无投掷
- **效果**: 左键击中生物时秒杀 + 显示该生物的有效血量上限

## 架构设计

### 组件注入 (Component + xdb)

3 个自定义 Component 通过 `VanillaEnhancementDatabase.xdb` 注册到 Player 实体：

| 组件 | LoadOrder | 功能 |
|------|-----------|------|
| ComponentTimeDisplay | 2147483647 | 注入 TimeDisplayWidget 到 GUI |
| ComponentMusketAutoReload | 2147483646 | 武器 R 键装填 + 长按持续装填 + 冷却检测 |
| ComponentEating | 2147483645 | 右键长按进食状态机 |

### Harmony 补丁注入

全部补丁定义在 `VanillaEnhancementModPatches.cs`，由 `VanillaEnhancementModLoader.__ModInitialize()` 中的 `harmony.PatchAll()` 一次性注入：

| 补丁 | 目标方法 | 功能 |
|------|---------|------|
| InventorySlotCooldownOverlayPatch | InventorySlotWidget.ctor | 添加冷却数字 Label |
| InventorySlotCooldownUpdatePatch | InventorySlotWidget.Update | 更新冷却数字显示（含模组武器） |
| MusketFireDetectionPatch | SubsystemMusketBlockBehavior.OnAim | 火枪发射→记录冷却 |
| CrossbowFireDetectionPatch | SubsystemCrossbowBlockBehavior.OnAim | 弩发射→记录冷却 |
| BowFireDetectionPatch | SubsystemBowBlockBehavior.OnAim | 弓发射→记录冷却 |
| InstantKillSpearHitPatch | ComponentMiner.Hit | 秒杀矛命中→秒杀+显示血量 |
| RightClickWearClothingPatch | ComponentMiner.Use | 右键手持衣物→自动穿戴（受 `EnableClothingWear` 控制） |
| RightClickEatPatch | ComponentMiner.Use | 右键手持食物→启动进食（受 `EnableEating` 控制） |
| MainMenuConfigButtonPatch | MainMenuScreen.Update | 主菜单右下角按钮点击→打开配置界面 |

### 配置系统

- **文件**: [TimeDisplayConfig.cs](VanillaEnhancement/TimeDisplayConfig.cs) + [VanillaEnhancementConfigScreen.cs](VanillaEnhancement/VanillaEnhancementConfigScreen.cs) + [VanillaEnhancementModLoader.cs](VanillaEnhancement/VanillaEnhancementModLoader.cs)
- **存储**: 通过 `LoadSettings`/`SaveSettings` 持久化到 `ModsSettings.xml`，API 自动调用
- **界面**: 主菜单右下角 "V" 按钮 → `ScreensManager.SwitchScreen("VanillaEnhancementConfig")` → Screen 页面即时调整即时生效
- **多语种**: 中文环境显示中文，其他语言显示英文（`Assets/Lang/zh-CN.json` + `en-US.json`）

### 方块注册 (csv)

秒杀矛通过 `InstantKillSpearBlocksData.csv` 注册为动态方块（Class Name 匹配 `InstantKillSpearBlock` 类）。

## 构建与安装

```powershell
dotnet build "VanillaEnhancement\VanillaEnhancement.csproj" -c Debug
```

输出: `VanillaEnhancement\bin\Debug\VanillaEnhancement.scmod`

将该文件复制到游戏 `Mods/` 目录即可。
