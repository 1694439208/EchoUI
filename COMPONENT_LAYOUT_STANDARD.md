# EchoUI 组件与布局标准

本文档基于当前 `EchoUI.Core` 组件模型和 `EchoUI.Render.Web` DOM 渲染器实现，约定组件编写、Props 设计、布局组合、Web 样式映射与常见使用模式。

## 1. 适用范围

- 适用于 EchoUI 函数组件、组合控件、业务组件和 Web 渲染目标。
- 核心布局统一使用 `Container`，文本统一使用 `Text`，单行输入统一使用 `Input` 或 `TextInput`。
- 需要访问浏览器原生标签或非内置属性时使用 `Native`。
- 本文档描述的是当前实现能力，不等同于完整 CSS/React 行为。

## 2. 组件模型标准

### 2.1 组件声明

业务组件应是纯函数风格，接收 `Props` 或自定义 Props，返回 `Element?`。

```csharp
public static Element? UserPanel(Props props)
{
    return Container([
        Text("Users")
    ]);
}
```

标准要求：

- 组件名使用 `PascalCase`。
- Props 类型命名为 `{ComponentName}Props`。
- Props 使用 `record class` 并继承 `Props`。
- 组件渲染阶段只描述 UI，不直接操作 DOM。
- 副作用放入 `Hooks.Effect`，状态放入 `Hooks.State`。
- Hook 调用顺序必须稳定，不能放在条件分支或循环中。

### 2.2 Element 工厂

可复用基础组件应定义在 `partial` 类型中，并使用 `[Element]` 生成命名参数重载。

```csharp
public record class BadgeProps : Props
{
    public string Text { get; init; } = "";
    public Color? Color { get; init; }
}

public partial class Elements
{
    [Element(DefaultProperty = nameof(BadgeProps.Text))]
    public static Element Badge(BadgeProps props)
    {
        return Container(new ContainerProps
        {
            Padding = new Spacing(Dimension.Pixels(4), Dimension.Pixels(8)),
            BorderRadius = 12,
            Children = [Text(new TextProps { Text = props.Text, Color = props.Color ?? Color.Black })]
        });
    }
}
```

调用优先使用源生成器重载：

```csharp
Badge("Active", Color: Color.Green)
```

当参数较多或需要清晰分组时，可使用对象初始化器：

```csharp
Badge(new BadgeProps
{
    Text = "Active",
    Color = Color.Green
})
```

### 2.3 Props 设计

Props 应保持不可变输入语义。

标准要求：

- 属性优先使用 `init`。
- 默认值应在 Props 中声明，避免组件内部散落魔法值。
- 事件属性命名使用 `OnXxx`。
- 回调参数应表达状态变化结果，例如 `Action<int> OnSelectionChanged`、`Action<bool> OnToggle`。
- 列表项必须提供稳定 `Key`，不要使用会变化的展示文本作为唯一身份。
- `Children` 只用于直接子元素，不要把业务数据混入 `Children`。

推荐：

```csharp
public record class CardProps : Props
{
    public string Title { get; init; } = "";
    public Color? BackgroundColor { get; init; }
    public Action<MouseButton>? OnClick { get; init; }
}
```

不推荐：

```csharp
public record class CardProps : Props
{
    public string title { get; set; }
    public Action? click { get; set; }
}
```

### 2.4 状态归属

优先把业务状态提升到上层组件，只在基础控件内部保存交互态。

适合组件内部状态：

- hover / pressed。
- 下拉是否展开。
- 临时移动索引。
- 动画中间态。

适合提升到父组件：

- 当前页面。
- 表单值。
- 选中的业务对象。
- 数据列表。
- 权限、主题、用户偏好。

示例：

```csharp
var (name, setName, _) = State("");

return Container([
    Text($"Name: {name.Value}"),
    Container(
        Height: Dimension.Pixels(36),
        BorderStyle: BorderStyle.Solid,
        BorderWidth: 1,
        BorderColor: Color.Gray,
        Children: [
            TextInput(
                Value: name.Value,
                OnValueChanged: value => setName(value)
            )
        ]
    )
]);
```

