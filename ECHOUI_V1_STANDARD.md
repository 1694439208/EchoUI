# EchoUI V1 标准组件、布局与动画规范

本文档定义 EchoUI V1 的平台无关、编程语言无关标准。它面向 EchoUI 核心概念、基础元素、标准组件、布局系统、事件与动画能力，用于指导不同语言绑定、不同渲染后端和业务组件库保持一致的语义。

本文档不规定 Web、DOM、CSS、Win32、GDI 或任一具体平台实现细节；具体渲染器必须把本文档中的抽象语义映射到自己的平台能力。

## 1. 规范目标

EchoUI V1 的目标是提供一组最小但完整的声明式 UI 标准：

- 用元素树描述界面，而不是直接操作平台控件。
- 用属性对象描述布局、外观、交互和子元素。
- 用统一的容器模型表达绝大多数页面布局。
- 用标准基础组件覆盖按钮、文本、输入、选择、开关、标签页等常用场景。
- 用统一的尺寸、间距、颜色、溢出、浮动和过渡模型保持跨平台一致性。
- 允许渲染器在不支持某些高级能力时进行可预测降级。

非目标：

- 不定义任何编程语言的函数签名、类型语法或源生成规则。
- 不定义具体平台标签、控件类、样式属性或消息循环。
- 不要求所有渲染器达到像素级完全一致，但要求语义一致。

## 2. 规范用语

本文档中的关键字含义如下：

- **必须**：V1 兼容实现必须满足。
- **应该**：强烈建议满足；不满足时必须有明确原因。
- **可以**：可选能力。
- **不得**：禁止行为。

核心术语：

| 术语 | 含义 |
|---|---|
| Element | 声明式 UI 节点，包含类型、属性和子元素。 |
| Props | 元素属性集合，描述输入数据、样式、布局、事件和 children。 |
| Component | 接收 Props 并返回 Element 的可复用 UI 单元。 |
| Primitive Element | EchoUI 标准基础元素，例如 Container、Text、Input。 |
| Composite Component | 由基础元素组合出的标准或业务组件，例如 Button、Tabs。 |
| Renderer | 把 EchoUI 元素树映射到具体平台的实现。 |
| Key | 同级子元素中的稳定身份，用于列表 diff 与状态保留。 |
| State | 组件内部状态，状态变化会触发界面更新。 |
| Effect | 与渲染结果相关的副作用及清理逻辑。 |
| Transition | 属性变化时的过渡动画描述。 |

## 3. EchoUI V1 设计原则

### 3.1 声明式优先

组件必须描述“期望的 UI 状态”，不得直接依赖平台命令式 API 来维持常规 UI。

标准要求：

- UI 由 Props 与 State 共同决定。
- 组件渲染过程应保持可重复执行。
- 渲染器负责把新旧元素树差异应用到平台。

### 3.2 平台无关优先

业务组件应该只依赖 EchoUI 标准元素和标准属性。

标准要求：

- 常规布局必须使用 Container。
- 常规文本必须使用 Text。
- 常规单行文本输入必须使用 Input 或 TextInput。
- 只有平台专属能力无法用标准元素表达时，才使用 Native / PlatformNative 逃生口。

### 3.3 最小核心，组合扩展

EchoUI V1 只内置少量基础元素。复杂控件应该通过组合构建。

标准要求：

- 标准组件不得要求渲染器新增专用原生控件才能工作。
- 标准组件应可由 Container、Text、Input 和标准事件系统组合实现；单行自绘输入可以由 Container 与 Text 组合为 TextInput。
- 渲染器可以对标准组件做平台优化，但优化不得改变组件语义。

### 3.4 可降级能力

不同平台能力可能不同。V1 要求降级行为可预测。

标准要求：

- 不支持动画时，属性必须立即切换到目标值。
- 不支持某类事件时，渲染器必须忽略该事件绑定，而不得破坏布局。
- 不支持 Native 时，只影响使用 Native 的组件，不得影响标准元素。

## 4. 元素与组件模型

### 4.1 Element 标准结构

一个 Element 至少包含：

| 字段 | 必须 | 含义 |
|---|---:|---|
| Type | 是 | 元素类型，可以是标准元素、组件或平台原生类型。 |
| Props | 是 | 属性集合。 |
| Key | 否 | 同级子元素稳定身份。 |
| Children | 否 | 子元素列表。 |
| Fallback | 否 | 异步组件加载期间展示的占位元素。 |

标准要求：

- Element 是声明数据，不应保存平台资源句柄。
- Element 每次渲染都可以重新创建。
- 运行时状态必须保存在组件实例或渲染器实例中，而不是保存在 Element 本身。

### 4.2 Props 标准

Props 是组件或元素的输入契约。

标准要求：

