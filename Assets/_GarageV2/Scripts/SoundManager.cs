using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

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
    }

    private void Start()
    {
        EnsureAudioSources();
        
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
    }

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

        bool ok = audioMixer.SetFloat("VehicleVolume", db);
        bool ok1 = audioMixer.SetFloat("volume", db);
        SaveManager.Instance.saveData.VehicleLevel = value;

        // Debug.Log($"Set VehicleVolume -> {db}, success = {ok}");
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

    public void PlaySfx(AudioClip clip)
    {
        EnsureAudioSources();

        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip,SaveManager.Instance.saveData.soundLevel);
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
