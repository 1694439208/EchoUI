# EchoUI 渲染器标准一致性修复计划

本文档记录标准修订后仍需跟进的 Web / Win32 渲染器一致性问题，并作为后续实现计划使用。

## 目标

- 消除隐式默认值导致的跨渲染器行为差异。
- 让 Web 与 Win32 对同一套 `Element` / `Props` 输出一致的布局、事件和资源生命周期语义。
- 对允许降级的能力给出明确标准、诊断或测试覆盖。

## 已处理基线

- 标准已补充默认值与隐式语义：未设置属性、默认值回退、`Dimension.Auto`、`FlexGrow/FlexShrink`、`Float` 默认尺寸、ComboBox 下拉宽度等。
- Dashboard 表单列已显式使用 `Width = Dimension.ZeroPixels`，避免内容 intrinsic width 影响等分列。
- ComboBox 下拉浮层已显式设置 `Width = Dimension.Percent(100)`。
- Web Float 未显式宽度时已补充 `width: 100%` 默认映射。

## 优先级说明

| 优先级 | 含义 |
|---|---|
| P0 | 会造成明显跨端行为错误、资源泄漏或核心控件语义错误，应优先修复。 |
| P1 | 影响一致性或标准完整性，但有临时规避方式。 |
| P2 | 体验、诊断、性能或测试增强。 |

---

## P0 修复项

### 1. Web Float 仍未真正脱离布局流

**影响范围**：`EchoUI.Render.Web`

**问题**：当前 Web Float 通过 `height: 0`、`min-height: 0`、`width: 100%`、`overflow: visible` 模拟浮层，但在 CSS Flex 中仍是 flex item，可能继续受 `gap`、主轴排列、兄弟节点顺序影响。

**标准期望**：`Float = true` 的元素不应占用父容器普通布局空间；默认宽度跟随父容器内容宽度，显式宽高优先。

**计划**：

- 为包含 Float 子元素的父容器建立定位上下文。
- 将 Float 元素映射为 overlay 语义，例如 `position: absolute`。
- 定义默认锚定规则，并保证 ComboBox / Tabs 等现有组件行为不回退。
- 增加横向 flex、纵向 flex、带 gap、带 overflow 的 Float 回归用例。

**验收标准**：

- Float 元素不改变兄弟节点布局尺寸和位置。
- ComboBox 下拉层宽度稳定跟随触发区域。
- 显式 `Width` / `Height` / `MinHeight` 不被 Float 默认值覆盖。

### 2. Web 事件与 DOM registry 子树清理

**影响范围**：`EchoUI.Render.Web`、`EchoUI.Demo.Web/wwwroot/dom.js`

**问题**：节点卸载时 C# 事件字典和 JS registry 没有完整递归清理子树，动态增删大量 UI 时可能保留闭包、DOM 引用或过期事件。

**标准期望**：卸载元素时必须释放该元素及其子树的事件绑定和平台资源。

**计划**：

- 在 Web renderer 增加按 element id 递归释放事件的接口。
- 在 JS bridge 中递归删除 registry 内子树节点。
- 卸载时先清理事件，再移除 DOM 节点。
- 增加动态创建/删除带事件子树的回归测试或调试断言。

**验收标准**：

- 移除节点后，对应 C# 事件表不再包含该子树 element id。
- JS registry 不保留已删除子树节点。
- 重复打开/关闭下拉层不会累积事件处理器。

### 3. Web MouseButton 映射错误

**影响范围**：`EchoUI.Render.Web`、`EchoUI.Demo.Web/wwwroot/dom.js`

**问题**：DOM `MouseEvent.button` 的顺序为 `0 = Left`、`1 = Middle`、`2 = Right`；EchoUI `MouseButton` 语义为 `Left`、`Right`、`Middle`。直接强转会导致中键和右键反转。

**标准期望**：两端事件参数应统一使用 EchoUI 的 `MouseButton` 枚举语义。

**计划**：

- 在 Web 事件桥接层显式转换 DOM button 值。
- 对未知 button 值给出安全默认或忽略。
- 增加左键、右键、中键事件映射测试。

