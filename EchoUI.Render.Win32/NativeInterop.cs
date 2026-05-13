using System.Runtime.InteropServices;

namespace EchoUI.Render.Win32
{
    /// <summary>
    /// Win32 API P/Invoke 声明集中管理
    /// </summary>
    internal static class NativeInterop
    {
        // --- 窗口消息 ---
        public const uint WM_DESTROY = 0x0002;
        public const uint WM_SIZE = 0x0005;
        public const uint WM_PAINT = 0x000F;
        public const uint WM_CLOSE = 0x0010;
        public const uint WM_ERASEBKGND = 0x0014;
        public const uint WM_MOUSEMOVE = 0x0200;
        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP = 0x0202;
        public const uint WM_RBUTTONDOWN = 0x0204;
        public const uint WM_RBUTTONUP = 0x0205;
        public const uint WM_MBUTTONDOWN = 0x0207;
        public const uint WM_MBUTTONUP = 0x0208;
        public const uint WM_MOUSEWHEEL = 0x020A;
        public const uint WM_MOUSELEAVE = 0x02A3;
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;
        public const uint WM_CHAR = 0x0102;
        public const uint WM_IME_STARTCOMPOSITION = 0x010D;
        public const uint WM_IME_ENDCOMPOSITION = 0x010E;
        public const uint WM_IME_COMPOSITION = 0x010F;
        public const uint WM_SETFOCUS = 0x0007;
        public const uint WM_KILLFOCUS = 0x0008;
        public const uint WM_TIMER = 0x0113;
        public const uint WM_COMMAND = 0x0111;
        public const uint WM_USER = 0x0400;
        public const uint WM_APP = 0x8000;

        // --- 自定义消息：用于调度更新 ---
        public const uint WM_ECHOUI_UPDATE = WM_APP + 1;

        // --- Edit 控件通知 ---
        public const int EN_SETFOCUS = 0x0100;
        public const int EN_KILLFOCUS = 0x0200;
        public const int EN_CHANGE = 0x0300;
        public const uint WM_CTLCOLOREDIT = 0x0133;
        public const uint WM_SETFONT = 0x0030;

        // --- 窗口样式 ---
        public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
        public const uint WS_VISIBLE = 0x10000000;
        public const uint WS_CHILD = 0x40000000;
        public const uint WS_BORDER = 0x00800000;
        public const uint WS_CLIPCHILDREN = 0x02000000;
        public const uint WS_CLIPSIBLINGS = 0x04000000;
        public const uint WS_VSCROLL = 0x00200000;

        // --- 扩展窗口样式 ---
        public const uint WS_EX_CLIENTEDGE = 0x00000200;
        public const uint WS_EX_COMPOSITED = 0x02000000;

        // --- Edit 控件样式 ---
        public const uint ES_AUTOHSCROLL = 0x0080;
        public const uint ES_LEFT = 0x0000;

        // --- IME composition flags ---
        public const uint GCS_COMPSTR = 0x0008;
        public const uint GCS_RESULTSTR = 0x0800;
        public const int CFS_POINT = 0x0002;
        public const int CFS_FORCE_POSITION = 0x0020;
        public const int CFS_CANDIDATEPOS = 0x0040;

        // --- 光标 ---
        public const int IDC_ARROW = 32512;
        public const int IDC_IBEAM = 32513;

        // --- 颜色 ---
        public const int COLOR_WINDOW = 5;

        // --- ShowWindow ---
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;

        // --- Virtual keys ---
        public const int VK_SHIFT = 0x10;

        // --- TrackMouseEvent ---
        public const uint TME_LEAVE = 0x00000002;

        // --- HIWORD/LOWORD ---
        public static int LOWORD(nint value) => (short)(value.ToInt64() & 0xFFFF);
        public static int HIWORD(nint value) => (short)((value.ToInt64() >> 16) & 0xFFFF);
        public static int GET_WHEEL_DELTA_WPARAM(nint wParam) => HIWORD(wParam);

