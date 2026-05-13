using System.Drawing;
using System.Runtime.InteropServices;
using EchoUI.Core;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// Win32 窗口管理器：创建窗口、运行消息循环、分发事件到 HitTestManager。
    /// </summary>
    public class Win32Window
    {
        private nint _hwnd;
        private readonly string _title;
        private readonly int _width;
        private readonly int _height;
        private Win32Renderer? _renderer;
        private bool _trackingMouse;

        // 防止 WndProc 委托被 GC 回收
        private NativeInterop.WndProc? _wndProcDelegate;

        public nint Hwnd => _hwnd;

        public Win32Window(string title, int width, int height)
        {
            _title = title;
            _width = width;
            _height = height;
        }

        internal void SetRenderer(Win32Renderer renderer)
        {
            _renderer = renderer;
        }

        /// <summary>
        /// 创建 Win32 窗口
        /// </summary>
        public void Create()
        {
            var hInstance = NativeInterop.GetModuleHandle(null);
            _wndProcDelegate = WndProc;

            var wc = new NativeInterop.WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<NativeInterop.WNDCLASSEX>(),
                style = 0x0003, // CS_HREDRAW | CS_VREDRAW
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = hInstance,
                hCursor = NativeInterop.LoadCursor(0, NativeInterop.IDC_ARROW),
                hbrBackground = NativeInterop.GetStockObject(NativeInterop.WHITE_BRUSH),
                lpszClassName = "EchoUIWin32Class"
            };

            NativeInterop.RegisterClassEx(ref wc);

            // 计算包含标题栏和边框的窗口尺寸，确保客户区为指定大小
            uint dwStyle = NativeInterop.WS_OVERLAPPEDWINDOW | NativeInterop.WS_CLIPCHILDREN;
            uint dwExStyle = NativeInterop.WS_EX_COMPOSITED;
            var rect = new NativeInterop.RECT { Left = 0, Top = 0, Right = _width, Bottom = _height };
            NativeInterop.AdjustWindowRectEx(ref rect, dwStyle, false, dwExStyle);

            _hwnd = NativeInterop.CreateWindowEx(
                dwExStyle,
                "EchoUIWin32Class",
                _title,
                dwStyle,
                100, 100, rect.Width, rect.Height,
                0, 0, hInstance, 0);

            if (_hwnd == 0)
                throw new InvalidOperationException("创建窗口失败");

            Win32SynchronizationContext.SetWindow(_hwnd);

            NativeInterop.ShowWindow(_hwnd, NativeInterop.SW_SHOW);
            NativeInterop.UpdateWindow(_hwnd);
        }

        /// <summary>
        /// 运行消息循环（阻塞调用）
        /// </summary>
        public void Run()
        {
            while (NativeInterop.GetMessage(out var msg, 0, 0, 0))
            {
                NativeInterop.TranslateMessage(ref msg);
                NativeInterop.DispatchMessage(ref msg);
            }
        }

        private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            switch (msg)
            {
                case NativeInterop.WM_PAINT:
                    OnPaint(hWnd);
                    return 0;

                case NativeInterop.WM_ERASEBKGND:
                    return 1; // 阻止背景擦除，避免闪烁

                case NativeInterop.WM_SIZE:
                    OnResize(hWnd);
                    return 0;

                case NativeInterop.WM_MOUSEMOVE:
                    OnMouseMove(hWnd, lParam);
                    return 0;

                case NativeInterop.WM_LBUTTONDOWN:
                    OnMouseButton(hWnd, lParam, MouseButton.Left, true);
                    return 0;

                case NativeInterop.WM_LBUTTONUP:
                    OnMouseButton(hWnd, lParam, MouseButton.Left, false);
                    return 0;

                case NativeInterop.WM_RBUTTONDOWN:
                    OnMouseButton(hWnd, lParam, MouseButton.Right, true);
                    return 0;

                case NativeInterop.WM_RBUTTONUP:
                    OnMouseButton(hWnd, lParam, MouseButton.Right, false);
                    return 0;

                case NativeInterop.WM_MBUTTONDOWN:
                    OnMouseButton(hWnd, lParam, MouseButton.Middle, true);
                    return 0;

                case NativeInterop.WM_MBUTTONUP:
                    OnMouseButton(hWnd, lParam, MouseButton.Middle, false);
                    return 0;

                case NativeInterop.WM_MOUSEWHEEL:
                    OnMouseWheel(hWnd, wParam, lParam);
                    return 0;

                case NativeInterop.WM_MOUSELEAVE:
                    _trackingMouse = false;
                    _renderer?.HitTestManager.HandleMouseLeave();
                    return 0;

                case NativeInterop.WM_KEYDOWN:
                    _renderer?.HitTestManager.HandleKeyDown((int)wParam);
                    return 0;

                case NativeInterop.WM_KEYUP:
                    _renderer?.HitTestManager.HandleKeyUp((int)wParam);
                    return 0;

                case NativeInterop.WM_COMMAND:
                    OnCommand(wParam, lParam);
                    return 0;

                case NativeInterop.WM_ECHOUI_UPDATE:
                    OnEchoUIUpdate();
                    return 0;

                case Win32SynchronizationContext.WM_SYNC_CONTEXT:
                    Win32SynchronizationContext.ProcessQueue();
                    return 0;

                case NativeInterop.WM_DESTROY:
                    NativeInterop.PostQuitMessage(0);
                    return 0;

                case NativeInterop.WM_CTLCOLOREDIT:
                    if (_renderer != null)
                    {
                        var element = _renderer.GetElementByEditHwnd(lParam);
                        if (element != null)
                        {
                            var hdc = wParam;
                            
                            // 设置文本颜色
                            var textColor = element.TextColor ?? EchoUI.Core.Color.Black;
                            int crText = (textColor.B << 16) | (textColor.G << 8) | textColor.R;
                            NativeInterop.SetTextColor(hdc, crText);

                            // 设置背景颜色（用于文字底色）
                            var bgColor = element.BackgroundColor ?? new EchoUI.Core.Color(255, 255, 255, 255);
                            int crBk = (bgColor.B << 16) | (bgColor.G << 8) | bgColor.R;
                            NativeInterop.SetBkColor(hdc, crBk); // 需要 SetBkColor
                            
                            // 返回背景画刷，用于擦除背景
                            if (element.NativeBrushHandle != 0)
                                return element.NativeBrushHandle;
                            
                            return NativeInterop.GetStockObject(NativeInterop.WHITE_BRUSH);
                        }
                    }
                    break;
            }

            return NativeInterop.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void OnPaint(nint hWnd)
        {
            NativeInterop.BeginPaint(hWnd, out var ps);
            try
            {
                NativeInterop.GetClientRect(hWnd, out var clientRect);
                int w = clientRect.Width;
                int h = clientRect.Height;

                if (w > 0 && h > 0 && _renderer?.RootElement != null)
                {
                    // 确保布局已计算
                    FlexLayout.ComputeLayout(_renderer.RootElement, w, h);
                    _renderer.UpdateAllEditPositions(w, h);

                    // 双缓冲绘制
                    using var bitmap = new Bitmap(w, h);
                    using var g = Graphics.FromImage(bitmap);
                    GdiPainter.Paint(g, _renderer.RootElement, _renderer.FloatingElements, w, h);

                    using var screenGraphics = Graphics.FromHdc(ps.hdc);
                    screenGraphics.DrawImageUnscaled(bitmap, 0, 0);
                }
            }
            finally
            {
                NativeInterop.EndPaint(hWnd, ref ps);
            }
        }

        private void OnResize(nint hWnd)
        {
            _renderer?.RequestRelayout();
        }

        private void OnMouseMove(nint hWnd, nint lParam)
        {
            if (!_trackingMouse)
            {
                var tme = new NativeInterop.TRACKMOUSEEVENT
                {
                    cbSize = Marshal.SizeOf<NativeInterop.TRACKMOUSEEVENT>(),
                    dwFlags = NativeInterop.TME_LEAVE,
                    hwndTrack = hWnd
                };
                NativeInterop.TrackMouseEvent(ref tme);
                _trackingMouse = true;
            }

            float x = NativeInterop.LOWORD(lParam);
            float y = NativeInterop.HIWORD(lParam);

            if (_renderer?.RootElement != null)
            {
                _renderer.HitTestManager.HandleMouseMove(_renderer.RootElement, x, y);
            }
        }

        private void OnMouseButton(nint hWnd, nint lParam, MouseButton button, bool isDown)
        {
            float x = NativeInterop.LOWORD(lParam);
            float y = NativeInterop.HIWORD(lParam);

            if (_renderer?.RootElement != null)
            {
                if (isDown)
                    _renderer.HitTestManager.HandleMouseDown(_renderer.RootElement, x, y, button);
                else
                    _renderer.HitTestManager.HandleMouseUp(_renderer.RootElement, x, y, button);
            }
        }

        private void OnMouseWheel(nint hWnd, nint wParam, nint lParam)
        {
            int delta = NativeInterop.GET_WHEEL_DELTA_WPARAM(wParam);

            // 获取鼠标在客户区的坐标
            NativeInterop.GetClientRect(hWnd, out var rect);
            float vpW = rect.Width;
            float vpH = rect.Height;

            // WM_MOUSEWHEEL 的坐标是屏幕坐标，需要转换
            var screenPoint = new NativeInterop.POINT
            {
                X = NativeInterop.LOWORD(lParam),
                Y = NativeInterop.HIWORD(lParam)
            };
            ScreenToClient(hWnd, ref screenPoint);

            if (_renderer?.RootElement != null)
            {
                _renderer.HitTestManager.HandleMouseWheel(
                    _renderer.RootElement, screenPoint.X, screenPoint.Y, delta, vpW, vpH);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(nint hWnd, ref NativeInterop.POINT lpPoint);

        private void OnCommand(nint wParam, nint lParam)
        {
            int notificationCode = NativeInterop.HIWORD(wParam);
            if (lParam == 0) return;

            if (notificationCode == NativeInterop.EN_CHANGE)
            {
                _renderer?.HandleEditChange(lParam);
            }
            else if (notificationCode == NativeInterop.EN_SETFOCUS)
            {
                _renderer?.HandleEditFocusChange(lParam, true);
            }
            else if (notificationCode == NativeInterop.EN_KILLFOCUS)
            {
                _renderer?.HandleEditFocusChange(lParam, false);
            }
        }

        private void OnEchoUIUpdate()
        {
            if (_renderer?.Scheduler != null)
            {
                // 在消息循环线程中同步执行更新
                var task = _renderer.Scheduler.ProcessPendingUpdates();
                // 更新完成后重新布局和绘制
                if (task.IsCompleted)
                {
                    _renderer.RequestRelayout();
                }
                else
                {
                    task.ContinueWith(_ => _renderer.RequestRelayout(),
                        TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }
    }
}