## 3. 内置元素标准

### 3.1 Container

`Container` 是唯一标准布局容器。Web 渲染器会映射为 `div`，并默认应用：

```text
display: flex
box-sizing: border-box
position: relative
flex-direction: column
justify-content: flex-start
align-items: flex-start
flex-grow: 0
flex-shrink: 0
overflow: visible
gap: 0px
margin: 0px
padding: 0px
background-color: rgba(0,0,0,0)
border-style: none
border-width: 0px
border-color: rgba(0,0,0,0)
border-radius: 0px
```

当 `Float = true` 时，Web 渲染器会把该容器改为 overlay 语义：`position: absolute`、默认 `left/top = 0`、未显式设置 `Width` 时默认 `width: 100%`，并脱离常规 flex 文档流。

`Container` 负责：

- 尺寸：`Width`、`Height`、`MinWidth`、`MaxWidth` 等。
- 间距：`Margin`、`Padding`、`Gap`。
- Flex 布局：`Direction`、`JustifyContent`、`AlignItems`、`FlexGrow`、`FlexShrink`。
- 外观：`BackgroundColor`、`BorderStyle`、`BorderColor`、`BorderWidth`、`BorderRadius`。
- 溢出：`Overflow`。
- 浮动：`Float`。
- 过渡：`Transitions`。
- 交互：鼠标与键盘事件。

标准要求：

- 所有布局分组都用 `Container` 表达。
- 根容器在 Web 页面中应显式设置宽高。
- 横向排列必须显式设置 `Direction = LayoutDirection.Horizontal`。
- 需要填充剩余空间时设置 `FlexGrow = 1`。
- 需要允许缩小时设置 `FlexShrink = 1`。
- 需要子元素填满交叉轴时显式设置 `AlignItems = AlignItems.Stretch` 或子元素 `Width/Height = Dimension.Percent(100)`，不得依赖浏览器 CSS 默认 stretch。
- 需要滚动时在滚动容器设置 `Overflow = Overflow.Auto` 或 `Overflow.Scroll`。

根布局示例：

```csharp
Container(
    Width: Dimension.Percent(100),
    Height: Dimension.ViewportHeight(100),
    Direction: LayoutDirection.Horizontal,
    Children: [
        Sidebar(),
        Container(
            FlexGrow: 1,
            FlexShrink: 1,
            Height: Dimension.Percent(100),
            AlignItems: AlignItems.Stretch,
            Overflow: Overflow.Auto,
            Children: [MainContent()]
        )
    ]
)
```

### 3.2 Text

`Text` 用于展示文本。Web 渲染器映射为 `span`，并默认应用：

```text
user-select: none
white-space: pre-wrap // NoWrap = false 时
white-space: pre      // NoWrap = true 时
pointer-events: none // 默认 MouseThrough = true 时
```

字体、字号、字重和文本色如果未显式设置，仍由宿主页面与浏览器默认值决定。

标准要求：

- 所有文本必须用 `Text` 包裹。
- 文本颜色、字号、字体粗细通过 `TextProps` 设置。
- 不要把复杂布局写进字符串，换行文本可依赖 `white-space: pre-wrap`。
- 单行文本应显式设置 `TextProps.NoWrap = true`。
- `Text.MouseThrough` 默认是 `true`，Web 会映射为 `pointer-events: none`。

示例：

```csharp
Text(
    Text: "Dashboard Overview",
    FontSize: 24,
    FontWeight: "600",
    Color: Color.FromHex("#111827")
)
```

### 3.3 Input

`Input` 是原生文本输入。Web 渲染器映射为 `input`，并默认应用：

```text
width: 100%
height: 100%
box-sizing: border-box
outline: none
appearance: none
-webkit-appearance: none
margin: 0px
padding: 0px
background-color: rgba(255,255,255,1)
color: rgba(0,0,0,1)
border-style: none
border-width: 0px
border-color: rgba(0,0,0,0)
border-radius: 0px
```

当前 Web 渲染器会明确处理：

