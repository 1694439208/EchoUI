# EchoUI 设计原理与实现功能梳理

本文档基于当前仓库源码梳理 EchoUI 的整体设计、核心实现、已具备功能、扩展点与当前限制。分析范围包括解决方案中的核心框架、源生成器、Web 渲染器、Win32 渲染器、Demo 宿主与串口工具。

## 1. 项目定位

EchoUI 是一个受 React 启发的 .NET 9.0 声明式 UI 框架，核心目标是用 C# 函数组件描述 UI，通过虚拟元素树和协调器进行更新，再由不同渲染后端输出到具体平台。

当前项目已经实现：

- 声明式元素模型：`Element`、`ElementType`、`Props`。
- 同步与异步函数组件：`Component`、`AsyncComponent`。
- Hooks 状态系统：`State`、`Effect`、`Memo`、`Shared`、`IsInitialRender`。
- 虚拟树协调器：挂载、组件渲染、属性 diff、children diff、卸载清理、批量更新调度。
- Roslyn 增量源生成器：为 `[Element]` 方法生成命名参数重载。
- 两个渲染后端：
  - Web：Blazor WebAssembly + `JSImport`/`JSExport` + DOM bridge。
  - Win32：原生窗口 + GDI+ 自绘 + 简化 Flexbox 布局。
- 内置元素与组合控件：`Container`、`Text`、`Input`、`Button`、`CheckBox`、`ComboBox`、`RadioGroup`、`Switch`、`Tabs`、`Native`。
- 示例与工具：Dashboard Demo、Markdown 渲染示例、Web/Win32 Demo 宿主、Win32 串口工具。

## 2. 解决方案结构

```text
EchoUI.slnx
├── EchoUI.Core/                  核心框架
│   ├── Element.cs                ElementAttribute、Element、Props、组件委托
│   ├── Elements.cs               基础原生元素与核心 Props
│   ├── Hooks.cs                  Hooks 系统
│   ├── Reconciler.cs             挂载、diff、调度、卸载
│   ├── IRenderer.cs              渲染器抽象
│   ├── Types.cs                  Color、Dimension、Spacing、Transition 等基础类型
│   └── Elements/                 Button、Input、Tabs 等组合/内置组件
├── EchoUI.Generator/             Roslyn 增量源生成器
├── EchoUI.Render.Web/            Web DOM 渲染器
├── EchoUI.Render.Win32/          Win32 GDI+ 渲染器
├── EchoUI.Demo/                  跨后端共享 Demo 组件
├── EchoUI.Demo.Web/              WebAssembly Demo 宿主
├── EchoUI.Demo.Win32/            Win32 Demo 宿主
└── EchoUI.Tools.Serial/          Win32 串口工具
```

仓库中还存在 `EchoUI.Core.Abstractions` 目录，但当前 `EchoUI.slnx` 未纳入该项目，主线实现应以 `EchoUI.Core` 为准。

## 3. 总体架构设计

EchoUI 的核心架构可以分为五层：

```text
用户组件层
  ↓
Element / Props 声明层
  ↓
Reconciler 协调层
  ↓
IRenderer 平台抽象层
  ↓
Web DOM / Win32 GDI+ 具体渲染层
```

### 3.1 声明式 UI

用户通过 C# 函数返回 `Element` 树描述界面，而不是直接操作 DOM 或 Win32 控件。

```csharp
static Element? Counter(Props props)
{
    var (count, _, updateCount) = State(0);

    return Container([
        Text($"count: {count.Value}"),
        Button("+", OnClick: _ => updateCount(v => v + 1))
    ]);
}
```

这种模式的核心思想是：

- UI 是状态的函数。
- 组件只描述“期望 UI”，具体增删改由协调器完成。
- 渲染后端只关心原生元素如何创建、更新、移动和删除。

### 3.2 元素树与实例树分离

项目中同时存在两类树：

- `Element` 树：用户组件每次 render 返回的声明式 UI 描述。
- `ComponentInstance` 树：运行时实例树，保存 hooks、children、原生元素引用、effect cleanup 等运行状态。

这种分离使框架可以反复生成新的 `Element` 树，再将其与旧实例树 diff，最终只向渲染器提交必要更新。

### 3.3 渲染后端无关

`EchoUI.Core` 不依赖 Web 或 Win32。核心只通过 `IRenderer` 与平台交互：

- `CreateElement(string type)`：创建平台原生元素。
- `PatchProperties(...)`：应用属性变化。
- `AddChild(...)` / `RemoveChild(...)` / `MoveChild(...)`：维护原生节点层级。
- `GetScheduler(...)`：获取平台更新调度器。

