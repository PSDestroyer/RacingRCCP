using HalvaStudio.Save;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("UI Sounds")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip buttonHoverClip;
    [SerializeField] private AudioClip backClip;

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
        
    }

    private void OnDestroy()
    {
      
    }

    private void ApplySettings()
    {
        // float musicFinal = data.musicEnabled ? data.masterVolume * data.musicVolume : 0f;
        // float sfxFinal = data.sfxEnabled ? data.masterVolume * data.sfxVolume : 0f;

        // if (musicSource != null)
            // musicSource.volume = musicFinal;

        // if (sfxSource != null)
            // sfxSource.volume = sfxFinal;
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

    public void PlayButtonHover()
    {
        PlaySfx(buttonHoverClip);
    }

    public void PlayBack()
    {
        PlaySfx(backClip);
    }
}