- `Value` → DOM `value`。
- `OnValueChanged` → DOM `input` 事件。
- `BackgroundColor` → `background-color`。
- `TextColor` → `color`。
- `BorderColor` → `border-color`。
- `FocusedBorderColor` → focus/blur 时切换 `border-color`。
- `Padding` → `padding-*`。

标准要求：

- `Input` 必须作为受控输入使用：`Value` 与 `OnValueChanged` 成对出现。
- 高度、宽度和复杂边框仍建议由外层 `Container` 承担。
- 外层容器必须给定高度，否则 `input` 的 `height: 100%` 没有稳定参考。
- 需要避免依赖原生 DOM input 时，应改用 `TextInput`。

推荐：

```csharp
Container(
    Height: Dimension.Pixels(40),
    BorderStyle: BorderStyle.Solid,
    BorderWidth: 1,
    BorderColor: Color.FromHex("#d1d5db"),
    BorderRadius: 6,
    Padding: new Spacing(Dimension.Pixels(8), Dimension.Pixels(4)),
    Children: [
        Input(
            Value: value.Value,
            OnValueChanged: newValue => setValue(newValue)
        )
    ]
)
```

### 3.4 Native

`Native` 用于创建 Web 原生标签或透传属性。Web 渲染器对未知类型直接按 DOM tag 创建。

示例：

```csharp
Native(
    Type: "img",
    Properties: [
        ["src", "/img/1.jpg"],
        ["style", "width: 32px; height: 32px; border-radius: 16px"],
        ["click", (MouseButton button) => SelectImage()]
    ]
)
```

标准要求：

- 优先使用内置元素，只有浏览器专属能力才使用 `Native`。
- DOM 属性名按真实 attribute/property 传入。
- DOM 事件名按浏览器事件名传入，例如 `click`、`input`。
- `Native` 中的 `style` 是字符串属性，不参与 `ContainerProps` 的结构化样式约束。

## 4. 组合控件标准

### 4.1 Button

`Button` 是由 `Container + Text` 组合的控件，不是原生 `<button>`。

默认行为：

- 默认背景：`Color.LightGray`。
- hover 背景：`Color.Gainsboro`。
- pressed 背景：`Color.Gray`。
- 默认高度：`30px`。
- 默认宽度：按文本长度估算。
- 点击回调：`Action<MouseButton>? OnClick`。

标准要求：

- 表单或列表中应显式设置 `Width` / `Height`，避免文本长度导致尺寸不一致。
- 用 `TextColor` 控制文字颜色。
- 用 `BackgroundColor`、`HoverColor`、`PressedColor` 控制状态色。

```csharp
Button(
    Text: "Save",
    Width: Dimension.Pixels(96),
    Height: Dimension.Pixels(36),
    BackgroundColor: Color.FromHex("#4f46e5"),
    HoverColor: Color.FromHex("#4338ca"),
    PressedColor: Color.FromHex("#3730a3"),
    TextColor: Color.White,
    OnClick: _ => Save()
)
```

### 4.2 CheckBox

`CheckBox` 内部使用 `State(props.IsChecked)`，当前表现更接近非受控组件。

标准要求：

- `IsChecked` 作为初始值使用。
- 后续业务状态通过 `OnToggle` 回调同步到父组件。
- 不要依赖父组件修改 `IsChecked` 来强制重置内部状态。

注意：当前实现会在内部状态切换后调用 `OnToggle`，回调值表示切换后的状态。

### 4.3 RadioGroup

`RadioGroup` 内部使用 `State(props.SelectedIndex)`。

标准要求：

- `SelectedIndex` 作为初始选中项使用。
- 使用 `OnSelectionChanged` 接收用户选择结果。
- `Options` 文本当前会作为子元素 `Key`，同组选项文本必须唯一。
- 横向布局时每项默认按 `100 / Options.Count %` 分配宽度。

```csharp
RadioGroup(
    Options: ["Admin", "Editor", "Viewer"],
    SelectedIndex: 0,
    Direction: LayoutDirection.Horizontal,
    OnSelectionChanged: index => setRole(index)
)
```

### 4.4 ComboBox

