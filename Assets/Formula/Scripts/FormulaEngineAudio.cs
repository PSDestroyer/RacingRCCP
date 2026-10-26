using System;
using UnityEngine;

namespace FormulaGame
{
    /// <summary>Formula-owned presentation of RCCP telemetry. Never writes vehicle physics.</summary>
    [DisallowMultipleComponent]
    public sealed class FormulaEngineAudio : MonoBehaviour
    {
        [Serializable]
        public sealed class EngineLayer
        {
            public AudioClip loaded, coast;
            [Range(0, 1)] public float rpmAnchor;
            [Range(.5f, 2f)] public float pitchAtAnchor = 1;
            [Min(0), Tooltip("Measured RPM of the source recording. Zero uses the provisional RPM anchor.")]
            public float referenceRpm;
        }

        // Sampled once per rendered frame from the actual powertrain, shared by audio and HUD.
        public struct PowertrainFrame
        {
            public int frame, gearIndex, displayGear;
            public float rpm, rawRpm, idleRpm, maxRpm, shiftRpm;
            public bool shifting, limiterCut, hasHigherGear;
            public float RevFraction => Mathf.InverseLerp(idleRpm, maxRpm, rpm);
            public float ShiftFraction => Mathf.InverseLerp(idleRpm, shiftRpm, rpm);
            public bool ShiftDue => hasHigherGear && !shifting && rpm >= shiftRpm;
        }
        public const float RpmResponseSeconds = .012f;

        [Tooltip("Optional authored bank, ordered idle to redline. Empty uses the existing RCCP clips.")]
        [SerializeField] private EngineLayer[] layers;
        [SerializeField, Range(0, 1)] private float engineGain = .48f;
        [SerializeField, Range(0, 1)] private float shiftGain = .22f;
        [SerializeField, Range(0, 1)] private float windGain = .12f;

        private RCCP_CarController car;
        private RCCP_Engine engine;
        private RCCP_Gearbox gearbox;
        private RCCP_Audio legacy;
        private GamePlayManager game;
        private RCCP_Camera raceCamera;
        private Camera listenerCamera;
        private GameObject sourceRoot;
        private AudioSource[] loadedSources, coastSources, opponentSources;
        private AudioSource shiftSource, windSource, startupSource;
        private AudioLowPassFilter[] engineFilters;
        private AudioClip[] legacyCrashClips, shiftClips;
        private AudioClip windClip, startupClip;
        private AudioSource[] legacySources;
        private bool[] legacySourceMute;
        private bool legacyEnabled, captured, initialized, wasStarting, isPlayer;
        private int lastGear, shiftClipIndex;
        private float nextResolve, rpm, load, combustion = 1, cameraBlend;
        private float[] weights;
        private PowertrainFrame feedback;
        private int feedbackFrame = -1;
        private bool hasFeedback;

        public bool Configured => initialized;
        public bool LegacySuppressed => captured && legacy != null && !legacy.enabled && legacy.crashSound != null && (legacy.crashSound.audioClips == null || legacy.crashSound.audioClips.Length == 0);
        public bool IsPlayerVehicle => isPlayer;
        public float SmoothedRpm => rpm;
        public PowertrainFrame Feedback { get { SampleFeedback(); return feedback; } }
        public int LastRenderedFrame { get; private set; } = -1;
        public int LastShiftFrame { get; private set; } = -1;
        public float SmoothedLoad => load;
        public float LastOutputGain { get; private set; }
        public int ShiftCount { get; private set; }
        public int UpshiftCount { get; private set; }
        public int DownshiftCount { get; private set; }
        public bool LimiterCutActive { get; private set; }
        public int OwnedSourceCount => initialized ? (isPlayer ? layers.Length * 2 + 3 : 3) : 0;
        public int ActiveLoopCount
        {
            get
            {
                int count = 0;
                CountPlaying(loadedSources, ref count); CountPlaying(coastSources, ref count); CountPlaying(opponentSources, ref count);
                if (windSource != null && windSource.isPlaying && windSource.volume > .001f) count++;
                return count;
            }
        }

        private void Awake()
        {
            car = GetComponent<RCCP_CarController>();
            if (car == null) { enabled = false; return; }
            engine = car.GetComponentInChildren<RCCP_Engine>(true);
            gearbox = car.GetComponentInChildren<RCCP_Gearbox>(true);
            legacy = car.GetComponentInChildren<RCCP_Audio>(true);
            if (engine == null || gearbox == null || legacy == null) { enabled = false; return; }
            CaptureLegacy();
            SuppressLegacy();
        }

