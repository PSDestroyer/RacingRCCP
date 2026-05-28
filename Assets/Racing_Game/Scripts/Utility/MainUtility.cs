//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using ALIyerEdon;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

namespace ALIyerEdon
{
	public class MainUtility : MonoBehaviour
	{
		public int targetFPS = 60;

		// Instantiate car at start
		public CarSelect carSelect;

		public AudioSource clickSound;

		public GameObject Loading, exitMenu;

		public int startingScore = 1400;

		public Text totalCoinsText;

        [Header("Camera Settings")]
        public Transform sportCamera;
        public Transform truckCamera;
        public Transform F1Camera;
        public Transform offroadCamera;

        [HideInInspector] public GameObject mainCamera;

        void Awake()
		{
			AudioListener.volume = 1f;

            Time.timeScale = 1f;

			PlayerPrefs.SetInt("Target FPS", targetFPS);

			Application.targetFrameRate = targetFPS;

            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

            if (Gamepad.current != null)
                Gamepad.current.SetMotorSpeeds(0, 0);

            // Is game first run?   3 => true    0 => false
            if (PlayerPrefs.GetInt("FirstRun") != 3)
			{
				PlayerPrefs.SetInt("OriginalX", Screen.width);
				PlayerPrefs.SetInt("OriginalY", Screen.height);

				// Default difficulty level => 0 = simulation , 1 = Arcade
				PlayerPrefs.SetInt("Difficulty Level", 0);

				// Set default control type to the arrow keys
				//Arrow keys = 0 , Joystick = 1 , acceleration = 2
				PlayerPrefs.SetInt("ControlType", 0);
				
				// Default quality level is Low
				PlayerPrefs.SetInt("QualityLevel", 1);

				PlayerPrefs.SetFloat("accelSensibility", 100f);

				PlayerPrefs.SetFloat("SteeringWheelSens", 250f);

				// Set music ambient sound in settings false
				PlayerPrefs.SetFloat("Music", 0.7f);

				// Enable right position ui info display
				PlayerPrefs.SetInt("ShowPositionUI", 3);

				// Open the first 2 sport cars
				PlayerPrefs.SetInt("Car_Sport0", 3);
				PlayerPrefs.SetInt("Car1_Sport", 3);
				// Open the first  truck
				PlayerPrefs.SetInt("Car_Truck0", 3);
				// Open the first  offroad car
				PlayerPrefs.SetInt("Car_Offroad0", 3);
				// Open the first  F1 car
				PlayerPrefs.SetInt("Car_F10", 3);

				// Set default suspension value for each cars to the 0
				PlayerPrefs.SetFloat("Car0Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car1Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car2Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car3Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car4Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car5Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car6Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car7Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car8Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car9Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car10Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car11Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car12Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car13Suspension", -0.1f);
				PlayerPrefs.SetFloat("Car14Suspension", -0.1f);

				// Open the first level (level0)
				PlayerPrefs.SetInt("Level0", 3);
				PlayerPrefs.SetInt("Level7", 3);

				// Set none  awards for all levels
				PlayerPrefs.SetInt("Award_Level_0", 3);
				PlayerPrefs.SetInt("Award_Level_1", 3);
				PlayerPrefs.SetInt("Award_Level_2", 3);
				PlayerPrefs.SetInt("Award_Level_3", 3);
				PlayerPrefs.SetInt("Award_Level_4", 3);
				PlayerPrefs.SetInt("Award_Level_5", 3);
				PlayerPrefs.SetInt("Award_Level_6", 3);
				PlayerPrefs.SetInt("Award_Level_7", 3);
				PlayerPrefs.SetInt("Award_Level_8", 3);
				PlayerPrefs.SetInt("Award_Level_9", 3);
				PlayerPrefs.SetInt("Award_Level_10", 3);

				// Player first time starting the game score
				PlayerPrefs.SetInt("TotalScores", startingScore);

				PlayerPrefs.SetInt("Dynamic Camera", 1);

				PlayerPrefs.SetInt("Display_FPS", 0);
				PlayerPrefs.SetInt("targetFPS", 2);

                // Find optimal screen resolution for target device
                StartCoroutine(Find_Resolutions());

                // Disable the first running the game settings
                PlayerPrefs.SetInt("FirstRun", 3);
			}

			if (totalCoinsText)
				totalCoinsText.text = PlayerPrefs.GetInt("TotalScores").ToString();
        }

        void Update()
        {
            #region Exit
            // Exit with back button
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonEast.wasPressedThisFrame)
                {
                    exitMenu.SetActive(!exitMenu.activeSelf);
                }
            }
            else
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    {
                        exitMenu.SetActive(!exitMenu.activeSelf);
                    }
                }
            }
            #endregion

            #region Delete_SavedData
            if (Keyboard.current != null)
            {
                if (Keyboard.current.hKey.wasPressedThisFrame)
                {
#if UNITY_EDITOR
                    PlayerPrefs.DeleteAll();
                    Debug.Log("PlayerPrefs.DeleteAll ();");
#endif
                }
            }
            #endregion

            #region Add_Coins
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
#if UNITY_EDITOR
                    PlayerPrefs.SetInt("TotalScores", PlayerPrefs.GetInt("TotalScores") + 30000);
                    Debug.Log("Added 30.000 coins");
