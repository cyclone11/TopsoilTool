# 表土工具（Topsoil Tool）设计方案

> 目标游戏：Captain of Industry 0.8.8（Update 4.3）
> 状态：设计 + 骨架代码；两处地形 API 需在装有游戏的机器上校准（见第 5 节）。

## 1. 需求与验收

| 项 | 内容 |
|---|---|
| 触发 | 按 `Shift + T` 激活工具 |
| 选区 | 鼠标左键拖拽框选（与游戏自带平整工具相同的矩形选区） |
| 高亮 | 合规格子显示为白色 |
| 执行 | 松开左键，立即把选区每格**最上方一层地形材料**替换为土（dirt） |
| 退出 | 执行完自动退出；右键 / Esc 取消 |

验收：
- 按 Shift+T 出现选区光标，拖拽时选区高亮白色；
- 松开后地形立刻变成土，不需要卡车/挖掘机，不产生任务；
- 选区内有海洋/地图外格子时跳过，不报错；
- 撤销：本工具 v1 不做撤销（可用游戏存档回退）。

## 2. 交互流程

```
[空闲]
  │ Shift+T（全局快捷键）
  ▼
[工具激活] ── 右键/Esc ──► [空闲]
  │ 左键按下并拖拽
  ▼
[框选中]（白色高亮，实时显示矩形）
  │ 松开左键
  ▼
[执行换土] → 写日志 → [空闲]
```

## 3. 技术方案

### 3.1 快捷键（官方文档已验证）

用官方 Key Bindings API（`Mafi.Unity.InputControl`）：

```csharp
public static readonly KbHandle ToggleTool = KbHandleBuilder
    .Start("TopsoilTool.Toggle", "表土工具")
    .SetPrimary(KbModifiers.Shift, KeyCode.T)   // Shift+T
    .Build();
```

注册：

```csharp
shortcutsRegistry.Add(mod, ToggleTool);
inputMgr.RegisterGlobalShortcut(ToggleTool, () => { ...; return true; });
```

回调里激活/关闭工具控制器。玩家可在「设置 → 控制」里改键。

### 3.2 工具与矩形选区（参考开源模组已验证）

游戏自带的 `BaseEntityCursorInputController` 是 internal，不能直接继承。CaptainOfCheats 的做法是**重写一份同结构控制器**，核心是：

```csharp
_areaTool = areaSelectionToolFactory.CreateInstance(
    OnAreaChanged,   // 拖拽中：实时选区
    OnAreaSelected,  // 松开：提交
    OnAreaCleared,
    Deactivate);
_areaTool.SetEdgeSizeLimit(new RelTile1i(200));
_areaTool.SetLeftClickColor(ColorRgba.White);   // 白色高亮
```

激活时 `_areaTool.TerrainCursor.Activate()`，输入循环里处理左键开始框选、右键取消。

### 3.3 白色高亮

`AreaSelectionTool.SetLeftClickColor(ColorRgba.White)` 会把左键拖拽的选区染成白色（游戏平整工具同款机制）。

> 限制：该 API 是**整块区域一个颜色**。如果要“只有合规格子白、不合规格子红”的逐格双色，需要自己写渲染（v1 不做）。

### 3.4 把顶层换成土

这是唯一需要现场校准的部分。已知（来自 CaptainOfCheats 0.8.7d 源码）：

```csharp
// 土是 loose product
var dirt = protosDb.GetOrThrow<LooseProductProto>(Ids.Products.Dirt);
var dirtThickness = new LooseProductQuantity(dirt, Quantity.MaxValue).ToTerrainThickness();

// 地形瓦片
tile.DepositMaterialOnTop(material, thickness,
    doNotRaiseEvents: true,
    doNotDisruptTerrain: true,
    doNotChangeHeight: true);   // 不改变高度
```

候选实现（按优先级）：

