using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Spider;

// 程序内合成的简单音效，不需要音频文件；用 winmm 异步播放，任何错误都忽略
public static class SoundFx
{
    public static bool Enabled;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
    static extern bool PlaySound(IntPtr data, IntPtr hmod, uint flags);
    const uint SND_ASYNC = 0x1, SND_NODEFAULT = 0x2, SND_MEMORY = 0x4;

    static readonly Dictionary<string, GCHandle> Cache = new();   // 异步播放要求缓冲区不能被移动，所以固定住
    static readonly Random Rnd = new();

    public static void Play(string kind)
    {
        if (!Enabled) return;
        try
        {
            if (!Cache.TryGetValue(kind, out var h))
            {
                h = GCHandle.Alloc(Build(kind), GCHandleType.Pinned);
                Cache[kind] = h;
            }
            PlaySound(h.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
        }
        catch { }
    }

    // eat：一串短促的沙沙声；dash：快速下滑的短音
    static byte[] Build(string kind)
    {
        const int rate = 22050;
        double dur = kind == "eat" ? 0.4 : 0.18;
        int n = (int)(rate * dur);
        var pcm = new short[n];
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n, v;
            if (kind == "eat")
            {
                double pulse = Math.Pow(Math.Max(0, Math.Sin(t * dur * 2 * Math.PI * 12)), 4);
                v = (Rnd.NextDouble() * 2 - 1) * pulse * (1 - t) * 0.3;
            }
            else
            {
                ph += 2 * Math.PI * (1000 - 700 * t) / rate;
                v = Math.Sin(ph) * Math.Pow(1 - t, 2) * 0.3;
            }
            pcm[i] = (short)(v * short.MaxValue);
        }

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int bytes = n * 2;
        w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
        foreach (var s in pcm) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}
