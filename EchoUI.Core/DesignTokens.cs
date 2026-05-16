namespace EchoUI.Core;

/// <summary>
/// Animal Island UI 设计令牌。集中管理所有色彩、尺寸、圆角等视觉常量。
/// 
/// 设计原则：
/// - 无纯黑：文字用暖棕色系
/// - 无直角：最小圆角 12px
/// - 暖灰阴影：禁止冷黑阴影
/// - 3D 厚底阴影：按钮有立体按压感
/// - 交互必有反馈：hover 上浮 / active 下压
/// - 焦点用黄色 #ffcc00，禁止蓝色焦点
/// </summary>
public static class DesignTokens
{
    // ── 主色 ──
    public static readonly Color Primary = Color.FromHex("#19c8b9");
    public static readonly Color PrimaryHover = Color.FromHex("#3dd4c6");
    public static readonly Color PrimaryActive = Color.FromHex("#11a89b");
    public static readonly Color PrimaryBg = Color.FromHex("#e6f9f6");

    // ── 文字（禁止纯黑） ──
    public static readonly Color TextTitle = Color.FromHex("#794f27");
    public static readonly Color TextBody = Color.FromHex("#725d42");
    public static readonly Color TextSecondary = Color.FromHex("#9f927d");
    public static readonly Color TextMuted = Color.FromHex("#8a7b66");
    public static readonly Color TextDisabled = Color.FromHex("#c4b89e");
    public static readonly Color TextInverse = Color.FromHex("#ffffff");

    // ── 背景 ──
    public static readonly Color BgMain = Color.FromHex("#f8f8f0");
    public static readonly Color BgContent = Color.FromHex("#f7f3df");
    public static readonly Color BgDisabled = Color.FromHex("#f0ece2");

    // ── 边框 ──
    public static readonly Color Border = Color.FromHex("#c4b89e");
    public static readonly Color BorderHover = Color.FromHex("#a89878");
    public static readonly Color BorderFocus = Color.FromHex("#ffcc00");

    // ── 3D 阴影色（暖色调，非黑） ──
    public static readonly Color ShadowBtn = Color.FromHex("#bdaea0");
    public static readonly Color ShadowInput = Color.FromHex("#d4c9b4");
    public static readonly Color ShadowSwitchOn = Color.FromHex("#5a9e1e");

    // ── 状态色 ──
    public static readonly Color Success = Color.FromHex("#6fba2c");
    public static readonly Color Warning = Color.FromHex("#f5c31c");
    public static readonly Color Error = Color.FromHex("#e05a5a");
    public static readonly Color ErrorActive = Color.FromHex("#c94444");

    // ── 圆角 ──
    public const float RadiusSm = 12f;
    public const float RadiusBase = 18f;
    public const float RadiusLg = 24f;
    public const float RadiusPill = 50f;

    // ── 字重 ──
    public const int WeightNormal = 500;
    public const int WeightSemibold = 600;
    public const int WeightBold = 700;
    public const int WeightHeavy = 900;

    /// <summary>生成 primary 风格按钮尺寸配置</summary>
    public static ButtonSizing PrimaryButton(string size = "middle") => size switch
    {
        "small" => new ButtonSizing(32f, 16f, 12f, RadiusSm),
        "large" => new ButtonSizing(48f, 32f, 16f, RadiusLg),
        _ => new ButtonSizing(45f, 20f, 14f, RadiusPill),
    };

    /// <summary>生成 input 尺寸配置</summary>
    public static InputSizing TextInput(string size = "middle") => size switch
    {
        "small" => new InputSizing(32f, 12f, 2.5f, 40f),
        "large" => new InputSizing(48f, 16f, 3f, 50f),
        _ => new InputSizing(40f, 14f, 2.5f, 50f),
    };

    /// <summary>生成 checkbox 尺寸配置</summary>
    public static CheckBoxSizing CheckBox(string size = "middle") => size switch
    {
        "small" => new CheckBoxSizing(18f, 2f, 12f),
        "large" => new CheckBoxSizing(28f, 3f, 16f),
        _ => new CheckBoxSizing(22f, 2.5f, 14f),
    };
}

public record struct ButtonSizing(float Height, float PaddingX, float FontSize, float Radius);
public record struct InputSizing(float Height, float FontSize, float BorderWidth, float Radius);
public record struct CheckBoxSizing(float BoxSize, float BorderWidth, float LabelFontSize);
