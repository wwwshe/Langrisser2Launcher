// Lang2Launcher - 랑그릿사 II (Windows판) 런처
// .NET Framework 4.x 기본 컴파일러(csc, C# 5)로 빌드됩니다. 빌드 방법은 README 참고.
// - 소리: 이 게임 볼륨만 조절 (실행 중에도 실시간 반영)
// - 화면: cnc-ddraw(ddraw.ini [ddraw] 섹션) 주요 설정 편집 (다음 실행부터 적용)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Langrisser II Launcher")]
[assembly: System.Reflection.AssemblyProduct("Langrisser II Launcher")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace Lang2Launcher
{
    // ================= Core Audio: 앱별 볼륨 =================
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCo { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }
    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
    }
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        int GetAudioSessionControl(IntPtr a, int b, out IntPtr c);
        int GetSimpleAudioVolume(IntPtr a, int b, out IntPtr c);
        int GetSessionEnumerator(out IAudioSessionEnumerator e);
    }
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        int GetCount(out int n);
        int GetSession(int i, out IAudioSessionControl2 s);
    }
    [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        int GetState(out int s); int GetDisplayName(out IntPtr n); int SetDisplayName(string n, ref Guid g);
        int GetIconPath(out IntPtr p); int SetIconPath(string p, ref Guid g); int GetGroupingParam(out Guid g);
        int SetGroupingParam(ref Guid g, ref Guid c); int RegisterAudioSessionNotification(IntPtr n);
        int UnregisterAudioSessionNotification(IntPtr n);
        int GetSessionIdentifier(out IntPtr s); int GetSessionInstanceIdentifier(out IntPtr s);
        int GetProcessId(out uint pid);
    }
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        int SetMasterVolume(float level, ref Guid ctx); int GetMasterVolume(out float level);
        int SetMute(bool mute, ref Guid ctx); int GetMute(out bool mute);
    }

    static class AppVolume
    {
        // pid의 오디오 세션 볼륨 설정. 세션을 찾으면 true
        public static bool Set(int pid, float level)
        {
            var en = (IMMDeviceEnumerator)(new MMDeviceEnumeratorCo());
            IMMDevice dev; en.GetDefaultAudioEndpoint(0, 1, out dev);
            Guid iid = typeof(IAudioSessionManager2).GUID; object o;
            dev.Activate(ref iid, 23, IntPtr.Zero, out o);
            var mgr = (IAudioSessionManager2)o;
            IAudioSessionEnumerator se; mgr.GetSessionEnumerator(out se);
            int n; se.GetCount(out n); bool found = false;
            for (int i = 0; i < n; i++)
            {
                IAudioSessionControl2 c; se.GetSession(i, out c);
                uint p; c.GetProcessId(out p);
                if (p == (uint)pid) { Guid g = Guid.Empty; ((ISimpleAudioVolume)c).SetMasterVolume(level, ref g); found = true; }
            }
            return found;
        }
    }

    // ================= ddraw.ini [ddraw] 섹션 읽기/쓰기 =================
    static class DdrawIni
    {
        public static Dictionary<string, string> Read(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return map;
            bool inSec = false;
            foreach (var raw in File.ReadAllLines(path))
            {
                var t = raw.Trim();
                var m = Regex.Match(t, @"^\[(.+)\]$");
                if (m.Success) { inSec = m.Groups[1].Value.Equals("ddraw", StringComparison.OrdinalIgnoreCase); continue; }
                if (!inSec || t.StartsWith(";")) continue;
                int eq = t.IndexOf('=');
                if (eq > 0) map[t.Substring(0, eq).Trim()] = t.Substring(eq + 1).Trim();
            }
            return map;
        }

        public static void Write(string path, Dictionary<string, string> values)
        {
            var lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string> { "[ddraw]" };
            int secStart = -1, secEnd = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                var m = Regex.Match(lines[i].Trim(), @"^\[(.+)\]$");
                if (!m.Success) continue;
                if (secStart >= 0) { secEnd = i; break; }
                if (m.Groups[1].Value.Equals("ddraw", StringComparison.OrdinalIgnoreCase)) secStart = i;
            }
            if (secStart < 0) { lines.Insert(0, "[ddraw]"); secStart = 0; secEnd = 1; }
            foreach (var kv in values)
            {
                bool done = false;
                var re = new Regex(@"^\s*" + Regex.Escape(kv.Key) + @"\s*=", RegexOptions.IgnoreCase);
                for (int i = secStart + 1; i < secEnd; i++)
                {
                    if (re.IsMatch(lines[i])) { lines[i] = kv.Key + "=" + kv.Value; done = true; break; }
                }
                if (!done) { lines.Insert(secStart + 1, kv.Key + "=" + kv.Value); secEnd++; }
            }
            File.WriteAllLines(path, lines.ToArray());
        }
    }

    class SizeOpt { public string Name; public int W, H; public SizeOpt(string n, int w, int h) { Name = n; W = w; H = h; } }
    class FilterOpt { public string Name, Shader, Renderer; public int D3d; public FilterOpt(string n, string s, int d, string r) { Name = n; Shader = s; D3d = d; Renderer = r; } }

    class LauncherForm : Form
    {
        readonly string gameDir, exePath, cfgPath, ddrawPath;

        readonly string[] modes = { "창 모드", "전체화면 (테두리 없음)", "전체화면" };
        readonly SizeOpt[] sizes = {
            new SizeOpt("1배 (640×480)", 640, 480),
            new SizeOpt("1.5배 (960×720)", 960, 720),
            new SizeOpt("2배 (1280×960)", 1280, 960),
            new SizeOpt("2.5배 (1600×1200)", 1600, 1200),
            new SizeOpt("3배 (1920×1440)", 1920, 1440),
            new SizeOpt("사용자 지정", 0, 0)
        };
        readonly FilterOpt[] filters = {
            new FilterOpt("매끈하게 (xBRZ)", @"Shaders\xbrz\xbrz-freescale-multipass.glsl", 0, "opengl"),
            new FilterOpt("도트 보간 강하게 (xBR-lv3)", @"Shaders\xbr\xbr-lv3.glsl", 0, "opengl")
        };

        Process game;
        bool needApply = true, loading = true, loadingSize = false;

        Label lblVol, lblStatus, lblScrNote;
        TrackBar track;
        ComboBox cmbMode, cmbSize, cmbFilter;
        NumericUpDown numW, numH;
        CheckBox chkAspect, chkBoxing, chkVsync, chkResize, chkClose;
        Button btn;
        Timer timer;

        public LauncherForm()
        {
            gameDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            exePath = Path.Combine(gameDir, "LANGPC2.EXE");
            cfgPath = Path.Combine(gameDir, "Lang2Launcher.ini");
            ddrawPath = Path.Combine(gameDir, "ddraw.ini");

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "랑그릿사2 런처";
            ClientSize = new Size(400, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Malgun Gothic", 9.5F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildUi();

            // 런처 설정
            int vol = 30; bool closeOnExit = true;
            if (File.Exists(cfgPath))
            {
                foreach (var line in File.ReadAllLines(cfgPath))
                {
                    var m = Regex.Match(line, @"^\s*volume\s*=\s*(\d+)");
                    if (m.Success) vol = Math.Min(100, int.Parse(m.Groups[1].Value));
                    m = Regex.Match(line, @"^\s*closeOnExit\s*=\s*(\w+)");
                    if (m.Success) closeOnExit = m.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            track.Value = vol;
            chkClose.Checked = closeOnExit;

            foreach (var p in Process.GetProcessesByName("LANGPC2")) { game = p; break; }

            LoadScreen();
            loading = false;
            UpdateUi();

            timer = new Timer { Interval = 300 };
            timer.Tick += OnTick;
            timer.Start();
        }

        T Add<T>(Control parent, int x, int y, int w, int h) where T : Control, new()
        {
            var c = new T { Location = new Point(x, y), Size = new Size(w, h) };
            parent.Controls.Add(c);
            return c;
        }

        void BuildUi()
        {
            var grpSnd = Add<GroupBox>(this, 12, 8, 376, 92); grpSnd.Text = "소리";
            lblVol = Add<Label>(grpSnd, 12, 22, 350, 20);
            track = Add<TrackBar>(grpSnd, 6, 42, 362, 45);
            track.Minimum = 0; track.Maximum = 100; track.TickFrequency = 10; track.LargeChange = 10;

            var grpScr = Add<GroupBox>(this, 12, 106, 376, 252); grpScr.Text = "화면 (cnc-ddraw)";
            Add<Label>(grpScr, 12, 26, 90, 20).Text = "표시 모드";
            cmbMode = Add<ComboBox>(grpScr, 110, 22, 250, 24); cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Items.AddRange(modes);

            Add<Label>(grpScr, 12, 60, 90, 20).Text = "창 크기";
            cmbSize = Add<ComboBox>(grpScr, 110, 56, 250, 24); cmbSize.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var s in sizes) cmbSize.Items.Add(s.Name);

            Add<Label>(grpScr, 110, 92, 40, 20).Text = "가로";
            numW = Add<NumericUpDown>(grpScr, 150, 89, 72, 24); numW.Minimum = 320; numW.Maximum = 7680; numW.Increment = 10;
            Add<Label>(grpScr, 236, 92, 40, 20).Text = "세로";
            numH = Add<NumericUpDown>(grpScr, 276, 89, 72, 24); numH.Minimum = 240; numH.Maximum = 4320; numH.Increment = 10;

            Add<Label>(grpScr, 12, 126, 90, 20).Text = "화질 필터";
            cmbFilter = Add<ComboBox>(grpScr, 110, 122, 250, 24); cmbFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var f in filters) cmbFilter.Items.Add(f.Name);

            chkAspect = Add<CheckBox>(grpScr, 12, 156, 175, 24); chkAspect.Text = "4:3 비율 유지";
            chkBoxing = Add<CheckBox>(grpScr, 190, 156, 175, 24); chkBoxing.Text = "정수배로만 확대";
            chkVsync = Add<CheckBox>(grpScr, 12, 182, 175, 24); chkVsync.Text = "수직동기";
            chkResize = Add<CheckBox>(grpScr, 190, 182, 175, 24); chkResize.Text = "창 크기 직접 조절";

            lblScrNote = Add<Label>(grpScr, 12, 214, 350, 30);
            lblScrNote.ForeColor = Color.DimGray;
            lblScrNote.Text = "화면 설정은 게임을 다음에 시작할 때 적용돼요.";

            chkClose = Add<CheckBox>(this, 16, 366, 360, 24); chkClose.Text = "게임 종료 시 런처도 닫기";
            btn = Add<Button>(this, 16, 394, 368, 38); btn.Text = "게임 시작";
            AcceptButton = btn;
            lblStatus = Add<Label>(this, 16, 440, 368, 22); lblStatus.ForeColor = Color.DimGray;

            track.ValueChanged += delegate { needApply = true; UpdateUi(); SaveConfig(); };
            chkClose.CheckedChanged += delegate { SaveConfig(); };
            cmbMode.SelectedIndexChanged += delegate { UpdateUi(); SaveScreen(); };
            cmbSize.SelectedIndexChanged += delegate
            {
                var s = sizes[cmbSize.SelectedIndex];
                if (s.W > 0) { loadingSize = true; numW.Value = s.W; numH.Value = s.H; loadingSize = false; }
                UpdateUi(); SaveScreen();
            };
            numW.ValueChanged += delegate { if (!loadingSize) SaveScreen(); };
            numH.ValueChanged += delegate { if (!loadingSize) SaveScreen(); };
            cmbFilter.SelectedIndexChanged += delegate { SaveScreen(); };
            foreach (var c in new[] { chkAspect, chkBoxing, chkVsync, chkResize }) c.CheckedChanged += delegate { SaveScreen(); };
            btn.Click += delegate { StartGame(); };
        }

        static string B(bool v) { return v ? "true" : "false"; }

        void SaveConfig()
        {
            if (loading) return;
            try { File.WriteAllText(cfgPath, "volume=" + track.Value + "\r\ncloseOnExit=" + B(chkClose.Checked) + "\r\n", Encoding.ASCII); } catch { }
        }

        void LoadScreen()
        {
            var d = DdrawIni.Read(ddrawPath);
            string v;
            bool win = d.TryGetValue("windowed", out v) && v == "true";
            bool fs = d.TryGetValue("fullscreen", out v) && v == "true";
            cmbMode.SelectedIndex = (win && !fs) ? 0 : (win && fs) ? 1 : 2;

            int w = 0, h = 0;
            if (d.TryGetValue("width", out v)) int.TryParse(v, out w);
            if (d.TryGetValue("height", out v)) int.TryParse(v, out h);
            if (w <= 0 || h <= 0) { w = 1280; h = 960; }
            loadingSize = true;
            numW.Value = Math.Max(numW.Minimum, Math.Min(numW.Maximum, w));
            numH.Value = Math.Max(numH.Minimum, Math.Min(numH.Maximum, h));
            int idx = sizes.Length - 1;
            for (int i = 0; i < sizes.Length - 1; i++) if (sizes[i].W == w && sizes[i].H == h) idx = i;
            cmbSize.SelectedIndex = idx;
            loadingSize = false;

            string sh = d.TryGetValue("shader", out v) ? v : "";
            int fi = 0;
            for (int i = 0; i < filters.Length; i++) if (string.Equals(sh, filters[i].Shader, StringComparison.OrdinalIgnoreCase)) fi = i;
            cmbFilter.SelectedIndex = fi;

            chkAspect.Checked = !(d.TryGetValue("maintas", out v) && v == "false");
            chkBoxing.Checked = d.TryGetValue("boxing", out v) && v == "true";
            chkVsync.Checked = d.TryGetValue("vsync", out v) && v == "true";
            chkResize.Checked = !(d.TryGetValue("resizable", out v) && v == "false");
        }

        void SaveScreen()
        {
            if (loading) return;
            var v = new Dictionary<string, string>();
            switch (cmbMode.SelectedIndex)
            {
                case 0: v["windowed"] = "true"; v["fullscreen"] = "false"; break;
                case 1: v["windowed"] = "true"; v["fullscreen"] = "true"; break;
                default: v["windowed"] = "false"; v["fullscreen"] = "true"; break;
            }
            v["width"] = ((int)numW.Value).ToString();
            v["height"] = ((int)numH.Value).ToString();
            var f = filters[Math.Max(0, cmbFilter.SelectedIndex)];
            v["shader"] = f.Shader;
            v["d3d9_filter"] = f.D3d.ToString();
            v["renderer"] = f.Renderer ?? "auto";
            v["maintas"] = B(chkAspect.Checked);
            v["boxing"] = B(chkBoxing.Checked);
            v["vsync"] = B(chkVsync.Checked);
            v["resizable"] = B(chkResize.Checked);
            try { DdrawIni.Write(ddrawPath, v); }
            catch (Exception ex) { lblScrNote.Text = "ddraw.ini 저장 실패: " + ex.Message; }
        }

        bool Running { get { return game != null && !game.HasExited; } }

        void UpdateUi()
        {
            lblVol.Text = "게임 볼륨: " + track.Value + "%";
            bool isWin = cmbMode.SelectedIndex == 0;
            cmbSize.Enabled = isWin;
            bool custom = isWin && cmbSize.SelectedIndex == sizes.Length - 1;
            numW.Enabled = custom; numH.Enabled = custom;
            chkResize.Enabled = isWin;
            btn.Enabled = !Running;
            lblStatus.Text = Running ? "실행 중 - 볼륨은 바로 조절돼요" : "대기 중";
        }

        void StartGame()
        {
            if (!File.Exists(exePath))
            {
                MessageBox.Show(this, "LANGPC2.EXE를 찾을 수 없어요.\n" + exePath, "랑그릿사2 런처");
                return;
            }
            SaveConfig(); SaveScreen();
            try
            {
                game = Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = gameDir, UseShellExecute = false });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "게임을 실행하지 못했어요.\n" + ex.Message, "랑그릿사2 런처");
                return;
            }
            needApply = true;
            UpdateUi();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (game == null) return;
            if (game.HasExited)
            {
                game = null;
                // cnc-ddraw가 종료 시 창 크기/위치를 저장하므로 다시 읽어옴
                loading = true; LoadScreen(); loading = false;
                UpdateUi();
                if (chkClose.Checked) Close();
                return;
            }
            if (needApply)
            {
                try { if (AppVolume.Set(game.Id, track.Value / 100f)) needApply = false; } catch { }
            }
        }
    }

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }
    }
}
