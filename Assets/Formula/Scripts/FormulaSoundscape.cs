using UnityEngine;
using UnityEngine.Audio;

namespace FormulaGame
{
    // Owned by the existing persistent SoundManager. No second audio singleton.
    [DisallowMultipleComponent]
    public sealed class FormulaSoundscape : MonoBehaviour
    {
        private SoundManager owner;
        private AudioSource room;
        private AudioMixerSnapshot menu, race, pause, results;
        private int previousState = -1;
        private bool hasResult;
        private GamePlayManager raceOwner;
        private void OnEnable()
        {
            FormulaSession.Finished += Finished;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
        }
        private void Finished(FormulaReceipt receipt) { hasResult = true; }
        private void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            hasResult = false;
            raceOwner = FindFirstObjectByType<GamePlayManager>();
        }
        private void Start()
        {
            owner = GetComponent<SoundManager>();
            var bank = FormulaSoundBank.Current;
            if (owner == null || bank == null) { enabled = false; return; }
            owner.PlayMusic(bank.menuMusic);
            raceOwner = FindFirstObjectByType<GamePlayManager>();
            var child = new GameObject("Formula Garage Room"); child.transform.SetParent(transform, false);
            room = child.AddComponent<AudioSource>(); room.clip = bank.garageRoom;
            room.outputAudioMixerGroup = bank.world; room.spatialBlend = 0; room.loop = true;
            room.playOnAwake = false; room.priority = 220; room.volume = 0;
            if (bank.mixer != null)
            {
                menu = bank.mixer.FindSnapshot("Menu"); race = bank.mixer.FindSnapshot("Race");
                pause = bank.mixer.FindSnapshot("Pause"); results = bank.mixer.FindSnapshot("Results");
            }
        }
        private void Update()
        {
            if (owner == null || room == null) return;
            bool racing = FormulaSession.InRace;
            bool result = racing && hasResult;
            bool paused = racing && !result && Time.timeScale <= 0;
            int state = result ? 3 : paused ? 2 : racing ? 1 : 0;
            if (state != previousState)
            {
                previousState = state;
                var snapshot = state == 0 ? menu : state == 1 ? race : state == 2 ? pause : results;
                if (snapshot != null) snapshot.TransitionTo(.45f);
            }
            float roomTarget = racing ? 0 : .12f;
            room.volume = Mathf.MoveTowards(room.volume, roomTarget, Time.unscaledDeltaTime * .15f);
            if (room.volume > .001f && !room.isPlaying && room.clip != null) room.Play();
            if (room.volume <= .001f && room.isPlaying) room.Stop();
            // One streamed instrumental continues through scene changes. Keep
            // the engine/countdown in front without altering the saved slider.
            float drivingGain = raceOwner == null || !raceOwner.FormulaRaceStarted ? .04f : .10f;
            if (raceOwner != null && raceOwner.CarController != null && raceOwner.FormulaRaceStarted)
                drivingGain = Mathf.Lerp(.10f, .065f, Mathf.Clamp01(raceOwner.CarController.throttleInput_P));
            owner.SetMusicPresentationGain(state == 1 ? drivingGain : state == 2 ? .12f : state == 3 ? .28f : .23f);
        }
        private void OnDisable()
        {
            FormulaSession.Finished -= Finished;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneLoaded;
            if (room != null) room.Stop(); previousState = -1;
        }
    }
}
