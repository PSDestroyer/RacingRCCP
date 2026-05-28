//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using ALIyerEdon;

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

		void Awake()
		{
			AudioListener.volume = 1f;
			
			Time.timeScale = 1f;

			PlayerPrefs.SetInt("Target FPS", targetFPS);

			Application.targetFrameRate = targetFPS;

			
			// Is game first run?   3 => true    0 => false
			if (PlayerPrefs.GetInt("FirstRun") != 3)
			{
				PlayerPrefs.SetInt("OriginalX", Screen.width);
				PlayerPrefs.SetInt("OriginalY", Screen.height);

				// Setr default resolution quality to the 0.7 of 1.0 (70% of 100%)
				PlayerPrefs.SetInt("ResQuality", 1);

				// Default difficulty level => 0 = simulation , 1 = Arcade
				PlayerPrefs.SetInt("Difficulty Level", 0);

				// Set default control type to the arrow keys
				//Arrow keys = 0 , Joystick = 1 , acceleration = 2
				PlayerPrefs.SetInt("ControlType", 0);
				
				// Default quality level is Low
				PlayerPrefs.SetInt("QualityLevel", 1);

				PlayerPrefs.SetFloat("accelSensibility", 100f);

				PlayerPrefs.SetFloat("SteeringWheelSens", 250f);

				// Set starting color for the first car (car0)
				PlayerPrefs.SetInt("CarColor0", 0);

				// Set music ambient sound in settings false
				PlayerPrefs.SetFloat("Music", 0.7f);

				// Enable right position ui info display
				PlayerPrefs.SetInt("ShowPositionUI", 1);

				// Open the first car (car0)
				PlayerPrefs.SetInt("Car0", 3);
				PlayerPrefs.SetInt("Car1", 3);

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

				PlayerPrefs.SetInt("MotionBlur", 1);
				PlayerPrefs.SetInt("SSR", 1);

				PlayerPrefs.SetInt("Show_FPS", 0);

				// Disable the first running the game settings
				PlayerPrefs.SetInt("FirstRun", 3);
			}

			if (PlayerPrefs.GetInt("ResQuality") == 0)
			{
				Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * 0.5f),
					(int)(PlayerPrefs.GetInt("OriginalY") * 0.5f), true);
			}
			if (PlayerPrefs.GetInt("ResQuality") == 1)
			{
				Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * 0.7f),
					(int)(PlayerPrefs.GetInt("OriginalY") * 0.7f), true);
			}
			if (PlayerPrefs.GetInt("ResQuality") == 2)
			{
				Screen.SetResolution((int)(PlayerPrefs.GetInt("OriginalX") * 1),
					(int)(PlayerPrefs.GetInt("OriginalY") * 1), true);
			}

			if (totalCoinsText)
				totalCoinsText.text = PlayerPrefs.GetInt("TotalScores").ToString();

			// Instantiate the last car at start
			// Instantiate(carSelect.cars[PlayerPrefs.GetInt("CarID")], carSelect.spawnPoint.position, carSelect.spawnPoint.rotation);
		}

		void Update()
		{
			// Exit with back button
			if (Input.GetKeyDown(KeyCode.Escape))
				exitMenu.SetActive(!exitMenu.activeSelf);

			if (Input.GetKeyDown(KeyCode.H))
			{
				PlayerPrefs.DeleteAll();
				Debug.Log("PlayerPrefs.DeleteAll ();");
			}
			if (Input.GetKeyDown(KeyCode.E))
				PlayerPrefs.SetInt("Coins", PlayerPrefs.GetInt("Coins") + 14000);
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
	}
}