- Props 必须有明确默认值。
- Props 应被视为不可变输入；组件不得修改外部传入的 Props。
- 事件属性使用 `On` 前缀语义，例如 OnClick、OnToggle。
- 事件回调参数必须表达事件结果，而不是依赖组件内部状态。
- Children 只表示 UI 子元素，不得混入业务数据集合。

### 4.2.1 默认值与隐式语义

所有标准属性必须遵守以下默认值规则：

- 未设置的属性必须按本文档定义的默认语义处理；渲染器不得引入未记录的隐藏默认值。
- 本文档允许“平台默认”的纯视觉属性时，具体渲染器必须在自己的实现文档中声明至少以下映射：Text 默认字体/字号/字重/文本色、宿主或根表面背景色、Input 默认背景色/文本色/边框色/焦点边框色/内边距；布局、尺寸、事件和状态语义不得使用未声明的平台默认值。
- 属性从非默认值变回未设置或默认值时，渲染器必须清除或重置之前写入的平台状态，避免旧样式残留。
- `Dimension` 未设置表示 `Auto`。普通布局元素的 `Auto` 主轴尺寸以内容固有尺寸为基准；百分比尺寸在父尺寸不可计算时不得参与固有尺寸测量。
- `Spacing` 未设置表示四边均为 `0`。
- `FlexGrow` 与 `FlexShrink` 未设置表示 `0`；伸缩只分配或回收剩余空间，不改变元素的基础尺寸语义。
- `Float = true` 元素不占据常规布局空间。未设置 `Width` 时，浮动层宽度默认为父容器内容宽度；未设置 `Height` 时，浮动层高度默认为 `0`；浮动层溢出默认为可见并应绘制在同级普通内容之上。
- 需要等分剩余空间且不希望内容固有尺寸影响列宽/行高时，必须显式设置主轴尺寸为 `Dimension.ZeroPixels`，再设置相同的 `FlexGrow`。

### 4.3 Component 标准

组件是从 Props 到 Element 的映射。

标准要求：

- 组件名应使用清晰的名词或名词短语。
- 组件渲染逻辑不得依赖调用次数。
- State、Effect、Memo 等运行时能力必须按稳定顺序调用。
- 不得在条件分支、循环或早返回之后改变状态钩子的调用顺序。

### 4.4 Key 标准

Key 用于标识同级动态子元素。

标准要求：

- 动态列表中的每个同级子元素必须有唯一稳定 Key。
- Key 必须来自业务稳定身份，例如 id、路径、枚举值。
- 不得使用可编辑名称、多语言文案、随机值作为长期 Key。
- 不应该使用数组索引作为可重排列表的 Key。

## 5. V1 标准值类型

### 5.1 Color

Color 表示 RGBA 颜色。

| 字段 | 含义 |
|---|---|
| R | 红色通道，0-255。 |
| G | 绿色通道，0-255。 |
| B | 蓝色通道，0-255。 |
| A | 透明度通道，0-255。 |

标准要求：

- 渲染器必须支持不透明颜色。
- 渲染器应该支持 Alpha 透明度。
- 颜色值不得绑定到特定平台格式。

V1 标准命名颜色：White、Black、Red、Green、Blue、Gray、LightGray、Gainsboro、Transparent。

### 5.2 Dimension

Dimension 表示尺寸。

| 单位 | 含义 | 使用场景 |
|---|---|---|
| Pixels | 固定设备无关像素或平台等价长度。 | 控件高度、侧栏宽度、圆角参考。 |
| Percent | 相对父容器尺寸的百分比。 | 填满父容器、等分区域。 |
| ViewportHeight | 相对可视区域高度的百分比。 | 整屏根容器。 |
| ZeroPixels | 0 像素。 | 折叠、占位、动画起点。 |

标准要求：

- 百分比高度必须依赖父容器明确高度。
- 根容器应该显式设置宽度和高度。
- 标准组件内部动画若依赖几何计算，应优先使用 Pixels。

### 5.3 Spacing

Spacing 表示四边间距。

| 字段 | 含义 |
|---|---|
| Left | 左侧间距。 |
| Top | 顶部间距。 |
| Right | 右侧间距。 |
| Bottom | 底部间距。 |

标准要求：

- Margin 表示元素外部间距。
- Padding 表示元素内部留白。
- 同级元素间距优先使用父容器 Gap。
- 不应该用空元素模拟常规间距。

### 5.4 布局枚举

| 类型 | 取值 |
|---|---|
| LayoutDirection | Vertical、Horizontal |
| JustifyContent | Start、Center、End、SpaceBetween、SpaceAround |
| AlignItems | Start、Center、End、Stretch |
| Overflow | Visible、Hidden、Scroll、Auto |
| BorderStyle | None、Solid、Dashed、Dotted |

### 5.5 事件值类型

| 类型 | 含义 |
|---|---|
| Point | 二维坐标。 |
| MouseButton | Left、Right、Middle。 |
| KeyCode | 平台抽象按键码，V1 仅要求整数或等价枚举表达。 |

