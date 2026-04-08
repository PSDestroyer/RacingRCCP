using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public void OnSetSfxVolume(float value)
    {
        SoundManager.Instance.SetSfxVolume(value);
    }
    public void OnSetVehicleVolume(float value)
    {
        SoundManager.Instance.SetVehicleVolume(value);
    }
    public void OnSetMusicVolume(float value)
    {
        SoundManager.Instance.SetMusicVolume(value);
    }
}
