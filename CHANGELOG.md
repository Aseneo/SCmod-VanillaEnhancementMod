# 更新日志

## v0.0.10 — 2026-05-10

### 修复
- **弹药消耗**: 修复使用R键装填后弹药不消耗的致命BUG。`ProcessInventoryItem`只更新武器槽状态不消耗弹药，现根据`pc==0`手动调用`RemoveSlotItems`移除弹药。
- **火枪已装填误判**: 发射后`BulletType`残留导致空枪被误判为已装填，改为`LoadState==Loaded`判断。
- **模组武器空枪误判**: `IsAlreadyLoaded`新增`GetLoadState==0`检测，避免模组火枪类武器发射后误判。
- **弩拉弦误报**: `TryCrankCrossbow`改为返回`bool`并检查是否已拉满(`curDraw>=15`)，修复拉弦满后误显示"没有可用的弩箭"。
- **长按满弹后误报**: 满弹后`m_reloadTimer`从`-10f`改为`-FirstDelay`，避免刷屏。
- **配置界面按钮文字**: 水平位置按钮从"左上/居中/右下"改为"左/中/右"，垂直位置按钮从统一的"左上/居中/右下"改为"上/中/下"，各自独立显示。

### 新增
- **弹药类型记录系统** (`s_compatibleAmmo`): behavior确认兼容的弹药值记录到`Dictionary<武器类型, HashSet<弹药值>>`，支持跨次按键判断。
- **`m_foundAmmo` 字段**: 本次按键期间behavior是否确认背包中有兼容弹药，替换不可靠的`s_loadedOnce`跨按键记忆机制。
- **`HasCompatibleAmmo` 方法**: 遍历背包检查是否含有曾记录过的兼容弹药，支持多弹药类型武器。

### 修改
- **`ShowFinalStatus` 状态判断**: 从`s_loadedOnce.Contains(wc)`改为`m_didProcessThisHold || IsAlreadyLoaded || m_foundAmmo || HasCompatibleAmmo`四条件判断。
- **`TryProcessAmmo` 核心逻辑**: 比较`ProcessInventoryItem`前后slotValue变化来判断是否真正装填成功，避免behavior返回但未消费时的误判。
- **弩拉弦触发条件**: `ProcessSingleStep`中仅在`!m_foundAmmo`时尝试拉弦，有弹药时不再重复拉弦。

---

## v0.0.9 — 2026-05-10

### 新增
- **游戏内配置界面**: 参照原版设置界面风格, 主菜单右下角按钮进入独立 Screen 页面, 即时调整所有配置项, 关闭即生效。
- **多语种支持**: 中文环境显示中文界面, 其他语言显示英文; 对齐值显示”左上/居中/右下“。
- **模组武器兼容开关** (`EnableModWeaponCompat`): 开启后三级检测(类型继承/behavior绑定/反射)激活, 关闭后 R 键仅对精确类型匹配的原版武器有效。
- **独立功能开关**: 时间显示、R键长按装填、装填冷却、右键自动穿衣、右键长按进食各自可单独开关。

### 修改
- **配置存储**: 从独立 JSON 文件迁移至 SCAPI 内建 `LoadSettings`/`SaveSettings` API, 配置持久化到 `ModsSettings.xml`。
- **R键装填行为分离**: 单按 R 始终执行(不受开关影响), 长按连续装填受 `EnableLongPressReload` 控制。
- **模组武器检测**: `MarkModWeapon` 受 `EnableModWeaponCompat` 控制, 关闭时完全不触发检测和冷却禁用。
- **配置界面文字优化**: 位置对齐改为”水平位置（左/中/右）””垂直位置（上/中/下）”, 更直观。

### 修复
- `EnableAutoReload` 关闭时单按 R 也被禁用, 已分离为单按+长按独立控制。

---

## v0.0.8 — 2026-05-10

### 修改
- **模组武器冷却禁用优化**: 新增 `ScanForModWeapons()` 全局方块扫描，游戏加载时遍历 `BlocksManager.Blocks`，检测到非原版的火枪/弩/弓子类时立即禁用装填冷却。