### 5.6 Transition 与 Easing

Transition 描述属性变化动画。

| 字段 | 含义 |
|---|---|
| DurationMs | 动画持续时间，单位毫秒。 |
| Easing | 缓动函数。 |

V1 标准缓动函数：Linear、Ease、EaseIn、EaseOut、EaseInOut。

## 6. 标准基础元素

### 6.1 Container

Container 是 EchoUI V1 的唯一标准布局容器。

职责：

- 组织子元素。
- 设置尺寸、间距、边框、背景、溢出和浮动。
- 承载标准鼠标和键盘事件。
- 定义子元素主轴、交叉轴和伸缩规则。
- 承载属性过渡动画。

#### 6.1.1 Container 标准属性

| 属性 | 类型 | 默认语义 |
|---|---|---|
| Width | Dimension | 自动宽度；普通元素以内容固有宽度为基准，浮动层缺省为父容器内容宽度。 |
| Height | Dimension | 自动高度；普通元素以内容固有高度为基准，浮动层缺省为 0。 |
| MinWidth / MinHeight | Dimension | 无最小约束。 |
| MaxWidth / MaxHeight | Dimension | 无最大约束。 |
| Margin | Spacing | 无外边距。 |
| Padding | Spacing | 无内边距。 |
| Direction | LayoutDirection | Vertical。 |
| JustifyContent | JustifyContent | Start。 |
| AlignItems | AlignItems | Start。 |
| FlexGrow | number | 0。 |
| FlexShrink | number | 0。 |
| Gap | number | 0。 |
| Overflow | Overflow | Visible。 |
| Float | boolean | false。 |
| BackgroundColor | Color | Transparent。 |
| BorderStyle | BorderStyle | None。 |
| BorderColor | Color | Transparent；只有显式设置颜色、BorderStyle 非 None 且 BorderWidth > 0 时才保证可见。 |
| BorderWidth | number | 0。 |
| BorderRadius | number | 0。 |
| Transitions | map<PropertyName, Transition> | 无过渡。 |
| Children | Element[] | 空列表。 |

#### 6.1.2 Container 事件

| 事件 | 参数 | 语义 |
|---|---|---|
| OnClick | MouseButton | 点击完成。 |
| OnMouseMove | Point | 指针在元素区域内移动。 |
| OnPointerDown | MouseEvent | 带坐标与按键信息的按下事件。 |
| OnPointerMove | MouseEvent | 带坐标与按键信息的移动事件。 |
| OnPointerUp | MouseEvent | 带坐标与按键信息的释放事件。 |
| OnMouseEnter | none | 指针进入元素区域。 |
| OnMouseLeave | none | 指针离开元素区域。 |
| OnMouseDown | none | 指针按下。 |
| OnMouseUp | none | 指针释放。 |
| OnKeyDown | KeyCode | 键盘按下。 |
| OnKeyUp | KeyCode | 键盘释放。 |
| OnTextInput | string | 已确认的文本输入。 |
| OnTextComposition | TextCompositionEvent | 输入法组合输入事件。 |
| OnFocus | none | 元素获得键盘焦点。 |
| OnBlur | none | 元素失去键盘焦点。 |

标准要求：

- 横向布局必须显式设置 Direction 为 Horizontal。
- 需要占满剩余空间时必须设置 FlexGrow。
- 允许内容收缩时必须设置 FlexShrink。
- `FlexGrow > 0` 且主轴尺寸未设置时，基础尺寸仍为内容固有尺寸；等分列/行必须显式设置主轴尺寸为 `Dimension.ZeroPixels`。
- 需要交叉轴填满时必须显式设置 AlignItems.Stretch 或子元素 100% 尺寸；渲染器不得依赖 Web CSS 默认 stretch。
- 可滚动区域必须同时具备明确尺寸和 Overflow Scroll/Auto。
- 绑定 `OnKeyDown`、`OnKeyUp`、`OnTextInput`、`OnTextComposition`、`OnFocus` 或 `OnBlur` 的 Container 必须可获得键盘焦点。
- Float 不得用于普通文档流布局，只用于浮层、下拉和悬浮菜单。

### 6.2 Text

Text 是 EchoUI V1 的标准文本元素。

职责：

- 展示纯文本。
- 设置字体、字号、字重和文本颜色。
- 默认不拦截鼠标事件，使父容器可统一处理点击。

标准属性：

| 属性 | 类型 | 默认语义 |
|---|---|---|
| Text | string | 空字符串。 |
| FontFamily | string | 平台默认字体。 |
| FontSize | number | 平台默认字号。 |
| FontWeight | string/number | 平台默认字重。 |
| Color | Color | 平台默认文本色。 |
| MouseThrough | boolean | true。 |
| NoWrap | boolean | false；允许按平台默认文本换行语义布局。 |

标准要求：

