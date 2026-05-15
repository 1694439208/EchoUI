# 修复 Win32 Flex 布局系统隐式默认值问题

## 问题总结

布局系统的隐式默认值存在以下确认的问题：

### 已确认的问题

| # | 问题 | 当前值 | CSS Flexbox 标准 | 影响 |
|---|------|--------|-----------------|------|
| 1 | `FlexShrink` 默认 0 | Props=null → Element=0 | `flex-shrink: 1` | 子元素溢出时不收缩，直接溢出容器 |
| 2 | `AlignItems` 默认 Start | Props=null → Element=Start | `align-items: stretch` | 子元素不会自动撑满交叉轴 |
| 3 | `Direction` 默认 Vertical | Props=Vertical → Element=Vertical | `flex-direction: row` | 方向与 CSS 相反 |
| 4 | `FlexGrow` 默认 0 | Props=null → Element=0 | `flex-grow: 0` | 与 CSS 一致，但配合 shrink=0 导致完全刚性 |
| 5 | 默认值散落三层 | Props初始化 / Win32Element字段 / Renderer fallback | - | 维护困难，容易不一致 |
| 6 | Nullable Props vs Non-nullable Element | Props用nullable，Element用non-nullable | - | 用户无法从API推断实际行为 |
| 7 | `CreateInitialPatch` 默认值检测脆弱 | 用 `Activator.CreateInstance` 判断 | - | 类型变更可能导致属性被跳过 |

### 设计决策点

**问题 3（Direction 默认 Vertical）不建议改为 Horizontal。** 理由：
- EchoUI 不是 CSS 的 1:1 映射，它是独立框架
- Vertical 作为默认方向在移动端/桌面端 UI 中更常见（页面自然是纵向流）
- 大量现有 Demo 代码依赖此默认值
- 但需要在 XML 注释中明确说明

**问题 1（FlexShrink）需要特殊处理。** 虽然 CSS Flexbox 默认为 1，但 EchoUI 的 FlexLayout 中 flex-shrink 的执行时机在 overflow 判断之前：shrink 会压缩 `CachedContentHeight/Width`，导致 `Overflow.Auto/Scroll` 检测不到溢出，滚动失效。因此 EchoUI 中 FlexShrink 默认值保持 0。
用户在需要弹性收缩时可显式设置 `FlexShrink = 1`。

**问题 2（AlignItems）是真正的痛点：**
- `AlignItems = Start` 导致子元素不撑满交叉轴，用户必须显式设置 Stretch

## 修改方案（仅 Win32 层）

### 第一步：引入 `LayoutDefaults` 常量类

在 `EchoUI.Core` 中新增一个集中定义所有布局默认值的静态类，作为唯一权威来源。

**文件**: `EchoUI.Core/LayoutDefaults.cs`（新建）

```csharp
namespace EchoUI.Core;

/// <summary>
/// 布局系统的默认值定义。所有渲染器和内部模型应引用此类，而非各自硬编码。
/// 
/// 注意：EchoUI 的默认值与 CSS Flexbox 有以下刻意差异：
/// - Direction 默认 Vertical（CSS 默认 row），因为纵向流更适合通用 UI
/// - FlexGrow 默认 0（与 CSS 一致）
/// 
/// 以下默认值与 CSS Flexbox 对齐：
/// - FlexShrink 默认 1（子元素溢出时自动收缩）
/// - AlignItems 默认 Stretch（子元素撑满交叉轴）
/// </summary>
public static class LayoutDefaults
{
    public const float FlexGrow = 0f;
    public const float FlexShrink = 1f;
    public const float Gap = 0f;
    public const float BorderWidth = 0f;
    public const float BorderRadius = 0f;
    public const float FontSize = 14f;

    public static readonly LayoutDirection Direction = LayoutDirection.Vertical;
    public static readonly JustifyContent JustifyContent = JustifyContent.Start;
    public static readonly AlignItems AlignItems = AlignItems.Stretch;
    public static readonly Overflow Overflow = Overflow.Visible;
    public static readonly BorderStyle BorderStyle = BorderStyle.None;
}
```

### 第二步：修改 `Win32Element` 字段默认值

**文件**: `EchoUI.Render.Win32/Win32Element.cs`

```diff
- public LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;
- public JustifyContent JustifyContent { get; set; } = JustifyContent.Start;
- public AlignItems AlignItems { get; set; } = AlignItems.Start;
- public float FlexGrow { get; set; }
- public float FlexShrink { get; set; }
- public float Gap { get; set; }
+ public LayoutDirection Direction { get; set; } = LayoutDefaults.Direction;
+ public JustifyContent JustifyContent { get; set; } = LayoutDefaults.JustifyContent;
+ public AlignItems AlignItems { get; set; } = LayoutDefaults.AlignItems;
+ public float FlexGrow { get; set; } = LayoutDefaults.FlexGrow;
+ public float FlexShrink { get; set; } = LayoutDefaults.FlexShrink;
+ public float Gap { get; set; } = LayoutDefaults.Gap;
```

### 第三步：修改 `Win32Renderer.PatchProperties` 中的 fallback 值

**文件**: `EchoUI.Render.Win32/Win32Renderer.cs`

将 `PatchProperties` 中的硬编码 fallback 改为引用 `LayoutDefaults`：

```diff
  case ContainerProps p:
-     element.Direction = p.Direction ?? LayoutDirection.Vertical;
-     element.JustifyContent = p.JustifyContent ?? JustifyContent.Start;
-     element.AlignItems = p.AlignItems ?? AlignItems.Start;
-     element.FlexShrink = p.FlexShrink ?? 0;
-     element.FlexGrow = p.FlexGrow ?? 0;
+     element.Direction = p.Direction ?? LayoutDefaults.Direction;
+     element.JustifyContent = p.JustifyContent ?? LayoutDefaults.JustifyContent;
+     element.AlignItems = p.AlignItems ?? LayoutDefaults.AlignItems;
+     element.FlexShrink = p.FlexShrink ?? LayoutDefaults.FlexShrink;
+     element.FlexGrow = p.FlexGrow ?? LayoutDefaults.FlexGrow;
      break;
```