因此添加新平台后端时，不需要改动大多数核心组件逻辑。

## 4. 核心数据模型

### 4.1 `ElementAttribute`

`[Element]` 标记元素工厂方法，供源生成器识别并生成展开参数重载。

```csharp
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class ElementAttribute : Attribute
{
    public string? DefaultProperty { get; set; }
}
```

`DefaultProperty` 用于指定生成重载的第一个位置参数，例如 `Text("hello")`、`Button("OK")`、`Container([...])`。

### 4.2 组件委托

核心支持两类函数组件：

```csharp
public delegate Element? Component(Props props);
public delegate Task<Element?> AsyncComponent(Props props);
```

同步组件直接返回元素；异步组件返回 `Task<Element?>`，并可通过 `Props.Fallback` 表示加载期间 UI。

### 4.3 `ElementType`

`ElementType` 是一个轻量 union 包装器，用同一个类型表示：

- 原生元素：`string`，如 `"EchoUI-Container"`、`"img"`。
- 同步组件：`Component`。
- 异步组件：`AsyncComponent`。

它通过隐式转换让元素创建更简洁：

```csharp
public static implicit operator ElementType(string nativeType);
public static implicit operator ElementType(Component component);
public static implicit operator ElementType(AsyncComponent asyncComponent);
```

### 4.4 `Element`

`Element` 是 UI 的声明式描述：

```csharp
public record Element(ElementType Type, Props Props);
```

它只包含类型与属性，不保存运行时状态。

### 4.5 `Props`

`Props` 是所有属性对象的基类：

- `Key`：用于 children diff 的稳定身份。
- `Children`：子元素列表。
- `Fallback`：异步组件加载期间元素。
- `AreEqual`：用于跳过不必要重渲染的比较函数。

当前代码中 `Elements.Memo(...)` 会设置 `AreEqual`，但 `Reconciler` 尚未实际使用该字段，因此该 API 目前更像预留能力。

### 4.6 `ComponentInstance`

`ComponentInstance` 是运行时实例节点，包含：

- 当前 `Element`。
- 平台原生元素引用 `NativeElement`。
- 父子实例关系。
- Hooks 状态数组 `HookStates`。
- Effect 清理函数 `EffectCleanups`。
- 首次渲染标记 `HasCompletedInitialRender`。
- 异步组件占位标记 `IsAsyncPlaceholder`。

它承担类似简化版 Fiber 节点的角色，但没有优先级调度、中断渲染或时间切片。

### 4.7 基础类型

`Types.cs` 定义了跨后端通用的值对象：

- `Color`：RGBA 颜色，支持 `FromHex`、`FromRgb`、`WithAlpha`。
- `Dimension`：尺寸，支持 `Pixels`、`Percent`、`ViewportHeight`。
- `Spacing`：四向边距/内边距。
- `Point`：鼠标坐标。
- `Transition` / `Easing`：过渡动画描述。
- `LayoutDirection`、`JustifyContent`、`AlignItems`、`BorderStyle`、`Overflow`、`MouseButton`。
- `ValueDictionary<TKey, TValue>`：可用集合表达式构建的字典包装，常用于 `Transitions` 与 `NativeProps.Properties`。

## 5. Reconciler 实现原理

`Reconciler` 是 EchoUI 的运行时核心，负责把声明式组件转换为平台更新。

### 5.1 挂载流程

`Mount(Delegate rootComponentDelegate)` 做以下事情：

1. 判断根委托返回类型是否为 `Task`，包装为 `Component` 或 `AsyncComponent`。
2. 创建根 `ComponentInstance`。
3. 调用 `RenderComponent` 渲染根组件。
4. 为渲染结果创建子实例。
5. 调用 `MountInstance` 递归挂载元素树。

### 5.2 组件渲染与 Hook 上下文

`RenderComponent` 在调用组件前设置 `Hooks.Context`：

- 保存当前 `ComponentInstance`。
- 提供 `ScheduleUpdate` 回调。
- 重置 `HookIndex`。
- 渲染后用 `finally` 恢复旧上下文。

`Hooks.Context` 使用 `[ThreadStatic]`，意味着 Hook 必须在组件 render 调用栈内使用，且依赖当前线程上下文。

### 5.3 原生元素挂载

原生元素挂载时：