**验收标准**：

- 左键触发 `MouseButton.Left`。
- 右键触发 `MouseButton.Right`。
- 中键触发 `MouseButton.Middle`。

### 4. Win32 窗口关闭时释放整棵树资源

**影响范围**：`EchoUI.Render.Win32`

**问题**：当前窗口关闭流程只退出消息循环，整棵 Element tree 上的 HWND、GDI 字体/画刷、图片和事件引用可能没有统一释放。

**标准期望**：renderer 生命周期结束时必须释放所有平台资源。

**计划**：

- 为 `Win32Renderer` 增加 `IDisposable` 或等价关闭接口。
- 窗口销毁时递归释放 root tree。
- 将已有 `RemoveChild` 资源释放逻辑抽出为可递归复用方法。
- 确保重复释放安全。

**验收标准**：

- 关闭窗口时 Input HWND、字体、画刷、图片均被释放。
- 动态卸载和窗口关闭走同一套资源释放路径。
- Native AOT 发布场景无资源释放异常。

### 5. Win32 Input 受控语义不严格

**影响范围**：`EchoUI.Render.Win32`

**问题**：Win32 `EDIT` 文本变化时 renderer 会直接更新本地 `InputValue`，即使上层没有更新 `Props.Value`，本地显示值也可能已经变化。

**标准期望**：`Input` 是受控组件；最终显示值应由 `InputProps.Value` 决定，用户输入只通过 `OnValueChanged` 通知上层。

**计划**：

- 将用户输入路径改为只触发 `OnValueChanged`。
- `InputValue` 与原生 EDIT 文本只由 Props patch 同步。
- 如上层拒绝输入或未更新状态，在回调后恢复当前 Props 值。
- 避免恢复文本时重复触发 `EN_CHANGE` 形成循环。

**验收标准**：

- 上层不更新 `Value` 时，Win32 输入框显示值恢复为旧 Props 值。
- 上层更新 `Value` 时，显示值与新 Props 值一致。
- Web 与 Win32 受控输入行为一致。

---

## P1 修复项

### 6. Web InputProps 样式属性未完整映射

**影响范围**：`EchoUI.Render.Web`

**问题**：Web input 当前主要处理 `Value` 与 `OnValueChanged`，未完整映射背景色、文字色、边框色、焦点边框色、Padding 等样式属性。

**标准期望**：`InputProps` 中跨端声明的视觉属性应在 Web 和 Win32 上尽量一致。

**计划**：

- 映射 `BackgroundColor`、`TextColor`、`BorderColor`、`Padding`。
- 为 `FocusedBorderColor` 增加 focus / blur 状态映射。
- 明确浏览器默认 outline 是否保留或禁用。

**验收标准**：

- 同一 Input 样式在 Web / Win32 上基础颜色、边框和 padding 一致。
- focus 状态边框颜色生效。

### 7. Web Native 属性和事件删除不完整

**影响范围**：`EchoUI.Render.Web`

**问题**：`NativeProps.Properties` 从有值变为无值时，Web 侧可能无法区分“未设置”和“需要移除 attribute”；JS 侧设置 `null` 也不等价于 `removeAttribute`。

**标准期望**：属性回到默认值或被移除时，renderer 必须清理平台残留状态。

**计划**：

- 扩展 DOM patch 结构，区分 attribute set / remove。
- 对事件也区分 add / remove。
- 明确 `null` 值在 Native 属性中的语义。

**验收标准**：

- 移除 `src`、`class`、`style` 等属性后 DOM 中不残留旧 attribute。
- Native 事件移除后不再触发旧回调。

### 8. Web Text MouseThrough 默认值显式化

**影响范围**：`EchoUI.Render.Web`

**问题**：Text 鼠标穿透依赖初始 patch 或属性差异触发，而不是 renderer 层每次按最终 Props 明确写入默认语义。

**标准期望**：Text 默认 `MouseThrough = true`，不应依赖浏览器默认行为或 patch 差异偶然生效。

**计划**：

- Text patch 时始终按最终 `TextProps.MouseThrough` 写入 `pointer-events`。
- 回退默认值时清理或重设 `pointer-events`。

