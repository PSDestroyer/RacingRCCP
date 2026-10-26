using System.Collections.Generic;
using UnityEngine;

namespace FormulaGame
{
    /// <summary>Observes RCCP contacts and wheel telemetry; it never writes to vehicle physics.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RCCP_CarController))]
    public sealed class FormulaContactAudio : MonoBehaviour
    {
        private enum ImpactFamily { Surface, Underbody, Body, Metal, Barrier }
        private sealed class WheelVoice
        {
            public RCCP_WheelCollider wheel;
            public AudioSource rolling;
            public float previousForce, airborneTime, travel;
            public bool sampled, grounded;
        }
        private struct LegacyVoice { public AudioSource source; public bool muted; }
        private static readonly List<FormulaContactAudio> cars = new List<FormulaContactAudio>(12);
        private readonly Dictionary<Collider, FormulaAudioSurface> surfaces = new Dictionary<Collider, FormulaAudioSurface>(64);
        private readonly List<LegacyVoice> legacy = new List<LegacyVoice>(4);
        private RCCP_CarController car;
        private Rigidbody body;
        private GamePlayManager game;
        private FormulaSoundBank bank;
        private WheelVoice[] wheels;
        private AudioSource[] transients;
        private AudioSource skid, scrape, opponentRolling;
        private AudioLowPassFilter skidFilter;
        private Transform voices;
        private bool initialized, player, audible, resetWheelHistory = true;
        private float enabledAt, nextBudgetCheck, nextLegacyScan, nextImpact, nextBump, nextInitialize;
        private int previousLight = -1, previousHeavy = -1;
        private float pendingGain, pendingSeverity, pendingImpulse, pendingAt;
        private ImpactFamily pendingFamily;
        private Vector3 pendingPoint;
        private float scrapeContactTime = -100, scrapeSampleTime = -100, scrapeTarget, scrapeSpeed;
        private Vector3 scrapePoint;
        private FormulaAudioSurfaceKind scrapeKind;
        public int ImpactCount { get; private set; }
        public int SuspensionHitCount { get; private set; }
        public float LastImpactGain { get; private set; }
        public float LastImpactImpulse { get; private set; }
        public string LastImpactCategory { get; private set; }
        public bool ScrapeActive => scrape != null && scrape.isPlaying && scrape.volume > .01f;
        public int GroundedWheels { get; private set; }
        public FormulaAudioSurfaceKind LastSurface { get; private set; }
        public bool LegacySkidSuppressed => legacy.Count > 0;
        public int ActiveVoices
        {
            get
            {
                int count = 0;
                if (wheels != null) foreach (var w in wheels) if (w.rolling != null && w.rolling.isPlaying) count++;
                if (transients != null) foreach (var source in transients) if (source != null && source.isPlaying) count++;
                if (skid != null && skid.isPlaying) count++;
                if (scrape != null && scrape.isPlaying) count++;
                if (opponentRolling != null && opponentRolling.isPlaying) count++;
                return count;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => cars.Clear();
        private void OnEnable()
        {
            if (!cars.Contains(this)) cars.Add(this);
            enabledAt = Time.time;
            resetWheelHistory = true;
            nextBudgetCheck = nextLegacyScan = 0;
        }

        private void Start()
        {
            car = GetComponent<RCCP_CarController>();
            body = car.Rigid;
            game = FindFirstObjectByType<GamePlayManager>();
            TryInitialize();
        }

        private void TryInitialize()
        {
            // The race manager registers its player after spawning cars. Role remains authoritative even
            // when a test/replay temporarily drives the player with RCCP_AI.
            if (!FormulaSession.InRace || car == null || body == null) return;
            if (game == null) game = FindFirstObjectByType<GamePlayManager>();
            if (game == null || game.CarController == null) return;
            bank = FormulaSoundBank.Current;
            if (bank == null) return;
            player = game.CarController == car;
            var node = new GameObject("Formula Contact Audio");
            node.transform.SetParent(transform, false); voices = node.transform;
            var colliders = car.AllWheelColliders;
            wheels = new WheelVoice[colliders.Length];
            for (int i = 0; i < wheels.Length; i++)
                wheels[i] = new WheelVoice { wheel = colliders[i], rolling = player ? Source("Wheel " + i + " Rolling", true, true) : null };
            if (!player) opponentRolling = Source("Opponent Road", true, true);
            skid = Source("Tyre Scrub", true, true);
            skidFilter = skid.gameObject.AddComponent<AudioLowPassFilter>();
            scrape = Source("Body Scrape", true, false);
            transients = new AudioSource[player ? 4 : 2];
            for (int i = 0; i < transients.Length; i++) transients[i] = Source("Contact " + i, false, false);
            initialized = true;
            SuppressLegacySkids();
        }

        private AudioSource Source(string name, bool loop, bool tyre)
        {
            var child = new GameObject(name); child.transform.SetParent(voices, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.volume = 0;
            source.spatialBlend = 1; source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = player ? 7 : 3; source.maxDistance = player ? 65 : 50;
            source.priority = player ? tyre ? 45 : 25 : tyre ? 175 : 130;
            source.outputAudioMixerGroup = FormulaAudioMix.Group(!player ? FormulaAudioBus.Opponents : tyre ? FormulaAudioBus.Tyres : FormulaAudioBus.Contacts);
            return source;
        }

        private void Update()
        {
            if (!initialized)
            {
                if (Time.unscaledTime >= nextInitialize) { TryInitialize(); nextInitialize = Time.unscaledTime + .25f; }
                if (!initialized) return;
            }
            if (Time.unscaledTime >= nextLegacyScan && legacy.Count < wheels.Length)
            {
                SuppressLegacySkids(); nextLegacyScan = Time.unscaledTime + 1;
            }
            if (!FormulaSession.InRace || Time.timeScale <= 0 || AudioListener.pause)
            {
                StopAll(); resetWheelHistory = true; pendingGain = 0; return;
            }
            if (Time.unscaledTime >= nextBudgetCheck)
            {
                bool wasAudible = audible;
                audible = WithinBudget(); nextBudgetCheck = Time.unscaledTime + .25f;
                if (audible != wasAudible) resetWheelHistory = true;
            }
            if (!audible) { StopAll(); pendingGain = 0; return; }
            UpdateWheels(Time.deltaTime);
            UpdateScrape(Time.deltaTime);
            FlushImpact();
        }

        private bool WithinBudget()
        {
            var target = game != null ? game.CarController : null;
            if (target == car) return true;
            if (target == null) return false;
            float distance = (target.transform.position - transform.position).sqrMagnitude;
            if (distance > 50 * 50) return false;
            int nearer = 0;
            for (int i = 0; i < cars.Count; i++)
            {
                var other = cars[i];
                if (other == null || other == this || other.car == target || !other.initialized) continue;
                float otherDistance = (target.transform.position - other.transform.position).sqrMagnitude;
                if (otherDistance < distance || Mathf.Approximately(otherDistance, distance) && other.GetInstanceID() < GetInstanceID()) nearer++;
            }
            return nearer < 2;
        }

        private void UpdateWheels(float dt)
        {
            float speed = body.linearVelocity.magnitude;
            float speedGain = Mathf.InverseLerp(.6f, 58f, speed);
            float expectedLoad = body.mass * Physics.gravity.magnitude / Mathf.Max(1, wheels.Length);
            float maxSlip = 0, bump = 0, rollGain = 0;
            Vector3 slipPoint = transform.position, bumpPoint = transform.position, roadPoint = transform.position;
            FormulaAudioSurfaceKind slipKind = FormulaAudioSurfaceKind.Asphalt, roadKind = FormulaAudioSurfaceKind.Asphalt;
            GroundedWheels = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                var state = wheels[i]; var wheel = state.wheel;
                bool grounded = wheel != null && wheel.enabled && wheel.WheelCollider.enabled && wheel.isGrounded && wheel.wheelHit.collider != null;
                if (!grounded)
                {
                    state.airborneTime += dt; state.grounded = false; state.previousForce = 0;
                    Loop(state.rolling, null, 0, 1, transform.position, dt);
                    continue;
                }
                var hit = wheel.wheelHit;
                var kind = Surface(hit.collider, hit.point); LastSurface = kind; GroundedWheels++;
                float force = Mathf.Max(0, hit.force);
                float load = Mathf.Clamp(force / Mathf.Max(1, expectedLoad), .15f, 1.5f);
                state.travel += speed * dt;
                float kerb = kind == FormulaAudioSurfaceKind.Kerb ? .68f + .32f * Mathf.Abs(Mathf.Sin(state.travel * 13.9626f)) : 1;
                float gain = speed > .6f ? Mathf.Lerp(.025f, .3f, Mathf.Sqrt(speedGain)) * load * kerb : 0;
                Loop(state.rolling, RollingClip(kind), gain / Mathf.Max(1, wheels.Length), Mathf.Lerp(.7f, 1.35f, speedGain), hit.point, dt);
                if (gain > rollGain) { rollGain = gain; roadKind = kind; roadPoint = hit.point; }
                float slip = Mathf.Max(Mathf.Abs(hit.sidewaysSlip), Mathf.Abs(hit.forwardSlip));
                if (slip > maxSlip && speed > 1.2f) { maxSlip = slip; slipPoint = hit.point; slipKind = kind; }
                if (!resetWheelHistory && state.sampled && speed > 1.5f)
                {
                    float forceRise = Mathf.Max(0, force - state.previousForce) / Mathf.Max(1, expectedLoad);
                    // A new grounded frame after airtime is a landing; regular force rises are suspension hits.
                    float strength = !state.grounded && state.airborneTime > .08f ? force / Mathf.Max(1, expectedLoad) : forceRise;
                    float value = Mathf.InverseLerp(.85f, 5, strength);
                    if (value > bump) { bump = value; bumpPoint = hit.point; }
                }
                state.sampled = true; state.grounded = true; state.airborneTime = 0; state.previousForce = force;
            }
            resetWheelHistory = false;
            if (!player) Loop(opponentRolling, RollingClip(roadKind), rollGain * .65f, Mathf.Lerp(.7f, 1.35f, speedGain), roadPoint, dt);
            float slipAmount = Mathf.InverseLerp(.065f, .55f, maxSlip);
            float skidGain = GroundedWheels > 0 && speed > 1.2f ? Mathf.Sqrt(slipAmount) * (player ? .4f : .19f) : 0;
            skidFilter.cutoffFrequency = Mathf.Lerp(1100, 6500, slipAmount);
            Loop(skid, SkidClip(slipKind), skidGain, Mathf.Lerp(.76f, 1.08f, slipAmount), slipPoint, dt);
            if (bump > 0 && Time.time >= nextBump && Time.time - enabledAt > .6f)
            {
                if (OneShot(bank.suspensionHit, Mathf.Lerp(.07f, .28f, Mathf.Sqrt(bump)), bumpPoint, .91f, 1.09f)) SuspensionHitCount++;
                nextBump = Time.time + .18f;
            }
        }

        private AudioClip RollingClip(FormulaAudioSurfaceKind kind)
        {
            switch (kind)
            {
                case FormulaAudioSurfaceKind.Kerb: return bank.rollKerb;
                case FormulaAudioSurfaceKind.Grass: return bank.rollGrass;
                case FormulaAudioSurfaceKind.Gravel: return bank.rollGravel;
                case FormulaAudioSurfaceKind.WetAsphalt: return bank.rollWet;
                case FormulaAudioSurfaceKind.Snow: return bank.rollSnow;
                default: return bank.rollAsphalt;
            }
        }
        private AudioClip SkidClip(FormulaAudioSurfaceKind kind) => kind == FormulaAudioSurfaceKind.Grass || kind == FormulaAudioSurfaceKind.Snow ? bank.skidGrass
            : kind == FormulaAudioSurfaceKind.Gravel ? bank.skidSand : bank.skidAsphalt;
        private FormulaAudioSurfaceKind Surface(Collider collider, Vector3 position)
        {
            if (collider == null) return FormulaAudioSurfaceKind.Asphalt;
            if (!surfaces.TryGetValue(collider, out var surface))
            {
                surface = collider.GetComponentInParent<FormulaAudioSurface>();
                if (surfaces.Count >= 256) surfaces.Clear();
                surfaces[collider] = surface;
            }
            return surface != null ? surface.Sample(position) : FormulaAudioSurfaceKind.Asphalt;
        }

        /// <summary>Mass-normalized impact strength. Normal speed preserves light contact detail.</summary>
        public static float EquivalentImpactSpeed(float impulse, float mass, float normalSpeed) =>
            Mathf.Max(Mathf.Max(0, impulse) / Mathf.Max(1, mass), Mathf.Abs(normalSpeed) * .55f);
        public static float ImpactGain(float impulse, float mass, float normalSpeed)
        {
            float speed = EquivalentImpactSpeed(impulse, mass, normalSpeed);
            if (speed <= .12f) return 0;
            return Mathf.Lerp(.1f, .85f, Mathf.Sqrt(Mathf.InverseLerp(.12f, 8f, speed)));
        }
        private bool CanContact => initialized && audible && enabled && FormulaSession.InRace && Time.timeScale > 0 && !AudioListener.pause && Time.time - enabledAt > .6f;
        private void OnCollisionEnter(Collision collision)
        {
            if (!CanContact || collision.contactCount == 0) return;
            var first = collision.GetContact(0);
            float normalSpeed = 0; Vector3 point = first.point, normal = first.normal;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                float value = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
                if (value > normalSpeed) { normalSpeed = value; point = contact.point; normal = contact.normal; }
            }
            float impulse = collision.impulse.magnitude;
            float gain = ImpactGain(impulse, body.mass, normalSpeed);
            if (gain > pendingGain)
            {
                pendingGain = gain; pendingSeverity = EquivalentImpactSpeed(impulse, body.mass, normalSpeed);
                pendingImpulse = impulse; pendingPoint = point; pendingAt = Time.time;
                var kind = Surface(collision.collider, point);
                bool otherVehicle = collision.rigidbody != null && collision.rigidbody.GetComponent<RCCP_CarController>() != null;
                pendingFamily = otherVehicle ? ImpactFamily.Body : kind == FormulaAudioSurfaceKind.Metal ? ImpactFamily.Metal
                    : kind == FormulaAudioSurfaceKind.Barrier ? ImpactFamily.Barrier
                    : Vector3.Dot(normal, Vector3.up) > .45f && transform.InverseTransformPoint(point).y < .25f ? ImpactFamily.Underbody : ImpactFamily.Surface;
            }
            CaptureScrape(collision);
        }
        private void OnCollisionStay(Collision collision) { if (CanContact) CaptureScrape(collision); }
        private void OnCollisionExit(Collision collision)
        {
            // Other chassis contacts may remain active. The short contact timeout ends the loop safely.
        }
        private void CaptureScrape(Collision collision)
        {
            if (collision.contactCount == 0) return;
            if (scrapeSampleTime != Time.fixedTime) { scrapeTarget = 0; scrapeSampleTime = Time.fixedTime; }
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                float tangentSpeed = Vector3.ProjectOnPlane(collision.relativeVelocity, contact.normal).magnitude;
                if (tangentSpeed < 1.4f) continue;
                float load = Mathf.Clamp01(collision.impulse.magnitude / Mathf.Max(1, body.mass * .5f));
                float gain = Mathf.InverseLerp(1.4f, 25, tangentSpeed) * Mathf.Lerp(.12f, .48f, Mathf.Sqrt(load));
                if (gain <= scrapeTarget) continue;
                scrapeTarget = gain; scrapeSpeed = tangentSpeed; scrapePoint = contact.point;
                scrapeKind = Surface(collision.collider, contact.point);
                if (collision.rigidbody != null && collision.rigidbody.mass > 100) scrapeKind = FormulaAudioSurfaceKind.Metal;
                scrapeContactTime = Time.time;
            }
        }
        private void UpdateScrape(float dt)
        {
            float gain = Time.time - scrapeContactTime <= Mathf.Max(.06f, Time.fixedDeltaTime * 2.5f) ? scrapeTarget : 0;
            bool metal = scrapeKind == FormulaAudioSurfaceKind.Metal || scrapeKind == FormulaAudioSurfaceKind.Barrier;
            Loop(scrape, metal ? bank.scrapeMetal : bank.scrapeAsphalt, gain * (player ? 1 : .65f), Mathf.Lerp(.78f, 1.25f, Mathf.InverseLerp(0, 40, scrapeSpeed)), scrapePoint, dt);
        }
        private void FlushImpact()
        {
            if (pendingGain <= 0) return;
            if (Time.time - pendingAt > .14f) { pendingGain = 0; return; }
            if (Time.time < nextImpact) return;
            bool heavy = pendingSeverity >= 2.2f;
            var clips = pendingFamily == ImpactFamily.Body ? bank.bodyImpacts : pendingFamily == ImpactFamily.Metal ? bank.metalImpacts
                : pendingFamily == ImpactFamily.Barrier ? bank.heavyImpacts : heavy ? bank.heavyImpacts : bank.lightImpacts;
            if (clips == null || clips.Length == 0) clips = heavy ? bank.heavyImpacts : bank.lightImpacts;
            int previous = heavy ? previousHeavy : previousLight;
            var clip = Pick(clips, ref previous);
            if (heavy) previousHeavy = previous; else previousLight = previous;
            if (OneShot(clip, pendingGain * (player ? 1 : .65f), pendingPoint, .94f, 1.06f))
            {
                ImpactCount++; LastImpactGain = pendingGain; LastImpactImpulse = pendingImpulse; LastImpactCategory = pendingFamily.ToString();
            }
            nextImpact = Time.time + .085f; pendingGain = 0;
        }
        private static AudioClip Pick(AudioClip[] clips, ref int previous)
        {
            if (clips == null || clips.Length == 0) return null;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == previous) index = (index + Random.Range(1, clips.Length)) % clips.Length;
            previous = index; return clips[index];
        }
        private bool OneShot(AudioClip clip, float gain, Vector3 point, float lowPitch, float highPitch)
        {
            if (clip == null || gain <= .001f || transients == null) return false;
            AudioSource chosen = null;
            foreach (var source in transients)
            {
                if (!source.isPlaying) { chosen = source; break; }
                if (chosen == null || source.volume < chosen.volume) chosen = source;
            }
            if (chosen == null || chosen.isPlaying && chosen.volume > gain) return false;
            chosen.Stop(); chosen.transform.position = point; chosen.clip = clip;
            chosen.volume = gain; chosen.pitch = Random.Range(lowPitch, highPitch); chosen.Play();
            return true;
        }
        private static void Loop(AudioSource source, AudioClip clip, float gain, float pitch, Vector3 point, float dt)
        {
            if (source == null) return;
            if (clip == null) gain = 0;
            if (source.clip != clip && gain > .001f)
            {
                source.Stop(); source.clip = clip; source.volume = 0;
            }
            source.transform.position = point;
            source.volume = Mathf.MoveTowards(source.volume, gain, dt * (gain > source.volume ? 1.7f : 3.5f));
            source.pitch = Mathf.MoveTowards(source.pitch, pitch, dt * 4);
            if (gain > .001f && clip != null && !source.isPlaying)
            {
                source.time = Random.Range(0f, Mathf.Max(0, clip.length - .02f)); source.Play();
            }
            if (source.volume <= .001f && gain <= .001f) source.Stop();
        }
        private void SuppressLegacySkids()
        {
            foreach (var source in GetComponentsInChildren<AudioSource>(true))
            {
                if (source.name != "Skid Sound AudioSource") continue;
                bool known = false;
                for (int i = 0; i < legacy.Count; i++) if (legacy[i].source == source) { known = true; break; }
                if (!known) legacy.Add(new LegacyVoice { source = source, muted = source.mute });
                source.mute = true;
            }
        }
        private void StopAll()
        {
            if (wheels != null) foreach (var wheel in wheels) Stop(wheel.rolling);
            Stop(skid); Stop(scrape); Stop(opponentRolling);
            if (transients != null) foreach (var source in transients) Stop(source);
            scrapeContactTime = -100;
        }
        private static void Stop(AudioSource source) { if (source != null) { source.Stop(); source.volume = 0; } }
        private void OnDisable()
        {
            cars.Remove(this); StopAll(); pendingGain = 0;
            foreach (var state in legacy) if (state.source != null) state.source.mute = state.muted;
            legacy.Clear();
        }
        private void OnDestroy() { if (voices != null) Destroy(voices.gameObject); }
    }
}
