using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace SM64O
{
    public class WindowPainter : IDisposable
    {
        public WindowPainter(IEmulatorAccessor mem)
        {
            _mem = mem;
            _defaultFont = SystemFonts.DefaultFont;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        private IntPtr _hook;
        private Graphics _g;
        private IEmulatorAccessor _mem;
        private Font _defaultFont;

        public Graphics Graphics
        {
            get { return _g; }
        }

        public bool Attach(string process)
        {
            IntPtr hWnd = Process.GetProcessesByName(process)[0].MainWindowHandle;
            IntPtr hDc = GetDC(hWnd);

            if (hDc.ToInt64() == 0)
                return false;

            _g = Graphics.FromHdc(hDc);
            return true;
        }

        public void Draw()
        {
            var campos = SuperMario64Addresses.GetCameraPosition(_mem);
            var camfoc = SuperMario64Addresses.GetCameraFocalPoint(_mem);
            var playerpos = SuperMario64Addresses.GetPlayerPosition(_mem, 0);

            string hud = string.Format(
                "CamPos: {0} {1} {2}\n" +
                "CamFoc: {3} {4} {5}\n" +
                "Pos   : {6} {7} {8}",
                campos.X, campos.Y, campos.Z,
                camfoc.X, camfoc.Y, camfoc.Z,
                playerpos.X, playerpos.Y, playerpos.Z);


            _g.DrawString(hud, _defaultFont, Brushes.White, 10, 20);
        }

        public void Dispose()
        {
            if (_g != null) _g.Dispose();

            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(this._hook);
            }
        }
    }

    public partial class MainWindow
    {
        IntPtr _hook;
        IntPtr _hwnd;
        NativeMethods.CBTProc _callback;

        public MainWindow()
        {
            // create an instance of the delegate that
            // won't be garbage collected to avoid:
            //   Managed Debugging Assistant 'CallbackOnCollectedDelegate' :** 
            //   'A callback was made on a garbage collected delegate of type 
            //   'WpfApp1!WpfApp1.MainWindow+NativeMethods+CBTProc::Invoke'. 
            //   This may cause application crashes, corruption and data loss. 
            //   When passing delegates to unmanaged code, they must be 
            //   kept alive by the managed application until it is guaranteed 
            //   that they will never be called.'
            _callback = this.CallBack;
            _hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.HookType.WH_MOUSE,
                _callback,
                instancePtr: IntPtr.Zero,
                threadID: NativeMethods.GetCurrentThreadId());
        }
        
        private IntPtr CallBack(int code, IntPtr wParam, IntPtr lParam)
        {
            return NativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }
    }

    public static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEHOOKSTRUCT
        {
            public POINT pt; // Can't use System.Windows.Point because that has X,Y as doubles, not integer
            public IntPtr hwnd;
            public uint wHitTestCode;
            public IntPtr dwExtraInfo;
            public override string ToString()
            {
                return $"({pt.X,4},{pt.Y,4})";
            }
        }

#pragma warning disable 649 // CS0649: Field 'MainWindow.NativeMethods.POINT.Y' is never assigned to, and will always have its default value 0
        public struct POINT
        {
            public int X;
            public int Y;
        }
#pragma warning restore 649

        // from WinUser.h
        public enum HookType
        {
            WH_MIN = (-1),
            WH_MSGFILTER = (-1),
            WH_JOURNALRECORD = 0,
            WH_JOURNALPLAYBACK = 1,
            WH_KEYBOARD = 2,
            WH_GETMESSAGE = 3,
            WH_CALLWNDPROC = 4,
            WH_CBT = 5,
            WH_SYSMSGFILTER = 6,
            WH_MOUSE = 7,
            WH_HARDWARE = 8,
            WH_DEBUG = 9,
            WH_SHELL = 10,
            WH_FOREGROUNDIDLE = 11,
            WH_CALLWNDPROCRET = 12,
            WH_KEYBOARD_LL = 13,
            WH_MOUSE_LL = 14
        }
        public enum HookCodes
        {
            HC_ACTION = 0,
            HC_GETNEXT = 1,
            HC_SKIP = 2,
            HC_NOREMOVE = 3,
            HC_NOREM = HC_NOREMOVE,
            HC_SYSMODALON = 4,
            HC_SYSMODALOFF = 5
        }
        public enum CBTHookCodes
        {
            HCBT_MOVESIZE = 0,
            HCBT_MINMAX = 1,
            HCBT_QS = 2,
            HCBT_CREATEWND = 3,
            HCBT_DESTROYWND = 4,
            HCBT_ACTIVATE = 5,
            HCBT_CLICKSKIPPED = 6,
            HCBT_KEYSKIPPED = 7,
            HCBT_SYSCOMMAND = 8,
            HCBT_SETFOCUS = 9
        }

        public delegate IntPtr CBTProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hookPtr);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hookPtr, int nCode, IntPtr wordParam, IntPtr longParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowsHookEx(HookType hookType, CBTProc hookProc, IntPtr instancePtr, uint threadID);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
    }
}