// Lang2Launcher - 랑그릿사 II (Windows판) 런처
// .NET Framework 4.x 기본 컴파일러(csc, C# 5)로 빌드됩니다. 빌드 방법은 README 참고.
// - 소리: 이 게임 볼륨만 조절 (실행 중에도 실시간 반영)
// - 화면: cnc-ddraw(ddraw.ini [ddraw] 섹션) 주요 설정 편집 (다음 실행부터 적용)
// - 게임 패치: 게임 시작 직전에 LANGPC2.EXE 버그 수정 바이트 패치를 자동 적용 (창 제목 글자색, 상태창 깨진 글자)
// - 게임 글꼴: 게임 글자 글꼴(Arial -> 굴림/돋움/바탕), 굵기, 크기(축소만) 변경
// - 효과음 균형: 유독 크게 녹음된 효과음(DSF)만 음량을 낮춤 (원본은 DSF_ORIG에 백업)
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

    // ================= LANGPC2.EXE 바이트 패치 =================
    class ExePatch
    {
        public readonly string Key;
        public readonly long Offset;           // 파일 오프셋
        public readonly byte[] Orig, Patched;
        public ExePatch(string key, long offset, string orig, string patched)
        {
            Key = key; Offset = offset; Orig = Hex(orig); Patched = Hex(patched);
        }
        static byte[] Hex(string h)
        {
            h = h.Replace(" ", "");
            var b = new byte[h.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
            return b;
        }
    }

    static class GamePatcher
    {
        // 창 제목(병사배속/아이템장비/지휘관배치 등) 글자색 수정 - 0x42275A
        // 원본이 SetTextColor(dc, -1)(=CLR_INVALID)을 써서 Windows NT 계열에서 글자색이 안 바뀌어
        // 그림자색(거의 검정)으로 찍히는 바람에 제목이 안 보임 -> 흰색(0x00FFFFFF)으로.
        //   원본: mov edx,[esp+c] / push 1 / push edx / call edi / mov eax,[esp+c] / push -1 / push eax / call ebx
        //   수정: mov eax,[esp+c] / push 0xFFFFFF / push eax / push 1 / push eax / call edi / call ebx / nop
        public static readonly ExePatch TitleColor = new ExePatch("titleFix", 0x21B5A,
            "8B 54 24 0C 6A 01 52 FF D7 8B 44 24 0C 6A FF 50 FF D3",
            "8B 44 24 0C 68 FF FF FF 00 50 6A 01 50 FF D7 FF D3 90");

        // 이전 개발 버전이 캠프 화면 '지휘관' 제목 호출(0x42ABCD, 0x42B003)을 nop 처리했던 것을 원래대로 되돌림
        public static readonly ExePatch CommanderTitleOnEnter = new ExePatch("commanderTitleOnEnter", 0x29FCD,
            "E8 AE AF FD FF",
            "90 90 90 90 90");
        public static readonly ExePatch CommanderTitleOnReturn = new ExePatch("commanderTitleOnReturn", 0x2A403,
            "E8 78 AB FD FF",
            "90 90 90 90 90");

        // 적용할 패치 / 원본으로 되돌릴 패치
        // 상태창 클래스명 뒤에 '후' 같은 깨진 글자가 붙는 문제 수정 - 유닛 이름 테이블(0x4410B8, 244칸)
        // 상태창(0x402E31)과 0x40E19E가 [유닛+0x37] 번호로 이 테이블에서 이름을 가져오는데,
        // 이름 없는 유닛용 칸(0, 121, 204~243)이 빈 문자열 대신 .bss의 런타임 변수(0x47ED30)를 가리킴.
        // 그 변수 값(예: C8 C4)이 그대로 한글로 찍힘 -> 빈 문자열(0x441020, '파이어스' 끝의 NUL)을 가리키게.
        const long NameTableOffset = 0x3FAB8;   // 파일 오프셋 (VA 0x4410B8)
        static ExePatch[] EmptyNamePatches()
        {
            var slots = new List<int> { 0, 121 };
            for (int i = 204; i <= 243; i++) slots.Add(i);
            var list = new List<ExePatch>();
            foreach (int i in slots)
                list.Add(new ExePatch("emptyName" + i, NameTableOffset + i * 4, "30 ED 47 00", "20 10 44 00"));
            return list.ToArray();
        }

        static ExePatch[] Concat(ExePatch[] a, ExePatch[] b)
        {
            var r = new ExePatch[a.Length + b.Length];
            a.CopyTo(r, 0); b.CopyTo(r, a.Length);
            return r;
        }

        static readonly ExePatch[] Apply = Concat(new[] { TitleColor }, EmptyNamePatches());
        static readonly ExePatch[] Restore = { CommanderTitleOnEnter, CommanderTitleOnReturn };

        // 모든 패치를 정해진 상태로 맞춤 (이미 맞으면 건너뜀). 결과 메시지(문제 없으면 null)
        public static string ApplyAll(string exe)
        {
            var list = new ExePatch[Apply.Length + Restore.Length];
            var want = new bool[list.Length];
            for (int i = 0; i < Apply.Length; i++) { list[i] = Apply[i]; want[i] = true; }
            for (int i = 0; i < Restore.Length; i++) { list[Apply.Length + i] = Restore[i]; want[Apply.Length + i] = false; }
            return Sync(exe, list, want);
        }

        // 각 패치를 원하는 상태(true=적용, false=원본)로 맞춤. 결과 메시지(문제 없으면 null)
        public static string Sync(string exe, ExePatch[] patches, bool[] want)
        {
            try
            {
                using (var fs = new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    for (int i = 0; i < patches.Length; i++)
                        if (State(fs, patches[i]) < 0) return "알 수 없는 LANGPC2.EXE 버전이라 게임 패치를 건너뛰었어요.";
                }
                using (var fs = new FileStream(exe, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    for (int i = 0; i < patches.Length; i++)
                    {
                        int st = State(fs, patches[i]);
                        bool on = st == 1;
                        if (on == want[i]) continue;
                        var bytes = want[i] ? patches[i].Patched : patches[i].Orig;
                        fs.Seek(patches[i].Offset, SeekOrigin.Begin);
                        fs.Write(bytes, 0, bytes.Length);
                    }
                }
                return null;
            }
            catch (UnauthorizedAccessException) { return "LANGPC2.EXE에 쓸 권한이 없어서 게임 패치를 적용하지 못했어요."; }
            catch (IOException ex) { return "게임 패치 실패: " + ex.Message; }
        }

        // 1 = 패치됨, 0 = 원본, -1 = 알 수 없음
        static int State(FileStream fs, ExePatch p)
        {
            int n = p.Orig.Length;
            if (fs.Length < p.Offset + n) return -1;
            var buf = new byte[n];
            fs.Seek(p.Offset, SeekOrigin.Begin);
            int read = 0;
            while (read < n) { int r = fs.Read(buf, read, n - read); if (r <= 0) return -1; read += r; }
            if (Same(buf, p.Patched)) return 1;
            if (Same(buf, p.Orig)) return 0;
            return -1;
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    // ================= 게임 글꼴 =================
    // 게임은 0x42E293 루프에서 CreateFontA로 글꼴 5개(높이 20/24/32/64/256)를 만들어 모든 글자를 그림.
    // 원본: 글꼴 "Arial", 문자셋 ANSI(0), 굵기 400, NONANTIALIASED.
    // Arial엔 한글이 없어서 Windows가 다른 글꼴을 끼워 넣어 그리고, 그게 필터를 거치며 일그러져 보임.
    // -> 글꼴 이름(8바이트)을 한글 글꼴로, 문자셋을 DEFAULT(1)로, 필요하면 굵기를 700으로.
    // 크기: 높이 테이블(0x4424F8, int 5개)을 비율로 줄임. 키우는 건 안 됨
    //       (0x402740이 글자 1바이트당 9px 폭으로 잘라 복사해서 키우면 잘리거나 겹침).
    class FontOpt { public string Name, Face; public FontOpt(string n, string f) { Name = n; Face = f; } }

    static class FontPatcher
    {
        const long FaceOffset = 0x46EEC;      // "Arial\0\0\0" (VA 0x4484EC)
        const long CharsetOffset = 0x2D6A4;   // push ebx(53, ANSI=0) -> push ebp(55, ebp=1=DEFAULT_CHARSET)
        const long WeightOffset = 0x2D6A9;    // push 0x190 의 imm32
        const long HeightOffset = 0x40EF8;    // 글꼴 높이 int[5] (VA 0x4424F8)
        static readonly int[] OrigHeights = { 20, 24, 32, 64, 256 };

        public static readonly int[] SizePercents = { 100, 95, 90, 85, 80, 75 };
        public static string SizeName(int pct) { return pct == 100 ? "원본 크기" : pct + "%"; }

        public static readonly FontOpt[] Fonts = {
            new FontOpt("원본 (Arial)", "Arial"),
            new FontOpt("굴림", "Gulim"),
            new FontOpt("돋움", "Dotum"),
            new FontOpt("바탕", "Batang"),
        };

        static byte[] Face(string f) { var b = new byte[8]; var a = Encoding.ASCII.GetBytes(f); Array.Copy(a, b, a.Length); return b; }

        // 결과 메시지(문제 없으면 null)
        public static string Apply(string exe, int font, bool bold, int sizeIdx)
        {
            if (font < 0 || font >= Fonts.Length) font = 0;
            if (sizeIdx < 0 || sizeIdx >= SizePercents.Length) sizeIdx = 0;
            try
            {
                using (var fs = new FileStream(exe, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    var face = Read(fs, FaceOffset, 8);
                    var cs = Read(fs, CharsetOffset, 1);
                    var wt = Read(fs, WeightOffset, 4);
                    bool faceOk = false;
                    foreach (var f in Fonts) if (Same(face, Face(f.Face))) faceOk = true;
                    bool csOk = cs != null && (cs[0] == 0x53 || cs[0] == 0x55);
                    bool wtOk = wt != null && (Same(wt, BitConverter.GetBytes(400)) || Same(wt, BitConverter.GetBytes(700)));
                    var ht = Read(fs, HeightOffset, OrigHeights.Length * 4);
                    bool htOk = ht != null;
                    for (int i = 0; htOk && i < OrigHeights.Length; i++)
                    {
                        int h = BitConverter.ToInt32(ht, i * 4);
                        if (h < OrigHeights[i] / 2 || h > OrigHeights[i]) htOk = false;
                    }
                    if (!faceOk || !csOk || !wtOk || !htOk) return "알 수 없는 LANGPC2.EXE 버전이라 글꼴 설정을 건너뛰었어요.";

                    bool orig = font == 0;
                    Write(fs, FaceOffset, Face(Fonts[font].Face));
                    Write(fs, CharsetOffset, new byte[] { (byte)(orig ? 0x53 : 0x55) });
                    Write(fs, WeightOffset, BitConverter.GetBytes(bold ? 700 : 400));
                    var nh = new byte[OrigHeights.Length * 4];
                    for (int i = 0; i < OrigHeights.Length; i++)
                    {
                        int h = (int)Math.Round(OrigHeights[i] * SizePercents[sizeIdx] / 100.0, MidpointRounding.AwayFromZero);
                        BitConverter.GetBytes(h).CopyTo(nh, i * 4);
                    }
                    Write(fs, HeightOffset, nh);
                }
                return null;
            }
            catch (UnauthorizedAccessException) { return "LANGPC2.EXE에 쓸 권한이 없어서 글꼴 설정을 적용하지 못했어요."; }
            catch (IOException ex) { return "글꼴 설정 실패: " + ex.Message; }
        }

        static byte[] Read(FileStream fs, long off, int n)
        {
            if (fs.Length < off + n) return null;
            var buf = new byte[n];
            fs.Seek(off, SeekOrigin.Begin);
            int read = 0;
            while (read < n) { int r = fs.Read(buf, read, n - read); if (r <= 0) return null; read += r; }
            return buf;
        }

        static void Write(FileStream fs, long off, byte[] b)
        {
            var cur = Read(fs, off, b.Length);
            if (cur != null && Same(cur, b)) return;
            fs.Seek(off, SeekOrigin.Begin);
            fs.Write(b, 0, b.Length);
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    // ================= 효과음(DSF) 음량 균형 =================
    // DSF 형식: [0]데이터 크기 [4]? [8]? [0x0C]? [0x10]WAVEFORMAT(PCM, 모노, 16비트) [0x20]"da" [0x22~]PCM
    // 1) RECORD49~95 등 나중에 추가된 효과음이 다른 효과음보다 약 2배(6~8dB) 크게 녹음돼 있어서,
    //    원본 피크가 LoudPeak를 넘는 파일을 Gain(-6dB)으로 낮춤.
    // 2) 맵 마법(스킬) 연출 효과음(효과음 슬롯 7,8,9,12,13,17,18,23,24,25)도 체감상 커서 같이 낮춤.
    static class SoundFix
    {
        const int HeaderSize = 0x22;
        const int LoudPeak = 16000;     // 기존 효과음 최대 피크 약 13,500 / 큰 효과음 최소 약 17,600
        const double Gain = 0.5;        // -6dB

        // 맵 마법(스킬) 연출 효과음
        static readonly string[] SkillSounds = {
            "RECORD14.DSF", "RECORD16.DSF", "RECORD18.DSF", "RECORD24.DSF", "RECORD26.DSF",
            "RECORD34.DSF", "RECORD36.DSF", "RECORD46.DSF", "RECORD48.DSF", "RECORD50.DSF"
        };

        static bool IsSkillSound(string name)
        {
            foreach (var n in SkillSounds) if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string Apply(string gameDir)
        {
            string dir = Path.Combine(gameDir, "DSF");
            string orig = Path.Combine(gameDir, "DSF_ORIG");
            if (!Directory.Exists(dir)) return null;
            try
            {
                if (!Directory.Exists(orig)) Directory.CreateDirectory(orig);
                foreach (var f in Directory.GetFiles(dir, "*.DSF"))
                {
                    string b = Path.Combine(orig, Path.GetFileName(f));
                    if (!File.Exists(b)) File.Copy(f, b);
                }
                foreach (var b in Directory.GetFiles(orig, "*.DSF"))
                {
                    byte[] src = File.ReadAllBytes(b);
                    if (!IsPcm16(src)) continue;
                    if (Peak(src) <= LoudPeak && !IsSkillSound(Path.GetFileName(b))) continue;
                    byte[] want = Scale(src, Gain);
                    string f = Path.Combine(dir, Path.GetFileName(b));
                    byte[] cur = File.Exists(f) ? File.ReadAllBytes(f) : null;
                    if (cur != null && Same(cur, want)) continue;          // 이미 적용됨
                    if (cur != null && !Same(cur, src)) continue;          // 사용자가 바꾼 파일은 건드리지 않음
                    File.WriteAllBytes(f, want);
                }
                return null;
            }
            catch (UnauthorizedAccessException) { return "DSF 폴더에 쓸 권한이 없어서 효과음 음량을 조정하지 못했어요."; }
            catch (IOException ex) { return "효과음 음량 조정 실패: " + ex.Message; }
        }

        static bool IsPcm16(byte[] d)
        {
            if (d.Length < HeaderSize) return false;
            int size = BitConverter.ToInt32(d, 0);
            int tag = BitConverter.ToUInt16(d, 0x10), ch = BitConverter.ToUInt16(d, 0x12), bits = BitConverter.ToUInt16(d, 0x1E);
            return size == d.Length - HeaderSize && tag == 1 && ch == 1 && bits == 16;
        }

        static int Peak(byte[] d)
        {
            int peak = 0;
            for (int i = HeaderSize; i + 1 < d.Length; i += 2)
            {
                int v = Math.Abs((int)BitConverter.ToInt16(d, i));
                if (v > peak) peak = v;
            }
            return peak;
        }

        static byte[] Scale(byte[] d, double gain)
        {
            var o = (byte[])d.Clone();
            for (int i = HeaderSize; i + 1 < o.Length; i += 2)
            {
                int v = (int)Math.Round(BitConverter.ToInt16(o, i) * gain, MidpointRounding.AwayFromZero);
                if (v > short.MaxValue) v = short.MaxValue; else if (v < short.MinValue) v = short.MinValue;
                o[i] = (byte)(v & 0xFF); o[i + 1] = (byte)((v >> 8) & 0xFF);
            }
            return o;
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
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
        string patchMsg;

        Label lblVol, lblStatus, lblScrNote;
        TrackBar track;
        ComboBox cmbMode, cmbSize, cmbFilter, cmbFont, cmbFontSize;
        NumericUpDown numW, numH;
        CheckBox chkAspect, chkBoxing, chkVsync, chkResize, chkClose, chkBold;
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
            ClientSize = new Size(400, 538);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Malgun Gothic", 9.5F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildUi();

            // 런처 설정
            int vol = 30; bool closeOnExit = true; int font = 0; bool bold = false; int fontSize = 0;
            if (File.Exists(cfgPath))
            {
                foreach (var line in File.ReadAllLines(cfgPath))
                {
                    var m = Regex.Match(line, @"^\s*volume\s*=\s*(\d+)");
                    if (m.Success) vol = Math.Min(100, int.Parse(m.Groups[1].Value));
                    m = Regex.Match(line, @"^\s*closeOnExit\s*=\s*(\w+)");
                    if (m.Success) closeOnExit = m.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    m = Regex.Match(line, @"^\s*font\s*=\s*(\d+)");
                    if (m.Success) font = Math.Min(FontPatcher.Fonts.Length - 1, int.Parse(m.Groups[1].Value));
                    m = Regex.Match(line, @"^\s*fontBold\s*=\s*(\w+)");
                    if (m.Success) bold = m.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    m = Regex.Match(line, @"^\s*fontSize\s*=\s*(\d+)");
                    if (m.Success) fontSize = Math.Min(FontPatcher.SizePercents.Length - 1, int.Parse(m.Groups[1].Value));
                }
            }
            track.Value = vol;
            chkClose.Checked = closeOnExit;
            cmbFont.SelectedIndex = font;
            chkBold.Checked = bold;
            cmbFontSize.SelectedIndex = fontSize;

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

            var grpScr = Add<GroupBox>(this, 12, 106, 376, 320); grpScr.Text = "화면";
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

            Add<Label>(grpScr, 12, 216, 90, 20).Text = "게임 글꼴";
            cmbFont = Add<ComboBox>(grpScr, 110, 212, 150, 24); cmbFont.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var f in FontPatcher.Fonts) cmbFont.Items.Add(f.Name);
            chkBold = Add<CheckBox>(grpScr, 272, 212, 90, 24); chkBold.Text = "굵게";

            Add<Label>(grpScr, 12, 250, 90, 20).Text = "글자 크기";
            cmbFontSize = Add<ComboBox>(grpScr, 110, 246, 150, 24); cmbFontSize.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var p in FontPatcher.SizePercents) cmbFontSize.Items.Add(FontPatcher.SizeName(p));

            lblScrNote = Add<Label>(grpScr, 12, 282, 350, 30);
            lblScrNote.ForeColor = Color.DimGray;
            lblScrNote.Text = "화면 설정은 게임을 다음에 시작할 때 적용돼요.";

            chkClose = Add<CheckBox>(this, 16, 434, 360, 24); chkClose.Text = "게임 종료 시 런처도 닫기";
            btn = Add<Button>(this, 16, 462, 368, 38); btn.Text = "게임 시작";
            AcceptButton = btn;
            lblStatus = Add<Label>(this, 16, 508, 368, 22); lblStatus.ForeColor = Color.DimGray;

            track.ValueChanged += delegate { needApply = true; UpdateUi(); SaveConfig(); };
            chkClose.CheckedChanged += delegate { SaveConfig(); };
            cmbFont.SelectedIndexChanged += delegate { SaveConfig(); };
            chkBold.CheckedChanged += delegate { SaveConfig(); };
            cmbFontSize.SelectedIndexChanged += delegate { SaveConfig(); };
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
            try { File.WriteAllText(cfgPath, "volume=" + track.Value + "\r\ncloseOnExit=" + B(chkClose.Checked)
                + "\r\nfont=" + Math.Max(0, cmbFont.SelectedIndex) + "\r\nfontBold=" + B(chkBold.Checked) + "\r\nfontSize=" + Math.Max(0, cmbFontSize.SelectedIndex) + "\r\n", Encoding.ASCII); } catch { }
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
            if (patchMsg != null) lblStatus.Text = patchMsg;
            else if (Running) lblStatus.Text = "실행 중 - 볼륨은 바로 조절돼요";
            else lblStatus.Text = "대기 중";
        }

        void StartGame()
        {
            if (!File.Exists(exePath))
            {
                MessageBox.Show(this, "LANGPC2.EXE를 찾을 수 없어요.\n" + exePath, "랑그릿사2 런처");
                return;
            }
            SaveConfig(); SaveScreen();
            string exeMsg = GamePatcher.ApplyAll(exePath);
            string fontMsg = FontPatcher.Apply(exePath, cmbFont.SelectedIndex, chkBold.Checked, cmbFontSize.SelectedIndex);
            string sndMsg = SoundFix.Apply(gameDir);
            patchMsg = exeMsg ?? fontMsg ?? sndMsg;
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
