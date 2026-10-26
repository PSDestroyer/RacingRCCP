using System;
using UnityEngine;
using UnityEngine.Audio;

namespace FormulaGame
{
    // All Formula-owned audio references live here; recordings can be replaced
    // without changing vehicle physics or code. Vendor clips remain untouched.
    [CreateAssetMenu(menuName = "Formula/Sound Bank")]
    public sealed class FormulaSoundBank : ScriptableObject
    {
        [Serializable] public struct CueClip { public FormulaAudio.Cue cue; public AudioClip clip; }
        private static FormulaSoundBank current;
        public static FormulaSoundBank Current => current != null ? current : current = Resources.Load<FormulaSoundBank>("Formula/Audio/SoundBank");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { current = null; }
        public AudioMixer mixer;
        public AudioMixerGroup playerEngine, opponents, tyres, contacts, world, ui, music;
        public AudioClip rollAsphalt, rollKerb, rollGrass, rollGravel, rollWet, rollSnow;
        public AudioClip scrapeAsphalt, scrapeMetal, suspensionHit;
        public AudioClip skidAsphalt, skidGrass, skidSand;
        public AudioClip[] lightImpacts = Array.Empty<AudioClip>(), heavyImpacts = Array.Empty<AudioClip>();
        public AudioClip[] metalImpacts = Array.Empty<AudioClip>(), bodyImpacts = Array.Empty<AudioClip>();
        public AudioClip crowd, rain, garageRoom, menuMusic;
        public CueClip[] cues = Array.Empty<CueClip>();
        public AudioClip GetCue(FormulaAudio.Cue cue)
        {
            for (int i = 0; i < cues.Length; i++) if (cues[i].cue == cue) return cues[i].clip;
            return null;
        }
    }
}