`ComboBox` 内部保存展开状态、选中索引和移动索引。

标准要求：

- `SelectedIndex` 作为初始值使用。
- 使用 `OnSelectionChanged` 接收选择结果。
- `Options` 文本当前会作为下拉项 `Key`，同组选项文本必须唯一。
- 下拉层依赖 `Float = true` 与高度过渡，父容器不要设置会裁剪下拉层的 `Overflow.Hidden`。

### 4.5 Switch

`Switch` 内部使用 `State(props.DefaultIsOn)`。

标准要求：

- `DefaultIsOn` 作为初始值使用。
- 使用 `OnToggle` 接收切换后的新值。
- 动画计算依赖像素尺寸；若传入非像素 `Width` / `Height`，当前实现会回退到默认尺寸。

### 4.6 Tabs

`Tabs` 内部使用 `State(props.InitialIndex)`。

标准要求：

- `InitialIndex` 作为初始 Tab 使用。
- 使用 `OnTabChanged` 接收切换结果。
- `Titles` 当前会作为 header 和 panel 的 `Key`，标题必须唯一。
- `Content` 函数应返回稳定结构，避免切换时重复创建不必要状态。

### 4.7 TextInput

`TextInput` 是非原生单行输入框，由 `Container + Text` 组合实现。

当前能力：

- 受控 `Value` / `OnValueChanged`。
- `Placeholder`、`PlaceholderColor`。
- `BackgroundColor`、`TextColor`、`BorderColor`、`FocusedBorderColor`、`CaretColor`。
- `Width`、`Height`、`Padding`、`BorderRadius`。
- `FontFamily`、`FontSize`、`FontWeight`。
- 单行编辑、光标显示与闪烁、点击到字符级定位、`Backspace / Delete / ← / → / Home / End`。
- 平台 IME 组合输入。

标准要求：

- `TextInput` 必须作为受控输入使用：`Value` 与 `OnValueChanged` 成对出现。
- `TextInput` 内部文本必须保持单行，因此文本片段统一使用 `TextProps.NoWrap = true`。
- `TextInput` 必须使用 renderer 级文本测量计算可见窗口，只渲染当前可见文本片段，不应把不可见全文本直接交给 `Text` 再依赖裁剪。
- 容器仍必须使用 `Overflow.Hidden` 兜底裁剪，但不得出现换行。
- `TextInput` 依赖 `ContainerProps.OnTextInput`、`OnTextComposition`、`OnFocus`、`OnBlur`、`OnKeyDown` 等事件。

当前限制：

- 暂不支持 IME、剪贴板、文本选区和点击到字符级定位。
- 超宽内容当前改为基于 renderer 文本测量的可见窗口渲染，优先保证当前光标附近可见。
- 当前可见窗口宽度仍以组件声明宽度为基准；像素宽度最准确，百分比/auto 宽度尚未接入提交后实际布局宽度。

## 5. 布局标准

### 5.1 尺寸单位

统一使用 `Dimension` 表示尺寸。

| API | Web CSS | 使用场景 |
|---|---|---|
| `Dimension.Pixels(40)` | `40px` | 固定控件尺寸、间距、边框参考 |
| `Dimension.Percent(100)` | `100%` | 填满父容器宽高 |
| `Dimension.ViewportHeight(100)` | `100vh` | 根容器或整屏区域 |
| `Dimension.ZeroPixels` | `0px` | 折叠、空元素、过渡起点 |

标准要求：

- 根 Web 页面优先使用 `Width = 100%` 与 `Height = 100vh`。
- 百分比高度必须确保父级已有明确高度。
- 固定输入框、按钮、侧栏使用像素尺寸。
- 主内容区使用 `FlexGrow = 1` 承接剩余空间。

### 5.2 间距

统一使用 `Spacing` 表示 `Margin` 与 `Padding`。

```csharp
// 四边相同
new Spacing(Dimension.Pixels(12))

// 水平 16px，垂直 8px
new Spacing(Dimension.Pixels(16), Dimension.Pixels(8))

// 左、上、右、下
new Spacing(
    Dimension.Pixels(8),
    Dimension.Pixels(4),
    Dimension.Pixels(8),
    Dimension.Pixels(4)
)
```

