using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using FormulaGame;

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
    private float musicPresentationGain;
    public AudioSource UiSource => sfxSource;
    public AudioSource MusicSource => musicSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSources();
        if (FormulaSoundBank.Current != null && GetComponent<FormulaSoundscape>() == null)
            gameObject.AddComponent<FormulaSoundscape>();
    }

    private void Start()
    {
        EnsureAudioSources();
        
        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
        {
            SetVehicleVolume(SaveManager.Instance.saveData.VehicleLevel);
            SetSfxVolume(SaveManager.Instance.saveData.soundLevel);
            SetMusicVolume(SaveManager.Instance.saveData.musicLevel);
        }
    }

    private void LateUpdate()
    {
        FormulaAudio.Flush();
        if (musicSource == null) return;
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, musicPresentationGain, Time.unscaledDeltaTime * .4f);
        if (musicSource.volume <= .001f && musicSource.isPlaying) musicSource.Pause();
        else if (musicSource.volume > .001f && !musicSource.isPlaying && musicSource.clip != null) musicSource.UnPause();
    }
    public void SetMusicPresentationGain(float gain) { musicPresentationGain = Mathf.Clamp01(gain); }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        EnsureAudioSources();
    }

    public void SetVehicleVolume(float value)
    {
        value = Mathf.Clamp01(value);

        float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;

        // RCCP_AudioMixer exposes its master gain as "volume".
        if (audioMixer != null) audioMixer.SetFloat("volume", db);
        FormulaAudioMix.SetVehicleLevel(value);
        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null) SaveManager.Instance.saveData.VehicleLevel = value;
    }
    public void SetSfxVolume(float value)
    {
        value = Mathf.Clamp01(value);

        float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;

        if (GlobalaudioMixer != null) GlobalaudioMixer.SetFloat("SfxVolume", db);
        FormulaAudioMix.SetSfxLevel(value);
        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null) SaveManager.Instance.saveData.soundLevel = value;
    }
    public void SetMusicVolume(float value)
    {
        value = Mathf.Clamp01(value);

        float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;

        if (GlobalaudioMixer != null) GlobalaudioMixer.SetFloat("MusicVolume", db);
        FormulaAudioMix.SetMusicLevel(value);
        if (SaveManager.Instance != null && SaveManager.Instance.saveData != null) SaveManager.Instance.saveData.musicLevel = value;
    }
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ApplySettings()
    {
            
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        EnsureAudioSources();

        if (musicSource == null || clip == null)
            return;

        if (musicSource.clip == clip)
            return;

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = 0;
        musicSource.Play();
    }

    public void StopMusic()
    {
        EnsureAudioSources();

        if (musicSource == null)
            return;

        musicSource.Stop();
        musicSource.clip = null;
    }

    public void PlaySfx(AudioClip clip) => PlaySfx(clip, 1f);

    public void PlaySfx(AudioClip clip, float gain)
    {
        EnsureAudioSources();

        if (sfxSource == null || clip == null)
            return;

        // The saved slider is applied once, on the mixer. Cue gain is artistic.
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(gain));
    }

    public void PlayButtonClick()
    {
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

    private void EnsureAudioSources()
    {
        if (musicSource == null)
            musicSource = FindOrCreateAudioSource("Music Source", loop: true, mixerGroup: musicMixerGroup);

        if (sfxSource == null)
            sfxSource = FindOrCreateAudioSource("Sfx Source", loop: false, mixerGroup: sfxMixerGroup);
        var bank = FormulaSoundBank.Current;
        if (bank != null)
        {
            musicSource.outputAudioMixerGroup = bank.music;
            sfxSource.outputAudioMixerGroup = bank.ui;
        }
        musicSource.ignoreListenerPause = true; musicSource.priority = 200; musicSource.spatialBlend = 0;
        sfxSource.ignoreListenerPause = true; sfxSource.priority = 8; sfxSource.spatialBlend = 0;
    }

    private AudioSource FindOrCreateAudioSource(string sourceName, bool loop, AudioMixerGroup mixerGroup)
    {
        Transform existingChild = transform.Find(sourceName);
        AudioSource source = existingChild != null ? existingChild.GetComponent<AudioSource>() : null;

        if (source == null)
        {
            AudioSource[] childSources = GetComponentsInChildren<AudioSource>(true);

            for (int i = 0; i < childSources.Length; i++)
            {
                if (childSources[i] == null)
                    continue;

                if (ReferenceEquals(childSources[i], musicSource) || ReferenceEquals(childSources[i], sfxSource))
                    continue;

                source = childSources[i];
                break;
            }
        }

        if (source == null)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            source = sourceObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = loop;

        if (mixerGroup != null)
            source.outputAudioMixerGroup = mixerGroup;

        return source;
    }
}
