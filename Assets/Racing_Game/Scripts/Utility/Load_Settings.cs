//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using ALIyerEdon;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using System;

namespace ALIyerEdon
{
    public class Load_Settings : MonoBehaviour
    {

        public Color fogColor = Color.white;

        public GameObject[] localFog;

        public Camera[] cinematicCameras;

        AudioSource music;

        IEnumerator Start()
        {

            #region Music
            music = GetComponentInChildren<AudioSource>();
            music.volume = PlayerPrefs.GetFloat("Music");
            #endregion

            #region Position_UI
           
                Set_ShowLocalPosition();
            
            #endregion

            #region Grass
            Set_Grass();
            #endregion

            #region Quality_Level
            Set_QualityLevel(PlayerPrefs.GetInt("QualityLevel"));
            #endregion

            #region MotionBlur
            UnityEngine.Rendering.PostProcessing.MotionBlur mBlur;

            GameObject.FindFirstObjectByType<
                    UnityEngine.Rendering.PostProcessing.PostProcessVolume>()
                    .profile.TryGetSettings(out mBlur);

            if (PlayerPrefs.GetInt("MotionBlur") == 1)
            {
                mBlur.enabled.overrideState = true;
                mBlur.enabled.value = true;
            }
            else
            {
                mBlur.enabled.overrideState = true;
                mBlur.enabled.value = false;
            }
            #endregion

            yield return new WaitForEndOfFrame();

            #region Fog Smoke
            if (localFog.Length != 0)
            {
                for (int a = 0; a < localFog.Length; a++)
                {
                    localFog[a].GetComponent<ParticleSystem>().startColor = fogColor;
                }
            }
            // Update wheel smoke effects
            EasyCarAudio[] carAudio = FindObjectsOfType<EasyCarAudio>();

            for (int a = 0; a < carAudio.Length; a++)
            {
                for (int b = 0; b < carAudio[a].wheelSmokes.Length; b++)
                {
                    carAudio[a].wheelSmokes[b].GetComponent<ParticleSystem>().startColor = fogColor;
                }

                carAudio[a].offroadSmoke.GetComponent<ParticleSystem>().startColor = fogColor;
            }
            #endregion

            /*    if (GameObject.FindGameObjectWithTag("Player"))
                {
                    GameObject.FindGameObjectWithTag("Player").GetComponent<EasyCarController>()
                        .Toggle_FrontLights(enableNightLights);
                    GameObject.FindGameObjectWithTag("Player").GetComponent<EasyCarController>()
                        .Toggle_RearLights(enableNightLights);
                }*/
        }

        public void Set_Reflection(bool ssr_1, bool ssr_2)
        {
            Trive.Rendering.StochasticReflections ssr2;

            UnityEngine.Rendering.PostProcessing.ScreenSpaceReflections ssr1;

            GameObject.FindFirstObjectByType<
                    UnityEngine.Rendering.PostProcessing.PostProcessVolume>()
                    .profile.TryGetSettings(out ssr2);

            GameObject.FindFirstObjectByType<
                    UnityEngine.Rendering.PostProcessing.PostProcessVolume>()
                    .profile.TryGetSettings(out ssr1);

            if (ssr_1)
            {
                foreach (Camera cam in FindObjectsOfType<Camera>())
                    cam.renderingPath = RenderingPath.DeferredShading;

                if (GameObject.FindGameObjectWithTag("MinimapCamera"))
                    GameObject.FindGameObjectWithTag("MinimapCamera").GetComponent<Camera>().renderingPath = RenderingPath.Forward;

                if (cinematicCameras.Length != 0)
                {
                    foreach (Camera cam2 in cinematicCameras)
                    {
                        if (cam2 != null)
                            cam2.renderingPath = RenderingPath.DeferredShading;
                    }
                }
                ssr2.enabled.value = false;
                ssr1.enabled.value = true;
            }
            if (ssr_2)
            {
                foreach (Camera cam in FindObjectsOfType<Camera>())
                    cam.renderingPath = RenderingPath.DeferredShading;

                if (GameObject.FindGameObjectWithTag("MinimapCamera"))
                    GameObject.FindGameObjectWithTag("MinimapCamera").GetComponent<Camera>().renderingPath = RenderingPath.Forward;

                if (cinematicCameras.Length != 0)
                {
                    foreach (Camera cam2 in cinematicCameras)
                    {
                        if (cam2 != null)
                            cam2.renderingPath = RenderingPath.DeferredShading;
                    }
                }
                ssr2.enabled.value = true;

                ssr2.resolveDownsample.value = false;
                ssr2.raycastDownsample.value = false;

                ssr1.enabled.value = false;
            }
            if (!ssr_1 && !ssr_2)
            {
                foreach (Camera cam in FindObjectsOfType<Camera>())
                    cam.renderingPath = RenderingPath.Forward;

                if (GameObject.FindGameObjectWithTag("MinimapCamera"))
                    GameObject.FindGameObjectWithTag("MinimapCamera").GetComponent<Camera>().renderingPath = RenderingPath.Forward;

                if (cinematicCameras.Length != 0)
                {
                    foreach (Camera cam2 in cinematicCameras)
                    {
                        if (cam2 != null)
                            cam2.renderingPath = RenderingPath.Forward;
                    }
                }
                ssr2.enabled.value = false;
                ssr1.enabled.value = false;
            }
        }