- 所有常规文本必须使用 Text。
- Text 不应该承载复杂布局。
- 多段内容应该由多个 Text 或 Container 组合表达。
- 单行文本场景应该显式设置 `NoWrap = true`。

### 6.3 Input

Input 是 EchoUI V1 的标准原生单行文本输入元素。

职责：

- 展示当前文本值。
- 接收用户文本输入。
- 通过变更事件通知外部。

标准属性：

| 属性 | 类型 | 默认语义 |
|---|---|---|
| Value | string | 空字符串。 |
| OnValueChanged | callback<string> | 文本变化回调。 |
| BackgroundColor | Color | White。 |
| TextColor | Color | Black。 |
| BorderColor | Color | Transparent。 |
| FocusedBorderColor | Color | Transparent。 |
| Padding | Spacing | 0。 |

标准要求：

- Input 在 V1 中必须按受控输入使用：Value 与 OnValueChanged 应成对出现。
- 用户输入不得直接修改业务状态；必须通过 OnValueChanged 交给上层更新。
- 输入框外部布局、尺寸和复杂边框可以由外层 Container 承担。
- 需要完全由标准元素组合、避免依赖平台原生输入控件时，应该使用 TextInput。

### 6.4 Native / PlatformNative

Native 是平台能力逃生口，不属于可移植 UI 的首选路径。

职责：

- 创建标准元素无法表达的平台原生节点或能力。
- 透传平台特定属性和事件。

标准要求：

- 标准组件不得依赖 Native 才能完成基本交互。
- 使用 Native 的业务组件必须声明其平台依赖。
- 渲染器可以不支持 Native；不支持时必须明确降级或报出可诊断错误。

## 7. V1 标准组合组件

### 7.1 Button

Button 是标准按钮组件。

行为：

- 展示文本。
- 响应点击。
- 可表达 hover 与 pressed 交互态。
- 默认由 Container 与 Text 组合实现。

标准属性：

| 属性 | 类型 | 默认语义 |
|---|---|---|
| Text | string | 按钮文本。 |
| Width | Dimension | 可由文本估算或平台自适应。 |
| Height | Dimension | 约 30px 或平台等价高度。 |
| OnClick | callback<MouseButton> | 点击回调。 |
| BackgroundColor | Color | LightGray。 |
| HoverColor | Color | Gainsboro。 |
| PressedColor | Color | Gray。 |
| TextColor | Color | Black。 |
| Padding | Spacing | 水平 8、垂直 4 的等价间距。 |
| BorderRadius | number | 4 的等价圆角。 |

标准要求：

- Button 不得把业务状态保存在内部。
- Button 内部只能保存 hover、pressed 等局部交互态。
- 同一按钮组中按钮应该显式设置统一尺寸。

### 7.2 CheckBox

CheckBox 是标准复选框组件。

行为：

- 显示选中或未选中状态。
- 可显示文本标签。
- 点击后切换内部状态并发出 OnToggle。

标准属性：

| 属性 | 类型 | 默认语义 |
|---|---|---|
| IsChecked | boolean | 初始选中状态。 |
| OnToggle | callback<boolean> | 切换后的状态。 |
| Label | string | 标签文本。 |
| CheckColor | Color | Black。 |
| BorderColor | Color | Gray。 |

V1 状态语义：

- CheckBox 在 V1 中是非受控选择组件。
- IsChecked 表示初始值，不保证后续 Props 更新会覆盖内部状态。
- 业务状态必须通过 OnToggle 同步到上层。

### 7.3 RadioGroup

RadioGroup 是标准单选组组件。

行为：

- 展示一组选项。
- 同一时间只允许一个选项被选中。
- 选中项变化时发出 OnSelectionChanged。

标准属性：

| 属性 | 类型 | 默认语义 |
|---|---|---|
| Options | string[] | 选项文本列表。 |
| SelectedIndex | number | 初始选中索引。 |
| OnSelectionChanged | callback<number> | 新选中索引。 |
| Direction | LayoutDirection | Horizontal。 |
| SelectedColor | Color | Black。 |
| BorderColor | Color | Gray。 |

V1 状态语义：

- RadioGroup 在 V1 中是非受控选择组件。
- SelectedIndex 表示初始索引。
- Options 文本在 V1 中必须在同组选项内唯一。

### 7.4 ComboBox

ComboBox 是标准下拉选择组件。

行为：

- 展示当前选中项。
- 点击后打开或关闭下拉列表。
- 选择项后关闭下拉并发出 OnSelectionChanged。
- 下拉列表使用浮动层语义。

标准属性：

| 属性 | 类型 | 默认语义 |
|---|---|---|
| Options | string[] | 可选项文本列表。 |
| SelectedIndex | number | 初始选中索引。 |
| OnSelectionChanged | callback<number> | 新选中索引。 |
| BackgroundColor | Color | White。 |
| TextColor | Color | Black。 |
| BorderColor | Color | Gray。 |
| DropdownBackgroundColor | Color | White。 |

