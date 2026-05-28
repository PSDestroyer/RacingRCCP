//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ALIyerEdon;

namespace ALIyerEdon
{
    public class SettingsMenu : MonoBehaviour
    {
        // UI items in settings menu window
        public Dropdown qualityLevel;
        public Dropdown controlType;
        public Dropdown grass;
        public Dropdown displayFPS;
        public Dropdown targetFPS;
        public Dropdown dynamicCamera;
        public Dropdown motionBlur;

        public Slider AccelSensibility;
        public Text AccelSensibilityInfo;
        public Slider musicVolume;
        public Text musicVolumeInfo;

        public Toggle positionUI;
        public Toggle localPosition;

        // Start is called before the first frame update
        void Start()
        {
            // Load initilial settings            
            AccelSensibility.value = PlayerPrefs.GetFloat("accelSensibility");
            AccelSensibilityInfo.text = AccelSensibility.value.ToString();

            controlType.value = PlayerPrefs.GetInt("ControlType");
            grass.value = PlayerPrefs.GetInt("Grass");
            displayFPS.value = PlayerPrefs.GetInt("Display_FPS");
            targetFPS.value = PlayerPrefs.GetInt("targetFPS");
            dynamicCamera.value = PlayerPrefs.GetInt("Dynamic Camera");
            motionBlur.value = PlayerPrefs.GetInt("MotionBlur");
            qualityLevel.value = PlayerPrefs.GetInt("QualityLevel");

            musicVolume.value = PlayerPrefs.GetFloat("Music");

            // 3 => true  0 => false
            if (PlayerPrefs.GetInt("ShowLocalPosition") == 3)
                localPosition.isOn = true;
            else
                localPosition.isOn = false;

            if (PlayerPrefs.GetInt("ShowPositionUI") == 3)
                positionUI.isOn = true;
            else
                positionUI.isOn = false;
        }

        // Accelerometer  Sensibility
        public void Accelometer_Sensibility()
        {
            PlayerPrefs.SetFloat("accelSensibility", AccelSensibility.value);
            AccelSensibilityInfo.text = AccelSensibility.value.ToString();
        }
        public void Music_Volume()
        {
            PlayerPrefs.SetFloat("Music", musicVolume.value);
            musicVolumeInfo.text = musicVolume.value.ToString();
            FindFirstObjectByType<Load_Settings>().Update_MusicVolume(musicVolume.value);
        }

        public void Set_QualityLevel()
        {
            FindFirstObjectByType<Load_Settings>().Set_QualityLevel(qualityLevel.value);
        }

        public void Toggle_LocalPosition()
        {
            StartCoroutine(Toggle_LocalPosition_Save());
        }

        IEnumerator Toggle_LocalPosition_Save()
        {
            yield return new WaitForEndOfFrame();

            if (localPosition.isOn)
                PlayerPrefs.SetInt("ShowLocalPosition", 3);
            else
                PlayerPrefs.SetInt("ShowLocalPosition", 0);

            if (FindAnyObjectByType<Load_Settings>())
                FindAnyObjectByType<Load_Settings>().Set_ShowLocalPosition();
        }

        // Racing Position UI Display
        public void Toggle_PositionUI()
        {
            StartCoroutine(Toggle_PositionUI_Save());
        }

        IEnumerator Toggle_PositionUI_Save()
        {
            yield return new WaitForEndOfFrame();

            if (positionUI.isOn)
                PlayerPrefs.SetInt("ShowPositionUI", 3);
            else
                PlayerPrefs.SetInt("ShowPositionUI", 0);
        }

        // Control type : accelerometer , steering wheel , arrow keys
        public void Set_ControlType()
        {
            PlayerPrefs.SetInt("ControlType", controlType.value);
        }

        public void Set_DisplayFPS()
        {
            PlayerPrefs.SetInt("Display_FPS", displayFPS.value);

            if (FindObjectOfType<Load_Settings>())
            {
                FindObjectOfType<Load_Settings>().Set_DisplayFPS();
            }
        }

        public void Set_Grass()
        {
            PlayerPrefs.SetInt("Grass", grass.value);

            if (FindObjectOfType<Load_Settings>())
            {
                FindObjectOfType<Load_Settings>().Set_Grass();
            }
        }

        public void Set_TargetFPS()
        {
            PlayerPrefs.SetInt("targetFPS", targetFPS.value);

            if (FindObjectOfType<Load_Settings>())
            {
                FindObjectOfType<Load_Settings>().Set_TargetFPS();
            }
        }

        public void Set_DynamicCamera()
        {
            PlayerPrefs.SetInt("Dynamic Camera", dynamicCamera.value);
        }

        public void Set_MotionBlur()
        {
            PlayerPrefs.SetInt("MotionBlur", motionBlur.value);

            if (FindObjectOfType<Load_Settings>())
            {
                FindObjectOfType<Load_Settings>().Set_MotionBlur();
            }
        }

        // Save reflection values after close the settings menu to avoid crash on the low-end devices
        public void Close_Save()
        {
            PlayerPrefs.SetInt("QualityLevel", qualityLevel.value);
        }

        public void Disable_Object(GameObject target)
        {
            target.SetActive(false);
        }
    }
}