1. **油漆式**（Omni-Tool 的 “Paint Surface” 就是这个能力）：若 `TerrainManager`/`TerrainTile` 有 `SetSurfaceMaterial` / `PaintSurface` 一类方法，直接设顶层材料为土，不改厚度。
2. **移除+铺**：读顶层材料与厚度 → `RemoveMaterialFromTop(thickness)` → `DepositMaterialOnTop(dirt, thickness, doNotChangeHeight: true)`。
3. **倾倒指定**：创建 dumping designation 指向该区域再瞬间完成（依赖 `ITerrainDumpingManager`，代码量大，不推荐）。

`TerrainTopsoil.cs` 已按第 2 种写好结构，两处调用标了 `TODO(API)`。

### 3.5 合规判断

- 在地图范围内；
- 不是海洋（`TerrainManager.IsOcean(...)`，崩溃日志中出现过该 API）；
- 瓦片存在。

## 4. 项目结构

```
TopsoilTool/
├─ README.md                 本设计文档
├─ manifest.json             模组清单
├─ TopsoilTool.csproj        项目文件（net48，引用游戏 DLL）
├─ src/
│  ├─ TopsoilToolMod.cs      模组入口（IMod）
│  ├─ TopsoilKeyBindings.cs  Shift+T 快捷键
│  ├─ TopsoilToolController.cs 选区工具控制器
│  └─ TerrainTopsoil.cs      地形换土逻辑（含 TODO）
└─ tools/
   └─ Dump-Api.ps1           在你游戏机器上反射打印 API 的脚本
```

## 5. API 状态清单

| API | 来源 | 状态 |
|---|---|---|
| `KbHandleBuilder` / `IShortcutsRegistry` / `RegisterGlobalShortcut` | 官方文档 | ✅ 已验证 |
| `AreaSelectionToolFactory.CreateInstance(...)` / `SetLeftClickColor` / `SetEdgeSizeLimit` | CaptainOfCheats | ✅ 已验证 |
| `IUnityInputMgr.ActivateController/DeactivateController` | CaptainOfCheats（Deactivate 已验证） | ⚠️ Activate 待确认 |
| `TerrainManager.IsOcean(...)` / `GetHeightOrOceanSurfaceInclOutsideOfMap(...)` | 崩溃日志 | ✅ 存在，签名待确认 |
| `TerrainTile.DepositMaterialOnTop(...)` | CaptainOfCheats | ✅ 已验证 |
| `LooseProductQuantity(...).ToTerrainThickness()` | CaptainOfCheats | ✅ 已验证 |
| `TerrainManager.GetTile(Tile2i)` / 按矩形枚举瓦片 | — | ❓ 待确认 |
| 顶层材料读取 / 移除 / 直接设材料 | — | ❓ 待确认 |
| `Ids.Products.Dirt` | 推测（游戏有土这种散料） | ❓ 待确认 |

## 6. 边界情况

- 海洋、地图外：跳过并计数，日志输出；
- 选区过大（>200 格边长）：工具自身限制；
- 有建筑/树：v1 不特殊处理（只改地形材料，不动实体）；
- 游戏暂停时也立即生效；
- 多人/联机不存在（CoI 单机）。

## 7. 编译与安装

与 Extra Transports 相同：需要 .NET SDK 8 + 游戏 `Captain of Industry_Data\Managed` 目录。

```powershell
dotnet build -c Release -p:CaptainIndustryManagedPath="...\Managed"
```

产物放 `%APPDATA%\Captain of Industry\Mods\TopsoilTool\`。

## 8. 下一步

1. 把游戏 `Managed` 目录（或 `Mafi.Core.dll`、`Mafi.Unity.dll`、`Mafi.Base.dll`、`Mafi.dll`）复制到本机，我直接反射校准并编译；
2. 或在你机器上跑 `tools/Dump-Api.ps1`，把 `api-dump.txt` 发我；
3. 校准后替换 `TerrainTopsoil.cs` 的 TODO，即可出可运行版本。

## 参考

- [官方 Modding 文档](https://coigame.com/Modding)
- [Key bindings 文档](https://coigame.com/Modding/Key-Bindings)
- [CaptainOfCheats 源码（地形/工具 API 参考）](https://github.com/ArgonCrystal/CaptainOfCheats)

## 许可证

本项目采用 [MIT](LICENSE) 许可证。