V1 状态语义：

- ComboBox 在 V1 中是非受控选择组件。
- SelectedIndex 表示初始索引。
- Options 文本在 V1 中必须在同组选项内唯一。
- 下拉浮动层默认宽度必须等于 ComboBox 触发框所在父容器内容宽度；标准组件实现应该显式设置浮动层 `Width = 100%`。
- ComboBox 的祖先容器不应该裁剪浮动层。

### 7.5 Switch

Switch 是标准开关组件。

行为：

- 展示开 / 关状态。
- 点击后切换状态并发出 OnToggle。
- 轨道背景与滑块位置可以使用过渡动画。

标准属性：

| 属性 | 类型 | 默认语义 |
|---|---|---|
| DefaultIsOn | boolean | 初始开关状态。 |
| OnToggle | callback<boolean> | 切换后的状态。 |
| OnColor | Color | Green。 |
| OffColor | Color | LightGray。 |
| ThumbColor | Color | White。 |
| Width | Dimension | 约 50px。 |
| Height | Dimension | 约 26px。 |
| AnimationDuration | number | 150ms。 |

V1 状态语义：

- Switch 在 V1 中是非受控选择组件。
- DefaultIsOn 表示初始值。
- 需要精确滑块动画时，Width 与 Height 应使用 Pixels。

### 7.6 Tabs

Tabs 是标准标签页组件。

行为：

- 展示标签头列表。
- 点击标签头后切换当前内容。
- 可使用宽度过渡表达内容切换动画。

标准属性：

| 属性 | 类型 | 默认语义 |
|---|---|---|
| Titles | string[] | 标签标题列表。 |
| Content | function(index) -> Element | 根据索引生成内容。 |
| InitialIndex | number | 初始选中标签。 |
| OnTabChanged | callback<number> | 新选中标签索引。 |
| ActiveTabBackgroundColor | Color | Gainsboro。 |
| InactiveTabBackgroundColor | Color | 透明或平台默认。 |
| ActiveTabTextColor | Color | Black。 |
| InactiveTabTextColor | Color | Gray。 |
| AnimationDuration | number | 250ms。 |

V1 状态语义：

- Tabs 在 V1 中是非受控选择组件。
- InitialIndex 表示初始标签索引。
- Titles 在 V1 中必须唯一。
- Content 应返回稳定结构，避免切换标签时意外丢失业务状态。

### 7.7 TextInput

TextInput 是标准的非原生单行文本输入组件。

行为：

- 展示当前文本值或占位文本。
- 点击后获得焦点并显示插入光标。
- 焦点期间插入光标应可闪烁；用户输入、点击或移动光标后应重置闪烁周期。
- 接收文本输入并通过变更事件通知外部。
- 支持单行编辑、左右移动、Home、End、Backspace 与 Delete。
- 支持鼠标拖拽框选文本、右键菜单、复制、剪切、粘贴与全选。
- 支持通过 `OnTextComposition` 表达平台 IME 的 Start / Update / Commit / End 生命周期。
- 默认由 Container、Text 与标准焦点/键盘/文本组合输入事件组合实现。

标准属性：

| 属性 | 类型 | 默认语义 |
|---|---|---|
| Value | string | 空字符串。 |
| Placeholder | string | 空字符串。 |
| OnValueChanged | callback<string> | 文本变化回调。 |
| Width | Dimension | 约 200px。 |
| Height | Dimension | 约 36px。 |
| BackgroundColor | Color | White。 |
| TextColor | Color | Black。 |
| PlaceholderColor | Color | Gray。 |
| BorderColor | Color | `#d1d5db` 等价浅边框色。 |
| FocusedBorderColor | Color | `#2563eb` 等价焦点边框色。 |
| CaretColor | Color | 跟随文本色。 |
| Padding | Spacing | 水平 10、垂直 6 的等价值。 |
| BorderRadius | number | 4。 |
| FontFamily | string | 跟随 Text 平台默认字体。 |
| FontSize | number | 跟随 Text 平台默认字号。 |
| FontWeight | string/number | 跟随 Text 平台默认字重。 |

标准要求：

- TextInput 在 V1 中必须按受控输入使用：Value 与 OnValueChanged 应成对出现。
- TextInput 必须保持单行，不得因内容超宽而自动换行。
- TextInput 的文本片段必须使用 `Text(NoWrap = true)` 或等价单行文本语义。
- 渲染器必须提供文本测量或等价能力，用于计算当前可见文本窗口与光标位置。
- 渲染器必须在内容超出宽度时裁剪、滚动或仅渲染可见片段，但不得改变单行语义。
- 基础可编辑键包括字符输入、Backspace、Delete、Left、Right、Home、End。
- V1 不强制 TextInput 支持 IME、剪贴板、文本选区、点击到字符级定位或多行编辑。

## 8. V1 布局标准

### 8.1 容器盒模型

Container 的布局由外到内分为：