1. 调用 `_renderer.CreateElement(type)` 创建平台元素。
2. 通过 `CreateInitialPatch` 收集初始非默认属性。
3. 调用 `_renderer.PatchProperties(...)` 应用属性。
4. 找到最近的原生父容器并调用 `_renderer.AddChild(...)`。
5. 递归挂载 `Props.Children`。

组件元素不会直接创建平台节点，而是先执行组件函数，拿到组件返回的子元素后继续挂载。

### 5.4 更新调度

`State` 更新会调用 `ScheduleUpdate(instance)`：

- 用 `_dirtyComponents` 收集脏组件。
- 用 `_isUpdateQueued` 避免重复入队。
- 通过平台 `IUpdateScheduler` 执行 `ProcessUpdates`。

Web 调度器当前直接执行更新；Win32 调度器通过 `PostMessage` 将更新投递到窗口消息循环。

### 5.5 属性 diff

`DiffProps` 使用反射比较旧 `Props` 与新 `Props` 的 public 属性：

- 忽略 `Children`，children 单独 diff。
- `NativeProps.Properties` 会被展开成字典 diff。
- 委托属性只比较 null 与非 null，不比较委托实例引用。
- 非委托属性使用 `Equals` 比较。
- 变化结果写入 `PropertyPatch.UpdatedProperties`。

委托比较策略可以避免 lambda 每次重建导致频繁 patch。为了避免旧闭包，两个渲染器都在 `PatchProperties` 内始终从完整 `newProps` 同步事件处理器。

### 5.6 Children diff

`DiffChildren` 支持两种匹配策略：

- 带 `Key` 的子元素：优先通过 key 复用旧实例。
- 无 key 的子元素：按当前位置与类型匹配。

处理流程：

1. 旧 children 中带 key 的实例构建字典。
2. 遍历新 children，查找可复用旧实例。
3. 可复用则调用 `DiffInstance`。
4. 不可复用则创建新实例并 mount。
5. 未被复用的旧实例执行 `UnmountInstance`。
6. 更新实例树顺序。
7. 如果父节点是原生元素，调用 `MoveChild` 调整平台节点顺序。

该算法已经覆盖常见插入、删除、重排场景，但重复 key 会导致字典构建异常，当前没有开发期诊断。

### 5.7 类型匹配

`ElementTypesMatch` 的规则：

- 原生元素：比较原生类型字符串。
- 组件元素：比较委托方法 `Method`。

如果类型不匹配，则卸载旧实例并挂载新实例；如果类型匹配，则复用实例并 patch/update。

### 5.8 卸载清理

`UnmountInstance` 会：

- 执行当前实例所有 Effect cleanup。
- 递归卸载子实例。
- 若存在原生元素，调用 `_renderer.RemoveChild(...)`。
- 从父实例 children 中移除自己。

### 5.9 异步组件

异步组件支持：

- 如果 `Task` 已成功完成，直接使用结果。
- 如果首次渲染未完成，返回 `props.Fallback`，并在任务完成后调度更新。
- 如果已经完成过初始渲染，则 await 任务结果。

当前实现依赖 `TaskScheduler.FromCurrentSynchronizationContext()` 注册 continuation，因此 Web/Win32 宿主需要正确提供同步上下文或调度环境。

## 6. Hooks 系统

### 6.1 `State<T>`

`State` 按 Hook 调用顺序保存 `Ref<T>`：

```csharp
var (value, setValue, updateValue) = State(initialValue);
```

特点：

- 返回 `Ref<T>`，事件回调读取 `value.Value` 可以拿到最新值。
- `setValue` 直接设置新值。
- `updateValue` 基于旧值计算新值。
- 值类型用值相等判断是否更新。
- 引用类型用引用相等判断是否更新。

### 6.2 `Effect`

`Effect(Func<Action?> effectAction, object[]? deps)` 支持：

- 依赖数组比较。
- 依赖变化时执行旧 cleanup。
- 执行新的 effect 并保存 cleanup。
- 卸载时由 Reconciler 执行 cleanup。

当前 `Effect` 是在组件 render 阶段同步执行，而不是 commit 后执行，这与 React 的 effect 语义不同。

### 6.3 `Memo<T>`

`Memo` 缓存昂贵计算结果：

- deps 未变时返回旧值。
- deps 为 null 时每次重新计算。

Demo 中用它保存串口 `PortHolder` 等跨 render 对象。

### 6.4 `Shared<T>`

`Shared<T>` 从当前 `Reconciler` 的 `_sharedStates` 字典获取同类型单例状态：

- 作用域是当前 Reconciler。
- 适合简单跨组件共享对象。
- 共享对象内部变化不会自动触发 UI 更新，仍需配合 `State` 或其他调度。