标准要求：

- 同级元素间距优先用父容器 `Gap`。
- 容器内部留白使用 `Padding`。
- 元素外部位移或动画使用 `Margin`。
- 不要用空 `Container` 充当常规间距，除非用于明确占位。

### 5.3 Flex 方向

`ContainerProps.Direction` 默认是 `LayoutDirection.Vertical`。

纵向布局：

```csharp
Container(
    Direction: LayoutDirection.Vertical,
    Gap: 12,
    Children: [Header(), Body(), Footer()]
)
```

横向布局：

```csharp
Container(
    Direction: LayoutDirection.Horizontal,
    AlignItems: AlignItems.Center,
    Gap: 12,
    Children: [Icon(), Text("Label")]
)
```

标准要求：

- 横向布局必须显式声明 `Direction.Horizontal`。
- 需要主轴居中时使用 `JustifyContent.Center`。
- 需要交叉轴居中时使用 `AlignItems.Center`。
- 需要交叉轴填满时使用 `AlignItems.Stretch` 或对子元素设置 `Width/Height = Dimension.Percent(100)`。
- 列表、表单、卡片内部优先纵向布局。

### 5.4 Flex 伸缩

Web 渲染器会对 `Container` 设置默认 `flex-grow: 0` 与 `flex-shrink: 0`。因此需要主动声明伸缩策略。

常见模式：

```csharp
// 固定侧栏 + 自适应主区
Container(
    Direction: LayoutDirection.Horizontal,
    Children: [
        Container(
            Width: Dimension.Pixels(250),
            FlexShrink: 0,
            Children: [SidebarContent()]
        ),
        Container(
            FlexGrow: 1,
            FlexShrink: 1,
            AlignItems: AlignItems.Stretch,
            Overflow: Overflow.Auto,
            Children: [MainContent()]
        )
    ]
)
```

标准要求：

- 固定宽度区域设置 `FlexShrink = 0`。
- 主内容区域设置 `FlexGrow = 1`。
- `FlexGrow` 不会把基础尺寸隐式改成 0；未设置主轴尺寸时仍按内容固有尺寸作为基础尺寸。
- 横向多个卡片或表单列需要严格等分时，每个卡片/列同时设置 `Width = Dimension.ZeroPixels` 和相同 `FlexGrow`。
- 内容可能过宽时设置 `FlexShrink = 1`，避免溢出。

### 5.5 滚动与溢出

`Overflow` 会映射为 CSS `overflow`。

| EchoUI | CSS | 使用场景 |
|---|---|---|
| `Overflow.Visible` | `visible` | 下拉、浮层、自然溢出 |
| `Overflow.Hidden` | `hidden` | 裁剪内容、动画遮罩 |
| `Overflow.Scroll` | `scroll` | 始终显示滚动能力 |
| `Overflow.Auto` | `auto` | 内容超出时滚动 |

标准要求：

- 页面主内容区使用 `Overflow.Auto`。
- 动画折叠面板可使用 `Overflow.Hidden`。
- 下拉菜单祖先容器避免使用 `Overflow.Hidden`。
- 滚动容器必须有明确高度。

### 5.6 浮动层

`Float = true` 在 Web 中会强制设置：

```text
position: absolute
left: 0
top: 0
height: 0 // 当 Height 未显式设置时
min-height: 0 // 当 MinHeight 未显式设置时
width: 100% // 当 Width 未显式设置时
overflow: visible
z-index: 1000
```

标准要求：

- 下拉、弹出层、悬浮菜单使用 `Float`。
- 浮动层不占据普通布局空间，不应推动同级普通元素。
- 浮动层未设置 `Width` 时默认跟随父容器内容宽度；标准组件建议显式设置 `Width = Dimension.Percent(100)`。
- 浮动层内容应再包一层真实尺寸容器。
- 浮动层父容器应允许 `Overflow.Visible`。
- 不要用 `Float` 做普通页面布局。