        public void Update_MusicVolume(float volume)
        {
            music.volume = volume;
        }

        public void Set_QualityLevel(int qualityLevel)
        {
            // Very Low
            if (qualityLevel == 0)
            {
                Screen.SetResolution((int)(PlayerPrefs.GetFloat("VeryLow_width")),
                    (int)(PlayerPrefs.GetFloat("VeryLow_height")), true);

                foreach (UnityEngine.Rendering.PostProcessing.PostProcessLayer lll in
                    FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>())
                {
                    lll.enabled = true;
                    lll.antialiasingMode = UnityEngine.Rendering.PostProcessing.PostProcessLayer.Antialiasing.None;
                }
                if (cinematicCameras.Length != 0)
                {
                    foreach (Camera cam in cinematicCameras)
                    {
                        if (cam != null)
                        {
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().enabled = true;
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().antialiasingMode = UnityEngine.Rendering.PostProcessing.PostProcessLayer.Antialiasing.None;
                        }
                    }
                }
                foreach (Light light in FindObjectsOfType<Light>())
                {
                    light.shadows = LightShadows.None;
                }
                foreach (LightingBox.Effects.GlobalFog gf in FindObjectsOfType<LightingBox.Effects.GlobalFog>())
                {
                    gf.enabled = false;
                }
                Set_Reflection(false, false);
                if (FindFirstObjectByType<Terrain>())
                {
                    FindFirstObjectByType<Terrain>().detailObjectDensity = 0;
                    FindFirstObjectByType<Terrain>().detailObjectDistance = 0;
                    FindFirstObjectByType<Terrain>().heightmapPixelError = 200f;
                }
            }
            //_________________________________________
            //  Low
            if (qualityLevel == 1)
            {
                Screen.SetResolution((int)(PlayerPrefs.GetFloat("Low_width")),
                    (int)(PlayerPrefs.GetFloat("Low_height")), true);

                foreach (UnityEngine.Rendering.PostProcessing.PostProcessLayer lll in
                    FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>())
                {
                    lll.enabled = true;
                    lll.antialiasingMode = UnityEngine.Rendering.PostProcessing.PostProcessLayer.Antialiasing.None;
                }
                if (cinematicCameras.Length != 0)
                {
                    foreach (Camera cam in cinematicCameras)
                    {
                        if (cam != null)
                        {
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().enabled = true;
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().antialiasingMode = UnityEngine.Rendering.PostProcessing.PostProcessLayer.Antialiasing.None;
                        }
                    }
                }
                foreach (Light light in FindObjectsOfType<Light>())
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowResolution =
                        UnityEngine.Rendering.LightShadowResolution.Low;
                    QualitySettings.shadowDistance = 150f;
                }
                foreach (LightingBox.Effects.GlobalFog gf in FindObjectsOfType<LightingBox.Effects.GlobalFog>())
                {
                    gf.enabled = false;
                }
                Set_Reflection(false, false);
                if (FindFirstObjectByType<Terrain>())
                {
                    FindFirstObjectByType<Terrain>().detailObjectDistance = 100f;
                    FindFirstObjectByType<Terrain>().heightmapPixelError = 170f;
                }
            }
            //_________________________________________
            //  Medium
            if (qualityLevel == 2)
            {
                Screen.SetResolution((int)(PlayerPrefs.GetFloat("Medium_width")),
                    (int)(PlayerPrefs.GetFloat("Medium_height")), true);

                foreach (UnityEngine.Rendering.PostProcessing.PostProcessLayer lll in
                    FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>())
                {
                    lll.enabled = true;
                    lll.antialiasingMode = UnityEngine.Rendering.PostProcessing.
                        PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
                    lll.subpixelMorphologicalAntialiasing.quality = UnityEngine.Rendering.PostProcessing.SubpixelMorphologicalAntialiasing.Quality.Low;
                }
                if (cinematicCameras.Length != 0)
                {
                    foreach (Camera cam in cinematicCameras)
                    {
                        if (cam != null)
                        {
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().enabled = true;
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().antialiasingMode = UnityEngine.Rendering.PostProcessing.
                                PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().subpixelMorphologicalAntialiasing.quality =
                                UnityEngine.Rendering.PostProcessing.SubpixelMorphologicalAntialiasing.Quality.Low;
                        }
                    }
                }
                foreach (Light light in FindObjectsOfType<Light>())
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowResolution =
                        UnityEngine.Rendering.LightShadowResolution.Medium;
                    QualitySettings.shadowDistance = 200f;
                }
                foreach (LightingBox.Effects.GlobalFog gf in FindObjectsOfType<LightingBox.Effects.GlobalFog>())
                {
                    gf.enabled = true;
                }
                Set_Reflection(false, false);
                if (FindFirstObjectByType<Terrain>())
                {
                    FindFirstObjectByType<Terrain>().detailObjectDistance = 150f;
                    FindFirstObjectByType<Terrain>().heightmapPixelError = 150f;
                }
            }
            //_________________________________________
            //  High
            if (qualityLevel == 3)
            {
                Screen.SetResolution((int)(PlayerPrefs.GetFloat("High_width")),
                    (int)(PlayerPrefs.GetFloat("High_height")), true);

                foreach (UnityEngine.Rendering.PostProcessing.PostProcessLayer lll in
                    FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>())
                {
                    lll.enabled = true;
                    lll.antialiasingMode = UnityEngine.Rendering.PostProcessing.
                        PostProcessLayer.Antialiasing.TemporalAntialiasing;
                }
                if (cinematicCameras.Length != 0)
                {
                    foreach (Camera cam in cinematicCameras)
                    {
                        if (cam != null)
                        {
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().enabled = true;
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().antialiasingMode = UnityEngine.Rendering.PostProcessing.
                                PostProcessLayer.Antialiasing.TemporalAntialiasing;
                        }
                    }
                }
                foreach (Light light in FindObjectsOfType<Light>())
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowResolution =
                        UnityEngine.Rendering.LightShadowResolution.High;
                    QualitySettings.shadowDistance = 250f;
                }
                foreach (LightingBox.Effects.GlobalFog gf in FindObjectsOfType<LightingBox.Effects.GlobalFog>())
                {
                    gf.enabled = true;
                }
                Set_Reflection(true, false);
                if (FindFirstObjectByType<Terrain>())
                {
                    FindFirstObjectByType<Terrain>().detailObjectDistance = 300f;
                    FindFirstObjectByType<Terrain>().heightmapPixelError = 100f;
                }
            }
            //_________________________________________
            //  Ultra
            if (qualityLevel == 4)
            {
                Screen.SetResolution((int)(PlayerPrefs.GetFloat("Ultra_width")),
                    (int)(PlayerPrefs.GetFloat("Ultra_height")), true);

                foreach (UnityEngine.Rendering.PostProcessing.PostProcessLayer lll in
                    FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>())
                {
                    lll.enabled = true;
                    lll.antialiasingMode = UnityEngine.Rendering.PostProcessing.
                        PostProcessLayer.Antialiasing.TemporalAntialiasing;
                }
                if (cinematicCameras.Length != 0)
                {
                    foreach (Camera cam in cinematicCameras)
                    {
                        if (cam != null)
                        {
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().enabled = true;
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().antialiasingMode = UnityEngine.Rendering.PostProcessing.
                                PostProcessLayer.Antialiasing.TemporalAntialiasing;
                        }
                    }
                }
                foreach (Light light in FindObjectsOfType<Light>())
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowResolution =
                        UnityEngine.Rendering.LightShadowResolution.VeryHigh;
                    QualitySettings.shadowDistance = 300f;
                }
                foreach (LightingBox.Effects.GlobalFog gf in FindObjectsOfType<LightingBox.Effects.GlobalFog>())
                {
                    gf.enabled = true;
                }
                Set_Reflection(false, true);
                if (FindFirstObjectByType<Terrain>())
                {
                    FindFirstObjectByType<Terrain>().detailObjectDistance = 300f;
                    FindFirstObjectByType<Terrain>().heightmapPixelError = 30f;
                }
            }
            //_________________________________________
            //  Max
            if (qualityLevel == 5)
            {
                Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * 0.7f),
                    (int)(PlayerPrefs.GetInt("OriginalY") * 0.7f), true);

                foreach (UnityEngine.Rendering.PostProcessing.PostProcessLayer lll in
                    FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>())
                {
                    lll.enabled = true;
                    lll.antialiasingMode = UnityEngine.Rendering.PostProcessing.
                        PostProcessLayer.Antialiasing.TemporalAntialiasing;
                }
                if (cinematicCameras.Length != 0)
                {
                    foreach (Camera cam in cinematicCameras)
                    {
                        if (cam != null)
                        {
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().enabled = true;
                            cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>().antialiasingMode = UnityEngine.Rendering.PostProcessing.
                                PostProcessLayer.Antialiasing.TemporalAntialiasing;
                        }
                    }
                }
                foreach (Light light in FindObjectsOfType<Light>())
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowResolution =
                        UnityEngine.Rendering.LightShadowResolution.VeryHigh;
                    QualitySettings.shadowDistance = 300f;
                }
                foreach (LightingBox.Effects.GlobalFog gf in FindObjectsOfType<LightingBox.Effects.GlobalFog>())
                {
                    gf.enabled = true;
                }
                Set_Reflection(false, true);

                if (FindFirstObjectByType<Terrain>())
                {
                    FindFirstObjectByType<Terrain>().detailObjectDistance = 300f;
                    FindFirstObjectByType<Terrain>().heightmapPixelError = 10f;
                }

            }



        }

        public void Set_Grass()
        {
            if (FindFirstObjectByType<Terrain>())
            {

                FindFirstObjectByType<Terrain>().detailObjectDistance = 100f;

                if (PlayerPrefs.GetInt("Grass") == 0)
                {
                    FindFirstObjectByType<Terrain>().detailObjectDensity = 0;
                    FindFirstObjectByType<Terrain>().detailObjectDistance = 0;
                }
                if (PlayerPrefs.GetInt("Grass") == 1)
                    FindFirstObjectByType<Terrain>().detailObjectDensity = 0.3f;
                if (PlayerPrefs.GetInt("Grass") == 2)
                    FindFirstObjectByType<Terrain>().detailObjectDensity = 0.5f;
                if (PlayerPrefs.GetInt("Grass") == 3)
                    FindFirstObjectByType<Terrain>().detailObjectDensity = 1f;
            }
        }

        public void Set_TargetFPS()
        {
            if (PlayerPrefs.GetInt("targetFPS") == 0)
                Application.targetFrameRate = 30;
            if (PlayerPrefs.GetInt("targetFPS") == 1)
                Application.targetFrameRate = 60;
            if (PlayerPrefs.GetInt("targetFPS") == 2)
                Application.targetFrameRate = 120;
        }

        public void Set_ShowLocalPosition()
        {
            if (FindFirstObjectByType<Race_Manager>())
            {
                if (PlayerPrefs.GetInt("ShowLocalPosition") == 3)
                    FindFirstObjectByType<Race_Manager>().showLocalPosition = true;
                else
                    FindFirstObjectByType<Race_Manager>().showLocalPosition = false;

                // Enable local position display on top of the cars
                foreach (Car_Position carPos in FindObjectsOfType<Car_Position>())
                {
                    // Show or hide car position on the top of the car
                    carPos.GetComponent<Car_Position>().displayPosition =
                        FindFirstObjectByType<Race_Manager>().showLocalPosition;
                }
            }
        }

        public void Set_MotionBlur()
        {
            UnityEngine.Rendering.PostProcessing.MotionBlur mBlur;

            GameObject.FindFirstObjectByType<
                    UnityEngine.Rendering.PostProcessing.PostProcessVolume>()
                    .profile.TryGetSettings(out mBlur);

            if (PlayerPrefs.GetInt("MotionBlur") == 1)
            {
                mBlur.enabled.overrideState = true;
                mBlur.enabled.value = true;
            }
            else
            {
                mBlur.enabled.overrideState = true;
                mBlur.enabled.value = false;
            }
        }
        public void Set_DisplayFPS()
        {
            if (FindObjectOfType<FPSCounter>())
            {
                if (PlayerPrefs.GetInt("Display_FPS") == 1)
                {
                    FindObjectOfType<FPSCounter>().panel.SetActive(true);
                }
                else
                {
                    FindObjectOfType<FPSCounter>().panel.SetActive(false);
                }
            }
        }
    }
}