### 6.5 `IsInitialRender`

通过当前 HookContext 判断组件是否首次渲染。

## 7. 源生成器设计

`EchoUI.Generator` 是 Roslyn `IIncrementalGenerator`，核心类为 `ExpandPropsGenerator`。

### 7.1 识别规则

生成器使用：

```csharp
context.SyntaxProvider.ForAttributeWithMetadataName(
    fullyQualifiedMetadataName: "EchoUI.Core.ElementAttribute",
    predicate: static (node, _) => node is MethodDeclarationSyntax,
    ...)
```

它只处理标记 `[Element]` 的方法，并要求第一个参数类型名为 `Props` 或继承链中存在名为 `Props` 的基类。

### 7.2 partial 要求

包含 `[Element]` 方法的类型必须是 `partial`。否则生成器报告诊断：

- `EG001`：容器类型必须是 partial。

### 7.3 生成规则

对于形如：

```csharp
[Element(DefaultProperty = nameof(ButtonProps.Text))]
public static Element Button(ButtonProps props)
```

生成器会枚举 `ButtonProps` 的 public 实例属性，生成类似：

```csharp
public static Element Button(string Text, Dimension? Width = null, ...)
{
    var __tmp = new ButtonProps { Text = Text, Width = Width, ... };
    return global::EchoUI.Core.Elements.Button(__tmp);
}
```

实际生成时还会：

- 将 `DefaultProperty` 对应属性放到参数列表最前。
- 根据属性初始化器推断编译期默认值或运行时默认值。
- 对可空属性生成默认 `null`。
- 对抽象/非 class Props 尝试生成本地 `PropsImpl__Method`。
- 生成文件名格式：`{ClassName}.{MethodName}.ElementOverload.g.cs`。

### 7.4 使用体验

源生成器让 API 从对象初始化器：

```csharp
Button(new ButtonProps { Text = "OK", OnClick = _ => Save() })
```

变成更接近声明式 DSL 的调用：

```csharp
Button("OK", OnClick: _ => Save())
```

Demo 中大量使用了这种重载，例如 `Container([...])`、`Text("...")`、`Switch()`、`ComboBox([...])`。

### 7.5 当前限制

当前生成器实现仍偏轻量，主要限制包括：

- `Props` 识别只看简单名，未校验完整命名空间。
- 未限制原方法必须为 static、单参数；复杂方法可能生成错误代码。
- hint name 不包含命名空间和签名，重名重载存在冲突风险。
- 对嵌套类型、泛型类型、泛型方法、约束支持不足。
- 诊断使用 `Location.None`，IDE 定位不够友好。
- 复制 `[Element]` 到生成方法上存在重复生成设计风险。
- 属性枚举对只读/init/private setter 等边界场景需要更严格验证。

## 8. 内置元素与组件能力

### 8.1 基础原生元素

#### `Container`

核心布局和样式容器，支持：

- 尺寸：`Width`、`Height`、`MinWidth`、`MaxWidth` 等。
- 间距：`Margin`、`Padding`。
- 布局：`Direction`、`JustifyContent`、`AlignItems`、`FlexGrow`、`FlexShrink`、`Gap`。
- 外观：`BackgroundColor`、`BorderStyle`、`BorderColor`、`BorderWidth`、`BorderRadius`。
- 溢出：`Overflow.Visible/Hidden/Scroll/Auto`。
- 浮动：`Float`。
- 动画描述：`Transitions`。
- 事件：点击、鼠标移动/进入/离开/按下/释放、键盘按下/抬起。

#### `Text`

文本显示元素，支持：

- `Text` 内容。
- `FontFamily`、`FontSize`、`FontWeight`。
- `Color`。
- `MouseThrough` 鼠标穿透。

#### `Input`

输入框元素，支持：

- `Value`。
- `OnValueChanged`。
- 背景色、文本色、边框色、焦点边框色、Padding。

Web 端映射为 DOM `input`；Win32 端使用原生 `EDIT` 控件。

#### `Native`

允许创建任意原生类型：

```csharp
Native(Type: "img", Properties: [
    ["src", "img/1.jpg"],
    ["style", "width:100%"]
])
```

Web 端会直接创建对应 DOM tag；Win32 端目前对 `img` 做了图片加载和绘制支持，对未知类型按容器处理。

### 8.2 组合控件

#### `Button`

由 `Container + Text` 组合实现。支持：

