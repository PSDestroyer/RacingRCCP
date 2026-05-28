//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________


using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ALIyerEdon
{
	public class Load_Settings : MonoBehaviour
	{


		public AudioSource music;
		public Material skyBox;

		public float reflectionIntensityLow = 1f;
		float reflectionIntensityDefault;

		public GameObject[] trees;

		void Start()
		{
			reflectionIntensityDefault = FindFirstObjectByType<ReflectionProbe>().intensity;

            if (skyBox)
			{
				if (PlayerPrefs.GetInt("QualityLevel") == 0
					|| PlayerPrefs.GetInt("QualityLevel") == 1)
				{
					RenderSettings.skybox = skyBox;
					RenderSettings.fog = true;

					FindFirstObjectByType<ReflectionProbe>().intensity = reflectionIntensityLow;

                }
				else
				{
                    RenderSettings.fog = false;

                    FindFirstObjectByType<ReflectionProbe>().intensity = reflectionIntensityDefault;
                }
            }
			if (PlayerPrefs.GetInt("RenderTrees") == 0)
			{
				try
				{
					if (trees[0])
					{
						for (int a = 0; a < trees.Length; a++)
							trees[a].SetActive(false);
					}
                }
                catch { }
			}

			music.volume = PlayerPrefs.GetFloat("Music");

			if (FindFirstObjectByType<Race_Manager>())
			{
				if (PlayerPrefs.GetInt("ShowLocalPosition") == 1)
					FindFirstObjectByType<Race_Manager>().showLocalPosition = true;
				else
					FindFirstObjectByType<Race_Manager>().showLocalPosition = false;
			}

			Set_QualityLevel(PlayerPrefs.GetInt("QualityLevel"));

			if (PlayerPrefs.GetInt("MotionBlur") == 0)
				Set_MotionBlur(false);
			if (PlayerPrefs.GetInt("MotionBlur") == 1)
				Set_MotionBlur(true);

			if (PlayerPrefs.GetInt("DepthOfField") == 0)
				Set_DepthOfField(false);
			if (PlayerPrefs.GetInt("DepthOfField") == 1)
				Set_DepthOfField(true);

			if (PlayerPrefs.GetInt("SSR") == 0)
				Set_SSR(false);
			if (PlayerPrefs.GetInt("SSR") == 1)
				Set_SSR(true);
		}

		public void Update_MusicVolume(float volume)
		{
			music.volume = volume;
		}

		public void Set_QualityLevel(int level)
		{

			QualitySettings.SetQualityLevel(level);


			if (skyBox)
			{
				if (PlayerPrefs.GetInt("QualityLevel") == 0
					|| PlayerPrefs.GetInt("QualityLevel") == 1)
				{
					RenderSettings.skybox = skyBox;
					RenderSettings.fog = true;
					FindFirstObjectByType<ReflectionProbe>().intensity = reflectionIntensityLow;
				}
				else
				{
					RenderSettings.fog = false;

					FindFirstObjectByType<ReflectionProbe>().intensity = reflectionIntensityDefault;
				}


				if (level == 0) // Very Low
				{
					foreach (Camera cam in FindObjectsOfType<Camera>())
					{
						cam.GetComponent<UniversalAdditionalCameraData>()
							.antialiasing = AntialiasingMode.None;
					}
				}
				if (level == 1) // Low
				{
					foreach (Camera cam in FindObjectsOfType<Camera>())
					{
						cam.GetComponent<UniversalAdditionalCameraData>()
							.antialiasing = AntialiasingMode.None;
					}
				}
				if (level == 2) // Medium
				{
					foreach (Camera cam in FindObjectsOfType<Camera>())
					{
						cam.GetComponent<UniversalAdditionalCameraData>()
							.antialiasing = AntialiasingMode.TemporalAntiAliasing;
						cam.GetComponent<UniversalAdditionalCameraData>()
								.taaSettings.quality = TemporalAAQuality.High;
					}
				}
				if (level == 3) // High
				{
					foreach (Camera cam in FindObjectsOfType<Camera>())
					{
						cam.GetComponent<UniversalAdditionalCameraData>()
							.antialiasing = AntialiasingMode.TemporalAntiAliasing;
						cam.GetComponent<UniversalAdditionalCameraData>()
								.taaSettings.quality = TemporalAAQuality.VeryHigh;
					}
				}
				if (level == 4) // Ultra
				{
					foreach (Camera cam in FindObjectsOfType<Camera>())
					{
						cam.GetComponent<UniversalAdditionalCameraData>()
							.antialiasing = AntialiasingMode.TemporalAntiAliasing;
						cam.GetComponent<UniversalAdditionalCameraData>()
								.taaSettings.quality = TemporalAAQuality.VeryHigh;
					}
				}
			}
		}


		public void Set_MotionBlur(bool enabled)
		{
			MotionBlur mb;

			Volume volume = FindFirstObjectByType<Volume>();

			volume.profile.TryGet<MotionBlur>(out mb);

			mb.active = enabled;
		}

		public void Set_DepthOfField(bool enabled)
		{
			DepthOfField dof;

			Volume volume = FindFirstObjectByType<Volume>();

			volume.profile.TryGet<DepthOfField>(out dof);

			dof.active = enabled;
		}

		public void Set_SSR(bool enabled)
		{
			ScreenSpaceReflection ssr;

			Volume volume = FindFirstObjectByType<Volume>();

			volume.profile.TryGet<ScreenSpaceReflection>(out ssr);

			ssr.active = enabled;
		}
	}
}