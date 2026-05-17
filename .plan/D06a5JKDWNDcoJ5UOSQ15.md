# EchoUI 架构演进：声明式 + 自绘引擎

## 目标架构

```
声明式组件层（Reconciler + Hooks + Element 树 — 保留不动）
    ↓ 产出 Element 树（含 LayoutProps、VisualProps）
LayoutEngine（布局计算 — 新建到 Core）
    ↓ 挂载 LayoutBox（绝对坐标 + 尺寸）
PaintEngine（生成绘制命令 — 新建到 Core）
    ↓ RenderCommand 列表（与平台无关）
CommandExecutor（Win32 薄层 — 新建）
    ↓ GDI+ / Direct2D 调用
```

## Phase 0 — 基础设施（仅 EchoUI.Core，不改现存代码）

### 0.1 新增 `LayoutBox`

```csharp
// EchoUI.Core/LayoutBox.cs
public readonly record struct LayoutBox(float X, float Y, float Width, float Height);
```

`Element` 或 `ComponentInstance` 上挂载 `LayoutBox? Layout { get; set; }`。

### 0.2 新增 `RenderCommand` 体系

```csharp
// EchoUI.Core/RenderCommand.cs
public abstract record RenderCommand(LayoutBox Layout);

public sealed record DrawRect(LayoutBox Layout, Color? BackgroundColor, float BorderRadius) : RenderCommand(Layout);
public sealed record DrawText(LayoutBox Layout, string Text, Color Color, string? FontFamily, float FontSize, string? FontWeight) : RenderCommand(Layout);
public sealed record DrawBorder(LayoutBox Layout, Color Color, float Width, float Radius, BorderStyle Style) : RenderCommand(Layout);
public sealed record DrawShadow(LayoutBox Layout, Color Color, float OffsetY) : RenderCommand(Layout);
public sealed record PushClip(LayoutBox Layout) : RenderCommand(Layout);
public sealed record PopClip : RenderCommand(LayoutBox.Zero);
```

所有命令与平台无关。阴影不再是某个组件的 hack，而是一个通用命令，任何元素都可以通过属性触发。

### 0.3 新增 `BoxShadow` 值对象

```csharp
// EchoUI.Core/BoxShadow.cs
public readonly record struct BoxShadow(Color Color, float OffsetY, float Blur = 0)
{
    public static readonly BoxShadow None = default;
}
```

后续加到 `ContainerProps` 中，替换 `ShadowColor` hack。

### 0.4 新增 `PaintEngine` 空壳

```csharp
// EchoUI.Core/PaintEngine.cs
public static class PaintEngine
{
    public static List<RenderCommand> GenerateCommands(Element root, LayoutBox? rootLayout = null)
    {
        // Phase 1 实现
        return [];
    }
}
```

目前返回空列表，旧渲染路径不受影响。

### 0.5 新建 `CommandExecutor` 目录

`EchoUI.Render.Win32/CommandExecutor/` 下建空文件 `Win32CommandExecutor.cs`，只贴 class 骨架。

## Phase 1 — Text 完整链路

### 1.1 LayoutEngine 处理 Text

将现有 `FlexLayout.MeasureText` / `GdiText.MeasureText` 逻辑封装进 LayoutEngine，产出 `LayoutBox` 挂到 Element。

### 1.2 PaintEngine 识别 Text 元素

遍历 Element 树，遇到 `ElementType == "EchoUI-Text"` 时生成 `DrawText` 命令。

### 1.3 Win32CommandExecutor 执行 DrawText

调用现有的 `GdiText.DrawText` 或 `GdiPlus.DrawString`。

### 1.4 验证

手工修改 Demo 中某个 Text 的渲染路径，切到新引擎。与旧渲染结果肉眼对比一致后合并。

## Phase 2 — Container + Flexbox 布局

### 2.1 LayoutEngine 处理 Container

将现有 `FlexLayout.Arrange` / `Measure` 逻辑提取到 LayoutEngine：
- 递归处理 Element 树
- 按 Flexbox 属性计算子元素位置
- 产出 `LayoutBox` 挂到每个子节点

### 2.2 PaintEngine 识别 Container

遇到 `Container` 时生成 `DrawRect` + `DrawBorder` 命令。

### 2.3 实现 DrawShadow 命令生成

Container 若有 `ShadowColor` 属性（后续改为 `BoxShadow`），自动插入 `DrawShadow` 命令。

## Phase 3 — 统一视觉属性

### 3.1 `ContainerProps.Shadow` 替换 `ShadowColor`

```csharp
public BoxShadow Shadow { get; init; } = BoxShadow.None;
```

### 3.2 PaintEngine 自动处理

检查每个 Element 的 `Shadow` 属性，有值则插入 `DrawShadow` 命令。

### 3.3 删除旧代码

- 从 `GdiPainter.PaintContainer` 中移除 `ShadowColor` 特殊渲染
- 从 `Win32Element` 中移除 `ShadowColor` 属性映射
- AIButton 等组件改为设置 `Shadow` 属性

## Phase 4 — 迁移现有组件

### 4.1 逐个迁移

- AIButton：从 `ShadowColor` + `BorderWidth` hack 改为 `Shadow = new BoxShadow(...)`
- AIInput / AITextInput：Shadow wrapper 模式保持或改为 BoxShadow
- AICard：border-as-shadow 改为 BoxShadow

### 4.2 Win32Renderer 减薄

移除 `PaintContainer` / `PaintText` / `PaintShadow` 中的绘制逻辑，改为调用 `Win32CommandExecutor.Execute(commands)`。

### 4.3 最终状态

```
Win32Renderer.Paint → PaintEngine.GenerateCommands → Win32CommandExecutor.Execute
```

## 执行顺序与依赖

```
Phase 0 ─────────────────────────────── (纯建文件，0 依赖)
    ↓
Phase 1 (Text 链路) ─── 依赖 Phase 0.1-0.5
    ↓
Phase 2 (Container) ─── 依赖 Phase 1（布局引擎可用后）
    ↓
Phase 3 (统一属性) ───── 依赖 Phase 2（命令体系跑通后）
    ↓
Phase 4 (组件迁移) ───── 依赖 Phase 3
```

## 验收标准

每阶段结束时：
```
dotnet build EchoUI.slnx  → 0 errors
dotnet run --project EchoUI.Demo.Win32  → 界面正常无退步
```

Phase 4 完成时：
- Win32Renderer 中 `PaintContainer` / `PaintText` 等绘制方法全部移除
- 所有视觉特性（阴影、圆角、边框）由属性驱动
- 添加新视觉特性只需加 RenderCommand + PaintEngine 处理，不碰任何组件