- 文本、宽高、点击事件。
- 背景色、Hover 色、Pressed 色、文字色、Padding、圆角。
- 内部用 `State` 管理 hover/pressed 状态。

#### `CheckBox`

由容器与文本组合绘制。支持：

- 初始选中状态。
- `OnToggle(bool)`。
- 标签文本、勾选颜色、边框颜色。

#### `ComboBox`

下拉选择框。支持：

- 选项列表。
- 初始选中索引。
- 选择变化回调。
- 背景、文字、边框、下拉背景。
- 下拉层使用 `Float`，高度变化使用 `Transitions`。

#### `RadioGroup`

单选按钮组。支持：

- 选项列表。
- 初始选中索引。
- 横向/纵向布局。
- 选中颜色、边框颜色。

#### `Switch`

开关组件。支持：

- 默认开关状态。
- 切换回调。
- 开/关背景色、滑块颜色、宽高。
- 背景与滑块 margin 过渡动画描述。

#### `Tabs`

标签页组件。支持：

- 标题列表。
- 根据索引生成内容的 `Content` 函数。
- 初始选中索引。
- 切换回调。
- active/inactive 样式。
- 内容面板宽度切换动画。

## 9. Web 渲染器

`EchoUI.Render.Web` 通过浏览器 DOM 实现 `IRenderer`。

### 9.1 DOM 映射

`WebRenderer.CreateElement` 生成 element id，并调用 JS：

- `EchoUI-Container` → `div`
- `EchoUI-Text` → `span`
- `EchoUI-Input` → `input`
- 其他类型 → 原样作为 DOM tag

JS 侧 `dom.js` 使用 `elementRegistry` 保存 `elementId -> HTMLElement` 映射。

### 9.2 属性 patch

`WebRenderer.PatchProperties` 将 EchoUI 属性转换为 `DomPropertyPatch`，序列化为 JSON 后交给 JS：

- `Styles`：CSS style。
- `Attributes`：DOM 属性或 attribute。
- `EventsToAdd` / `EventsToRemove`：事件监听变更。

样式映射包括 flexbox、尺寸、margin/padding、背景、边框、溢出、transition、文本样式、input value 等。

### 9.3 事件桥接

事件流程：

1. C# `WebRenderer` 用静态字典保存 `(elementId, eventName) -> Delegate`。
2. `DomPropertyPatch` 通知 JS 添加/移除 DOM listener。
3. JS listener 调用 `window.EchoUIHelper.RaiseEventAsync(...)`。
4. C# `[JSExport]` 转发到 `WebRenderer.RaiseEventAsync`。
5. 根据委托类型反序列化参数并调用用户回调。

支持的事件参数包括：

- `Action`。
- `Action<string>`，用于 input。
- `Action<Point>`，用于鼠标移动。
- `Action<MouseButton>`，用于点击。
- `Action<int>`，用于键盘。

### 9.4 Web 宿主启动

`EchoUI.Demo.Web` 启动流程：

1. `index.html` 提供 `<div id="app"></div>`。
2. `main.js` 加载 .NET WASM，并通过 `setModuleImports('dom', { dom })` 注册 JS bridge。
3. `Program.cs` 创建 `WebRenderer("app")` 与 `Reconciler`。
4. 挂载 `Demo.Render`。
5. `EchoUIHelper.RaiseEventAsync` 作为 JS 调 C# 的事件入口。

### 9.5 Web 后端特性与限制

特性：

- 充分复用浏览器 DOM、CSS Flexbox、CSS transition、原生 input。
- DOM 操作集中在 JS bridge，C# 只负责 diff 和 patch 描述。
- 使用 `System.Text.Json` 源生成上下文减少序列化反射成本。

限制：

- `WebUpdateScheduler` 当前直接执行更新，没有 requestAnimationFrame/microtask 聚合。
- C# 事件字典在 `RemoveChild` 时没有递归清理子树事件，长期动态增删可能泄漏委托。
- `dom.js` 当前包含较多 `console.log` 调试输出。
- CSS transition 属性名转换只显式处理少量 C# 属性名，复杂属性可能无法正确转为 CSS kebab-case。

## 10. Win32 渲染器

`EchoUI.Render.Win32` 是一个单窗口自绘渲染后端。

### 10.1 窗口与消息循环

`Win32Window` 负责：

- 注册窗口类。
- 创建 Win32 窗口。
- 运行阻塞消息循环。
- 处理 `WM_PAINT`、`WM_SIZE`、鼠标、键盘、滚轮、`WM_COMMAND`、自定义更新消息。
- 通过 `Win32SynchronizationContext` 将 async/await continuation 调回 UI 线程。