**验收标准**：

- 未设置 `MouseThrough` 的 Text 不拦截鼠标。
- 显式 `MouseThrough = false` 的 Text 可以命中事件。
- 从 false 切回默认 true 后行为正确。

### 9. Web Transition 属性名映射不完整

**影响范围**：`EchoUI.Render.Web`

**问题**：部分 C# 属性名没有转换为 CSS kebab-case，例如 `Width`、`Height` 等可能直接输出不规范属性名。

**标准期望**：支持的 transition 属性必须映射到合法 CSS 属性；不支持的属性应明确忽略或诊断。

**计划**：

- 建立 C# Props 属性名到 CSS 属性名的白名单映射。
- 补齐 `Width`、`Height`、`MinWidth`、`MaxWidth`、`Margin`、`Padding`、`BackgroundColor` 等常用属性。
- 对未知属性选择忽略并在 Debug 下诊断。

**验收标准**：

- 常用布局和颜色 transition 在 Web 上输出合法 CSS。
- 不支持属性不会生成无效 CSS。

### 10. Win32 Native 不支持能力静默忽略

**影响范围**：`EchoUI.Render.Win32`

**问题**：Win32 Native 只支持少量属性和 `img`，其他属性静默忽略，调用方难以判断跨端能力差异。

**标准期望**：Native 是平台逃逸口；不支持时应明确降级范围或提供可诊断行为。

**计划**：

- 文档化 Win32 Native 支持矩阵。
- Debug 模式下对未知 Native type / property 输出诊断。
- 对常见属性补齐最小支持，或明确声明不支持。

**验收标准**：

- 使用未知 Native 属性时开发者能看到诊断。
- Win32 Native 支持范围在标准或 renderer 文档中可查。

### 11. Web / Win32 鼠标坐标语义不一致

**影响范围**：`EchoUI.Render.Web`、`EchoUI.Render.Win32`

**问题**：Web 当前更接近 element-local 坐标；Win32 当前更接近窗口客户区坐标。相同组件在两端收到的 `Point` 语义不同。

**标准期望**：`Point` 坐标系必须明确，并由两个 renderer 统一实现。

**计划**：

- 在标准中定义 `Point` 是 viewport/client 坐标还是 element-local 坐标。
- 推荐统一为 element-local，便于组件内交互计算。
- 修改另一端转换逻辑。
- 如需 viewport 坐标，后续新增事件参数类型。

**验收标准**：

- 同一元素左上角点击，两端 `Point` 均接近 `(0, 0)` 或均为标准定义的同一坐标系。
- 嵌套容器、滚动容器、Float 元素下坐标一致。

### 12. Win32 Click 判定过宽

**影响范围**：`EchoUI.Render.Win32`

**问题**：当前按下和释放目标存在祖先/后代关系时也可能触发 click，可能与 Web 点击语义不一致。

**标准期望**：click 应基于稳定的按下目标和释放目标；是否允许冒泡需要标准明确。

**计划**：

- 记录 mouse down 时的可点击 target。
- mouse up 时必须同一 target 才触发 click。
- 如需冒泡，由事件分发阶段按父链处理，而不是放宽 click target 判定。

**验收标准**：

- 在子元素按下、父元素释放不会误触发子元素 click。
- 与 Web click 行为一致。

---

## P2 跟进项

### 13. Win32 ScrollOffset 布局后越界

**影响范围**：`EchoUI.Render.Win32`

**问题**：滚动偏移主要在滚轮时 clamp。内容变少或容器变大后，旧 `ScrollOffsetY` 可能超过新最大值。

**计划**：

- 每次 layout 后根据内容高度和 viewport 高度 clamp scroll offset。
- 内容高度不足时自动归零。

**验收标准**：

- 删除内容后不会停留在空白滚动区域。
- 调整窗口大小后滚动条位置合法。

### 14. Win32 Overflow.Scroll 与 Auto 视觉区分

**影响范围**：`EchoUI.Render.Win32`

