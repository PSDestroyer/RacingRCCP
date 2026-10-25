using UnityEngine;

namespace FormulaGame
{
    // Authored cues share the existing SFX source, mixer and player volume setting.
    public static class FormulaAudio
    {
        public enum Cue { Focus, Confirm, Record, Result, Unlock, End }
        private static float lastFocus = -1;
        private static int lastFrame = -1;
        private static Cue lastCue;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { lastFocus = -1; lastFrame = -1; }

        public static void Play(Cue cue)
        {
            if (SoundManager.Instance == null) return;
            var assets = FormulaMenuAssets.Current;
            if (assets == null) return;
            if (cue == Cue.Focus && Time.unscaledTime - lastFocus < .075f) return;
            if (lastFrame == Time.frameCount && lastCue == cue) return;
            lastCue = cue; lastFrame = Time.frameCount;
            if (cue == Cue.Focus) lastFocus = Time.unscaledTime;
            else if (cue != Cue.Record) lastFocus = Time.unscaledTime + .1f;
            AudioClip clip = cue == Cue.Focus ? assets.focusSound : cue == Cue.Confirm ? assets.confirmSound
                : cue == Cue.Record ? assets.recordSound : cue == Cue.Result ? assets.resultSound
                : cue == Cue.Unlock ? assets.unlockSound : assets.endSound;
            SoundManager.Instance.PlaySfx(clip, cue == Cue.Focus ? .22f : cue == Cue.Confirm ? .5f : .7f);
        }
    }
}
