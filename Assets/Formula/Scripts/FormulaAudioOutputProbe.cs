#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using UnityEngine;

// Validation only. Records the actual listener DSP buffer without changing it.
public sealed class FormulaAudioOutputProbe : MonoBehaviour
{
    [Serializable] public sealed class Metrics
    {
        public int frames, channels, sampleRate, clippedSamples;
        public float peak, rms;
    }
    private float[] buffer;
    private volatile bool recording;
    private int written, channels, sampleRate;
    public void Begin(float seconds)
    {
        recording = false; sampleRate = AudioSettings.outputSampleRate;
        buffer = new float[Mathf.CeilToInt(Mathf.Clamp(seconds, 1, 60) * sampleRate * 8)];
        written = channels = 0; recording = true;
    }
    private void OnAudioFilterRead(float[] data, int channelCount)
    {
        if (!recording || buffer == null) return;
        channels = channelCount;
        int offset = written, count = Math.Min(data.Length, buffer.Length - offset);
        if (count <= 0) { recording = false; return; }
        Array.Copy(data, 0, buffer, offset, count);
        Volatile.Write(ref written, offset + count);
    }
    public Metrics Write(string path)
    {
        recording = false;
        int count = Volatile.Read(ref written), channelCount = Math.Max(1, channels);
        count -= count % channelCount;
        var result = new Metrics { frames = count / channelCount, channels = channelCount, sampleRate = sampleRate };
        double squared = 0;
        for (int i = 0; i < count; i++)
        {
            float value = Mathf.Abs(buffer[i]); result.peak = Mathf.Max(result.peak, value);
            squared += (double)value * value; if (value >= 1) result.clippedSamples++;
        }
        result.rms = count > 0 ? (float)Math.Sqrt(squared / count) : 0;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
            writer.Write((short)channelCount); writer.Write(sampleRate); writer.Write(sampleRate * channelCount * 2);
            writer.Write((short)(channelCount * 2)); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (int i = 0; i < count; i++) writer.Write((short)(Mathf.Clamp(buffer[i], -1, 1) * 32767));
        }
        return result;
    }
    private void OnDisable() { recording = false; }
}

#endif