        private void OnEnable() { if (captured) SuppressLegacy(); }

        private void CaptureLegacy()
        {
            legacyEnabled = legacy.enabled;
            legacyCrashClips = legacy.crashSound != null ? legacy.crashSound.audioClips : null;
            shiftClips = legacy.gearboxSound != null ? legacy.gearboxSound.audioClips : null;
            windClip = legacy.windSound != null ? legacy.windSound.audioClips : null;
            startupClip = legacy.engineStart != null ? legacy.engineStart.audioClips : null;
            legacySources = Array.Empty<AudioSource>();
            legacySourceMute = Array.Empty<bool>();
            if (layers == null || layers.Length == 0) UseLegacyBank();
            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++)
                    if (layers[i] == null) layers[i] = new EngineLayer { rpmAnchor = (float)i / Mathf.Max(1, layers.Length - 1) };
                Array.Sort(layers, (a, b) => a.rpmAnchor.CompareTo(b.rpmAnchor));
            }
            captured = true;
        }

        private void UseLegacyBank()
        {
            if (legacy.engineSounds == null || legacy.engineSounds.Length == 0) return;
            var sounds = (RCCP_Audio.EngineSound[])legacy.engineSounds.Clone();
            Array.Sort(sounds, (a, b) => (a?.minRPM ?? 0).CompareTo(b?.minRPM ?? 0));
            layers = new EngineLayer[sounds.Length];
            for (int i = 0; i < sounds.Length; i++)
            {
                float anchor = sounds.Length == 4 ? (i == 0 ? 0 : i == 1 ? .22f : i == 2 ? .58f : 1) : (float)i / Mathf.Max(1, sounds.Length - 1);
                // These are provisional sports-car samples, not measured Formula recordings.
                // Anchor RPM is provisional until an RPM-calibrated bank replaces it.
                layers[i] = new EngineLayer { loaded = sounds[i]?.audioClipOn, coast = sounds[i]?.audioClipOff,
                    rpmAnchor = anchor, pitchAtAnchor = Mathf.Lerp(1.05f, 1.45f, anchor) };
            }
        }

        private void SuppressLegacy()
        {
            // Disabling this component temporarily hands ownership back to RCCP,
            // which can create new voices before its deferred source cache runs.
            // Capture those voices before RCCP.OnDisable can deactivate them.
            CaptureNewLegacySources();
            // RCCP calls Audio.OnCollision directly, even when its component is disabled.
            if (legacy.crashSound != null) legacy.crashSound.audioClips = Array.Empty<AudioClip>();
            legacy.enabled = false;
            for (int i = 0; i < legacySources.Length; i++)
                if (legacySources[i] != null) { legacySources[i].Stop(); legacySources[i].mute = true; }
        }

        private void CaptureNewLegacySources()
        {
            var current = legacy.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < current.Length; i++)
            {
                var source = current[i];
                // Wheel audio has a separate owner that restores its original mute state.
                if (source == null || source.name == "Skid Sound AudioSource" || Array.IndexOf(legacySources, source) >= 0) continue;
                int index = legacySources.Length;
                Array.Resize(ref legacySources, index + 1);
                Array.Resize(ref legacySourceMute, index + 1);
                legacySources[index] = source;
                legacySourceMute[index] = source.mute;
            }
        }

        private void SampleFeedback()
        {
            if (!captured || feedbackFrame == Time.frameCount) return;
            feedbackFrame = Time.frameCount;
            float raw = Mathf.Max(0, engine.engineRPM);
            if (!hasFeedback) { rpm = raw; hasFeedback = true; }
            else if (Time.timeScale > 0 && !AudioListener.pause)
                rpm = Damp(rpm, raw, RpmResponseSeconds, Mathf.Min(Time.unscaledDeltaTime, .1f));
            bool reverse = gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.InReverseGear;
            bool neutral = gearbox.currentGearState.gearState == RCCP_Gearbox.CurrentGearState.GearState.Neutral;
            feedback = new PowertrainFrame
            {
                frame = feedbackFrame, rpm = rpm, rawRpm = raw,
                idleRpm = engine.minEngineRPM, maxRpm = engine.maxEngineRPM,
                shiftRpm = Mathf.Clamp(gearbox.shiftUpRPM, engine.minEngineRPM + 1, engine.maxEngineRPM),
                gearIndex = gearbox.currentGear,
                displayGear = DisplayGearFor(gearbox.currentGear, reverse, neutral),
                shifting = gearbox.shiftingNow,
                limiterCut = engine.engineRevLimiter && engine.cutFuel && raw >= engine.maxEngineRPM - 180,
                hasHigherGear = !reverse && !neutral && gearbox.currentGear < gearbox.gearRatios.Length - 1
            };
        }

        public static int DisplayGearFor(int gearIndex, bool reverse, bool neutral) => reverse ? -1 : neutral ? 0 : Mathf.Max(0, gearIndex) + 1;
        public static float ShiftLightThreshold(int index, int count) => Mathf.Lerp(.45f, 1, count <= 1 ? 1 : (float)index / (count - 1));

        private void LateUpdate()
        {
            if (!captured) return;
            if (!FormulaSession.InRace) { StopOwnedSources(); return; }
            if (!initialized)
            {
                if (Time.unscaledTime < nextResolve) return;
                nextResolve = Time.unscaledTime + .25f;
                game = FindFirstObjectByType<GamePlayManager>();
                // The registered player is authoritative even if a test/finish driver adds RCCP_AI.
                if (game == null || game.CarController == null || game.gameObject.scene != gameObject.scene) return;
                isPlayer = game.CarController == car;
                if (layers == null || layers.Length == 0) return;
                InitializeSources();
            }
            if (AudioListener.pause || Time.timeScale <= 0) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            var sample = Feedback;
            float throttle = Mathf.Clamp01(car.throttleInput_V);
            float torque = Mathf.Clamp01(engine.producedTorqueAsNM / Mathf.Max(1, engine.maximumTorqueAsNM));
            float targetLoad = throttle * (.6f + .25f * Mathf.Clamp01(engine.engineLoad) + .15f * torque);
            load = Damp(load, targetLoad, targetLoad > load ? .055f : .11f, dt);
            LimiterCutActive = sample.limiterCut;
            float cutTarget = !engine.engineRunning ? 0 : LimiterCutActive ? .3f : sample.shifting ? .48f : 1;
            combustion = Damp(combustion, cutTarget, cutTarget < combustion ? .012f : .025f, dt);

            if (lastGear != sample.gearIndex)
            {
                bool up = sample.gearIndex > lastGear;
                if (engine.engineRunning && car.absoluteSpeed > 3) PlayShift(up);
                lastGear = sample.gearIndex;
            }
            if (startupSource != null && engine.engineStarting && !wasStarting && startupClip != null)
            {
                startupSource.clip = startupClip; startupSource.volume = .25f; startupSource.Play();
            }
            wasStarting = engine.engineStarting;

            float normalized = Mathf.InverseLerp(engine.minEngineRPM, engine.maxEngineRPM, rpm);
            CalculateLayerWeights(layers, normalized, weights);
            if (isPlayer) UpdatePlayer(dt);
            else UpdateOpponent(dt);
            LastRenderedFrame = sample.frame;
        }

        private void InitializeSources()
        {
            sourceRoot = new GameObject("Formula Engine Audio");
            sourceRoot.transform.SetParent(transform, false);
            weights = new float[layers.Length];
            var scene = RCCP_SceneManager.Instance;
            raceCamera = scene != null ? scene.activePlayerCamera : null;
            listenerCamera = Camera.main;
            if (isPlayer)
            {
                loadedSources = new AudioSource[layers.Length]; coastSources = new AudioSource[layers.Length];
                engineFilters = new AudioLowPassFilter[layers.Length * 2];
                for (int i = 0; i < layers.Length; i++)
                {
                    loadedSources[i] = NewSource("Engine Load " + i, FormulaAudioBus.PlayerEngine, true, 24);
                    coastSources[i] = NewSource("Engine Coast " + i, FormulaAudioBus.PlayerEngine, true, 28);
                    loadedSources[i].clip = layers[i]?.loaded; coastSources[i].clip = layers[i]?.coast ?? layers[i]?.loaded;
                    engineFilters[i * 2] = loadedSources[i].gameObject.AddComponent<AudioLowPassFilter>();
                    engineFilters[i * 2 + 1] = coastSources[i].gameObject.AddComponent<AudioLowPassFilter>();
                }
                windSource = NewSource("Cockpit Wind", FormulaAudioBus.PlayerEngine, true, 100); windSource.clip = windClip;
                startupSource = NewSource("Engine Start", FormulaAudioBus.PlayerEngine, false, 35);
            }
            else
            {
                opponentSources = new[] { NewSource("Opponent Engine A", FormulaAudioBus.Opponents, true, 110), NewSource("Opponent Engine B", FormulaAudioBus.Opponents, true, 115) };
                engineFilters = new[] { opponentSources[0].gameObject.AddComponent<AudioLowPassFilter>(), opponentSources[1].gameObject.AddComponent<AudioLowPassFilter>() };
            }
            shiftSource = NewSource("Gear Change", isPlayer ? FormulaAudioBus.PlayerEngine : FormulaAudioBus.Opponents, false, isPlayer ? 30 : 120);
            lastGear = gearbox.currentGear; wasStarting = engine.engineStarting;
            load = Mathf.Clamp01(car.throttleInput_V);
            initialized = true;
        }

        private AudioSource NewSource(string label, FormulaAudioBus bus, bool loop, int priority)
        {
            var child = new GameObject(label); child.transform.SetParent(sourceRoot.transform, false);
            child.transform.localPosition = new Vector3(0, .55f, -.9f);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.volume = 0; source.priority = priority;
            source.outputAudioMixerGroup = FormulaAudioMix.Group(bus);
            source.spatialBlend = isPlayer ? .15f : 1; source.dopplerLevel = isPlayer ? 0 : .35f;
            source.rolloffMode = AudioRolloffMode.Logarithmic; source.minDistance = isPlayer ? 8 : 5; source.maxDistance = 110;
            source.reverbZoneMix = .15f; return source;
        }

        private void UpdatePlayer(float dt)
        {
            bool cockpit = raceCamera != null && raceCamera.cameraMode == RCCP_Camera.CameraMode.FPS;
            cameraBlend = Damp(cameraBlend, cockpit ? 1 : 0, .2f, dt);
            float cameraGain = Mathf.Lerp(1, .86f, cameraBlend);
            float on = Mathf.Sqrt(load), off = Mathf.Sqrt(1 - load);
            LastOutputGain = engineGain * combustion * cameraGain;
            for (int i = 0; i < layers.Length; i++)
            {
                float pitch = PitchForLayer(layers[i], rpm, engine.minEngineRPM, engine.maxEngineRPM);
                float gain = weights[i] * LastOutputGain;
                if (loadedSources[i].clip == coastSources[i].clip)
                {
                    // The donor idle uses the same clip in both slots; doubling coherent loops boosts it.
                    SetLoop(loadedSources[i], gain * Mathf.Lerp(.82f, 1, load), pitch, dt);
                    SetLoop(coastSources[i], 0, pitch, dt);
                }
                else
                {
                    SetLoop(loadedSources[i], gain * on, pitch, dt);
                    SetLoop(coastSources[i], gain * off * .82f, pitch, dt);
                }
                float cutoff = Mathf.Lerp(19000, 10000, cameraBlend);
                engineFilters[i * 2].cutoffFrequency = cutoff; engineFilters[i * 2 + 1].cutoffFrequency = cutoff;
            }
            float speed = Mathf.InverseLerp(35, 300, car.absoluteSpeed);
            SetLoop(windSource, windGain * speed * speed * Mathf.Lerp(.65f, 1, cameraBlend), Mathf.Lerp(.85f, 1.15f, speed), dt);
        }

        private void UpdateOpponent(float dt)
        {
            float distance = listenerCamera != null ? Vector3.Distance(listenerCamera.transform.position, transform.position) : 0;
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(65, 110, distance));
            LastOutputGain = engineGain * .7f * combustion * fade * Mathf.Lerp(.5f, 1, load);
            // Two sources per opponent: retain each band in its parity slot through crossfades.
            // Unlike the local car, opponents do not allocate throttle-on/off pairs or wind loops.
            for (int slot = 0; slot < 2; slot++)
            {
                int band = -1;
                for (int i = slot; i < layers.Length; i += 2) if (weights[i] > .0001f) { band = i; break; }
                var source = opponentSources[slot];
                if (band >= 0)
                {
                    var clip = layers[band]?.loaded ?? layers[band]?.coast;
                    if (source.clip != clip) { source.Stop(); source.volume = 0; source.clip = clip; }
                    SetLoop(source, weights[band] * LastOutputGain, PitchForLayer(layers[band], rpm, engine.minEngineRPM, engine.maxEngineRPM), dt);
                }
                else SetLoop(source, 0, source.pitch, dt);
                engineFilters[slot].cutoffFrequency = Mathf.Lerp(15000, 2200, Mathf.InverseLerp(10, 100, distance));
            }
        }

        private void PlayShift(bool up)
        {
            LastShiftFrame = feedback.frame;
            ShiftCount++; if (up) UpshiftCount++; else DownshiftCount++;
            if (shiftClips == null || shiftClips.Length == 0 || shiftSource == null || (!isPlayer && listenerCamera != null && Vector3.Distance(listenerCamera.transform.position, transform.position) > 45)) return;
            int split = Mathf.Max(1, (shiftClips.Length + 1) / 2);
            int start = up ? 0 : Mathf.Min(split, shiftClips.Length - 1);
            int length = up ? split : Mathf.Max(1, shiftClips.Length - start);
            shiftSource.clip = shiftClips[start + shiftClipIndex++ % length];
            if (shiftSource.clip == null) return;
            shiftSource.pitch = up ? 1.08f : .94f;
            shiftSource.volume = shiftGain * (isPlayer ? 1 : .55f) * (up ? 1 : .8f);
            shiftSource.Play();
            // Generic donor shifts contain long road-car tails; keep only a short mechanical transient.
            shiftSource.SetScheduledEndTime(AudioSettings.dspTime + Mathf.Min(.22f, shiftSource.clip.length / shiftSource.pitch));
        }

        private static void SetLoop(AudioSource source, float gain, float pitch, float dt)
        {
            if (source == null || source.clip == null) return;
            gain = Mathf.Clamp01(gain);
            source.volume = Damp(source.volume, gain, gain > source.volume ? .035f : .065f, dt);
            // RPM is already dezippered once in the shared audio/HUD frame.
            source.pitch = pitch;
            if (gain > .001f && !source.isPlaying) { source.time = (float)(AudioSettings.dspTime % source.clip.length); source.Play(); }
            else if (gain <= .001f && source.volume < .001f && source.isPlaying) { source.Stop(); source.volume = 0; }
        }

        public static float Damp(float current, float target, float seconds, float deltaTime)
        {
            if (deltaTime <= 0) return current;
            return Mathf.Lerp(current, target, 1 - Mathf.Exp(-deltaTime / Mathf.Max(.001f, seconds)));
        }

        public static void CalculateLayerWeights(EngineLayer[] bank, float normalizedRpm, float[] result)
        {
            if (bank == null || result == null || result.Length < bank.Length) return;
            Array.Clear(result, 0, result.Length);
            if (bank.Length == 0) return;
            float rpm01 = Mathf.Clamp01(normalizedRpm);
            if (rpm01 <= bank[0].rpmAnchor) { result[0] = 1; return; }
            for (int i = 0; i < bank.Length - 1; i++)
            {
                if (rpm01 > bank[i + 1].rpmAnchor) continue;
                float blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(bank[i].rpmAnchor, bank[i + 1].rpmAnchor, rpm01));
                result[i] = Mathf.Sqrt(1 - blend); result[i + 1] = Mathf.Sqrt(blend); return;
            }
            result[bank.Length - 1] = 1;
        }

        public static float PitchForLayer(EngineLayer layer, float rpm, float idleRpm, float maxRpm)
        {
            if (layer == null) return 1;
            float reference = layer.referenceRpm > 0 ? layer.referenceRpm : Mathf.Lerp(idleRpm, maxRpm, layer.rpmAnchor);
            // Broad safety bounds stay outside the active adjacent-band range. A narrow
            // per-layer ratio clamp creates audible plateaus as the tachometer climbs.
            return Mathf.Clamp(layer.pitchAtAnchor * Mathf.Max(0, rpm) / Mathf.Max(1, reference), .25f, 3f);
        }

        private static void CountPlaying(AudioSource[] sources, ref int count)
        {
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++) if (sources[i] != null && sources[i].isPlaying && sources[i].volume > .001f) count++;
        }

        private void StopOwnedSources()
        {
            Stop(loadedSources); Stop(coastSources); Stop(opponentSources);
            if (windSource != null) windSource.Stop(); if (shiftSource != null) shiftSource.Stop(); if (startupSource != null) startupSource.Stop();
            LastOutputGain = 0;
        }
        private static void Stop(AudioSource[] sources)
        {
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++) if (sources[i] != null) { sources[i].Stop(); sources[i].volume = 0; }
        }

        private void OnDisable()
        {
            StopOwnedSources();
            if (!captured || legacy == null) return;
            if (legacy.crashSound != null) legacy.crashSound.audioClips = legacyCrashClips;
            for (int i = 0; i < legacySources.Length; i++) if (legacySources[i] != null) legacySources[i].mute = legacySourceMute[i];
            legacy.enabled = legacyEnabled;
        }

        private void OnDestroy() { if (sourceRoot != null) Destroy(sourceRoot); }
    }
}