Win32 Demo 与串口工具入口都显式设置了：

```csharp
SynchronizationContext.SetSynchronizationContext(new Win32SynchronizationContext());
```

窗口创建后调用 `Win32SynchronizationContext.SetWindow(hwnd)`，使 `Post` 可以投递自定义消息。

### 10.2 原生元素模型

Win32 后端用 `Win32Element` 表示每个原生元素节点，保存：

- 元素类型。
- 子节点与父节点。
- 布局结果：相对坐标、绝对坐标、宽高。
- 样式：尺寸、间距、Flex、背景、边框、文本。
- 事件处理器。
- Input 原生 HWND、字体/画刷资源。
- 滚动偏移 `ScrollOffsetY`。
- 图片资源 `NativeImage`。

### 10.3 GDI+ 绘制

`GdiPainter` 负责绘制整棵 `Win32Element` 树：

- 双缓冲绘制。
- 背景与边框。
- 圆角矩形。
- 文本绘制。
- Input 背景/边框绘制。
- `img` 图片绘制。
- Overflow 裁剪。
- Float 顶层绘制。
- 自绘垂直滚动条。

`Input` 的文本内容不由 GDI+ 绘制，而是由嵌入的 Win32 `EDIT` 控件绘制。

### 10.4 简化 Flexbox 布局

`FlexLayout` 支持一个单行 Flexbox 子集：

- `Direction`：横向/纵向。
- `JustifyContent`。
- `AlignItems`。
- `FlexGrow` / `FlexShrink`。
- `Gap`。
- `Padding` / `Margin`。
- `Width` / `Height` / `Min` / `Max`。
- `Pixels` / `Percent` / `ViewportHeight`。
- `Float`。
- `Overflow` 与垂直滚动偏移。
- 没有显式尺寸的容器可由内容撑开。

它不是完整 CSS Flexbox：目前不支持 wrap、align-self、baseline、复杂 intrinsic sizing 等。

### 10.5 命中测试与事件分发

`HitTestManager` 负责从鼠标坐标定位元素并触发事件：

- 优先检查 Float 顶层元素。
- 再检查普通元素树。
- 遵守 `Overflow` 裁剪。
- Text 默认 `MouseThrough`。
- 从后往前遍历子元素以符合绘制层级。
- 支持鼠标进入/离开链式触发。
- 支持按下、释放、点击。
- 支持向父链查找事件处理器。
- 支持键盘事件发给当前 focused element。
- 支持滚轮查找最近 `Overflow.Auto/Scroll` 容器并更新 `ScrollOffsetY`。

### 10.6 Input 控件

Win32 `Input` 通过原生 `EDIT` 控件实现：

- 创建子窗口 HWND。
- 同步文本值。
- 处理 `EN_CHANGE` 回调到 `OnValueChanged`。
- 设置字体。
- 通过 `WM_CTLCOLOREDIT` 设置文本色与背景刷。
- 布局变化时调用 `MoveWindow` 更新位置。
- 卸载时销毁 HWND 并释放 GDI 资源。

### 10.7 Win32 后端特性与限制

特性：

- 不依赖浏览器，原生 Windows 窗口运行。
- 自绘大部分 UI，样式与布局由 EchoUI 控制。
- 支持 Native AOT 发布配置。
- 支持基本鼠标、键盘、滚动、图片、输入框。

限制：

- `Transitions` 在 Win32 中明确忽略，当前没有动画引擎。
- 每次 `WM_PAINT` 会创建整窗口 bitmap 并重新绘制，复杂界面性能可能受限。
- 每次绘制前也会计算布局，layout/paint invalidation 尚未分离。
- 原生 `EDIT` 与自绘体系混合，滚动裁剪、z-order、圆角、透明、IME 等场景仍较基础。
- 滚动目前是垂直滚动；滚动条仅绘制，不支持拖动。

## 11. Demo 与工具功能

### 11.1 当前 Demo 入口

`EchoUI.Demo.Demo.Render` 当前直接返回 `Dashboard.Create(props)`，因此 Web/Win32 Demo 默认展示 Dashboard 界面。

旧的 Tabs 示例集合仍保留在 `App.cs` 中，但已注释，不在当前入口展示。

### 11.2 Dashboard Demo

Dashboard 展示了较完整的业务 UI 场景：

