#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FormulaGame;
using TMPro;
using UnityEngine;

// Play-validation only: samples completed rendering frames, never drives the car.
[DefaultExecutionOrder(1000)]
public sealed class FormulaEngineSyncProbe : MonoBehaviour
{
    [Serializable] public sealed class Metrics
    {
        public int frames, frameMismatches, gearMismatches, rpmTextMismatches, risingFrames, fallingFrames, shiftFrames;
        public float maxAudioHudRpmDifference, maxPlaybackPitchErrorCents;
    }
    private struct Row
    {
        public int frame, gear;
        public double dsp;
        public float rawRpm, feedbackRpm, hudRpm, pitch0, pitch1, pitch2, pitch3;
        public bool shifting;
    }
    private FormulaEngineAudio engine;
    private FormulaRaceView hud;
    private FormulaEngineAudio.EngineLayer[] layers;
    private TMP_Text rpmText, gearText;
    private AudioSource[] sources;
    private int[] sourceBands;
    private readonly List<Row> rows = new List<Row>(2400);
    public Metrics Result { get; } = new Metrics();
    private bool recording;
    private float previousRpm;
    public void Begin(FormulaEngineAudio target, FormulaRaceView view, FormulaEngineAudio.EngineLayer[] bank, TMP_Text rpmLabel, TMP_Text gearLabel)
    {
        engine = target; hud = view; layers = bank; rpmText = rpmLabel; gearText = gearLabel;
        var root = target.transform.Find("Formula Engine Audio");
        var selected = new List<AudioSource>(); var bands = new List<int>();
        foreach (var source in root.GetComponentsInChildren<AudioSource>())
        {
            if (!source.name.StartsWith("Engine Load ") && !source.name.StartsWith("Engine Coast ")) continue;
            int band = int.Parse(source.name.Substring(source.name.LastIndexOf(' ') + 1), CultureInfo.InvariantCulture);
            selected.Add(source); bands.Add(band);
        }
        sources = selected.ToArray(); sourceBands = bands.ToArray(); previousRpm = target.Feedback.rpm; recording = true;
    }
    private void LateUpdate()
    {
        if (!recording || engine == null || hud == null || Time.timeScale <= 0 || AudioListener.pause) return;
        var f = engine.Feedback;
        Result.frames++;
        if (engine.LastRenderedFrame != Time.frameCount || hud.InstrumentFrame != Time.frameCount || f.frame != Time.frameCount) Result.frameMismatches++;
        Result.maxAudioHudRpmDifference = Mathf.Max(Result.maxAudioHudRpmDifference, Mathf.Abs(f.rpm - hud.InstrumentRpm));
        string expected = f.displayGear < 0 ? "R" : f.displayGear == 0 ? "N" : f.displayGear.ToString(CultureInfo.InvariantCulture);
        if (hud.InstrumentGear != f.displayGear || gearText.text != expected) Result.gearMismatches++;
        if (rpmText.text != (Mathf.Round(f.rpm / 100) * 100).ToString("0", CultureInfo.InvariantCulture) + " RPM") Result.rpmTextMismatches++;
        if (f.rawRpm - previousRpm > 10) Result.risingFrames++;
        if (previousRpm - f.rawRpm > 10) Result.fallingFrames++;
        if (f.shifting) Result.shiftFrames++;
        previousRpm = f.rawRpm;
        var row = new Row { frame = Time.frameCount, dsp = AudioSettings.dspTime, rawRpm = f.rawRpm, feedbackRpm = f.rpm,
            hudRpm = hud.InstrumentRpm, gear = f.displayGear, shifting = f.shifting };
        for (int i = 0; i < sources.Length; i++)
        {
            var source = sources[i]; int band = sourceBands[i];
            if (source.clip == null) continue;
            float expectedPitch = FormulaEngineAudio.PitchForLayer(layers[band], f.rpm, f.idleRpm, f.maxRpm);
            if (source.isPlaying && source.volume > .001f)
                Result.maxPlaybackPitchErrorCents = Mathf.Max(Result.maxPlaybackPitchErrorCents, Mathf.Abs(1200 * Mathf.Log(source.pitch / expectedPitch, 2)));
            if (source.name.StartsWith("Engine Load "))
            {
                if (band == 0) row.pitch0 = source.pitch; else if (band == 1) row.pitch1 = source.pitch;
                else if (band == 2) row.pitch2 = source.pitch; else if (band == 3) row.pitch3 = source.pitch;
            }
        }
        rows.Add(row);
    }
    public void Finish(string path)
    {
        recording = false;
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("frame,dsp_seconds,raw_rpm,audio_rpm,hud_rpm,display_gear,shifting,pitch_idle,pitch_low,pitch_mid,pitch_high");
            foreach (var row in rows)
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F6},{2:F2},{3:F2},{4:F2},{5},{6},{7:F6},{8:F6},{9:F6},{10:F6}",
                    row.frame, row.dsp, row.rawRpm, row.feedbackRpm, row.hudRpm, row.gear, row.shifting ? 1 : 0, row.pitch0, row.pitch1, row.pitch2, row.pitch3));
        }
    }
}
#endif