        // --- 结构体 ---
        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public nint hwnd;
            public uint message;
            public nint wParam;
            public nint lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PAINTSTRUCT
        {
            public nint hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX
        {
            public int cbSize;
            public uint style;
            public nint lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
            public nint hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TRACKMOUSEEVENT
        {
            public int cbSize;
            public uint dwFlags;
            public nint hwndTrack;
            public uint dwHoverTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COMPOSITIONFORM
        {
            public int dwStyle;
            public POINT ptCurrentPos;
            public RECT rcArea;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CANDIDATEFORM
        {
            public uint dwIndex;
            public int dwStyle;
            public POINT ptCurrentPos;
            public RECT rcArea;
        }

        // --- 委托 ---
        public delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

        // --- User32.dll ---
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(nint hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(nint hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

        [DllImport("user32.dll")]
        public static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

        [DllImport("user32.dll")]
        public static extern bool InvalidateRect(nint hWnd, nint lpRect, bool bErase);

        [DllImport("user32.dll")]
        public static extern nint BeginPaint(nint hWnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        public static extern bool EndPaint(nint hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(nint hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern nint LoadCursor(nint hInstance, int lpCursorName);

        [DllImport("user32.dll")]
        public static extern nint SetCursor(nint hCursor);

        [DllImport("user32.dll")]
        public static extern bool MoveWindow(nint hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(nint hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetFocus(nint hWnd);

        [DllImport("user32.dll")]
        public static extern nint GetFocus();

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(nint hWnd, [Out] char[] lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(nint hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool SetWindowText(nint hWnd, string lpString);

        [DllImport("user32.dll")]
        public static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

        [DllImport("user32.dll")]
        public static extern nint SetTimer(nint hWnd, nint nIDEvent, uint uElapse, nint lpTimerFunc);

        [DllImport("user32.dll")]
        public static extern bool KillTimer(nint hWnd, nint uIDEvent);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(nint hWnd);

        [DllImport("user32.dll")]
        public static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle);

        [DllImport("user32.dll")]
        public static extern int SetWindowRgn(nint hWnd, nint hRgn, bool bRedraw);

        // --- Kernel32.dll ---
        [DllImport("kernel32.dll")]
        public static extern nint GetModuleHandle(string? lpModuleName);

        // --- Gdi32.dll ---
        [DllImport("gdi32.dll")]
        public static extern nint GetStockObject(int fnObject);

        public const int WHITE_BRUSH = 0;
        public const int TRANSPARENT = 1;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

        [DllImport("gdi32.dll")]
        public static extern int SetBkMode(nint hdc, int iBkMode);

        [DllImport("gdi32.dll")]
        public static extern uint SetBkColor(nint hdc, int crColor);

        [DllImport("gdi32.dll")]
        public static extern uint SetTextColor(nint hdc, int crColor);

        [DllImport("gdi32.dll")]
        public static extern nint CreateSolidBrush(int crColor);

        [DllImport("gdi32.dll")]
        public static extern nint CreateRectRgn(int x1, int y1, int x2, int y2);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(nint hObject);

        [DllImport("imm32.dll")]
        public static extern nint ImmGetContext(nint hWnd);

        [DllImport("imm32.dll")]
        public static extern bool ImmReleaseContext(nint hWnd, nint hIMC);

        [DllImport("imm32.dll")]
        public static extern int ImmGetCompositionStringW(nint hIMC, uint dwIndex, byte[]? lpBuf, int dwBufLen);

        [DllImport("imm32.dll")]
        public static extern bool ImmSetCompositionWindow(nint hIMC, ref COMPOSITIONFORM lpCompForm);

        [DllImport("imm32.dll")]
        public static extern bool ImmSetCandidateWindow(nint hIMC, ref CANDIDATEFORM lpCandidate);
    }
}