- 左侧 Sidebar。
- 页面导航：Dashboard、Analytics、Settings、Documentation。
- 统计卡片：Total Users、Active Sessions、Server Load。
- 用户创建表单：Input、ComboBox、RadioGroup、Switch、Button。
- 最近用户列表。
- 系统状态卡片。
- 滚动主内容区。

该 Demo 主要用于验证布局、交互、状态更新、组合控件和跨平台渲染能力。

### 11.3 保留示例

`App.cs` 中还保留了：

- `Counter`：计数器，加减和重置。
- `InputTest`：输入框状态绑定。
- `ImageTest`：native `img` 展示与点击切换。
- `Markdown`：MarkdownRenderer 示例。
- `OtherTest`：Switch、RadioGroup、CheckBox、ComboBox、Button 集合。

### 11.4 MarkdownRenderer

`EchoUI.Demo/MarkdownRenderer.cs` 使用 Markdig 解析 Markdown AST，再转换成 EchoUI 元素。

支持：

- 文档和列表项容器。
- 标题。
- 段落。
- 有序/无序列表。
- 引用块。
- 围栏代码块。
- 水平分割线。
- 图片内联节点转 native `img`。

限制：

- 粗体、斜体等内联格式主要降级为纯文本。
- 普通链接未实现可点击交互。
- 表格、任务列表、HTML 等高级 Markdown 特性未实现。

### 11.5 Web Demo 宿主

`EchoUI.Demo.Web` 是 WASM 宿主：

- 使用 `Microsoft.NET.Sdk.WebAssembly`。
- 引用 `EchoUI.Demo` 与 `EchoUI.Render.Web`。
- 开启 trimming、InvariantGlobalization 等 WASM 相关配置。
- `index.html` 引入 vConsole 与 `main.js`。

### 11.6 Win32 Demo 宿主

`EchoUI.Demo.Win32` 是桌面宿主：

- `OutputType=WinExe`。
- `TargetFramework=net9.0-windows`。
- `PublishAot=true`。
- 创建 Win32 窗口并挂载同一套 `Demo.Render`。
- 将 `img/1.jpg`、`img/2.jpg` 复制到输出/发布目录。

### 11.7 串口工具

`EchoUI.Tools.Serial` 是一个独立 Win32 串口工具，基于 EchoUI 和 Win32 后端实现。

功能包括：

- 枚举串口并刷新。
- 配置波特率、数据位、停止位、校验位。
- 打开/关闭串口。
- 接收数据并显示。
- 支持 UTF-8 文本显示或 HEX 显示。
- 清空接收区。
- 输入发送内容。
- 支持普通文本发送或 HEX 发送。
- 串口连接/断开/发送错误写入接收区。

实现要点：

- 使用 `System.IO.Ports.SerialPort`。
- 用 `Effect` 管理串口打开、事件订阅、关闭与释放。
- `DataReceived` 在非 UI 线程触发，通过 `SynchronizationContext.Post` 切回 UI 更新状态。
- 用 `Memo` 保存 `PortHolder`，避免每次 render 丢失串口对象。

## 12. 构建与运行

常用命令：

```bash
dotnet restore EchoUI.slnx
dotnet build EchoUI.slnx

dotnet run --project EchoUI.Demo.Web/EchoUI.Demo.Web.csproj
dotnet run --project EchoUI.Demo.Win32/EchoUI.Demo.Win32.csproj
dotnet run --project EchoUI.Tools.Serial/EchoUI.Tools.Serial.csproj
```

项目目标框架与关键依赖：

| 项目 | TFM | 关键依赖/特性 |
|---|---|---|
| EchoUI.Core | net9.0 | 引用 Generator 作为 Analyzer |
| EchoUI.Generator | netstandard2.0 | Microsoft.CodeAnalysis.CSharp 4.14.0 |
| EchoUI.Render.Web | net9.0 | JS interop、unsafe |
| EchoUI.Render.Win32 | net9.0-windows | System.Drawing.Common 9.0.0、GDI+、unsafe |
| EchoUI.Demo | net9.0 | Markdig 0.41.3 |
| EchoUI.Demo.Web | net9.0 | WebAssembly SDK |
| EchoUI.Demo.Win32 | net9.0-windows | WinExe、Native AOT |
| EchoUI.Tools.Serial | net9.0-windows | System.IO.Ports、Native AOT |

## 13. 当前实现风险与技术债

### 13.1 `Elements.Memo` 语义未完整接入

`Props.AreEqual` 已定义，`Elements.Memo` 也会设置比较函数，但 `Reconciler` 当前没有读取 `AreEqual` 来跳过组件更新或原生 patch。调用方可能误以为 memo 已具备 React.memo 式优化。

