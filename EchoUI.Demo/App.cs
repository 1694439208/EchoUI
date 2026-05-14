namespace EchoUI.Demo;

using EchoUI.Core;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static EchoUI.Core.Elements;
using static EchoUI.Core.Hooks;

public static class Demo
{
    private const string SampleMarkdown = @"
# Welcome to EchoUI Markdown

This is a demonstration of rendering Markdown content directly within the EchoUI framework.

- Bullet points are supported.
- **Bold** and *italic* text are rendered as plain text in this basic version.

## Code Blocks

```csharp
public static Element App() {
    var (count, setCount, _) = Hooks.State(0);

    return Container(new ContainerProps {
        Children = [
            Text(new TextProps { Text = $""Counter: {count.Value}"" }),
            Button(new ButtonProps {
                Text = ""Click Me!"",
                OnClick = () => setCount(count.Value + 1)
            })
        ]
    });
}
";

    public static Element? Render(Props props)
    {
        return AnimationDemo.Create(props);
        //var tabsContent = Memo(() => new List<Element>
        //{
        //    Create(Counter, new()
        //    {
        //        Key = "Counter",
        //    }),
        //    Create(InputTest, new()
        //    {
        //        Key = "Input",
        //    }),
        //    Create(ImageTest, new()
        //    {
        //        Key = "Image",
        //    }),
        //    Create(Markdown, new()
        //    {
        //        Key = "Markdown",
        //    }),
        //    Create(OtherTest, new()
        //    {
        //        Key = "Other",
        //    }),
        //}, []);

        //return Container(
        //    Width: Dimension.Percent(100),
        //    Height: Dimension.Percent(100),
        //    Children: [
        //        Tabs(
        //            Titles : ["Counter", "Input", "Image", "Markdown", "Other"],
        //            Content : i => tabsContent[i]
        //        )
        //    ]
        //);
    }

    public static Element? Markdown(Props props)
    {
        return Container(new ContainerProps
        {
            Width = Dimension.Percent(100),
            Height = Dimension.Percent(100),
            Padding = new Spacing(Dimension.Pixels(20)),
            BackgroundColor = Color.White,
            Overflow = Overflow.Auto,
            Children =
            [
                EchoUI.Demo.Elements.MarkdownRenderer(new MarkdownProps
                {
                    Content = SampleMarkdown
                })
            ]
        });
    }

    public static Element? InputTest(Props props)
    {
        var (inputText, setInputText, _) = State("test");

        return Container([
            Text(inputText.Value),
            Container(
                Width: Dimension.Percent(100),
                Height: Dimension.Pixels(50),
                BorderStyle: BorderStyle.Solid,
                BorderWidth: 1,
                BorderColor: Color.Black,
                Children: [
                    Input(
                        Value: inputText.Value,
                        OnValueChanged: v => setInputText(v)
                    )
                ]
            )
        ]);
    }

    public static Element? ImageTest(Props props)
    {
        string[] images = [
            "/img/1.jpg",
            "/img/2.jpg"
        ];

        var (index, _, updateIndex) = State(0);
        var (select, setSelect, _) = State("Click Image...");

        return Container([
            Text(select),
            Native(
                Type: "img",
                Properties: [
                    [ "style", "width: 30px;height: 30px;user-select:none;border-radius:15px" ],
                    [ "src", images[index] ],
                    [ "click", (MouseButton v) => setSelect(images[index]) ]
                ]
            ),
            Button(
                Text: "Next Image",
                OnClick: _ => updateIndex(i => i + 1 < images.Length ? i + 1 : 0)
            ),
        ]);
    }

    public static Element? Counter(Props props)
    {
        var (count, setCount, updateCount) = State(0);
        return Container([
            Text(
                Text : $"count: {count.Value}",
                Color : count.Value == 0 ? Color.Black : (count.Value < 0 ? Color.Red : Color.Green)
            ),
            Container(
                Direction : LayoutDirection.Horizontal,
                Padding :  new Spacing(Dimension.Pixels(5)),
                Gap : 5,
                Children : [
                    Button(
                        Text : "-",
                        Height : Dimension.Pixels(30),
                        Width : Dimension.Pixels(30),
                        OnClick : _ => updateCount(v => v - 1)
                    ),
                    Button(
                        Text : "+",
                        Height : Dimension.Pixels(30),
                        Width : Dimension.Pixels(30),
                        OnClick : _ => updateCount(v => v + 1)
                    ),
                    Button(
                        Text : "Reset",
                        OnClick : _ => setCount(0)
                    ),
                ]
            ),
        ]);
    }

    public static Element? OtherTest(Props props)
    {
        return Container([
            Switch(),
            RadioGroup([
                "aaa",
                "bbb",
                "ccc"
            ]),
            CheckBox("test"),
            ComboBox([
                "test1",
                "test2",
                "test3"
            ]),
            Button("testButton")
        ]);
    }
}