```text
Margin
  Border
    Padding
      Content / Children
```

标准要求：

- Width 与 Height 描述 Border 外沿以内的元素尺寸，具体盒模型映射由渲染器决定，但必须保持可预测一致。
- Padding 影响子元素布局区域。
- Margin 影响元素在父容器中的占位。
- BorderRadius 只影响视觉圆角，不应改变布局尺寸。

### 8.2 主轴与交叉轴

Direction 决定主轴：

| Direction | 主轴 | 交叉轴 |
|---|---|---|
| Vertical | 从上到下 | 从左到右 |
| Horizontal | 从左到右 | 从上到下 |

标准要求：

- JustifyContent 控制主轴排列。
- AlignItems 控制交叉轴排列。
- Gap 作用于同级子元素之间的主轴间距。

### 8.3 伸缩规则

V1 采用简化 Flex 语义。

标准要求：

- FlexGrow 默认为 0，元素默认不主动占用剩余空间。
- FlexShrink 默认为 0，元素默认不主动缩小。
- 主轴尺寸未设置时，伸缩基础尺寸为内容固有尺寸；`FlexGrow` 不等同于 `flex-basis: 0`。
- 固定尺寸区域应该设置 FlexShrink 为 0。
- 自适应主内容区应该设置 FlexGrow 为 1。
- 横向布局中可能被挤压的区域应该设置 FlexShrink 为 1。
- 多个区域需要严格等分时，横向布局必须给每项设置 `Width = Dimension.ZeroPixels`，纵向布局必须给每项设置 `Height = Dimension.ZeroPixels`，并设置相同 `FlexGrow`。

### 8.4 根布局

标准页面根容器应该满足：

- 明确宽度。
- 明确高度。
- 明确主布局方向。
- 明确背景色或继承策略。

推荐结构：

```text
PageRoot
  Direction: Horizontal
  Width: 100%
  Height: 100 viewport height

  Sidebar
    Width: fixed pixels
    FlexShrink: 0

  MainArea
    FlexGrow: 1
    FlexShrink: 1
    Overflow: Auto
```

### 8.5 滚动与溢出

Overflow 标准语义：

| 值 | 语义 |
|---|---|
| Visible | 内容可以超出容器可见区域。 |
| Hidden | 超出部分被裁剪。 |
| Scroll | 容器具备滚动能力。 |
| Auto | 内容超出时具备滚动能力。 |

标准要求：

- 滚动容器必须有明确高度或可计算高度。
- 页面主内容区应该使用 Auto。
- 折叠动画容器可以使用 Hidden。
- 浮动层祖先不应该使用 Hidden 裁剪。

### 8.6 浮动层

Float 表示元素不占据常规布局空间，用于下拉菜单、浮层、悬浮菜单。

标准要求：

- Float 元素必须依附于一个常规布局父容器。
- Float 元素不得用于普通排版。
- Float 元素不参与父容器主轴空间计算，也不得推动同级普通元素。
- Float 元素未设置 Width 时，宽度默认为父容器内容宽度；未设置 Height 时，高度默认为 0。
- Float 元素的 Overflow 默认按 Visible 处理，即使外层用于占位的高度为 0，内部真实尺寸容器仍可显示。
- Float 元素视觉层级应该高于同级普通内容。
- Float 元素内部应该再包含一个具有真实尺寸的内容容器。

推荐结构：

```text
Wrapper
  Overflow: Visible

  Trigger

  FloatingLayer
    Float: true
    Width: 100%

    Surface
      Width: 100%
      BackgroundColor: White
      Children: menu items
```

## 9. V1 动画与过渡标准

### 9.1 动画模型

EchoUI V1 使用属性过渡模型，而不是关键帧动画模型。

一个过渡项由以下内容定义：

```text
PropertyName -> Transition(DurationMs, Easing)
```

标准要求：

- 动画必须由状态变化或 Props 变化触发。
- 动画不得要求组件直接控制每一帧。
- 渲染器不支持动画时，必须立即应用目标属性值。
- 过渡结束后，元素最终状态必须等于目标 Props。

### 9.2 V1 标准可动画属性

V1 标准组件使用以下可动画属性：

| 属性 | 使用场景 |
|---|---|
| BackgroundColor | Button hover/pressed、Switch 轨道。 |
| Margin | Switch 滑块位置。 |
| Width | Tabs 内容切换。 |
| Height | ComboBox 下拉展开/收起。 |

渲染器可以支持更多属性，但不得要求业务组件依赖非标准动画属性。

### 9.3 动画时长建议

| 场景 | 推荐时长 |
|---|---:|
| hover / pressed 反馈 | 100-200ms |
| Switch 切换 | 120-200ms |
| 下拉展开 / 收起 | 120-200ms |
| Tab 内容切换 | 200-300ms |
| 大面积页面切换 | 250-400ms |

标准要求：