推荐结构：

```csharp
Container(
    Overflow: Overflow.Visible,
    Children: [
        Trigger(),
        Container(
            Float: true,
            Width: Dimension.Percent(100),
            Children: [
                Container(
                    Width: Dimension.Percent(100),
                    BackgroundColor: Color.White,
                    Children: [DropdownItems()]
                )
            ]
        )
    ]
)
```

## 6. Web 渲染器样式映射

### 6.1 原生标签映射

| EchoUI Type | Web Tag |
|---|---|
| `EchoUI-Container` | `div` |
| `EchoUI-Text` | `span` |
| `EchoUI-Input` | `input` |
| `NativeProps.Type` | 原样作为 DOM tag |

### 6.2 ContainerProps 到 CSS

| Props | CSS |
|---|---|
| `Width` | `width` |
| `Height` | `height` |
| `MinWidth` | `min-width` |
| `MinHeight` | `min-height` |
| `MaxWidth` | `max-width` |
| `MaxHeight` | `max-height` |
| `Margin` | `margin-top/right/bottom/left` |
| `Padding` | `padding-top/right/bottom/left` |
| `Overflow` | `overflow` |
| `Direction` | `flex-direction` |
| `JustifyContent` | `justify-content` |
| `AlignItems` | `align-items` |
| `Gap` | `gap` |
| `FlexGrow` | `flex-grow` |
| `FlexShrink` | `flex-shrink` |
| `BackgroundColor` | `background-color` |
| `BorderStyle` | `border-style` |
| `BorderColor` | `border-color` |
| `BorderWidth` | `border-width` |
| `BorderRadius` | `border-radius` |
| `Transitions` | `transition` |

### 6.3 TextProps 到 CSS/DOM

| Props | Web 映射 |
|---|---|
| `Text` | `textContent` |
| `FontFamily` | `font-family` |
| `FontSize` | `font-size` |
| `FontWeight` | `font-weight` |
| `Color` | `color` |
| `MouseThrough` | `pointer-events` |
| `NoWrap` | `white-space: pre / pre-wrap` |

### 6.4 InputProps 到 CSS/DOM

| Props | Web 映射 |
|---|---|
| `Value` | `value` |
| `OnValueChanged` | `input` event |
| `BackgroundColor` | `background-color` |
| `TextColor` | `color` |
| `BorderColor` | `border-color` |
| `FocusedBorderColor` | focus / blur 时切换 `border-color` |
| `Padding` | `padding-top/right/bottom/left` |

如果未设置 `BorderColor` / `FocusedBorderColor`，Web 渲染器会保持无可见边框的 baseline；一旦显式设置任一边框色，渲染器会接管 `1px solid` 边框与焦点态颜色切换。

### 6.5 Color 映射

`Color` 会映射为：

```text
rgba(R,G,B,A/255)
```

推荐使用：

```csharp
Color.FromHex("#4f46e5")
Color.FromRgb(79, 70, 229)
Color.FromHex("#10b981").WithAlpha(25)
```

### 6.6 Transition 映射

`ContainerProps.Transitions` 会映射为 CSS `transition`。

```csharp
Transitions: [
    [nameof(ContainerProps.BackgroundColor), new Transition(200, Easing.EaseOut)]
]
```

当前属性名转换明确支持：

- `Width` → `width`。
- `Height` → `height`。
- `MinWidth` → `min-width`。
- `MinHeight` → `min-height`。
- `MaxWidth` → `max-width`。
- `MaxHeight` → `max-height`。
- `Margin` → `margin`。
- `Padding` → `padding`。
- `BackgroundColor` → `background-color`。
- `BorderColor` → `border-color`。
- `BorderWidth` → `border-width`。
- `BorderRadius` → `border-radius`。
- `Gap` → `gap`。

未列出的属性不会生成 CSS transition；Debug 构建下会输出诊断。

## 7. Web 事件标准

### 7.1 Container 事件

