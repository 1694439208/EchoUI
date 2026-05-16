using System.Threading;
using EchoUI.Core;
using EchoUI.Render.Win32;

var syncCtx = new Win32SynchronizationContext();
SynchronizationContext.SetSynchronizationContext(syncCtx);

var window = new Win32Window("Animal Island UI", 1200, 800);
window.Create();

var renderer = new Win32Renderer(window);
var reconciler = new Reconciler(renderer, "root");

await reconciler.Mount(AnimalIslandDemo.Render);
renderer.RequestRelayout();
window.Run();
