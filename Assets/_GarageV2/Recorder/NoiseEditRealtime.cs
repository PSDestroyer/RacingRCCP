using Unity.Cinemachine;
using UnityEngine;

public class NoiseEditRealtime : MonoBehaviour
{
    public float noise;

    public CinemachineBasicMultiChannelPerlin cinemachineCamera;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        cinemachineCamera.FrequencyGain = noise;
    }
}
