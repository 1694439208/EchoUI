using EchoUI.Core;
using EchoUI.Tools.Serial;
using EchoUI.Render.Win32;
using System.Threading;
using static EchoUI.Core.Elements;

// Win32 消息循环需要 SynchronizationContext 来正确处理 async/await
var syncCtx = new Win32SynchronizationContext();
SynchronizationContext.SetSynchronizationContext(syncCtx);

var window = new Win32Window("EchoUI Serial Tool", 1600, 1100);
window.Create();

var renderer = new Win32Renderer(window);
var reconciler = new Reconciler(renderer, "root");

await reconciler.Mount(SerialPage.Create);

// 初始布局
renderer.RequestRelayout();

// 进入消息循环（阻塞）
window.Run();