同样修改 `ApplyContainerProperty` 中的 fallback：

```diff
  case nameof(ContainerProps.Direction):
-     element.Direction = propValue is LayoutDirection dir ? dir : LayoutDirection.Vertical;
+     element.Direction = propValue is LayoutDirection dir ? dir : LayoutDefaults.Direction;
      break;
  case nameof(ContainerProps.JustifyContent):
-     element.JustifyContent = propValue is JustifyContent jc ? jc : JustifyContent.Start;
+     element.JustifyContent = propValue is JustifyContent jc ? jc : LayoutDefaults.JustifyContent;
      break;
  case nameof(ContainerProps.AlignItems):
-     element.AlignItems = propValue is AlignItems ai ? ai : AlignItems.Start;
+     element.AlignItems = propValue is AlignItems ai ? ai : LayoutDefaults.AlignItems;
      break;
  case nameof(ContainerProps.FlexGrow):
-     element.FlexGrow = propValue is float fg ? fg : 0;
+     element.FlexGrow = propValue is float fg ? fg : LayoutDefaults.FlexGrow;
      break;
  case nameof(ContainerProps.FlexShrink):
-     element.FlexShrink = propValue is float fs ? fs : 0;
+     element.FlexShrink = propValue is float fs ? fs : LayoutDefaults.FlexShrink;
      break;
  case nameof(ContainerProps.Gap):
-     element.Gap = propValue is float gap ? gap : 0;
+     element.Gap = propValue is float gap ? gap : LayoutDefaults.Gap;
      break;
```

### 第四步：修改 `ContainerProps` 的 XML 注释

**文件**: `EchoUI.Core/Elements.cs`

不改变 Props 的 nullable 设计（保持 API 兼容），但在 XML 注释中明确标注实际默认行为：

```diff
  /// <summary>
- /// 子元素在交叉轴上的对齐方式。
+ /// 子元素在交叉轴上的对齐方式。未设置时默认为 Stretch（子元素撑满交叉轴）。
  /// </summary>
  public AlignItems? AlignItems { get; init; }

  /// <summary>
- /// 子元素在主轴上的缩小比例。
+ /// 子元素在主轴上的缩小比例。未设置时默认为 1（允许收缩）。
  /// </summary>
  public float? FlexShrink { get; init; }

  /// <summary>
- /// 子元素在主轴上的放大比例。
+ /// 子元素在主轴上的放大比例。未设置时默认为 0（不自动增长）。
  /// </summary>
  public float? FlexGrow { get; init; }

  /// <summary>
- /// 子元素的布局方向（垂直或水平）。
+ /// 子元素的布局方向。未设置时默认为 Vertical（纵向排列）。
  /// </summary>
  public LayoutDirection? Direction { get; init; } = LayoutDirection.Vertical;

  /// <summary>
- /// 子元素在主轴上的对齐方式。
+ /// 子元素在主轴上的对齐方式。未设置时默认为 Start。
  /// </summary>
  public JustifyContent? JustifyContent { get; init; }
```

### 第五步：修复 Demo 中因默认值变更而受影响的布局

`AlignItems` 从 Start → Stretch、`FlexShrink` 从 0 → 1 会影响现有 Demo 布局。需要检查并修复：

**排查范围**：
- `EchoUI.Demo/` 下所有组件
- `EchoUI.Tools.Serial/` 下的串口工具

**修复策略**：
- 原本依赖 `AlignItems = Start` 隐式行为的容器，显式加上 `AlignItems: AlignItems.Start`
- 原本依赖 `FlexShrink = 0` 不收缩行为的元素，显式加上 `FlexShrink: 0`

### 第六步：验证

1. `dotnet build EchoUI.slnx` 编译通过
2. 运行 `EchoUI.Demo.Win32` 检查 Dashboard 布局是否正常
3. 运行 `EchoUI.Tools.Serial` 检查串口工具布局是否正常

## 风险与假设

| 项目 | 说明 |
|------|------|
| API 兼容性 | Props 类型签名不变，只改运行时默认行为。已有代码如果依赖旧默认值会布局变化 |
| Web 端不动 | 本方案只改 Win32 层。Web 端如果后续也要对齐，引用同一个 `LayoutDefaults` 即可 |
| `CreateInitialPatch` | 本方案不改此逻辑。`FlexShrink` 在 Props 中仍是 nullable，null 不会进入 patch，由 Renderer 的 fallback 兜底。这意味着 `LayoutDefaults.FlexShrink = 1` 的效果是通过 Renderer 层生效的，而非通过 patch 传递 |
| Direction 不改 | 保持 Vertical 默认，这是 EchoUI 的设计选择而非 bug |

## 变更文件清单

| 文件 | 操作 |
|------|------|
| `EchoUI.Core/LayoutDefaults.cs` | 新建 |
| `EchoUI.Core/Elements.cs` | 修改 XML 注释 |
| `EchoUI.Render.Win32/Win32Element.cs` | 修改字段默认值 |
| `EchoUI.Render.Win32/Win32Renderer.cs` | 修改 fallback 引用 |
| `EchoUI.Demo/**/*.cs` | 可能需要显式设置 AlignItems/FlexShrink |
| `EchoUI.Tools.Serial/**/*.cs` | 可能需要显式设置 AlignItems/FlexShrink |