**问题**：`Overflow.Scroll` 和 `Overflow.Auto` 当前视觉上基本一致，通常只有内容超出才绘制滚动条。

**计划**：

- 选择标准语义：
  - 如果 `Scroll` 应始终显示 scrollbar track，则补实现。
  - 如果允许降级为 `Auto`，则在标准中声明。

**验收标准**：

- `Overflow.Scroll` 行为符合标准声明。

### 15. Web 默认视觉属性依赖浏览器默认值

**影响范围**：`EchoUI.Render.Web`

**问题**：部分视觉默认值依赖浏览器 UA stylesheet，例如背景、边框、overflow、gap 等。

**计划**：

- 为核心元素建立 renderer baseline style。
- 对标准要求的默认值显式输出。
- 对纯平台视觉默认值在标准中声明可差异。

**验收标准**：

- 同一核心元素在不同浏览器中默认布局不出现明显偏差。
- 属性回到默认值时能清理旧 style。

### 16. dom.js 调试日志清理

**影响范围**：`EchoUI.Demo.Web/wwwroot/dom.js`

**问题**：JS bridge 中存在较多 `console.log`，尤其高频事件会影响性能和调试噪音。

**计划**：

- 增加 debug flag 控制日志，默认关闭。
- 删除 mousemove 等高频日志。

**验收标准**：

- 默认运行 Demo 时控制台无高频渲染/事件日志。
- Debug 模式仍可开启必要诊断。

### 17. Win32 Transitions 降级测试

**影响范围**：`EchoUI.Render.Win32`

**问题**：Win32 标准允许忽略动画并立即应用最终值，但缺少回归验证。

**计划**：

- 为 `Width`、`Height`、`Margin`、`BackgroundColor` 等 transition 场景增加测试或手动验证 Demo。
- 确认 Win32 即使忽略动画，也不会忽略最终值。

**验收标准**：

- 设置 transition 的属性在 Win32 上最终状态正确。
- 不支持动画不会破坏布局或绘制。

### 18. 平台默认视觉值文档化

**影响范围**：`ECHOUI_V1_STANDARD.md`、Web / Win32 renderer 文档

**问题**：部分纯视觉默认值允许平台差异，但当前需要更清楚列出两端默认映射。

**计划**：

- 列出 Web baseline style。
- 列出 Win32 默认字体、字号、颜色、根背景、Input 默认色等。
- 明确哪些默认值属于跨端强约束，哪些属于平台外观差异。

**验收标准**：

- 实现 renderer 时无需猜测默认视觉值。
- 新后端可按文档选择强一致或声明平台差异。

---

## 建议执行顺序

1. 修复 P0-3：Web MouseButton 映射，范围小、风险低。
2. 修复 P0-2：Web 事件与 registry 清理，避免后续浮层和动态 UI 泄漏。
3. 修复 P0-1：Web Float 真实 overlay 语义。
4. 修复 P0-5：Win32 Input 受控语义。
5. 修复 P0-4：Win32 renderer 生命周期释放。
6. 批量处理 P1 Web 映射问题：Input 样式、Native 删除、Text 默认值、Transition 属性名。
7. 统一事件坐标与 click 语义。
8. 补 P2 文档、诊断和测试。

## 测试计划

- 新增或补充核心 renderer 行为用例：
  - Float 不占布局空间。
  - ComboBox 下拉宽度跟随触发容器。
  - Flex 等分列需要 `Width = Dimension.ZeroPixels`。
  - 属性从显式值回退默认值时清理平台状态。
  - Web 事件卸载后不再触发。
  - MouseButton 左/右/中键映射。
  - Input 受控输入拒绝和接受两种路径。
  - ScrollOffset 内容变化后 clamp。
- 手动验证 Demo：
  - Dashboard 表单列布局。
  - ComboBox 展开/收起。
  - Input 样式、输入、焦点。
  - Win32 窗口关闭资源释放。

## 完成定义

- P0 全部修复并构建通过。
- P1 有明确实现或文档化降级策略。
- 所有跨端语义差异均能在标准中找到解释。
- Web / Win32 对标准强约束项不再依赖隐式平台默认值。