### 修复
- `DetectPattern` 第一级分支（`block is XxxBlock`）对继承原版武器的模组子类未调用 `MarkModWeapon()`，导致 `class ModShotgun : MusketBlock` 等继承型武器不触发冷却禁用。

---

## v0.0.7 — 2026-05-10

### 修改
- **时间显示重构**: 从"距离 XX: X:XX"改为"当前时段名称 + 结束倒计时"格式（黎明/白昼/黄昏/夜晚/月圆之夜 HH:MM），每个时段颜色独立配置。
- **配置文件颜色字段**: 旧的倒计时颜色（`DuskCountdownColor` 等）替换为时段颜色（`DawnSegmentColor`、`DaySegmentColor`、`DuskSegmentColor`、`NightSegmentColor`、`FullMoonNightColor`）。
- **长按装填延迟**: 首次自动装填延迟从 0.08s 延长至 0.50s，减少误触发。
- **装填状态记忆优化**: `s_loadedOnce` 以武器类型为键记录首次成功装填状态，满弹后反复按 R 不再误报。

### 修复
- `s_loadedOnce` 以 `(wc << 16 | data)` 为键时因发射后 data 变化导致状态检测失效。
- `IsAlreadyLoaded` 在 `s_getArrowType` 命中但返回 null 时提前退出，阻断兜底扫描导致部分模组武器满弹误报。

---

## v0.0.6 — 2026-05-06~05-09

### 新增
- **长按 R 键持续装填**: 首次 0.08s 延迟后每 0.04s 递进一步,约2.5秒进行60次装填。
- **模组武器兼容系统**: 一定程度上兼容了部分模组武器的装填模式。
- **弹药兼容系统**: 优先使用官方API，兜底反射弹药块 `Get*Type` 方法签名，一定程度上兼容部分自定义模组弹药。
- **模组武器自动禁用冷却**: 检测到非原版武器时自动关闭装填冷却。
- **配置文件新增**: `EnableReloadCooldown` 开关，手动控制装填冷却启停；旧版配置文件自动补齐新字段。
- **右键自动穿衣**: 手持衣物按右键自动穿戴到对应槽位，含层级和等级检查。

### 修改
- **时间显示位置**: 从顶部居中移至左下角，支持配置文件调整。
- **弹药搜索**: 从武器槽位右侧环绕搜索，淘汰品质排序。
- **创造模式**: 弹药搜索限定快捷栏（VisibleSlotsCount），不再误扫全物品目录。
- **火枪装填状态判断**: 改用弹药类型检测（`GetBulletType != null`）替代 LoadState 枚举，兼容模组武器自定义状态枚举。
- **弩拉弦**: 分步拉弦与装箭，模组多发弩由 behavior 控制逐发装填。
- **配置文件升级**: 新增 `EnableReloadCooldown` 字段；旧文件读取后自动写回补齐。


### 修复
- 创造模式装填时误扫描全物品目录导致装填错误弹药。
- 模组武器满弹后反复提示"没有可用的 子弹"。
- `s_loadedOnce` 缓存被过早删除导致状态检测反复失效。
- `SlotRef` struct 默认值 Index=0 导致弹药搜索空引用异常。
- 配置文件新增字段未出现在旧版本生成的配置中。

---

## v0.0.5 — 2026-05（初版）

### 功能
- 昼夜/月圆时间显示（距离天黑、天亮、月圆结束倒计时）。
- R 键火枪/弩/弓快速装填，含冷却覆盖层。
- 右键长按吃食物，含手部动画和咀嚼音效。
- 秒杀测试矛（攻击力 9999，显示生物血量）。
- 武器装填冷却追踪（Harmony 监听发射事件）。
- 时间显示配置文件系统（位置、颜色可定制）。

### 文件
- `ComponentTimeDisplay.cs` + `TimeDisplayWidget.cs` — 时间显示
- `ComponentMusketAutoReload.cs` — 武器装填
- `ComponentEating.cs` — 进食状态机
- `InstantKillSpearBlock.cs` — 秒杀矛
- `VanillaEnhancementModPatches.cs` — 全部 Harmony 补丁
- `TimeDisplayConfig.cs` — 配置文件系统
- `VanillaEnhancementModLoader.cs` — 模组入口