### 13.2 Effect 执行时机偏早

当前 `Effect` 在 render 阶段同步执行，而非 commit 后执行。副作用如果读取 UI 状态或触发状态更新，可能产生与成熟 UI 框架不同的行为。

### 13.3 异步组件缺少错误与过期结果处理

异步组件当前支持 fallback 和完成后更新，但没有：

- 异常边界。
- cancellation。
- 过期请求结果丢弃。
- 无同步上下文时的 fallback 调度策略。

### 13.4 key 重复风险

`DiffChildren` 对旧 keyed children 使用 `ToDictionary`。如果同级元素 key 重复，会直接抛异常。部分内置组件使用选项文本作为 key，如 `ComboBox`、`RadioGroup`、`Tabs`，当选项/标题重复时存在风险。

### 13.5 受控/非受控组件边界不清晰

一些组合控件使用 props 初始值初始化内部 `State`，后续 props 变化不一定同步到内部状态，例如 `CheckBox.IsChecked`、`ComboBox.SelectedIndex`、`RadioGroup.SelectedIndex`、`Switch.DefaultIsOn`。这更接近 uncontrolled 模式，需要文档明确或补充 controlled 设计。

### 13.6 Web 事件生命周期

Web 端 C# 事件字典是静态字典，删除 DOM 节点时 JS 会从 registry 删除节点，但 C# 字典没有按元素子树清理事件委托。动态增删大量节点时可能保留过期委托和闭包。

### 13.7 Win32/Web 能力不完全一致

- Web 使用浏览器 CSS Flexbox 和 transition。
- Win32 使用自研简化 FlexLayout，且忽略 transition。
- Input 在 Web 是 DOM input，在 Win32 是原生 EDIT，细节行为难完全一致。

### 13.8 缺少自动化测试

当前仓库未见测试项目。核心 diff、hooks、源生成器、renderer 映射都适合补充单元测试或快照测试。

## 14. 后续演进建议

### 14.1 核心层

- 将 `Props.AreEqual` 接入 `Reconciler`，完善 `Elements.Memo` 语义。
- 将 `Effect` 改为 render 阶段收集、commit 后执行。
- 增加错误边界和异步组件异常处理。
- 对重复 key 给出明确诊断。
- 优化 dirty components 处理，避免父子重复更新。

### 14.2 源生成器

- 使用完整符号匹配识别 `EchoUI.Core.Props`。
- 限制或支持多参数/实例方法/泛型方法。
- hint name 加入命名空间与签名 hash。
- 完善 nested/partial/generic 类型生成。
- 提供可定位诊断。
- 增加生成器测试。

### 14.3 Web 后端

- 更新调度与 requestAnimationFrame 或 microtask 对齐。
- 清理 C# 事件字典中的已卸载元素事件。
- 完善 CSS 属性名转换。
- 减少生产环境 console 日志。
- 增加 focus、blur、wheel、composition 等事件支持。

### 14.4 Win32 后端

- 分离 layout invalidation 和 paint invalidation。
- 缓存 backbuffer，降低频繁绘制分配成本。
- 补齐 transition 动画引擎。
- 完善滚动条交互与水平滚动。
- 增强 Input 的焦点、IME、裁剪、z-order 处理。
- 评估 Direct2D/DirectWrite 替代 GDI+ 的可行性。

### 14.5 Demo 与工具

- 将 Dashboard 与旧示例统一到可导航 Demo 中。
- MarkdownRenderer 补齐粗体、斜体、链接、表格、任务列表。
- 串口工具增加接收缓冲上限、自动滚动、时间戳、日志保存、HEX 输入实时校验、CR/LF 发送选项。

## 15. 总结

EchoUI 当前已经具备一个声明式 UI 框架的核心闭环：

```text
函数组件 + Hooks
  → Element 树
  → Reconciler diff
  → PropertyPatch / child 操作
  → Web 或 Win32 渲染
```

它的设计重点是把 UI 声明、状态管理、diff 协调与平台渲染分离。`EchoUI.Core` 提供平台无关的组件模型和更新机制；`EchoUI.Generator` 提升 C# 调用体验；Web 与 Win32 后端验证了同一套组件可以输出到浏览器和原生桌面窗口。

当前实现整体清晰、轻量，适合作为 React-like .NET UI 框架的原型和实验平台。后续如果要走向更稳定的生产级框架，需要重点补齐 memo 语义、effect 生命周期、异步错误处理、renderer 能力一致性、源生成器健壮性以及自动化测试。