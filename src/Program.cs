using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ProStudioEdge
{
    static class NativeMethods
    {
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int WM_GETMINMAXINFO = 0x0024;
        public const int WM_COPYDATA = 0x004A;
        public const int WM_SETICON = 0x0080;

        public const int HTCAPTION = 2;
        public const int HTTRANSPARENT = -1;
        public const int HTCLIENT = 1;
        public const int HTLEFT = 10;
        public const int HTRIGHT = 11;
        public const int HTTOP = 12;
        public const int HTTOPLEFT = 13;
        public const int HTTOPRIGHT = 14;
        public const int HTBOTTOM = 15;
        public const int HTBOTTOMLEFT = 16;
        public const int HTBOTTOMRIGHT = 17;

        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_BORDER_COLOR = 34;
        public const int DWMWCP_DONOTROUND = 1;
        public const int DWMWCP_ROUND = 2;

        public const uint IMAGE_ICON = 1;
        public const uint LR_LOADFROMFILE = 0x0010;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        public static readonly IntPtr COPYDATA_MAGIC = new IntPtr(0x50534531);

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
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForSystem();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        public static void EnableHighDpi()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch { }
            try
            {
                SetProcessDPIAware();
            }
            catch { }
        }

        public static float QuerySystemDpiScale()
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi >= 96 && dpi <= 480) return dpi / 96f;
            }
            catch { }
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    if (g.DpiX >= 96f) return g.DpiX / 96f;
                }
            }
            catch { }
            return 1f;
        }

        public static void ApplyWin11Chrome(IntPtr hwnd, bool rounded)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int one = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref one, 4);
            }
            catch { }
            try
            {
                int pref = rounded ? DWMWCP_ROUND : DWMWCP_DONOTROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4);
            }
            catch { }
            try
            {
                // #1E2A38 in COLORREF (0x00BBGGRR)
                int borderColor = 0x00382A1E;
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, 4);
            }
            catch { }
        }

        public static IntPtr FindExistingInstanceWindow()
        {
            int currentPid = Process.GetCurrentProcess().Id;
            var targetPids = new HashSet<uint>();
            try
            {
                var procs = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
                foreach (var p in procs)
                {
                    if (p.Id != currentPid) targetPids.Add((uint)p.Id);
                    p.Dispose();
                }
            }
            catch { }

            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (targetPids.Contains(pid))
                {
                    var sb = new StringBuilder(128);
                    GetWindowText(h, sb, sb.Capacity);
                    if (sb.ToString() == "Pro Studio")
                    {
                        found = h;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static void NotifyExistingInstance(IntPtr hwnd, string url)
        {
            if (hwnd == IntPtr.Zero) return;
            string payload = url ?? "";
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            IntPtr mem = IntPtr.Zero;
            try
            {
                if (bytes.Length > 0)
                {
                    mem = Marshal.AllocHGlobal(bytes.Length);
                    Marshal.Copy(bytes, 0, mem, bytes.Length);
                }
                var cds = new COPYDATASTRUCT();
                cds.dwData = COPYDATA_MAGIC;
                cds.cbData = bytes.Length;
                cds.lpData = mem;

                IntPtr cdsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(cds));
                try
                {
                    Marshal.StructureToPtr(cds, cdsPtr, false);
                    SendMessage(hwnd, WM_COPYDATA, IntPtr.Zero, cdsPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(cdsPtr);
                }
            }
            catch { }
            finally
            {
                if (mem != IntPtr.Zero) Marshal.FreeHGlobal(mem);
            }
            try
            {
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
            catch { }
        }
    }

    static class Theme
    {
        public static float Scale = 1f;

        public static int S(int px)
        {
            return (int)Math.Round(px * Scale);
        }

        public static float Sf(float px)
        {
            return px * Scale;
        }

        public static Color Darken(Color c, float amount)
        {
            float f = 1f - amount;
            int r = Math.Max(0, (int)Math.Floor(c.R * f) - (c.R > 4 ? 2 : 0));
            int g = Math.Max(0, (int)Math.Floor(c.G * f) - (c.G > 4 ? 2 : 0));
            int b = Math.Max(0, (int)Math.Floor(c.B * f) - (c.B > 4 ? 2 : 0));
            return Color.FromArgb(c.A, r, g, b);
        }

        public static Color WithAlpha(Color c, float alphaRatio)
        {
            int a = Math.Max(0, Math.Min(255, (int)Math.Round(255f * alphaRatio)));
            return Color.FromArgb(a, c.R, c.G, c.B);
        }

        public static readonly Color Bg = Color.FromArgb(11, 15, 20);           // #0B0F14
        public static readonly Color TitleBar = Color.FromArgb(13, 19, 26);     // #0D131A
        public static readonly Color Side = Color.FromArgb(15, 23, 32);         // #0F1720
        public static readonly Color Bar = Color.FromArgb(15, 22, 30);          // #0F161E
        public static readonly Color Line = Color.FromArgb(30, 42, 56);         // #1E2A38
        public static readonly Color LineSubtle = Color.FromArgb(24, 36, 49);   // #182431
        public static readonly Color Field = Color.FromArgb(12, 20, 28);        // #0C141C
        public static readonly Color Fg = Color.FromArgb(232, 238, 246);        // #E8EEF6
        public static readonly Color FgSoft = Color.FromArgb(198, 211, 226);    // #C6D3E2
        public static readonly Color Muted = Color.FromArgb(134, 152, 171);     // #8698AB
        public static readonly Color SectionFg = Color.FromArgb(104, 122, 142); // #687A8E
        public static readonly Color Accent = Color.FromArgb(124, 196, 255);    // #7CC4FF
        public static readonly Color AccentViolet = Color.FromArgb(148, 126, 248);

        public static readonly Color NavHover = Color.FromArgb(25, 39, 54);
        public static readonly Color NavHoverLine = Color.FromArgb(52, 79, 108);
        public static readonly Color NavActive = Color.FromArgb(19, 41, 63);
        public static readonly Color NavActiveLine = Color.FromArgb(46, 98, 144);

        public static readonly Color BtnFill = Color.FromArgb(20, 30, 41);
        public static readonly Color BtnHover = Color.FromArgb(30, 46, 63);
        public static readonly Color BtnHoverLine = Color.FromArgb(66, 100, 134);

        public static readonly Color CapsuleFill = Color.FromArgb(20, 38, 56);
        public static readonly Color CapsuleHover = Color.FromArgb(29, 56, 82);
        public static readonly Color CapsuleLine = Color.FromArgb(44, 86, 124);
        public static readonly Color CapsuleHoverLine = Color.FromArgb(90, 158, 218);

        public static readonly Color CloseHover = Color.FromArgb(232, 17, 35);  // #E81123
        public static readonly Color ClosePress = Color.FromArgb(196, 15, 31);

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float maxR = Math.Min(r.Width, r.Height) / 2f;
            float rad = Math.Max(0f, Math.Min(radius, maxR));
            float d = rad * 2f;
            if (d <= 0.5f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        public static void DrawBrandBadge(Graphics g, RectangleF rect, float radius, float fontPixelSize)
        {
            // dark rounded plate
            using (var path = RoundRect(rect, radius))
            using (var plate = new SolidBrush(Color.FromArgb(11, 15, 20)))
                g.FillPath(plate, path);

            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;
            float R = rect.Width * 0.40f;
            Color[] cols = new Color[] {
                Color.FromArgb(234, 67, 53),   // top red
                Color.FromArgb(251, 188, 5),   // right yellow
                Color.FromArgb(52, 168, 83),   // bottom green
                Color.FromArgb(66, 133, 244)   // left blue
            };
            float[] angs = new float[] { 0f, 90f, 180f, 270f };
            for (int k = 0; k < 4; k++)
            {
                var st = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(angs[k]);
                using (var b = new SolidBrush(cols[k]))
                    g.FillEllipse(b, -R * 0.30f, -R * 1.00f, R * 0.60f, R * 1.00f);
                g.Restore(st);
            }
        }
    }

    static class AppIconHelper
    {
        public static string IconFilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "ProStudioEdge", "app.ico");
            }
        }

        static Bitmap RenderBadgeBitmap(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                float pad = Math.Max(0.5f, size * 0.04f);
                var rect = new RectangleF(pad, pad, size - pad * 2f, size - pad * 2f);
                float rad = rect.Width * 0.28f;
                Theme.DrawBrandBadge(g, rect, rad, rect.Width * 0.58f);
            }
            return bmp;
        }

        public static void EnsureMultiSizeIcoFile()
        {
            try
            {
                string path = IconFilePath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(path))
                {
                    using (var st = typeof(AppIconHelper).Assembly.GetManifestResourceStream("app.ico"))
                    {
                        if (st != null) { using (var fs = File.Create(path)) st.CopyTo(fs); }
                    }
                }
                if (File.Exists(path) && new FileInfo(path).Length > 8000) return;

                int[] sizes = new int[] { 16, 32, 48, 256 };
                var images = new List<byte[]>(sizes.Length);
                foreach (int sz in sizes)
                {
                    using (var bmp = RenderBadgeBitmap(sz))
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        images.Add(ms.ToArray());
                    }
                }

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var bw = new BinaryWriter(fs))
                {
                    // ICONDIR header (6 bytes)
                    bw.Write((ushort)0);
                    bw.Write((ushort)1); // 1 = ICO
                    bw.Write((ushort)sizes.Length);

                    int offset = 6 + 16 * sizes.Length;
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        byte dim = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
                        bw.Write(dim);                  // bWidth
                        bw.Write(dim);                  // bHeight
                        bw.Write((byte)0);              // bColorCount
                        bw.Write((byte)0);              // bReserved
                        bw.Write((ushort)1);            // wPlanes
                        bw.Write((ushort)32);           // wBitCount
                        bw.Write(images[i].Length);     // dwBytesInRes
                        bw.Write(offset);               // dwImageOffset
                        offset += images[i].Length;
                    }
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        bw.Write(images[i]);
                    }
                }
            }
            catch { }
        }

        public static Icon CreateRuntimeIcon(int size)
        {
            using (var bmp = RenderBadgeBitmap(size))
            {
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (var tmp = Icon.FromHandle(hIcon))
                        return (Icon)tmp.Clone();
                }
                finally
                {
                    NativeMethods.DestroyIcon(hIcon);
                }
            }
        }

        public static void ApplyHighResWindowIcons(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                if (File.Exists(IconFilePath))
                {
                    IntPtr hBig = NativeMethods.LoadImage(IntPtr.Zero, IconFilePath, NativeMethods.IMAGE_ICON, 48, 48, NativeMethods.LR_LOADFROMFILE);
                    IntPtr hSmall = NativeMethods.LoadImage(IntPtr.Zero, IconFilePath, NativeMethods.IMAGE_ICON, 16, 16, NativeMethods.LR_LOADFROMFILE);
                    if (hBig != IntPtr.Zero) NativeMethods.SendMessage(hwnd, NativeMethods.WM_SETICON, new IntPtr(NativeMethods.ICON_BIG), hBig);
                    if (hSmall != IntPtr.Zero) NativeMethods.SendMessage(hwnd, NativeMethods.WM_SETICON, new IntPtr(NativeMethods.ICON_SMALL), hSmall);
                }
            }
            catch { }
        }
    }

    class BufferedPanel : Panel
    {
        public bool EdgeResizePassThrough;

        public BufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void WndProc(ref Message m)
        {
            if (EdgeResizePassThrough && m.Msg == NativeMethods.WM_NCHITTEST)
            {
                var mf = FindForm() as MainForm;
                if (mf != null && mf.IsOnResizeEdge(m.LParam))
                {
                    m.Result = new IntPtr(NativeMethods.HTTRANSPARENT);
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }

    class NavScrollPanel : FlowLayoutPanel
    {
        public NavScrollPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_NCHITTEST)
            {
                var mf = FindForm() as MainForm;
                if (mf != null && mf.IsOnResizeEdge(m.LParam))
                {
                    m.Result = new IntPtr(NativeMethods.HTTRANSPARENT);
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }

    class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(Theme.Sf(4f), Theme.Sf(2f), e.Item.Width - Theme.Sf(8f), e.Item.Height - Theme.Sf(4f));
            if (e.Item.Selected)
            {
                using (var p = Theme.RoundRect(rect, Theme.Sf(6f)))
                using (var b = new SolidBrush(Theme.NavHover))
                    g.FillPath(b, p);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Theme.Fg : Theme.FgSoft;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(Theme.Line, 1f))
                e.Graphics.DrawRectangle(pen, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(Theme.Line, 1f))
                e.Graphics.DrawLine(pen, Theme.S(10), y, e.Item.Width - Theme.S(10), y);
        }

        class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.Side; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Side; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Side; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Side; } }
            public override Color MenuBorder { get { return Theme.Line; } }
            public override Color MenuItemBorder { get { return Color.Transparent; } }
        }
    }

    enum CaptionKind { Minimize, Maximize, Close }

    class CaptionButton : Control
    {
        public CaptionKind Kind;
        public bool IsMaximizedState;
        bool hover;
        bool pressed;

        public CaptionButton(CaptionKind kind)
        {
            Kind = kind;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            BackColor = Theme.TitleBar;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_NCHITTEST)
            {
                var mf = FindForm() as MainForm;
                if (mf != null && mf.IsOnTopRightResizeEdge(m.LParam))
                {
                    m.Result = new IntPtr(NativeMethods.HTTRANSPARENT);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (pressed) { pressed = false; Invalidate(); } base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color bg = Theme.TitleBar;
            if (Kind == CaptionKind.Close)
            {
                if (pressed) bg = Theme.ClosePress;
                else if (hover) bg = Theme.CloseHover;
            }
            else
            {
                if (pressed) bg = Theme.Darken(Theme.BtnHover, 0.05f);
                else if (hover) bg = Color.FromArgb(30, 44, 60);
            }
            g.Clear(bg);

            Color fg = (Kind == CaptionKind.Close && (hover || pressed))
                ? Color.White
                : (hover ? Theme.Fg : Theme.Muted);

            float cx = Width / 2f;
            float cy = Height / 2f;
            float stroke = Math.Max(1.1f, Theme.Sf(1.15f));

            using (var pen = new Pen(fg, stroke))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (Kind == CaptionKind.Minimize)
                {
                    float w = Theme.Sf(10f);
                    g.DrawLine(pen, cx - w / 2f, cy + Theme.Sf(0.5f), cx + w / 2f, cy + Theme.Sf(0.5f));
                }
                else if (Kind == CaptionKind.Maximize)
                {
                    if (IsMaximizedState)
                    {
                        float sz = Theme.Sf(8f);
                        float off = Theme.Sf(2f);
                        // Back square (top-right offset)
                        g.DrawRectangle(pen, cx - sz / 2f + off, cy - sz / 2f - off, sz, sz);
                        // Front square filled with bg to occlude back
                        var front = new RectangleF(cx - sz / 2f - Theme.Sf(0.5f), cy - sz / 2f + Theme.Sf(0.5f), sz, sz);
                        using (var b = new SolidBrush(bg)) g.FillRectangle(b, front);
                        g.DrawRectangle(pen, front.X, front.Y, front.Width, front.Height);
                    }
                    else
                    {
                        float sz = Theme.Sf(9.5f);
                        g.DrawRectangle(pen, cx - sz / 2f, cy - sz / 2f, sz, sz);
                    }
                }
                else
                {
                    float d = Theme.Sf(4.5f);
                    g.DrawLine(pen, cx - d, cy - d, cx + d, cy + d);
                    g.DrawLine(pen, cx + d, cy - d, cx - d, cy + d);
                }
            }
        }
    }

    class CustomTitleBar : BufferedPanel
    {
        public readonly CaptionButton MinBtn;
        public readonly CaptionButton MaxBtn;
        public readonly CaptionButton CloseBtn;

        public event EventHandler MinimizeClicked;
        public event EventHandler MaximizeToggleClicked;
        public event EventHandler CloseClicked;

        public CustomTitleBar()
        {
            EdgeResizePassThrough = true;
            BackColor = Theme.TitleBar;
            Height = Theme.S(40);
            Dock = DockStyle.Top;

            MinBtn = new CaptionButton(CaptionKind.Minimize);
            MaxBtn = new CaptionButton(CaptionKind.Maximize);
            CloseBtn = new CaptionButton(CaptionKind.Close);

            MinBtn.Click += (s, e) => { if (MinimizeClicked != null) MinimizeClicked(this, EventArgs.Empty); };
            MaxBtn.Click += (s, e) => { if (MaximizeToggleClicked != null) MaximizeToggleClicked(this, EventArgs.Empty); };
            CloseBtn.Click += (s, e) => { if (CloseClicked != null) CloseClicked(this, EventArgs.Empty); };

            Controls.Add(MinBtn);
            Controls.Add(MaxBtn);
            Controls.Add(CloseBtn);

            Resize += (s, e) => LayoutButtons();
            LayoutButtons();
        }

        public void SetMaximized(bool maximized)
        {
            MaxBtn.IsMaximizedState = maximized;
            MaxBtn.Invalidate();
        }

        void LayoutButtons()
        {
            int btnW = Theme.S(46);
            int btnH = Height - 1;
            CloseBtn.Bounds = new Rectangle(Width - btnW, 0, btnW, btnH);
            MaxBtn.Bounds = new Rectangle(Width - btnW * 2, 0, btnW, btnH);
            MinBtn.Bounds = new Rectangle(Width - btnW * 3, 0, btnW, btnH);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (e.Clicks == 2)
            {
                if (MaximizeToggleClicked != null) MaximizeToggleClicked(this, EventArgs.Empty);
                return;
            }
            var form = FindForm();
            if (form != null)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(form.Handle, NativeMethods.WM_NCLBUTTONDOWN, new IntPtr(NativeMethods.HTCAPTION), IntPtr.Zero);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Theme.TitleBar);

            // 20x20 gradient P logo
            float logoSz = Theme.Sf(20f);
            float logoY = (Height - 1 - logoSz) / 2f;
            var logoRect = new RectangleF(Theme.Sf(14f), logoY, logoSz, logoSz);
            Theme.DrawBrandBadge(g, logoRect, Theme.Sf(5.5f), Theme.Sf(12f));

            using (var f = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold))
            {
                int textX = Theme.S(42);
                int maxW = Math.Max(Theme.S(80), MinBtn.Left - textX - Theme.S(12));
                TextRenderer.DrawText(g, "Pro Studio", f,
                    new Rectangle(textX, 0, maxW, Height - 1),
                    Theme.Fg, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }

            using (var pen = new Pen(Theme.Line, 1f))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    class SplashForm : Form
    {
        readonly System.Windows.Forms.Timer animTimer = new System.Windows.Forms.Timer();
        float phase = -0.35f;

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(Theme.S(420), Theme.S(220));
            BackColor = Theme.Side;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;

            HandleCreated += (s, e) => NativeMethods.ApplyWin11Chrome(Handle, true);

            animTimer.Interval = 16;
            animTimer.Tick += (s, e) =>
            {
                phase += 0.032f;
                if (phase > 1.35f) phase = -0.35f;
                Invalidate();
            };
            animTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) animTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Theme.Side);

            // Subtle ambient top glow
            using (var glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(Width / 2f - Theme.Sf(140f), -Theme.Sf(60f), Theme.Sf(280f), Theme.Sf(170f));
                using (var pgb = new PathGradientBrush(glowPath))
                {
                    pgb.CenterColor = Color.FromArgb(34, 124, 196, 255);
                    pgb.SurroundColors = new Color[] { Color.FromArgb(0, 15, 23, 32) };
                    g.FillPath(pgb, glowPath);
                }
            }

            // 54x54 gradient P badge
            float badgeSz = Theme.Sf(54f);
            var badgeRect = new RectangleF((Width - badgeSz) / 2f, Theme.Sf(36f), badgeSz, badgeSz);
            Theme.DrawBrandBadge(g, badgeRect, Theme.Sf(15f), Theme.Sf(31f));

            using (var fTitle = new Font("Microsoft JhengHei UI", 15f, FontStyle.Bold))
                TextRenderer.DrawText(g, "Pro Studio", fTitle,
                    new Rectangle(0, Theme.S(102), Width, Theme.S(32)),
                    Theme.Fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            using (var fSub = new Font("Microsoft JhengHei UI", 9.25f))
                TextRenderer.DrawText(g, "正在啟動…", fSub,
                    new Rectangle(0, Theme.S(136), Width, Theme.S(24)),
                    Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            // Sleek animated progress bar
            float barW = Theme.Sf(180f);
            float barH = Math.Max(3f, Theme.Sf(3f));
            float barX = (Width - barW) / 2f;
            float barY = Theme.Sf(176f);
            var trackRect = new RectangleF(barX, barY, barW, barH);
            using (var trackPath = Theme.RoundRect(trackRect, barH / 2f))
            {
                using (var b = new SolidBrush(Color.FromArgb(26, 38, 52)))
                    g.FillPath(b, trackPath);

                var prevClip = g.Clip;
                g.SetClip(trackPath);
                float segW = barW * 0.42f;
                float segX = barX + phase * barW;
                var segRect = new RectangleF(segX, barY, segW, barH);
                using (var grad = new LinearGradientBrush(segRect, Theme.Accent, Theme.AccentViolet, 0f))
                    g.FillRectangle(grad, segRect);
                g.Clip = prevClip;
            }

            // Crisp outer border
            using (var pen = new Pen(Theme.Line, 1f))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    class SectionHeader : Control
    {
        public bool TopDivider;

        public SectionHeader(string title, bool topDivider)
        {
            Text = title;
            TopDivider = topDivider;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Side;
            Font = new Font("Microsoft JhengHei UI", 8.25f, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Side);
            int textTop = Theme.S(3);
            if (TopDivider)
            {
                using (var pen = new Pen(Theme.LineSubtle, 1f))
                    g.DrawLine(pen, Theme.S(4), Theme.S(4), Width - Theme.S(4), Theme.S(4));
                textTop = Theme.S(11);
            }
            var rect = new Rectangle(Theme.S(8), textTop, Width - Theme.S(12), Height - textTop);
            TextRenderer.DrawText(g, Text, Font, rect, Theme.SectionFg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    class AccountStatusLabel : Control
    {
        string statusText = "目前已選：帳號 1";

        public string StatusText
        {
            get { return statusText; }
            set
            {
                if (statusText != value)
                {
                    statusText = value;
                    Invalidate();
                }
            }
        }

        public AccountStatusLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Side;
            Font = new Font("Microsoft JhengHei UI", 8f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Side);

            float dot = Theme.Sf(5.5f);
            using (var b = new SolidBrush(Theme.Accent))
                g.FillEllipse(b, Theme.Sf(4f), (Height - dot) / 2f, dot, dot);

            int textX = Theme.S(15);
            var r = new Rectangle(textX, 0, Width - textX, Height);
            TextRenderer.DrawText(g, statusText, Font, r, Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    enum FlatButtonStyle
    {
        SidebarNav,
        TopIcon,
        Capsule,
        AccountChip
    }

    class FlatButton : Control
    {
        public bool Active;
        public string Glyph = "";
        public float GlyphSize = 10.5f;
        public string GlyphFontName = "Segoe UI Symbol";
        public int Radius = 9;
        public FlatButtonStyle Style = FlatButtonStyle.SidebarNav;
        public bool AccentTint;

        bool dimmed;
        bool hover;
        bool pressed;
        float spinAngle;
        float flashIntensity;
        readonly System.Windows.Forms.Timer animTimer = new System.Windows.Forms.Timer();

        public bool Dimmed
        {
            get { return dimmed; }
            set
            {
                if (dimmed != value)
                {
                    dimmed = value;
                    Cursor = dimmed ? Cursors.Default : Cursors.Hand;
                    Invalidate();
                }
            }
        }

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft JhengHei UI", 9.5f);
            animTimer.Interval = 16;
            animTimer.Tick += OnAnimTick;
        }

        public void TriggerSpinFeedback()
        {
            spinAngle = 0f;
            flashIntensity = 1f;
            animTimer.Start();
            Invalidate();
        }

        void OnAnimTick(object sender, EventArgs e)
        {
            spinAngle += 18f;
            flashIntensity = Math.Max(0f, flashIntensity - 0.055f);
            if (spinAngle >= 360f && flashIntensity <= 0f)
            {
                spinAngle = 0f;
                flashIntensity = 0f;
                animTimer.Stop();
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) animTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left && !dimmed) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (pressed) { pressed = false; Invalidate(); } base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            Color parentBg = Parent != null ? Parent.BackColor : Theme.Side;
            g.Clear(parentBg);

            float inset = 0.5f;
            var rect = new RectangleF(inset, inset, Width - inset * 2f - 1f, Height - inset * 2f - 1f);
            float r = Style == FlatButtonStyle.Capsule ? rect.Height / 2f : Theme.Sf(Radius);

            Color fill = parentBg;
            Color border = Color.Transparent;
            bool drawFill = false;
            bool drawBorder = false;

            if (Style == FlatButtonStyle.SidebarNav)
            {
                if (Active)
                {
                    fill = pressed ? Theme.Darken(Theme.NavActive, 0.04f) : Theme.NavActive;
                    border = hover ? Theme.CapsuleHoverLine : Theme.NavActiveLine;
                    drawFill = true;
                    drawBorder = true;
                }
                else if (pressed)
                {
                    fill = Theme.Darken(Theme.NavHover, 0.05f);
                    border = Theme.Darken(Theme.NavHoverLine, 0.05f);
                    drawFill = true;
                    drawBorder = true;
                }
                else if (hover)
                {
                    fill = Theme.NavHover;
                    border = Theme.NavHoverLine;
                    drawFill = true;
                    drawBorder = true;
                }
            }
            else if (Style == FlatButtonStyle.TopIcon)
            {
                if (dimmed)
                {
                    fill = Theme.WithAlpha(Theme.BtnFill, 0.30f);
                    border = Theme.WithAlpha(Theme.Line, 0.30f);
                }
                else
                {
                    Color baseFill = hover ? Theme.BtnHover : Theme.BtnFill;
                    fill = pressed ? Theme.Darken(baseFill, 0.04f) : baseFill;
                    if (flashIntensity > 0f)
                    {
                        border = Color.FromArgb(
                            (int)(Theme.BtnHoverLine.R + (Theme.Accent.R - Theme.BtnHoverLine.R) * flashIntensity),
                            (int)(Theme.BtnHoverLine.G + (Theme.Accent.G - Theme.BtnHoverLine.G) * flashIntensity),
                            (int)(Theme.BtnHoverLine.B + (Theme.Accent.B - Theme.BtnHoverLine.B) * flashIntensity));
                    }
                    else
                    {
                        border = pressed ? Theme.Darken(Theme.BtnHoverLine, 0.04f) : (hover ? Theme.BtnHoverLine : Theme.Line);
                    }
                }
                drawFill = true;
                drawBorder = true;
            }
            else if (Style == FlatButtonStyle.Capsule)
            {
                Color baseFill = hover ? Theme.CapsuleHover : Theme.CapsuleFill;
                fill = pressed ? Theme.Darken(baseFill, 0.04f) : baseFill;
                border = pressed ? Theme.Darken(Theme.CapsuleHoverLine, 0.04f) : (hover ? Theme.CapsuleHoverLine : Theme.CapsuleLine);
                drawFill = true;
                drawBorder = true;
            }
            else if (Style == FlatButtonStyle.AccountChip)
            {
                if (Active)
                {
                    fill = pressed ? Theme.Darken(Theme.NavActive, 0.04f) : Theme.NavActive;
                    border = Theme.Accent;
                }
                else if (AccentTint)
                {
                    Color baseFill = hover ? Theme.CapsuleHover : Theme.CapsuleFill;
                    fill = pressed ? Theme.Darken(baseFill, 0.04f) : baseFill;
                    border = hover ? Theme.CapsuleHoverLine : Theme.CapsuleLine;
                }
                else
                {
                    Color baseFill = hover ? Theme.BtnHover : Theme.BtnFill;
                    fill = pressed ? Theme.Darken(baseFill, 0.04f) : baseFill;
                    border = hover ? Theme.BtnHoverLine : Theme.Line;
                }
                drawFill = true;
                drawBorder = true;
            }

            using (var path = Theme.RoundRect(rect, r))
            {
                if (drawFill)
                {
                    using (var b = new SolidBrush(fill))
                        g.FillPath(b, path);
                }
                if (drawBorder)
                {
                    using (var pen = new Pen(border, 1f))
                        g.DrawPath(pen, path);
                }
            }

            if (Style == FlatButtonStyle.SidebarNav && Active)
            {
                float barW = Math.Max(3f, Theme.Sf(3f));
                float barH = Theme.Sf(20f);
                var barRect = new RectangleF(Theme.Sf(2f), (Height - barH) / 2f, barW, barH);
                using (var barPath = Theme.RoundRect(barRect, barW / 2f))
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillPath(b, barPath);
            }

            if (Style == FlatButtonStyle.SidebarNav)
            {
                Color textColor = Active ? Color.FromArgb(242, 248, 255) : (hover ? Theme.Fg : Theme.FgSoft);
                Color glyphColor = Active ? Theme.Accent : (hover ? Color.FromArgb(212, 232, 252) : Theme.Muted);

                if (Glyph.Length > 0)
                {
                    var iconRect = new RectangleF(Theme.Sf(12f), 0f, Theme.Sf(20f), Height);
                    using (var f = new Font(GlyphFontName, GlyphSize, FontStyle.Regular))
                    using (var b = new SolidBrush(glyphColor))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(Glyph, f, b, iconRect, sf);
                }

                int textLeft = Theme.S(38);
                var textRect = new Rectangle(textLeft, 0, Width - textLeft - Theme.S(10), Height);
                TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            else if (Style == FlatButtonStyle.TopIcon)
            {
                Color glyphColor;
                if (dimmed) glyphColor = Theme.WithAlpha(Theme.Fg, 0.30f);
                else if (flashIntensity > 0f) glyphColor = Theme.Accent;
                else glyphColor = hover ? Theme.Fg : Color.FromArgb(190, 206, 222);

                float yOff = (Glyph == "\u2039" || Glyph == "\u203A") ? -Theme.Sf(1.2f) : Theme.Sf(0.4f);
                var state = g.Save();
                if (spinAngle > 0.1f)
                {
                    g.TranslateTransform(Width / 2f, Height / 2f);
                    g.RotateTransform(spinAngle);
                    g.TranslateTransform(-Width / 2f, -Height / 2f);
                }
                var iconRect = new RectangleF(0f, yOff, Width, Height);
                using (var f = new Font(GlyphFontName, GlyphSize, FontStyle.Regular))
                using (var b = new SolidBrush(glyphColor))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(Glyph, f, b, iconRect, sf);
                g.Restore(state);
            }
            else if (Style == FlatButtonStyle.Capsule)
            {
                Color textColor = hover ? Color.FromArgb(246, 250, 255) : Theme.Fg;
                Color glyphColor = hover ? Theme.Accent : Color.FromArgb(148, 206, 255);
                if (Glyph.Length > 0)
                {
                    int iconW = Theme.S(16);
                    var iconRect = new RectangleF(Theme.Sf(11f), 0f, iconW, Height);
                    using (var f = new Font(GlyphFontName, GlyphSize, FontStyle.Regular))
                    using (var b = new SolidBrush(glyphColor))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(Glyph, f, b, iconRect, sf);

                    int tx = Theme.S(27);
                    var textRect = new Rectangle(tx, 0, Width - tx - Theme.S(10), Height);
                    TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
                }
                else
                {
                    var textRect = new Rectangle(Theme.S(8), 0, Width - Theme.S(16), Height);
                    TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
                }
            }
            else if (Style == FlatButtonStyle.AccountChip)
            {
                Color textColor;
                if (Active) textColor = Theme.Accent;
                else if (AccentTint) textColor = hover ? Theme.Fg : Color.FromArgb(172, 214, 250);
                else textColor = hover ? Theme.Fg : Theme.Muted;

                var textRect = new Rectangle(Theme.S(2), 0, Width - Theme.S(4), Height);
                TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    class UrlPill : Control
    {
        bool hover;
        bool pressed;
        bool loading;
        float spinnerAngle;
        string displayOverride;
        readonly System.Windows.Forms.Timer flashTimer = new System.Windows.Forms.Timer();

        public bool Loading
        {
            get { return loading; }
            set
            {
                if (loading != value)
                {
                    loading = value;
                    Invalidate();
                }
            }
        }

        public void StepSpinner(float deltaDeg)
        {
            spinnerAngle = (spinnerAngle + deltaDeg) % 360f;
            if (loading) Invalidate();
        }

        public UrlPill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Font = new Font("Segoe UI", 8.75f);
            Cursor = Cursors.Hand;
            flashTimer.Interval = 1200;
            flashTimer.Tick += (s, e) =>
            {
                flashTimer.Stop();
                displayOverride = null;
                Invalidate();
            };
        }

        public void FocusAndCopy()
        {
            Focus();
            CopyUrlWithFeedback();
        }

        void CopyUrlWithFeedback()
        {
            if (!string.IsNullOrEmpty(Text))
            {
                try
                {
                    Clipboard.SetText(Text);
                    displayOverride = "已複製網址 — " + Text;
                    flashTimer.Stop();
                    flashTimer.Start();
                    Invalidate();
                }
                catch { }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) flashTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (pressed) { pressed = false; Invalidate(); } base.OnMouseUp(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            CopyUrlWithFeedback();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Theme.Bar);

            var r = new RectangleF(0.5f, 0.5f, Width - 2f, Height - 2f);
            float rad = r.Height / 2f;
            Color fill = pressed ? Theme.Darken(Theme.Field, 0.04f) : Theme.Field;
            Color border = Focused ? Theme.Accent : (hover ? Theme.BtnHoverLine : Theme.Line);
            using (var p = Theme.RoundRect(r, rad))
            {
                using (var b = new SolidBrush(fill))
                    g.FillPath(b, p);
                using (var pen = new Pen(border, 1f))
                    g.DrawPath(pen, p);
            }

            if (loading)
            {
                float sz = Theme.Sf(10f);
                var spRect = new RectangleF(Theme.Sf(11f), (Height - sz) / 2f, sz, sz);
                using (var trackPen = new Pen(Color.FromArgb(40, 124, 196, 255), Theme.Sf(1.6f)))
                    g.DrawEllipse(trackPen, spRect);
                using (var arcPen = new Pen(Theme.Accent, Theme.Sf(1.6f)))
                {
                    arcPen.StartCap = LineCap.Round;
                    arcPen.EndCap = LineCap.Round;
                    g.DrawArc(arcPen, spRect, spinnerAngle, 115f);
                }
            }
            else
            {
                float dotSize = Theme.Sf(6f);
                Color dotColor = displayOverride != null ? Theme.Accent :
                                 (Text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                                     ? Color.FromArgb(74, 184, 146)
                                     : Theme.Muted);
                using (var b = new SolidBrush(dotColor))
                    g.FillEllipse(b, Theme.Sf(13f), (Height - dotSize) / 2f, dotSize, dotSize);
            }

            string shown = displayOverride != null ? displayOverride : Text;
            Color fg = displayOverride != null ? Theme.Accent : ((Focused || hover) ? Theme.FgSoft : Theme.Muted);
            int left = Theme.S(27);
            var textRect = new Rectangle(left, 0, Width - left - Theme.S(14), Height);
            TextRenderer.DrawText(g, shown, Font, textRect, fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    static class Boot
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        internal static void Init()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                string simple = new System.Reflection.AssemblyName(e.Name).Name;
                if (!simple.StartsWith("Microsoft.Web.WebView2", StringComparison.OrdinalIgnoreCase)) return null;
                using (Stream st = typeof(Boot).Assembly.GetManifestResourceStream(simple + ".dll"))
                {
                    if (st == null) return null;
                    byte[] buf = new byte[st.Length];
                    st.Read(buf, 0, buf.Length);
                    return System.Reflection.Assembly.Load(buf);
                }
            };

            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "ProStudioEdge");
                Directory.CreateDirectory(dir);
                string loader = Path.Combine(dir, "WebView2Loader.dll");
                using (Stream st = typeof(Boot).Assembly.GetManifestResourceStream("WebView2Loader.dll"))
                {
                    if (st != null)
                    {
                        byte[] buf = new byte[st.Length];
                        st.Read(buf, 0, buf.Length);
                        bool needWrite = true;
                        if (File.Exists(loader))
                        {
                            try
                            {
                                byte[] old = File.ReadAllBytes(loader);
                                needWrite = old.Length != buf.Length;
                            }
                            catch { needWrite = true; }
                        }
                        if (needWrite) File.WriteAllBytes(loader, buf);
                    }
                }
                SetDllDirectory(dir);
            }
            catch { }
        }
    }

    static class Program
    {
        internal static string StartUrl = null;

        [STAThread]
        static void Main(string[] args)
        {
            Boot.Init();
            if (args != null && args.Length > 0) StartUrl = args[0];

            Mutex singleMutex = null;
            bool createdNew = false;
            try
            {
                singleMutex = new Mutex(true, "Global\\ProStudioEdge.SingleInstance", out createdNew);
            }
            catch
            {
                try
                {
                    singleMutex = new Mutex(true, "Local\\ProStudioEdge.SingleInstance", out createdNew);
                }
                catch { createdNew = true; }
            }

            if (!createdNew)
            {
                IntPtr existing = NativeMethods.FindExistingInstanceWindow();
                if (existing != IntPtr.Zero)
                    NativeMethods.NotifyExistingInstance(existing, StartUrl);
                if (singleMutex != null) singleMutex.Dispose();
                return;
            }

            try
            {
                NativeMethods.EnableHighDpi();
                Theme.Scale = NativeMethods.QuerySystemDpiScale();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppIconHelper.EnsureMultiSizeIcoFile();
                Application.Run(new MainForm());
            }
            finally
            {
                if (singleMutex != null)
                {
                    try { singleMutex.ReleaseMutex(); } catch { }
                    singleMutex.Dispose();
                }
            }
        }
    }

    class MainForm : Form
    {
        readonly WebView2 web = new WebView2();
        readonly UrlPill url = new UrlPill();
        readonly CustomTitleBar titleBar = new CustomTitleBar();
        readonly BufferedPanel bodyPanel = new BufferedPanel();
        readonly BufferedPanel side = new BufferedPanel();
        readonly BufferedPanel top = new BufferedPanel();
        readonly NavScrollPanel nav = new NavScrollPanel();
        readonly BufferedPanel footer = new BufferedPanel();
        readonly BufferedPanel acctRow = new BufferedPanel();
        readonly AccountStatusLabel acctStatus = new AccountStatusLabel();
        readonly FlatButton[] acctBtns = new FlatButton[3];
        readonly List<FlatButton> navItems = new List<FlatButton>();
        readonly List<Control> navScrollChildren = new List<Control>();
        readonly Dictionary<string, FlatButton> serviceButtons = new Dictionary<string, FlatButton>();
        readonly List<KeyValuePair<string, FlatButton>> setupButtons = new List<KeyValuePair<string, FlatButton>>();

        FlatButton loginNavBtn;
        FlatButton homePremiumBtn;
        FlatButton lastClickedNav;
        FlatButton backBtn;
        FlatButton fwdBtn;
        FlatButton relBtn;
        FlatButton openBtn;

        readonly System.Windows.Forms.Timer loadingTimer = new System.Windows.Forms.Timer();
        bool isLoading;
        float loadingPhase;

        Rectangle lastNormalBounds;
        bool isFullScreen;
        FormWindowState preFullScreenState;
        Rectangle preFullScreenBounds;
        bool startupRevealed;
        SplashForm splash;

        Icon appIcon;
        NotifyIcon trayIcon;
        ContextMenuStrip trayMenu;
        bool allowRealExit;
        bool shownTrayTip;

        int authUser = 0;
        readonly string[] order = { "gemini", "flow", "colab", "cloud", "notebook", "ai" };

        static readonly string[,] SERVICES = new string[,] {
            { "Gemini 4 Neon", "\u2726", "https://gemini.google.com/app" },
            { "Flow 影片",     "\u25B6", "https://labs.google/flow" },
            { "Colab",         "\u2318", "https://colab.research.google.com" },
            { "Cloud Console", "\u2601", "https://console.cloud.google.com" },
            { "NotebookLM",    "\u25A4", "https://notebooklm.google.com" },
            { "AI Studio",     "\u26A1", "https://aistudio.google.com" },
        };

        static readonly string[,] SETUP = new string[,] {
            { "權益中心",          "\u25C8", "https://one.google.com/benefits" },
            { "Developer Program", "\u2699", "https://developers.google.com/program" },
            { "YouTube Premium",   "\u25B6", "https://www.youtube.com/premium" },
            { "Home Premium",      "\u2302", "https://one.google.com/benefits" },
            { "Google One",        "\u25CD", "https://one.google.com" },
        };

        static string ProfileDir
        {
            get
            {
                string d = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "ProStudioEdge", "profile");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        static string WindowStateFile
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "ProStudioEdge", "window.txt");
            }
        }

        public MainForm()
        {
            Text = "Pro Studio";
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            Font = new Font("Microsoft JhengHei UI", 9.5f);
            DoubleBuffered = true;
            KeyPreview = true;
            Padding = new Padding(1);

            // Start hidden behind Splash until WebView2 is ready
            Opacity = 0;
            ShowInTaskbar = false;

            try
            {
                appIcon = AppIconHelper.CreateRuntimeIcon(48);
                Icon = appIcon;
            }
            catch { }

            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            MinimumSize = new Size(Math.Min(Theme.S(860), work.Width), Math.Min(Theme.S(540), work.Height));
            RestoreWindowBounds();

            BuildTitleBar();
            BuildSidebar();
            BuildTopBar();
            BuildTrayIcon();

            var right = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Bg, EdgeResizePassThrough = true };
            web.Dock = DockStyle.Fill;
            web.BackColor = Theme.Bg;
            web.DefaultBackgroundColor = Theme.Bg;
            web.KeyDown += (s, e) =>
            {
                if (HandleShortcut(e.KeyData))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            right.Controls.Add(web);
            right.Controls.Add(top);

            bodyPanel.Dock = DockStyle.Fill;
            bodyPanel.BackColor = Theme.Bg;
            bodyPanel.EdgeResizePassThrough = true;
            bodyPanel.Controls.Add(right);
            bodyPanel.Controls.Add(side);

            Controls.Add(bodyPanel);
            Controls.Add(titleBar);

            loadingTimer.Interval = 16;
            loadingTimer.Tick += (s, e) =>
            {
                loadingPhase += 0.024f;
                if (loadingPhase > 1.35f) loadingPhase = -0.35f;
                url.StepSpinner(10f);
                top.Invalidate(new Rectangle(0, top.Height - Theme.S(4), top.Width, Theme.S(4)));
            };

            Move += (s, e) => TrackNormalBounds();
            Resize += (s, e) =>
            {
                TrackNormalBounds();
                bool isMax = WindowState == FormWindowState.Maximized;
                titleBar.SetMaximized(isMax);
                Padding = (isMax || isFullScreen) ? new Padding(0) : new Padding(1);
                if (IsHandleCreated)
                    NativeMethods.ApplyWin11Chrome(Handle, !isMax && !isFullScreen);
            };
            FormClosing += OnMainFormClosing;

            HandleCreated += (s, e) =>
            {
                bool rounded = WindowState != FormWindowState.Maximized && !isFullScreen;
                NativeMethods.ApplyWin11Chrome(Handle, rounded);
                AppIconHelper.ApplyHighResWindowIcons(Handle);
            };

            Load += OnMainFormLoad;
        }

        async void OnMainFormLoad(object sender, EventArgs e)
        {
            TrackNormalBounds();
            try
            {
                splash = new SplashForm();
                splash.Show();
                splash.Refresh();
            }
            catch { }

            try
            {
                SetLoading(true);
                var env = await CoreWebView2Environment.CreateAsync(null, ProfileDir, null);
                await web.EnsureCoreWebView2Async(env);
                web.DefaultBackgroundColor = Theme.Bg;
                web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                web.CoreWebView2.NewWindowRequested += (a, b) =>
                {
                    b.Handled = true;
                    try { Process.Start(new ProcessStartInfo(b.Uri) { UseShellExecute = true }); } catch { }
                };
                web.CoreWebView2.NavigationStarting += (a, b) => SetLoading(true);
                web.CoreWebView2.SourceChanged += (a, b) => SyncFromActualWebView();
                web.CoreWebView2.NavigationCompleted += (a, b) =>
                {
                    SetLoading(false);
                    SyncFromActualWebView();
                    try { web.CoreWebView2.ExecuteScriptAsync("document.documentElement.style.setProperty('color-scheme','dark')"); } catch { }
                };

                if (!string.IsNullOrEmpty(Program.StartUrl))
                {
                    GoRaw(Program.StartUrl);
                    SyncActiveNavByUrl(Program.StartUrl);
                }
                else
                {
                    Navigate("gemini");
                }
            }
            catch { }
            finally
            {
                RevealMainWindowAfterSplash();
            }
        }

        void RevealMainWindowAfterSplash()
        {
            if (startupRevealed) return;
            startupRevealed = true;
            try
            {
                if (splash != null)
                {
                    splash.Close();
                    splash.Dispose();
                    splash = null;
                }
            }
            catch { }

            ShowInTaskbar = true;
            Opacity = 1;
            if (!Visible) Show();
            NativeMethods.ApplyWin11Chrome(Handle, WindowState != FormWindowState.Maximized && !isFullScreen);
            AppIconHelper.ApplyHighResWindowIcons(Handle);
            Activate();
            NativeMethods.SetForegroundWindow(Handle);
        }

        void BuildTitleBar()
        {
            titleBar.MinimizeClicked += (s, e) => WindowState = FormWindowState.Minimized;
            titleBar.MaximizeToggleClicked += (s, e) => ToggleMaximize();
            titleBar.CloseClicked += (s, e) => Close();
        }

        void BuildTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Renderer = new DarkMenuRenderer();
            trayMenu.ShowImageMargin = false;
            trayMenu.Font = new Font("Microsoft JhengHei UI", 9.25f);
            trayMenu.BackColor = Theme.Side;

            var showItem = new ToolStripMenuItem("顯示 Pro Studio");
            showItem.Click += (s, e) => RestoreFromTray();
            var exitItem = new ToolStripMenuItem("離開");
            exitItem.Click += (s, e) => ExitApplication();

            trayMenu.Items.Add(showItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(exitItem);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Pro Studio";
            trayIcon.Icon = appIcon != null ? appIcon : SystemIcons.Application;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        void RestoreFromTray()
        {
            if (!startupRevealed) RevealMainWindowAfterSplash();
            ShowInTaskbar = true;
            Opacity = 1;
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            NativeMethods.ShowWindow(Handle, NativeMethods.SW_SHOW);
            NativeMethods.SetForegroundWindow(Handle);
        }

        void ExitApplication()
        {
            allowRealExit = true;
            SaveWindowBounds();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            Close();
            Application.Exit();
        }

        void OnMainFormClosing(object sender, FormClosingEventArgs e)
        {
            SaveWindowBounds();
            if (!allowRealExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                if (!shownTrayTip && trayIcon != null)
                {
                    shownTrayTip = true;
                    trayIcon.ShowBalloonTip(3000, "Pro Studio", "已縮到托盤，右鍵可退出", ToolTipIcon.Info);
                }
                return;
            }
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }

        void ToggleMaximize()
        {
            if (isFullScreen)
            {
                ToggleFullScreen();
                return;
            }
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        void ToggleFullScreen()
        {
            if (!isFullScreen)
            {
                isFullScreen = true;
                preFullScreenState = WindowState;
                preFullScreenBounds = WindowState == FormWindowState.Normal ? Bounds : lastNormalBounds;

                titleBar.Visible = false;
                side.Visible = false;
                top.Visible = false;
                Padding = new Padding(0);

                if (WindowState == FormWindowState.Maximized)
                    WindowState = FormWindowState.Normal;
                Rectangle mon = Screen.FromControl(this).Bounds;
                Bounds = mon;
                NativeMethods.ApplyWin11Chrome(Handle, false);
            }
            else
            {
                isFullScreen = false;
                titleBar.Visible = true;
                side.Visible = true;
                top.Visible = true;

                if (preFullScreenState == FormWindowState.Maximized)
                {
                    Padding = new Padding(0);
                    WindowState = FormWindowState.Maximized;
                    NativeMethods.ApplyWin11Chrome(Handle, false);
                }
                else
                {
                    Padding = new Padding(1);
                    WindowState = FormWindowState.Normal;
                    if (preFullScreenBounds.Width >= MinimumSize.Width && preFullScreenBounds.Height >= MinimumSize.Height)
                        Bounds = preFullScreenBounds;
                    NativeMethods.ApplyWin11Chrome(Handle, true);
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleShortcut(keyData)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        bool HandleShortcut(Keys keyData)
        {
            if (keyData == Keys.F11)
            {
                ToggleFullScreen();
                return true;
            }
            if ((keyData & Keys.Modifiers) == Keys.Control)
            {
                Keys key = keyData & Keys.KeyCode;
                if (key >= Keys.D1 && key <= Keys.D6)
                {
                    Navigate(order[key - Keys.D1]);
                    return true;
                }
                if (key >= Keys.NumPad1 && key <= Keys.NumPad6)
                {
                    Navigate(order[key - Keys.NumPad1]);
                    return true;
                }
                if (key == Keys.R)
                {
                    if (relBtn != null) relBtn.TriggerSpinFeedback();
                    try { if (web.CoreWebView2 != null) web.CoreWebView2.Reload(); } catch { }
                    return true;
                }
                if (key == Keys.L)
                {
                    if (isFullScreen) ToggleFullScreen();
                    url.FocusAndCopy();
                    return true;
                }
            }
            return false;
        }

        static Point DecodeLParamPoint(IntPtr lParam)
        {
            int raw = lParam.ToInt32();
            int x = (short)(raw & 0xFFFF);
            int y = (short)((raw >> 16) & 0xFFFF);
            return new Point(x, y);
        }

        public bool IsOnResizeEdge(IntPtr lParam)
        {
            if (WindowState != FormWindowState.Normal || isFullScreen) return false;
            Point pt = PointToClient(DecodeLParamPoint(lParam));
            int grip = Theme.S(8);
            return pt.X < grip || pt.X >= ClientSize.Width - grip ||
                   pt.Y < grip || pt.Y >= ClientSize.Height - grip;
        }

        public bool IsOnTopRightResizeEdge(IntPtr lParam)
        {
            if (WindowState != FormWindowState.Normal || isFullScreen) return false;
            Point pt = PointToClient(DecodeLParamPoint(lParam));
            int grip = Theme.S(5);
            return pt.Y < grip || pt.X >= ClientSize.Width - grip;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_NCHITTEST && WindowState == FormWindowState.Normal && !isFullScreen)
            {
                Point pt = PointToClient(DecodeLParamPoint(m.LParam));
                int grip = Theme.S(8);
                int w = ClientSize.Width;
                int h = ClientSize.Height;

                bool left = pt.X < grip;
                bool right = pt.X >= w - grip;
                bool topEdge = pt.Y < grip;
                bool bottom = pt.Y >= h - grip;

                if (topEdge && left) { m.Result = new IntPtr(NativeMethods.HTTOPLEFT); return; }
                if (topEdge && right) { m.Result = new IntPtr(NativeMethods.HTTOPRIGHT); return; }
                if (bottom && left) { m.Result = new IntPtr(NativeMethods.HTBOTTOMLEFT); return; }
                if (bottom && right) { m.Result = new IntPtr(NativeMethods.HTBOTTOMRIGHT); return; }
                if (left) { m.Result = new IntPtr(NativeMethods.HTLEFT); return; }
                if (right) { m.Result = new IntPtr(NativeMethods.HTRIGHT); return; }
                if (topEdge) { m.Result = new IntPtr(NativeMethods.HTTOP); return; }
                if (bottom) { m.Result = new IntPtr(NativeMethods.HTBOTTOM); return; }
            }
            else if (m.Msg == NativeMethods.WM_GETMINMAXINFO && !isFullScreen)
            {
                base.WndProc(ref m);
                IntPtr monitor = NativeMethods.MonitorFromWindow(Handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var mi = new NativeMethods.MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                    if (NativeMethods.GetMonitorInfo(monitor, ref mi))
                    {
                        var mmi = (NativeMethods.MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.MINMAXINFO));
                        mmi.ptMaxPosition.X = Math.Abs(mi.rcWork.Left - mi.rcMonitor.Left);
                        mmi.ptMaxPosition.Y = Math.Abs(mi.rcWork.Top - mi.rcMonitor.Top);
                        mmi.ptMaxSize.X = Math.Abs(mi.rcWork.Right - mi.rcWork.Left);
                        mmi.ptMaxSize.Y = Math.Abs(mi.rcWork.Bottom - mi.rcWork.Top);
                        Marshal.StructureToPtr(mmi, m.LParam, true);
                    }
                }
                return;
            }
            else if (m.Msg == NativeMethods.WM_COPYDATA)
            {
                try
                {
                    var cds = (NativeMethods.COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.COPYDATASTRUCT));
                    if (cds.dwData == NativeMethods.COPYDATA_MAGIC)
                    {
                        string incomingUrl = "";
                        if (cds.cbData > 0 && cds.lpData != IntPtr.Zero)
                        {
                            byte[] buf = new byte[cds.cbData];
                            Marshal.Copy(cds.lpData, buf, 0, cds.cbData);
                            incomingUrl = Encoding.UTF8.GetString(buf).Trim();
                        }
                        BeginInvoke((Action)(() =>
                        {
                            RestoreFromTray();
                            if (!string.IsNullOrEmpty(incomingUrl))
                            {
                                GoRaw(incomingUrl);
                                SyncActiveNavByUrl(incomingUrl);
                            }
                        }));
                        m.Result = new IntPtr(1);
                        return;
                    }
                }
                catch { }
            }

            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (WindowState == FormWindowState.Normal && !isFullScreen)
            {
                using (var pen = new Pen(Theme.Line, 1f))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        void RestoreWindowBounds()
        {
            Rectangle primaryWork = Screen.PrimaryScreen.WorkingArea;
            int defW = Math.Min(Theme.S(1440), primaryWork.Width - Theme.S(32));
            int defH = Math.Min(Theme.S(900), primaryWork.Height - Theme.S(32));

            try
            {
                if (File.Exists(WindowStateFile))
                {
                    string raw = File.ReadAllText(WindowStateFile).Trim();
                    string[] parts = raw.Split(',');
                    if (parts.Length >= 4)
                    {
                        int x, y, w, h;
                        if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) &&
                            int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out w) &&
                            int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out h))
                        {
                            var saved = new Rectangle(x, y, w, h);
                            if (w >= MinimumSize.Width && h >= MinimumSize.Height && IsWithinAnyScreen(saved))
                            {
                                StartPosition = FormStartPosition.Manual;
                                Bounds = saved;
                                lastNormalBounds = saved;
                                if (parts.Length >= 5 && parts[4].Trim() == "1")
                                    WindowState = FormWindowState.Maximized;
                                return;
                            }
                        }
                    }
                }
            }
            catch { }

            StartPosition = FormStartPosition.CenterScreen;
            Width = defW;
            Height = defH;
        }

        static bool IsWithinAnyScreen(Rectangle r)
        {
            int tol = Theme.S(16);
            foreach (var scr in Screen.AllScreens)
            {
                Rectangle wa = scr.WorkingArea;
                if (r.Left >= wa.Left - tol &&
                    r.Top >= wa.Top - tol &&
                    r.Right <= wa.Right + tol &&
                    r.Bottom <= wa.Bottom + tol)
                {
                    return true;
                }
            }
            return false;
        }

        void TrackNormalBounds()
        {
            if (!isFullScreen && WindowState == FormWindowState.Normal &&
                Width >= MinimumSize.Width && Height >= MinimumSize.Height)
            {
                lastNormalBounds = Bounds;
            }
        }

        void SaveWindowBounds()
        {
            try
            {
                Rectangle b;
                if (isFullScreen)
                    b = preFullScreenBounds.Width > 0 ? preFullScreenBounds : lastNormalBounds;
                else if (WindowState == FormWindowState.Normal)
                    b = Bounds;
                else
                    b = lastNormalBounds.Width > 0 ? lastNormalBounds : RestoreBounds;

                if (b.Width < MinimumSize.Width || b.Height < MinimumSize.Height) return;
                int maximized = (!isFullScreen && WindowState == FormWindowState.Maximized) ? 1 : 0;
                string dir = Path.GetDirectoryName(WindowStateFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string content = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4}",
                    b.Left, b.Top, b.Width, b.Height, maximized);
                File.WriteAllText(WindowStateFile, content);
            }
            catch { }
        }

        void SetLoading(bool value)
        {
            isLoading = value;
            url.Loading = value;
            if (value)
            {
                loadingPhase = -0.35f;
                loadingTimer.Start();
            }
            else
            {
                loadingTimer.Stop();
            }
            top.Invalidate();
        }

        void SyncFromActualWebView()
        {
            try
            {
                if (web.CoreWebView2 == null) return;
                string src = web.CoreWebView2.Source;
                if (!string.IsNullOrEmpty(src) && !src.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
                {
                    url.Text = src;
                    url.Invalidate();
                    SyncActiveNavByUrl(src);
                }
                if (backBtn != null) backBtn.Dimmed = !web.CoreWebView2.CanGoBack;
                if (fwdBtn != null) fwdBtn.Dimmed = !web.CoreWebView2.CanGoForward;
            }
            catch { }
        }

        void SetActiveNav(FlatButton target)
        {
            foreach (var btn in navItems)
            {
                bool shouldBeActive = (btn == target);
                if (btn.Active != shouldBeActive)
                {
                    btn.Active = shouldBeActive;
                    btn.Invalidate();
                }
            }
        }

        void SyncActiveNavByUrl(string targetUrl)
        {
            if (string.IsNullOrEmpty(targetUrl)) return;
            Uri uri;
            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out uri)) return;

            string host = uri.Host.ToLowerInvariant();
            string path = uri.AbsolutePath.ToLowerInvariant();

            if (HostMatches(host, "gemini.google.com")) { ActivateService("gemini"); return; }
            if (HostMatches(host, "labs.google")) { ActivateService("flow"); return; }
            if (HostMatches(host, "colab.research.google.com")) { ActivateService("colab"); return; }
            if (HostMatches(host, "console.cloud.google.com")) { ActivateService("cloud"); return; }
            if (HostMatches(host, "notebooklm.google.com")) { ActivateService("notebook"); return; }
            if (HostMatches(host, "aistudio.google.com")) { ActivateService("ai"); return; }

            if (HostMatches(host, "developers.google.com")) { ActivateSetupByUrl("https://developers.google.com/program"); return; }
            if (HostMatches(host, "youtube.com")) { ActivateSetupByUrl("https://www.youtube.com/premium"); return; }
            if (HostMatches(host, "one.google.com"))
            {
                if (path.StartsWith("/benefits"))
                {
                    if (lastClickedNav == homePremiumBtn && homePremiumBtn != null)
                        SetActiveNav(homePremiumBtn);
                    else
                        ActivateSetupByUrl("https://one.google.com/benefits");
                }
                else
                {
                    ActivateSetupByUrl("https://one.google.com");
                }
                return;
            }

            if (HostMatches(host, "accounts.google.com") && loginNavBtn != null)
            {
                SetActiveNav(loginNavBtn);
                return;
            }

            SetActiveNav(null);
        }

        static bool HostMatches(string actualHost, string domain)
        {
            return actualHost == domain || actualHost.EndsWith("." + domain);
        }

        void ActivateService(string key)
        {
            FlatButton btn;
            if (serviceButtons.TryGetValue(key, out btn))
                SetActiveNav(btn);
        }

        void ActivateSetupByUrl(string exactUrl)
        {
            foreach (var pair in setupButtons)
            {
                if (string.Equals(pair.Key, exactUrl, StringComparison.OrdinalIgnoreCase))
                {
                    SetActiveNav(pair.Value);
                    return;
                }
            }
        }

        void BuildSidebar()
        {
            side.Dock = DockStyle.Left;
            side.Width = Theme.S(236);
            side.BackColor = Theme.Side;
            side.EdgeResizePassThrough = true;
            side.Padding = new Padding(0, 0, 1, 0);
            side.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.Line, 1f))
                    e.Graphics.DrawLine(p, side.Width - 1, 0, side.Width - 1, side.Height);
            };

            var brand = new BufferedPanel { Dock = DockStyle.Top, Height = Theme.S(72), BackColor = Theme.Side, EdgeResizePassThrough = true };
            brand.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Theme.Side);

                var badgeRect = new RectangleF(Theme.Sf(16f), Theme.Sf(19f), Theme.Sf(34f), Theme.Sf(34f));
                Theme.DrawBrandBadge(g, badgeRect, Theme.Sf(10f), Theme.Sf(20f));

                using (var fTitle = new Font("Microsoft JhengHei UI", 11.25f, FontStyle.Bold))
                    TextRenderer.DrawText(g, "Pro Studio", fTitle,
                        new Rectangle(Theme.S(60), Theme.S(17), brand.Width - Theme.S(68), Theme.S(22)),
                        Theme.Fg, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                using (var fSub = new Font("Microsoft JhengHei UI", 8.25f))
                    TextRenderer.DrawText(g, "Google AI Pro 工作台", fSub,
                        new Rectangle(Theme.S(60), Theme.S(39), brand.Width - Theme.S(68), Theme.S(18)),
                        Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                using (var pen = new Pen(Theme.LineSubtle, 1f))
                    g.DrawLine(pen, Theme.S(14), brand.Height - 1, brand.Width - Theme.S(14), brand.Height - 1);
            };

            nav.FlowDirection = FlowDirection.TopDown;
            nav.WrapContents = false;
            nav.Dock = DockStyle.Fill;
            nav.AutoScroll = true;
            nav.HorizontalScroll.Maximum = 0;
            nav.HorizontalScroll.Visible = false;
            nav.BackColor = Theme.Side;
            nav.Padding = new Padding(Theme.S(12), Theme.S(4), Theme.S(11), Theme.S(10));

            int itemW = Theme.S(212);
            int itemH = Theme.S(38);

            var sec1 = MakeSectionHeader("常用", false, itemW);
            navScrollChildren.Add(sec1);
            nav.Controls.Add(sec1);
            for (int i = 0; i < SERVICES.GetLength(0); i++)
            {
                int idx = i;
                var b = new FlatButton
                {
                    Text = SERVICES[i, 0],
                    Glyph = SERVICES[i, 1],
                    GlyphSize = 10.5f,
                    Style = FlatButtonStyle.SidebarNav,
                    Radius = 9,
                    Width = itemW,
                    Height = itemH,
                    Margin = new Padding(0, Theme.S(1), 0, Theme.S(2))
                };
                b.Click += (s, e) => Navigate(order[idx]);
                navItems.Add(b);
                navScrollChildren.Add(b);
                serviceButtons[order[idx]] = b;
                nav.Controls.Add(b);
            }

            var sec2 = MakeSectionHeader("開通", true, itemW);
            navScrollChildren.Add(sec2);
            nav.Controls.Add(sec2);
            for (int i = 0; i < SETUP.GetLength(0); i++)
            {
                string u = SETUP[i, 2];
                var b = new FlatButton
                {
                    Text = SETUP[i, 0],
                    Glyph = SETUP[i, 1],
                    GlyphSize = 10.5f,
                    Style = FlatButtonStyle.SidebarNav,
                    Radius = 9,
                    Width = itemW,
                    Height = itemH,
                    Margin = new Padding(0, Theme.S(1), 0, Theme.S(2))
                };
                if (SETUP[i, 0] == "Home Premium") homePremiumBtn = b;
                FlatButton captured = b;
                b.Click += (s, e) =>
                {
                    lastClickedNav = captured;
                    SetActiveNav(captured);
                    GoRaw(u);
                };
                navItems.Add(b);
                navScrollChildren.Add(b);
                setupButtons.Add(new KeyValuePair<string, FlatButton>(u, b));
                nav.Controls.Add(b);
            }

            nav.Layout += (s, e) =>
            {
                int avail = Math.Max(Theme.S(160), nav.ClientSize.Width - nav.Padding.Left - nav.Padding.Right);
                foreach (var c in navScrollChildren)
                {
                    if (c.Width != avail) c.Width = avail;
                }
            };

            footer.Dock = DockStyle.Bottom;
            footer.Height = Theme.S(146);
            footer.BackColor = Theme.Side;
            footer.EdgeResizePassThrough = true;
            footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Line, 1f))
                    e.Graphics.DrawLine(pen, Theme.S(12), 0, footer.Width - Theme.S(12), 0);
            };

            int padL = Theme.S(12);
            int curY = Theme.S(8);

            var acctHeader = new SectionHeader("帳號", false)
            {
                Bounds = new Rectangle(padL, curY, itemW, Theme.S(22))
            };
            footer.Controls.Add(acctHeader);
            curY += Theme.S(24);

            loginNavBtn = new FlatButton
            {
                Text = "登入 / 新增帳號",
                Glyph = "\u279C",
                GlyphSize = 10.5f,
                Style = FlatButtonStyle.SidebarNav,
                Radius = 9,
                Bounds = new Rectangle(padL, curY, itemW, itemH)
            };
            loginNavBtn.Click += (s, e) =>
            {
                lastClickedNav = loginNavBtn;
                SetActiveNav(loginNavBtn);
                GoRaw("https://accounts.google.com/AddSession");
            };
            navItems.Add(loginNavBtn);
            footer.Controls.Add(loginNavBtn);
            curY += itemH + Theme.S(7);

            acctRow.Bounds = new Rectangle(padL, curY, itemW, Theme.S(30));
            acctRow.BackColor = Theme.Side;

            int gap = Theme.S(4);
            int addW = Theme.S(56);
            int chipW = (itemW - addW - gap * 3) / 3;
            int chipH = Theme.S(30);
            var chipFont = new Font("Microsoft JhengHei UI", 8.25f);

            int curX = 0;
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                var b = new FlatButton
                {
                    Text = "帳號" + (i + 1),
                    Style = FlatButtonStyle.AccountChip,
                    Radius = 8,
                    Font = chipFont,
                    Bounds = new Rectangle(curX, 0, chipW, chipH)
                };
                b.Click += (s, e) => SetAccount(n);
                acctBtns[i] = b;
                acctRow.Controls.Add(b);
                curX += chipW + gap;
            }

            var addBtn = new FlatButton
            {
                Text = "＋登入",
                Style = FlatButtonStyle.AccountChip,
                AccentTint = true,
                Radius = 8,
                Font = chipFont,
                Bounds = new Rectangle(curX, 0, itemW - curX, chipH)
            };
            addBtn.Click += (s, e) =>
            {
                lastClickedNav = loginNavBtn;
                SetActiveNav(loginNavBtn);
                GoRaw("https://accounts.google.com/AddSession");
            };
            acctRow.Controls.Add(addBtn);
            footer.Controls.Add(acctRow);
            curY += chipH + Theme.S(6);

            acctStatus.Bounds = new Rectangle(padL, curY, itemW, Theme.S(20));
            footer.Controls.Add(acctStatus);

            side.Controls.Add(nav);
            side.Controls.Add(footer);
            side.Controls.Add(brand);
            UpdateAcctButtons();
        }

        Control MakeSectionHeader(string text, bool topDivider, int width)
        {
            return new SectionHeader(text, topDivider)
            {
                Width = width,
                Height = topDivider ? Theme.S(31) : Theme.S(24),
                Margin = new Padding(0, topDivider ? Theme.S(4) : Theme.S(2), 0, Theme.S(2))
            };
        }

        void BuildTopBar()
        {
            top.Dock = DockStyle.Top;
            top.Height = Theme.S(48);
            top.BackColor = Theme.Bar;
            top.EdgeResizePassThrough = true;
            top.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using (var p = new Pen(Theme.Line, 1f))
                    g.DrawLine(p, 0, top.Height - 1, top.Width, top.Height - 1);

                if (isLoading && top.Width > 0)
                {
                    int barH = Math.Max(2, Theme.S(2));
                    int barY = top.Height - barH;
                    using (var track = new SolidBrush(Color.FromArgb(20, 32, 46)))
                        g.FillRectangle(track, 0, barY, top.Width, barH);

                    float segW = Math.Max(Theme.Sf(110f), top.Width * 0.28f);
                    float segX = loadingPhase * top.Width;
                    var segRect = new RectangleF(segX, barY, segW, barH);
                    using (var grad = new LinearGradientBrush(segRect, Color.Transparent, Color.Transparent, 0f))
                    {
                        var cb = new ColorBlend();
                        cb.Positions = new float[] { 0f, 0.35f, 0.7f, 1f };
                        cb.Colors = new Color[]
                        {
                            Color.FromArgb(0, Theme.Accent),
                            Theme.Accent,
                            Theme.AccentViolet,
                            Color.FromArgb(0, Theme.AccentViolet)
                        };
                        grad.InterpolationColors = cb;
                        g.FillRectangle(grad, segRect);
                    }
                }
            };

            backBtn = new FlatButton
            {
                Text = "",
                Glyph = "\u2039",
                GlyphFontName = "Segoe UI",
                GlyphSize = 14f,
                Style = FlatButtonStyle.TopIcon,
                Radius = 8,
                Dimmed = true
            };
            backBtn.Click += (s, e) => { try { if (web.CoreWebView2.CanGoBack) web.CoreWebView2.GoBack(); } catch { } };

            fwdBtn = new FlatButton
            {
                Text = "",
                Glyph = "\u203A",
                GlyphFontName = "Segoe UI",
                GlyphSize = 14f,
                Style = FlatButtonStyle.TopIcon,
                Radius = 8,
                Dimmed = true
            };
            fwdBtn.Click += (s, e) => { try { if (web.CoreWebView2.CanGoForward) web.CoreWebView2.GoForward(); } catch { } };

            relBtn = new FlatButton
            {
                Text = "",
                Glyph = "\u21BB",
                GlyphFontName = "Segoe UI Symbol",
                GlyphSize = 11.5f,
                Style = FlatButtonStyle.TopIcon,
                Radius = 8
            };
            relBtn.Click += (s, e) =>
            {
                relBtn.TriggerSpinFeedback();
                try { web.CoreWebView2.Reload(); } catch { }
            };

            openBtn = new FlatButton
            {
                Text = "用 Edge 開",
                Glyph = "\u2197",
                GlyphFontName = "Segoe UI Symbol",
                GlyphSize = 9.5f,
                Style = FlatButtonStyle.Capsule,
                Font = new Font("Microsoft JhengHei UI", 8.75f)
            };
            openBtn.Click += (s, e) => { try { Process.Start(new ProcessStartInfo(url.Text) { UseShellExecute = true }); } catch { } };

            url.Text = "https://gemini.google.com/app";

            top.Controls.Add(backBtn);
            top.Controls.Add(fwdBtn);
            top.Controls.Add(relBtn);
            top.Controls.Add(url);
            top.Controls.Add(openBtn);

            Action layoutTop = () =>
            {
                int h = top.Height - 1;
                int ctrlH = Theme.S(30);
                int y = (h - ctrlH) / 2;
                int btnW = Theme.S(30);
                int gap = Theme.S(6);
                int x = Theme.S(12);

                backBtn.Bounds = new Rectangle(x, y, btnW, ctrlH);
                x += btnW + gap;
                fwdBtn.Bounds = new Rectangle(x, y, btnW, ctrlH);
                x += btnW + gap;
                relBtn.Bounds = new Rectangle(x, y, btnW, ctrlH);
                x += btnW + Theme.S(12);

                int openW = Theme.S(102);
                int openX = Math.Max(x + Theme.S(80), top.Width - Theme.S(12) - openW);
                openBtn.Bounds = new Rectangle(openX, y, openW, ctrlH);

                int pillW = Math.Max(Theme.S(80), openX - x - Theme.S(12));
                url.Bounds = new Rectangle(x, y, pillW, ctrlH);
            };

            top.Resize += (s, e) => layoutTop();
            layoutTop();
        }

        void SetAccount(int n)
        {
            authUser = n;
            UpdateAcctButtons();
            try { GoRaw(WithAuth(web.CoreWebView2.Source)); } catch { }
        }

        void UpdateAcctButtons()
        {
            for (int i = 0; i < 3; i++)
            {
                acctBtns[i].Active = (i == authUser);
                acctBtns[i].Invalidate();
            }
            acctStatus.StatusText = "目前已選：帳號 " + (authUser + 1);
        }

        string WithAuth(string u)
        {
            if (u == null) return u;
            if (u.StartsWith("https://accounts.google.com", StringComparison.OrdinalIgnoreCase)) return u;
            try
            {
                var ub = new UriBuilder(u);
                var q = System.Web.HttpUtility.ParseQueryString(ub.Query);
                if (authUser > 0) q["authuser"] = authUser.ToString();
                else q.Remove("authuser");
                ub.Query = q.ToString();
                if (ub.Port == 443) ub.Port = -1;
                return ub.ToString();
            }
            catch { return u; }
        }

        void Navigate(string key)
        {
            int i = Array.IndexOf(order, key);
            if (i < 0) return;
            FlatButton btn;
            if (serviceButtons.TryGetValue(key, out btn))
            {
                lastClickedNav = btn;
                SetActiveNav(btn);
            }
            GoRaw(WithAuth(SERVICES[i, 2]));
        }

        void GoRaw(string u)
        {
            try
            {
                SetLoading(true);
                web.CoreWebView2.Navigate(u);
                url.Text = u;
                url.Invalidate();
            }
            catch
            {
                SetLoading(false);
                SyncFromActualWebView();
            }
        }
    }
}
