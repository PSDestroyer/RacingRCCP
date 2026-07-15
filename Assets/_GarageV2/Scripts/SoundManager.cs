using HalvaStudio.Save;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    public AudioMixerGroup vehicleMixer;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixer GlobalaudioMixer;
    public AudioMixerGroup sfxMixerGroup;
    public AudioMixerGroup musicMixerGroup;

    [Header("UI Sounds")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip buttonErrorClip;
    [SerializeField] private AudioClip NewCarClip;
    [SerializeField] private bool autoBindButtonClickSounds = true;
    [SerializeField] private float buttonBindRefreshInterval = .5f;

    private readonly List<Button> boundClickButtons = new List<Button>();
    private float nextButtonBindRefreshTime;
    private int lastButtonClickFrame = -1;
    private bool vehicleAudioMutedForPause;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance == this)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        
        // RCCP_Audio audio = FindAnyObjectByType<RCCP_Audio>();
        //
        // if (audio != null)
        // {
        //     audio.audioMixer = vehicleMixer;
        //     audio.Reload();
        // }
        
        
        SetVehicleVolume(SaveManager.Instance.saveData.VehicleLevel);
        SetSfxVolume(SaveManager.Instance.saveData.soundLevel);
        SetMusicVolume(SaveManager.Instance.saveData.musicLevel);
        BindButtonClickSounds();
    }

    private void Update()
    {
        if (!autoBindButtonClickSounds || Time.unscaledTime < nextButtonBindRefreshTime)
            return;

        nextButtonBindRefreshTime = Time.unscaledTime + buttonBindRefreshInterval;
        BindButtonClickSounds();
    }
    public void SetVehicleVolume(float value)
    {
        value = Mathf.Clamp01(value);

        float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;

        bool ok = audioMixer.SetFloat("VehicleVolume", db);
        bool ok1 = audioMixer.SetFloat("volume", db);
        SaveManager.Instance.saveData.VehicleLevel = value;

        // Debug.Log($"Set VehicleVolume -> {db}, success = {ok}");
    }

    public void SetVehicleAudioPaused(bool paused)
    {
        vehicleAudioMutedForPause = paused;

        if (audioMixer == null || SaveManager.Instance == null || SaveManager.Instance.saveData == null)
            return;

        float targetVolume = paused ? 0f : SaveManager.Instance.saveData.VehicleLevel;
        float db = targetVolume <= 0.0001f ? -80f : Mathf.Log10(targetVolume) * 20f;

        audioMixer.SetFloat("VehicleVolume", db);
        audioMixer.SetFloat("volume", db);
    }
    public void SetSfxVolume(float value)
    {
        value = Mathf.Clamp01(value);

        float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;

        bool ok = GlobalaudioMixer.SetFloat("SfxVolume", db);
        SaveManager.Instance.saveData.soundLevel = value;

        // Debug.Log($"Set VehicleVolume -> {db}, success = {ok}");
    }
    public void SetMusicVolume(float value)
    {
        value = Mathf.Clamp01(value);

        float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;

        bool ok = GlobalaudioMixer.SetFloat("MusicVolume", db);
        SaveManager.Instance.saveData.musicLevel = value;

        // Debug.Log($"Set VehicleVolume -> {db}, success = {ok}");
    }
    private void OnDestroy()
    {
        foreach (Button button in boundClickButtons)
        {
            if (button != null)
                button.onClick.RemoveListener(PlayButtonClick);
        }

        boundClickButtons.Clear();
    }

    private void ApplySettings()
    {
            
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null)
            return;

        if (musicSource.clip == clip)
            return;

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource == null)
            return;

        musicSource.Stop();
        musicSource.clip = null;
    }

    public void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip,SaveManager.Instance.saveData.soundLevel);
    }

    public void PlayButtonClick()
    {
        if (Time.frameCount == lastButtonClickFrame)
            return;

        lastButtonClickFrame = Time.frameCount;
        PlaySfx(buttonClickClip);
    }

    public void PlayButtonError()
    {
        PlaySfx(buttonErrorClip);
    }

    public void PlayNewCarClip()
    {
        PlaySfx(NewCarClip);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindButtonClickSounds();
    }

    private void BindButtonClickSounds()
    {
        if (!autoBindButtonClickSounds)
            return;

        for (int i = boundClickButtons.Count - 1; i >= 0; i--)
        {
            if (boundClickButtons[i] == null)
                boundClickButtons.RemoveAt(i);
        }

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button == null || boundClickButtons.Contains(button))
                continue;

            button.onClick.AddListener(PlayButtonClick);
            boundClickButtons.Add(button);
        }
    }
}
