using UnityEngine;
using UnityEngine.Audio;

namespace FormulaGame
{
    public enum FormulaAudioBus { PlayerEngine, Opponents, Tyres, Contacts, World, UI, Music }

    public static class FormulaAudioMix
    {
        public static AudioMixerGroup Group(FormulaAudioBus bus)
        {
            var bank = FormulaSoundBank.Current;
            if (bank == null) return null;
            switch (bus)
            {
                case FormulaAudioBus.PlayerEngine: return bank.playerEngine;
                case FormulaAudioBus.Opponents: return bank.opponents;
                case FormulaAudioBus.Tyres: return bank.tyres;
                case FormulaAudioBus.Contacts: return bank.contacts;
                case FormulaAudioBus.World: return bank.world;
                case FormulaAudioBus.UI: return bank.ui;
                default: return bank.music;
            }
        }
        public static float Decibels(float linear) => linear <= .0001f ? -80 : Mathf.Log10(Mathf.Clamp01(linear)) * 20;
        public static void SetVehicleLevel(float value) => Set("VehicleVolume", value);
        public static void SetSfxLevel(float value) => Set("EffectsVolume", value);
        public static void SetMusicLevel(float value) => Set("MusicVolume", value);
        private static void Set(string parameter, float value)
        {
            var bank = FormulaSoundBank.Current;
            if (bank != null && bank.mixer != null) bank.mixer.SetFloat(parameter, Decibels(value));
        }
    }
}
