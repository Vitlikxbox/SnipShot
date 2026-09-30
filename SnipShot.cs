using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class SnipShot : Form
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4;
    const int HotRegion = 1, HotScreen = 2, HotWindow = 3;

    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnipShot");
    CheckBox copyBox, saveBox, soundBox;
    Label folderLabel, lastLabel;
    NotifyIcon tray;

    SnipShot()
    {
        Text = "SnipShot";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(380, 330);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;

        var region = Big("Область", "Ctrl + Alt + A", 12, () => BeginInvoke(new Action(CaptureRegion)));
        var screen = Big("Весь экран", "Ctrl + Alt + S", 132, CaptureScreen);
        var window = Big("Окно", "Ctrl + Alt + W", 252, () => MessageBox.Show(this, "Нажми Ctrl + Alt + W, находясь в нужном окне", Text));
        Controls.Add(region); Controls.Add(screen); Controls.Add(window);

        copyBox = new CheckBox { Text = "Копировать в буфер обмена", Checked = true, Location = new Point(14, 104), AutoSize = true };
        saveBox = new CheckBox { Text = "Сохранять PNG в папку:", Checked = true, Location = new Point(14, 132), AutoSize = true };
        soundBox = new CheckBox { Text = "Звук затвора", Checked = true, Location = new Point(14, 188), AutoSize = true };
        Controls.Add(copyBox); Controls.Add(saveBox); Controls.Add(soundBox);
        folderLabel = new LinkLabel { Text = folder, Location = new Point(32, 158), Size = new Size(336, 22), AutoEllipsis = true };
        ((LinkLabel)folderLabel).LinkClicked += (s, e) => { Directory.CreateDirectory(folder); Process.Start("explorer.exe", "\"" + folder + "\""); };
        Controls.Add(folderLabel);
        var change = new Button { Text = "Изменить…", Location = new Point(262, 128), Size = new Size(106, 28) };
        change.Click += (s, e) =>
        {
            using (var d = new FolderBrowserDialog { SelectedPath = folder })
                if (d.ShowDialog(this) == DialogResult.OK) { folder = d.SelectedPath; folderLabel.Text = folder; }
        };
        Controls.Add(change);

        lastLabel = new Label { Location = new Point(12, 222), Size = new Size(356, 44), ForeColor = Color.Gray, Text = "Горячие клавиши работают, даже когда окно свёрнуто в трей. В режиме области Esc — отмена" };
        Controls.Add(lastLabel);
        var hide = new Button { Text = "Свернуть в трей", Location = new Point(12, 276), Size = new Size(356, 40) };
        hide.Click += (s, e) => Hide();
        Controls.Add(hide);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Снимок области", null, (s, e) => BeginInvoke(new Action(CaptureRegion)));
        menu.Items.Add("Весь экран", null, (s, e) => CaptureScreen());
        menu.Items.Add("Открыть папку", null, (s, e) => { Directory.CreateDirectory(folder); Process.Start("explorer.exe", "\"" + folder + "\""); });
        menu.Items.Add("Настройки", null, (s, e) => ShowWindow());
        menu.Items.Add("Выход", null, (s, e) => Close());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, ContextMenuStrip = menu, Text = "SnipShot: Ctrl+Alt+A — снимок области" };
        tray.DoubleClick += (s, e) => BeginInvoke(new Action(CaptureRegion));
        tray.BalloonTipClicked += (s, e) => { if (lastFile != null && File.Exists(lastFile)) Process.Start("explorer.exe", "/select,\"" + lastFile + "\""); };

        Resize += (s, e) => { if (WindowState == FormWindowState.Minimized) Hide(); };
    }

    string lastFile;

    Control Big(string title, string hotkey, int x, Action click)
    {
        var b = new Button { Location = new Point(x, 12), Size = new Size(116, 80), Text = title + "\n\n" + hotkey, Font = new Font("Segoe UI", 9) };
        b.Click += (s, e) => click();
        return b;
    }

    void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        bool ok = RegisterHotKey(Handle, HotRegion, MOD_CONTROL | MOD_ALT, (uint)Keys.A)
                & RegisterHotKey(Handle, HotScreen, MOD_CONTROL | MOD_ALT, (uint)Keys.S)
                & RegisterHotKey(Handle, HotWindow, MOD_CONTROL | MOD_ALT, (uint)Keys.W);
        if (!ok) BeginInvoke(new Action(() => lastLabel.Text = "Часть горячих клавиш занята другой программой. Кнопки и меню в трее работают"));
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) // WM_HOTKEY
        {
            int id = (int)m.WParam;
            if (id == HotRegion) CaptureRegion();
            else if (id == HotScreen) CaptureScreen();
            else if (id == HotWindow) CaptureWindow();
        }
        base.WndProc(ref m);
    }

    static Bitmap Grab(Rectangle r)
    {
        var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        return bmp;
    }

    bool capturing;

    void CaptureRegion()
    {
        if (capturing) return;
        capturing = true;
        bool wasVisible = Visible;
        if (wasVisible) { Hide(); Application.DoEvents(); System.Threading.Thread.Sleep(150); }
        var bounds = SystemInformation.VirtualScreen;
        using (var shot = Grab(bounds))
        using (var overlay = new RegionForm(shot, bounds))
        {
            var ok = overlay.ShowDialog() == DialogResult.OK;
            var selection = overlay.Selection;
            if (ok && selection.Width > 1 && selection.Height > 1)
                using (var part = shot.Clone(selection, shot.PixelFormat)) Deliver(part);
        }
        if (wasVisible) Show();
        capturing = false;
    }

    void CaptureScreen()
    {
        var screen = Screen.FromPoint(Cursor.Position).Bounds;
        using (var bmp = Grab(screen)) Deliver(bmp);
    }

    void CaptureWindow()
    {
        RECT r;
        IntPtr h = GetForegroundWindow();
        if (h == IntPtr.Zero || DwmGetWindowAttribute(h, 9, out r, 16) != 0) { CaptureScreen(); return; }
        var rect = Rectangle.FromLTRB(r.L, r.T, r.R, r.B);
        rect.Intersect(SystemInformation.VirtualScreen);
        if (rect.Width < 2 || rect.Height < 2) return;
        using (var bmp = Grab(rect)) Deliver(bmp);
    }

    void Deliver(Bitmap bmp)
    {
        if (soundBox.Checked) System.Media.SystemSounds.Asterisk.Play();
        string info = bmp.Width + " × " + bmp.Height;
        if (copyBox.Checked) Clipboard.SetImage(bmp);
        if (saveBox.Checked)
        {
            try
            {
                Directory.CreateDirectory(folder);
                lastFile = Path.Combine(folder, "Снимок " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".png");
                bmp.Save(lastFile, ImageFormat.Png);
            }
            catch (Exception ex) { lastFile = null; info += ", не удалось сохранить: " + ex.Message; }
        }
        string where = copyBox.Checked && saveBox.Checked && lastFile != null ? "в буфере и в папке" : copyBox.Checked ? "в буфере обмена" : lastFile != null ? "сохранён" : "";
        lastLabel.Text = "Последний снимок: " + info + ", " + where;
        tray.ShowBalloonTip(2500, "Снимок готов", info + ", " + where + (lastFile != null ? ". Нажми, чтобы открыть папку" : ""), ToolTipIcon.None);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterHotKey(Handle, HotRegion);
        UnregisterHotKey(Handle, HotScreen);
        UnregisterHotKey(Handle, HotWindow);
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosing(e);
    }

    class RegionForm : Form
    {
        public Rectangle Selection;
        readonly Bitmap shot, dimmed;
        Point start;
        bool dragging;
        Point mouse;

        public RegionForm(Bitmap shot, Rectangle bounds)
        {
            this.shot = shot;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            KeyPreview = true;
            dimmed = new Bitmap(shot);
            using (var g = Graphics.FromImage(dimmed))
            using (var b = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillRectangle(b, 0, 0, dimmed.Width, dimmed.Height);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; } };
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; return; }
                start = e.Location; dragging = true; Selection = Rectangle.Empty;
            };
            MouseMove += (s, e) => { mouse = e.Location; if (dragging) Selection = Normalize(start, e.Location); Invalidate(); };
            MouseUp += (s, e) => { if (dragging && e.Button == MouseButtons.Left) { dragging = false; DialogResult = DialogResult.OK; } };
            Shown += (s, e) => Activate();
        }

        static Rectangle Normalize(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(dimmed, 0, 0);
            if (Selection.Width > 0 && Selection.Height > 0)
            {
                g.DrawImage(shot, Selection, Selection, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.FromArgb(0, 150, 255), 1)) g.DrawRectangle(pen, Selection.X, Selection.Y, Selection.Width - 1, Selection.Height - 1);
                Label(g, Selection.Width + " × " + Selection.Height, new Point(Selection.X, Selection.Y - 26 < 0 ? Selection.Bottom + 4 : Selection.Y - 26));
            }
            else
            {
                using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255)) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(pen, mouse.X, 0, mouse.X, Height);
                    g.DrawLine(pen, 0, mouse.Y, Width, mouse.Y);
                }
                Label(g, "Выдели область мышью. Esc или правый клик — отмена", new Point(Width / 2 - 200, 30));
            }
        }

        void Label(Graphics g, string text, Point p)
        {
            using (var font = new Font("Segoe UI", 10))
            {
                var size = TextRenderer.MeasureText(text, font);
                var r = new Rectangle(p.X, p.Y, size.Width + 12, size.Height + 6);
                using (var b = new SolidBrush(Color.FromArgb(220, 20, 24, 30))) g.FillRectangle(b, r);
                TextRenderer.DrawText(g, text, font, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) dimmed.Dispose();
            base.Dispose(disposing);
        }
    }

    [STAThread]
    static void Main()
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.Run(new SnipShot());
    }
}
