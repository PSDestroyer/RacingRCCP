using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class EnableCinemachine : MonoBehaviour
{
    public List<CinemachineCamera> cinemachineCamera = new List<CinemachineCamera>();

    public void CameraChange(CinemachineCamera cameraOn)
    {
        foreach (var camera in cinemachineCamera)
        {
            camera.gameObject.SetActive(false);
        }
        cameraOn.gameObject.SetActive(true);
    }
}