#endif
                }
            }
            #endregion

        }

        public void Exit()
		{
			Application.Quit();
		}

		public void SetTrue(GameObject target)
		{
			target.SetActive(true);
		}

		public void SetFalse(GameObject target)
		{
			target.SetActive(false);
		}

		public void ToggleObject(GameObject target)
		{
			target.SetActive(!target.activeSelf);
		}

		public void LoadLevel(string name)
		{

			Loading.SetActive(true);
			SceneManager.LoadSceneAsync(name);
		}

		public void OpenURL(string val)
		{
			Application.OpenURL(val);
		}

		public void Click_Sound()
		{
			if (clickSound)
				clickSound.PlayOneShot(clickSound.clip);
		}

        public void Set_CameraPosition(int GarageCameraState)
        {
            if(GarageCameraState == 0)
                mainCamera.transform.position = sportCamera.position;
            if(GarageCameraState == 1)
                mainCamera.transform.position = truckCamera.position;
            if(GarageCameraState == 2)
                mainCamera.transform.position = F1Camera.position;
            if(GarageCameraState == 3)
                mainCamera.transform.position = offroadCamera.position;
        }

		public void GameMode_Select(int id)
		{
            PlayerPrefs.SetInt("GameMode",id);
        }

        #region Find_Resolution_Quality


        [Header("Screen Dpi")]
        public int veryLowDpi = 300;
        public int lowDpi = 400;
        public int mediumDpi = 500;
        public int highDpi = 700;
        public int ultraDpi = 1000;
        public int maxDpi = 1500;

        float factor;
        int number;

        IEnumerator Find_Resolutions()
        {
            factor = 1f;

            float currentDPI_VeryLow = Screen.width * Screen.height;
            float currentDPI_Low = Screen.width * Screen.height;
            float currentDPI_Medium = Screen.width * Screen.height;
            float currentDPI_High = Screen.width * Screen.height;
            float currentDPI_Ultra = Screen.width * Screen.height;
            float currentDPI_Max = Screen.width * Screen.height;

            /*Debug.Log(PlayerPrefs.GetInt("OriginalX") + " x " +
                   (PlayerPrefs.GetInt("OriginalY")));
            */
            // Very Low
            number = 0;
            while (currentDPI_VeryLow > (veryLowDpi * 1000))
            {
                #region Very_Low
                number++;

                if (number == 0)
                    factor = 1f;
                if (number == 1)
                    factor = 0.9f;
                if (number == 2)
                    factor = 0.8f;
                if (number == 3)
                    factor = 0.7f;
                if (number == 4)
                    factor = 0.6f;
                if (number == 5)
                    factor = 0.5f;
                if (number == 6)
                    factor = 0.4f;
                if (number == 7)
                    factor = 0.3f;
                if (number == 8)
                    factor = 0.2f;

                // Debug.Log(factor);

                Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * factor),
               (int)(PlayerPrefs.GetInt("OriginalY") * factor), true);

                yield return new WaitForEndOfFrame();

                currentDPI_VeryLow = Screen.width * Screen.height;

                //  Debug.Log("Very Low : " + currentDPI_VeryLow + " - " + Screen.width + " x " + Screen.height);
            }
            PlayerPrefs.SetFloat("VeryLow_width", Screen.width);
            PlayerPrefs.SetFloat("VeryLow_height", Screen.height);

            // Debug.Log(number + " - VeryLow");
            #endregion

            // Low
            number = 0;
            while (currentDPI_Low > (lowDpi * 1000))
            {
                #region Low
                number++;

                if (number == 0)
                    factor = 1f;
                if (number == 1)
                    factor = 0.9f;
                if (number == 2)
                    factor = 0.8f;
                if (number == 3)
                    factor = 0.7f;
                if (number == 4)
                    factor = 0.6f;
                if (number == 5)
                    factor = 0.5f;
                if (number == 6)
                    factor = 0.4f;
                if (number == 7)
                    factor = 0.3f;
                if (number == 8)
                    factor = 0.2f;

                // Debug.Log(factor);

                Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * factor),
               (int)(PlayerPrefs.GetInt("OriginalY") * factor), true);

                yield return new WaitForEndOfFrame();

                currentDPI_Low = Screen.width * Screen.height;

                // Debug.Log("Low : " + currentDPI_Low + " - " + Screen.width + " x " + Screen.height);
            }
            PlayerPrefs.SetFloat("Low_width", Screen.width);
            PlayerPrefs.SetFloat("Low_height", Screen.height);

            // Debug.Log(number + " - Low");

            #endregion

            // Medium
            number = 0;
            while (currentDPI_Medium > (mediumDpi * 1000))
            {
                #region Medium
                number++;

                if (number == 0)
                    factor = 1f;
                if (number == 1)
                    factor = 0.9f;
                if (number == 2)
                    factor = 0.8f;
                if (number == 3)
                    factor = 0.7f;
                if (number == 4)
                    factor = 0.6f;
                if (number == 5)
                    factor = 0.5f;
                if (number == 6)
                    factor = 0.4f;
                if (number == 7)
                    factor = 0.3f;
                if (number == 8)
                    factor = 0.2f;

                // Debug.Log(factor);

                Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * factor),
               (int)(PlayerPrefs.GetInt("OriginalY") * factor), true);

                yield return new WaitForEndOfFrame();

                currentDPI_Medium = Screen.width * Screen.height;

                // Debug.Log("Low : " + currentDPI_Low + " - " + Screen.width + " x " + Screen.height);
            }
            PlayerPrefs.SetFloat("Medium_width", Screen.width);
            PlayerPrefs.SetFloat("Medium_height", Screen.height);

            // Debug.Log(number + " - Medium");

            #endregion

            // High
            number = 0;
            while (currentDPI_High > (highDpi * 1000))
            {
                #region High
                number++;

                if (number == 0)
                    factor = 1f;
                if (number == 1)
                    factor = 0.9f;
                if (number == 2)
                    factor = 0.8f;
                if (number == 3)
                    factor = 0.7f;
                if (number == 4)
                    factor = 0.6f;
                if (number == 5)
                    factor = 0.5f;
                if (number == 6)
                    factor = 0.4f;
                if (number == 7)
                    factor = 0.3f;
                if (number == 8)
                    factor = 0.2f;

                // Debug.Log(factor);

                Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * factor),
               (int)(PlayerPrefs.GetInt("OriginalY") * factor), true);

                yield return new WaitForEndOfFrame();

                currentDPI_High = Screen.width * Screen.height;

                // Debug.Log("Low : " + currentDPI_Low + " - " + Screen.width + " x " + Screen.height);
            }
            PlayerPrefs.SetFloat("High_width", Screen.width);
            PlayerPrefs.SetFloat("High_height", Screen.height);

            // Debug.Log(number + " - High");

            #endregion

            // Ultra
            number = 0;
            while (currentDPI_Ultra > (ultraDpi * 1000))
            {
                #region Ultra
                number++;

                if (number == 0)
                    factor = 1f;
                if (number == 1)
                    factor = 0.9f;
                if (number == 2)
                    factor = 0.8f;
                if (number == 3)
                    factor = 0.7f;
                if (number == 4)
                    factor = 0.6f;
                if (number == 5)
                    factor = 0.5f;
                if (number == 6)
                    factor = 0.4f;
                if (number == 7)
                    factor = 0.3f;
                if (number == 8)
                    factor = 0.2f;

                // Debug.Log(factor);

                Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * factor),
               (int)(PlayerPrefs.GetInt("OriginalY") * factor), true);

                yield return new WaitForEndOfFrame();

                currentDPI_Ultra = Screen.width * Screen.height;

                // Debug.Log("Low : " + currentDPI_Low + " - " + Screen.width + " x " + Screen.height);
            }
            PlayerPrefs.SetFloat("Ultra_width", Screen.width);
            PlayerPrefs.SetFloat("Ultra_height", Screen.height);

            //Debug.Log(number + " - Ultra");

            #endregion

            // Max
            number = 0;
            while (currentDPI_Max > (maxDpi * 1000))
            {
                #region Max
                number++;

                if (number == 0)
                    factor = 1f;
                if (number == 1)
                    factor = 0.9f;
                if (number == 2)
                    factor = 0.8f;
                if (number == 3)
                    factor = 0.7f;
                if (number == 4)
                    factor = 0.6f;
                if (number == 5)
                    factor = 0.5f;
                if (number == 6)
                    factor = 0.4f;
                if (number == 7)
                    factor = 0.3f;
                if (number == 8)
                    factor = 0.2f;

                // Debug.Log(factor);

                Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * factor),
               (int)(PlayerPrefs.GetInt("OriginalY") * factor), true);

                yield return new WaitForEndOfFrame();

                currentDPI_Max = Screen.width * Screen.height;

                // Debug.Log("Low : " + currentDPI_Low + " - " + Screen.width + " x " + Screen.height);
            }
            PlayerPrefs.SetFloat("Max_width", Screen.width);
            PlayerPrefs.SetFloat("Max_height", Screen.height);

            //  Debug.Log(number + " - Max");

            #endregion
        }
        #endregion
    }
}