- 常规交互动效应保持短促。
- 不得用长动画阻碍用户操作。
- 折叠容器动画应配合 Overflow Hidden。
- 位移动画应优先使用不会破坏布局稳定性的属性。

### 9.4 降级与可访问性

标准要求：

- 渲染器或宿主环境如果启用减少动态效果，应可以缩短或禁用动画。
- 动画禁用时，组件仍必须完整可用。
- 不得只依赖动画表达状态变化；状态变化必须同时通过颜色、文本、形状或布局表达。

## 10. 交互与事件标准

### 10.1 事件触发原则

标准要求：

- 事件不得在组件渲染过程中同步触发业务回调。
- 用户交互事件必须在交互发生后触发。
- 事件回调中可以更新状态，更新由运行时调度。
- 高频事件如指针移动应谨慎触发状态更新。

### 10.2 标准事件列表

| 事件 | 标准参数 | 常见使用 |
|---|---|---|
| OnClick | MouseButton | 按钮、列表项、卡片点击。 |
| OnMouseMove | Point | 拖拽、悬停索引。 |
| OnMouseEnter | none | hover 态开始。 |
| OnMouseLeave | none | hover 态结束。 |
| OnMouseDown | none | pressed 态开始。 |
| OnMouseUp | none | pressed 态结束。 |
| OnKeyDown | KeyCode | 快捷键、输入辅助。 |
| OnKeyUp | KeyCode | 键盘释放。 |
| OnTextInput | string | 已确认文本输入。 |
| OnTextComposition | TextCompositionEvent | IME 组合输入生命周期。 |
| OnFocus | none | 焦点进入。 |
| OnBlur | none | 焦点离开。 |
| OnValueChanged | string | 文本输入变化。 |
| OnToggle | boolean | CheckBox、Switch 状态变化。 |
| OnSelectionChanged | number | RadioGroup、ComboBox 选中项变化。 |
| OnTabChanged | number | Tabs 当前标签变化。 |

### 10.3 受控与非受控组件

V1 标准状态语义：

| 组件 | V1 状态模式 |
|---|---|
| Input | 受控。Value 由外部提供，OnValueChanged 通知外部。 |
| TextInput | 受控。Value 由外部提供，OnValueChanged 通知外部。 |
| Button | 无业务状态，仅内部交互态。 |
| CheckBox | 非受控。IsChecked 为初始值。 |
| RadioGroup | 非受控。SelectedIndex 为初始值。 |
| ComboBox | 非受控。SelectedIndex 为初始值。 |
| Switch | 非受控。DefaultIsOn 为初始值。 |
| Tabs | 非受控。InitialIndex 为初始值。 |

标准要求：

- 非受控组件必须在用户交互后发出结果事件。
- 上层业务状态不得假设后续 Props 会强制覆盖非受控组件内部状态。
- 如需强制重置非受控组件，可以通过改变组件 Key 重新创建实例。

## 11. 标准页面结构模式

### 11.1 App Shell

```text
AppShell
  Container Direction: Horizontal

  Sidebar
    Width: fixed
    FlexShrink: 0

  ContentHost
    FlexGrow: 1
    FlexShrink: 1
    Direction: Vertical

    Header
      Height: fixed

    MainScrollArea
      FlexGrow: 1
      Overflow: Auto
```

适用场景：后台管理、工具类应用、Dashboard。

### 11.2 Card

```text
Card
  BackgroundColor: White
  BorderStyle: Solid
  BorderWidth: 1
  BorderRadius: 8
  Padding: 16-24
  Direction: Vertical
  Gap: 12-16

  Title Text
  Body Children
```

标准要求：

- Card 不应该自行获取数据。
- Card 只负责视觉分组和布局。

### 11.3 Form Field

```text
FormField
  Direction: Vertical
  Gap: 6

  Label Text
  InputFrame Container
    Height: 36-40
    BorderStyle: Solid
    BorderWidth: 1
    BorderRadius: 4-8
    Padding: horizontal 8-12

    Input
      Value
      OnValueChanged
```

标准要求：

- Label 与输入控件应该保持同一布局组。
- 输入框错误、帮助文本应作为 FormField 的子元素扩展。

### 11.4 Toolbar

```text
Toolbar
  Direction: Horizontal
  AlignItems: Center
  JustifyContent: SpaceBetween
  Gap: 8-12

  LeftActions
  RightActions
```

标准要求：

- Toolbar 中的按钮应统一高度。
- 操作组之间用 Gap 或 SpaceBetween，不应使用空元素撑开，除非该空元素明确承担 FlexGrow。

### 11.5 Scroll List

```text
ListViewport
  Height: fixed or computed
  Overflow: Auto
  Direction: Vertical

  Row
    Key: stable id
    Height: fixed or content-based
```

标准要求：

- 每一行必须有稳定 Key。
- 列表项内部状态依赖 Key 保持。
- 大型列表可以由后续版本提供虚拟化组件，V1 不强制。

