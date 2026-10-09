using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace DailyPlanner.Services;

// ===== A short two-note chime (made in code, no sound files needed) =====
public static class Sound
{
    static MediaPlayer? player;

    public static async void Chime()
    {
        if (Store.St["snd"]?.ToString() == "false") return;
        try
        {
            byte[] wav = Make(new[] { (659.25, 0.0), (880.0, 0.12) }, 0.55);
            var stream = new InMemoryRandomAccessStream();
            using (var w = new DataWriter(stream)) { w.WriteBytes(wav); await w.StoreAsync(); await w.FlushAsync(); w.DetachStream(); }
            stream.Seek(0);
            player ??= new MediaPlayer();
            player.Source = MediaSource.CreateFromStream(stream, "audio/wav");
            player.Play();
        }
        catch { }
    }

    static byte[] Make((double freq, double start)[] notes, double seconds)
    {
        const int rate = 44100;
        int n = (int)(rate * seconds);
        var samples = new short[n];
        foreach (var (freq, start) in notes)
        {
            int s0 = (int)(start * rate);
            for (int i = 0; i < (int)(0.38 * rate) && s0 + i < n; i++)
            {
                double t = i / (double)rate;
                double env = Math.Min(1, t / 0.02) * Math.Exp(-t * 9);
                samples[s0 + i] += (short)(Math.Sin(2 * Math.PI * freq * t) * env * 7000);
            }
        }
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write("RIFF"u8.ToArray()); bw.Write(36 + n * 2); bw.Write("WAVE"u8.ToArray());
        bw.Write("fmt "u8.ToArray()); bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write("data"u8.ToArray()); bw.Write(n * 2);
        foreach (var s in samples) bw.Write(s);
        bw.Flush();
        return ms.ToArray();
    }
}
