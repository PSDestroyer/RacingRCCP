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
        public Slider AccelSensibility;
        public Text AccelSensibilityInfo;
        public Slider musicVolume;
        public Text musicVolumeInfo;
        public Dropdown controlType;
        public Toggle renderTrees;
        public Toggle localPosition;
        public Toggle SidePosition;
        public Toggle fpsCounter;

        public Dropdown QualityLevel;
        public Dropdown MotionBlur;
        public Dropdown DepthOfField;
        public Dropdown SSR;

        // Start is called before the first frame update
        void Start()
        {
            // Load initilial settings            
            AccelSensibility.value = PlayerPrefs.GetFloat("accelSensibility");
            AccelSensibilityInfo.text = AccelSensibility.value.ToString();

            controlType.value = PlayerPrefs.GetInt("ControlType");

            QualityLevel.value = PlayerPrefs.GetInt("QualityLevel");
            MotionBlur.value = PlayerPrefs.GetInt("MotionBlur");
            DepthOfField.value = PlayerPrefs.GetInt("DepthOfField");
            SSR.value = PlayerPrefs.GetInt("SSR");

            musicVolume.value = PlayerPrefs.GetFloat("Music");

            // 3 => true  0 => false
            if (PlayerPrefs.GetInt("ShowLocalPosition") == 1)
                localPosition.isOn = true;
            else
                localPosition.isOn = false;

            // 3 => true  0 => false
            if (PlayerPrefs.GetInt("ShowPositionUI") == 1)
                SidePosition.isOn = true;
            else
                SidePosition.isOn = false;

            if (PlayerPrefs.GetInt("RenderTrees") == 3)
                renderTrees.isOn = true;
            else
                renderTrees.isOn = false;

            if (PlayerPrefs.GetInt("Show_FPS") == 1)
                fpsCounter.isOn = true;
            else
                fpsCounter.isOn = false;

            if (QualityLevel.value > 2)
                SSR.gameObject.SetActive(true);
            else
                SSR.gameObject.SetActive(false);
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
        // Control type : accelerometer , steering wheel , arrow keys
        public void Set_ControlType()
        {
            if (controlType.value == 0)
                PlayerPrefs.SetInt("ControlType", 0);
            if (controlType.value == 1)
                PlayerPrefs.SetInt("ControlType", 1);
            if (controlType.value == 2)
                PlayerPrefs.SetInt("ControlType", 2);
        }

        // Screen resolution quality : best for performance

        public void Select_QualityLevel()
        {
            PlayerPrefs.SetInt("QualityLevel", QualityLevel.value);

            if (QualityLevel.value > 2)
                SSR.gameObject.SetActive(true);
            else
                SSR.gameObject.SetActive(false);

            FindFirstObjectByType<Load_Settings>().
                Set_QualityLevel(PlayerPrefs.GetInt("QualityLevel"));
        }

        public void Set_MotionBlur()
        {
            PlayerPrefs.SetInt("MotionBlur", MotionBlur.value);

            if (PlayerPrefs.GetInt("MotionBlur") == 0)
                FindFirstObjectByType<Load_Settings>().Set_MotionBlur(false);
            if (PlayerPrefs.GetInt("MotionBlur") == 1)
                FindFirstObjectByType<Load_Settings>().Set_MotionBlur(true);
        }

        public void Set_DepthOfField()
        {
            PlayerPrefs.SetInt("DepthOfField", DepthOfField.value);

            if (PlayerPrefs.GetInt("DepthOfField") == 0)
                FindFirstObjectByType<Load_Settings>().Set_DepthOfField(false);
            if (PlayerPrefs.GetInt("DepthOfField") == 1)
                FindFirstObjectByType<Load_Settings>().Set_DepthOfField(true);
        }

        public void Set_SSR()
        {
            PlayerPrefs.SetInt("SSR", SSR.value);

            if (PlayerPrefs.GetInt("SSR") == 0)
                FindFirstObjectByType<Load_Settings>().Set_SSR(false);
            if (PlayerPrefs.GetInt("SSR") == 1)
                FindFirstObjectByType<Load_Settings>().Set_SSR(true);
        }

        public void Toggle_LocalPosition()
        {
            StartCoroutine(Toggle_LocalPosition_Save());
        }

        IEnumerator Toggle_LocalPosition_Save()
        {
            yield return new WaitForEndOfFrame();

            if (localPosition.isOn)
                PlayerPrefs.SetInt("ShowLocalPosition", 1);
            else
                PlayerPrefs.SetInt("ShowLocalPosition", 0);


            foreach (Car_Position pos in FindObjectsOfType<Car_Position>())
            {
                if (PlayerPrefs.GetInt("ShowLocalPosition") == 1)
                    pos.displayPosition = true;
                else
                    pos.displayPosition = false;
            }
        }

        public void Toggle_SidePosition()
        {
            StartCoroutine(Toggle_SidePosition_Save());
        }

        IEnumerator Toggle_SidePosition_Save()
        {
            yield return new WaitForEndOfFrame();

            if (SidePosition.isOn)
                PlayerPrefs.SetInt("ShowPositionUI", 1);
            else
                PlayerPrefs.SetInt("ShowPositionUI", 0);

            // Enable or disable right side position ui
            if (PlayerPrefs.GetInt("ShowPositionUI") == 1)
                FindFirstObjectByType<Race_Manager>().positionUI.SetActive(true);
            else
                FindFirstObjectByType<Race_Manager>().positionUI.SetActive(false);
        }


        // Racing Position UI Display
        public void Toggle_RenderTrees()
        {
            StartCoroutine(Toggle_RenderTrees_Save());
        }

        IEnumerator Toggle_RenderTrees_Save()
        {
            yield return new WaitForEndOfFrame();

            if (renderTrees.isOn)
                PlayerPrefs.SetInt("RenderTrees", 3);
            else
                PlayerPrefs.SetInt("RenderTrees", 0);
        }

        // Diplay fps
        public void Toggle_FPSCounter()
        {
            StartCoroutine(Toggle_FPSCounter_Save());
        }

        IEnumerator Toggle_FPSCounter_Save()
        {
            yield return new WaitForEndOfFrame();

            if (fpsCounter.isOn)
                PlayerPrefs.SetInt("Show_FPS", 1);
            else
                PlayerPrefs.SetInt("Show_FPS", 0);

            if (FindFirstObjectByType<FPSCounter>())
                FindFirstObjectByType<FPSCounter>().Update_Settings();

        }
    }
}