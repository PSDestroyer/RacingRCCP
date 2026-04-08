using HalvaStudio.Save;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    public AudioMixerGroup vehicleMixer;
    [SerializeField] private AudioMixer audioMixer;
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
    }

    private void Start()
    {
        
        RCCP_Audio audio = FindAnyObjectByType<RCCP_Audio>();

        if (audio != null)
        {
            audio.audioMixer = vehicleMixer;
            audio.Reload();
        }
        SetVehicleVolume(SaveManager.Instance.saveData.soundLevel);
    }
    public void SetVehicleVolume(float value)
    {
        value = Mathf.Clamp01(value);

        float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;

        bool ok = audioMixer.SetFloat("VehicleVolume", db);
        bool ok1 = audioMixer.SetFloat("volume", db);
        Debug.Log($"Set VehicleVolume -> {db}, success = {ok}");
    }
    
    private void OnDestroy()
    {
      
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
}