| Props | DOM 事件 | 回调类型 |
|---|---|---|
| `OnClick` | `click` | `Action<MouseButton>` |
| `OnMouseMove` | `mousemove` | `Action<Point>` |
| `OnMouseEnter` | `mouseenter` | `Action` |
| `OnMouseLeave` | `mouseleave` | `Action` |
| `OnMouseDown` | `mousedown` | `Action` |
| `OnMouseUp` | `mouseup` | `Action` |
| `OnKeyDown` | `keydown` | `Action<int>` |
| `OnKeyUp` | `keyup` | `Action<int>` |
| `OnTextInput` | `keypress` | `Action<string>` |
| `OnTextComposition` | 逻辑事件 | `Action<TextCompositionEvent>` |
| `InputMethodAnchorPoint` | 属性映射 | `Point?` |
| `OnFocus` | `focus` | `Action` |
| `OnBlur` | `blur` | `Action` |

标准要求：

- 点击事件使用 `_ => Handler()` 忽略鼠标按键时保持签名一致。
- 鼠标移动只用于必要交互，避免高频更新造成重复渲染。
- 绑定键盘或焦点事件的 `Container`，WebRenderer 会自动加 `tabindex=0` 以获得 DOM focus。
- `OnTextInput` 当前映射为 `keypress`，用于接收已确认字符输入。

### 7.2 Input 事件

`Input.OnValueChanged` 映射为 DOM `input` 事件。

```csharp
Input(
    Value: text.Value,
    OnValueChanged: value => setText(value)
)
```

### 7.3 Native 事件

`NativeProps.Properties` 中值为委托的属性会被视为 DOM 事件。

```csharp
Native(
    Type: "button",
    Properties: [
        ["textContent", "Native Button"],
        ["click", (MouseButton _) => Save()]
    ]
)
```

标准要求：

- 事件名使用浏览器原生事件名。
- 回调类型应使用当前 WebRenderer 支持的类型：`Action`、`Action<string>`、`Action<Point>`、`Action<MouseButton>`、`Action<int>`。

## 8. 列表与 Key 标准

协调器支持 key 化 children diff。所有动态列表都必须设置稳定且唯一的 `Key`。

推荐：

```csharp
Container(
    Children: users.Value.Select(user => UserRow(new UserRowProps
    {
        Key = user.Id,
        User = user
    })).ToList()
)
```

不推荐：

```csharp
Container(
    Children: users.Value.Select(user => UserRow(new UserRowProps
    {
        Key = user.Name,
        User = user
    })).ToList()
)
```

标准要求：

- 同级 children 的 `Key` 必须唯一。
- 不要使用数组索引作为长期 Key，除非列表永不重排、插入和删除。
- 不要使用可编辑名称、展示文案、多语言文本作为 Key。
- 内置 `ComboBox`、`RadioGroup`、`Tabs` 当前使用选项文本/标题作为 Key，传入文本必须唯一。

## 9. 页面结构标准

### 9.1 推荐分层

```text
PageRoot
├── AppShell
│   ├── Sidebar / Header
│   └── MainScrollArea
│       ├── PageHeader
│       ├── Toolbar
│       ├── ContentSection
│       └── Footer
└── FloatingLayer
```

### 9.2 标准页面骨架

```csharp
public static Element Page(Props props)
{
    return Container(
        Width: Dimension.Percent(100),
        Height: Dimension.ViewportHeight(100),
        Direction: LayoutDirection.Horizontal,
        BackgroundColor: Color.FromHex("#f3f4f6"),
        Children: [
            Sidebar(),
            Container(
                FlexGrow: 1,
                FlexShrink: 1,
                Height: Dimension.Percent(100),
                Padding: new Spacing(Dimension.Pixels(30)),
                Direction: LayoutDirection.Vertical,
                Gap: 30,
                Overflow: Overflow.Auto,
                Children: [PageContent()]
            )
        ]
    );
}
```

### 9.3 标准卡片