## 12. 渲染器兼容性要求

一个 EchoUI V1 兼容渲染器必须支持：

- 创建和销毁 Container、Text、Input。
- 按 Props 更新基础属性。
- 添加、删除、移动子元素。
- 基于 Key 保留或重建组件实例。
- 分发 V1 标准事件中其平台支持的事件。
- 在不支持动画时立即应用最终值。
- 按本文档默认值处理所有未设置属性，并在属性回到默认值时清除平台残留状态。
- 在卸载组件时释放事件、平台资源和 Effect cleanup。

一个 EchoUI V1 兼容渲染器应该支持：

- Color Alpha。
- BorderRadius。
- Overflow Auto / Scroll。
- Float 浮动层。
- BackgroundColor、Margin、Width、Height 的过渡动画。

渲染器不得：

- 改变标准组件事件回调语义。
- 因不支持某一可选视觉能力而破坏基础布局。
- 把平台专有属性泄漏为标准组件的必需输入。

### 12.1 官方渲染器默认视觉基线

以下表格描述当前官方 Web / Win32 renderer 的默认视觉映射。除“强约束”项外，新后端可以采用不同外观，但必须在实现文档中声明。

| 项目 | Web 官方 renderer | Win32 官方 renderer | 约束类型 |
|---|---|---|---|
| Container 默认背景 / 边框 | Transparent / None / 0 / Transparent / Radius 0 | Transparent / None / 0 / Transparent / Radius 0 | 强约束 |
| Container 默认 Margin / Padding / Gap | 0 / 0 / 0 | 0 / 0 / 0 | 强约束 |
| Text 默认字体 | 浏览器 / 宿主默认字体 | `Segoe UI`；疑似 Emoji 文本回退 `Segoe UI Emoji` | 平台差异，可声明 |
| Text 默认字号 | 浏览器 / 宿主默认字号 | `14px` | 平台差异，可声明 |
| Text 默认字重 | 浏览器 / 宿主默认字重 | `Regular` | 平台差异，可声明 |
| Text 默认文本色 | 浏览器 / 宿主当前文本色 | Black | 平台差异，可声明 |
| 宿主 / 根表面背景 | 由宿主 DOM / CSS 决定，不属于标准元素默认值 | 根画布清空为 White | 平台差异，可声明 |
| Input 默认背景色 | White | White | 强约束 |
| Input 默认文本色 | Black | Black | 强约束 |
| Input 默认边框 / 焦点边框 | None / Transparent；未显式设置颜色时无可见边框 | None / Transparent；未显式设置颜色时无可见边框 | 强约束 |
| Input 默认 Padding | 0 | 0 | 强约束 |

补充要求：

- 需要稳定跨端外观时，不应依赖 Text 的平台默认字体、字号、字重或文本色，应显式设置对应 Props。
- `Input` 的默认白底、黑字、无边框、零内边距属于跨端强约束；新后端如不能满足，必须在实现文档中明确声明差异。
- Web 宿主页面背景不属于 `Container` 或 `Text` 的标准默认值；如果业务需要固定背景，必须由宿主 CSS 或根容器显式设置。

## 13. 组件设计验收清单

新增 V1 标准组件或业务基础组件前，应检查：

- 是否能用 Container、Text、Input 组合实现。
- Props 是否有明确默认值。
- 事件命名是否符合 OnXxx。
- 回调参数是否表达变化后的结果。
- 是否明确受控或非受控语义。
- 内部 State 是否仅用于局部交互态。
- 动态 children 是否有稳定唯一 Key。
- 是否避免依赖平台专属能力。
- 布局是否使用 Direction、Gap、Padding、FlexGrow/FlexShrink，而不是硬编码平台布局。
- 动画是否只依赖 V1 标准可动画属性，且可降级。
- 不支持动画或浮动层时，组件是否仍可基本使用。

## 14. V1 范围外能力

以下能力不属于 EchoUI V1 基础标准，可以在后续版本或扩展库中定义：

- 主题系统与 Design Token 运行时切换。
- 完整焦点管理与无障碍树映射。
- 多行富文本编辑器。
- 表格、数据网格、虚拟列表。
- 模态框、Toast、Tooltip、Popover 完整组件体系。
- 拖拽排序与手势系统。
- 关键帧动画、物理动画、布局动画。
- 响应式断点系统。
- 国际化文本排版细则。

## 15. 总结

EchoUI V1 标准建立在以下最小闭环上：

```text
Component + Props + State
  -> Element Tree
  -> Reconciler Diff
  -> Renderer Patch
  -> Platform UI
```

V1 标准的核心不是绑定某个运行平台，而是保证组件语义、布局行为、事件结果和动画降级在不同渲染器之间保持一致。业务组件应优先依赖本文档中的标准基础元素、标准组合组件和标准布局规则；只有在确有必要时才使用平台原生逃生口。