```csharp
public static Element Card(string title, IReadOnlyList<Element> children)
{
    return Container(
        BackgroundColor: Color.White,
        Padding: new Spacing(Dimension.Pixels(24)),
        BorderRadius: 8,
        BorderWidth: 1,
        BorderStyle: BorderStyle.Solid,
        BorderColor: Color.FromHex("#e5e7eb"),
        Direction: LayoutDirection.Vertical,
        Gap: 15,
        Children: [
            Text(title, FontSize: 18, FontWeight: "600", Color: Color.FromHex("#111827")),
            .. children
        ]
    );
}
```

### 9.4 标准表单行

```csharp
Container(
    Direction: LayoutDirection.Vertical,
    Gap: 6,
    Children: [
        Text("Name", FontSize: 13, Color: Color.FromHex("#374151")),
        Container(
            Height: Dimension.Pixels(40),
            BorderStyle: BorderStyle.Solid,
            BorderWidth: 1,
            BorderColor: Color.FromHex("#d1d5db"),
            BorderRadius: 6,
            Padding: new Spacing(Dimension.Pixels(8), Dimension.Pixels(4)),
            Children: [Input(Value: name.Value, OnValueChanged: value => setName(value))]
        )
    ]
)
```

## 10. 编写规范清单

### 10.1 必须遵守

- 使用 `Container` 表达布局，不直接依赖外部 CSS。
- 根容器显式设置宽高。
- 横向布局显式设置 `Direction.Horizontal`。
- 可滚动区域必须有明确高度和 `Overflow.Auto` / `Overflow.Scroll`。
- 动态列表必须设置唯一稳定 `Key`。
- `Input` 与 `TextInput` 必须使用受控模式。
- 下拉、浮层父级必须允许 `Overflow.Visible`。
- 组合控件的选项文本或标题不要重复。

### 10.2 推荐遵守

- 间距使用 `Gap` / `Padding`，少用空容器占位。
- 固定区域设置 `FlexShrink = 0`。
- 自适应区域设置 `FlexGrow = 1` 和必要的 `FlexShrink = 1`。
- 卡片、面板统一使用白底、圆角、浅边框。
- 颜色使用 `Color.FromHex`，避免硬编码在 `Native style` 字符串中。
- 过渡优先用于 `BackgroundColor` 和 `Margin`。

### 10.3 避免使用

- 避免在组件 render 中直接启动异步任务或操作 DOM。
- 避免将 Hook 放在条件语句、循环或早返回之后。
- 避免使用可变集合原地修改后再传回 `State`；应创建新集合实例。
- 避免把业务状态封装在不可控基础控件内部。
- 避免大量高频 `OnMouseMove` 中直接更新复杂 UI。
- 需要稳定视觉结果时，避免依赖宿主页面的默认字体、字号或文本色；应显式设置 `TextProps`。

## 11. 当前实现限制

- `Props.AreEqual` 当前尚未在 `Reconciler` 中实际用于跳过更新。
- `Effect` 当前在 render 阶段同步执行，不是 commit 后执行。
- Web 调度器当前直接执行更新，未合并到 `requestAnimationFrame`。
- `Transition` 的 C# 属性名到 CSS 属性名转换仍是白名单机制，未列出的属性不会生成 transition。
- `TextProps` 未显式设置字体、字号、字重、颜色时，Web 端仍依赖宿主页面与浏览器默认值。
- `TextInput` 已支持 IME 与字符级点击定位；当前仍缺少剪贴板、文本选区，以及 IME 候选窗对光标像素位置的精确跟随；可见窗口已基于 renderer 文本测量实现，但百分比/auto 宽度尚未接入提交后实际布局宽度。
- `CheckBox`、`ComboBox`、`RadioGroup`、`Switch`、`Tabs` 更接近非受控组件，初始 Props 后续变化不会自动同步内部状态。

## 12. 新组件验收标准

新增组件在合并前应满足：

- Props 命名、默认值、事件命名符合本文档。
- 布局只依赖 EchoUI 内置样式属性。
- 动态 children 有稳定 `Key`。
- 组件内部状态仅用于局部交互态。
- 输入类组件提供清晰的受控或非受控语义。
- Web 端所需样式和事件已在 `WebRenderer` 中映射，或通过 `Native` 明确隔离。
- 示例代码能在 Web Demo 中正常渲染和交互。
