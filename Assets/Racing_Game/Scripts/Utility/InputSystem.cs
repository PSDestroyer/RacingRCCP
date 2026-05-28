//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using UnityEngine;
using System.Collections;
using ALIyerEdon;

namespace ALIyerEdon
{
	public class InputSystem : MonoBehaviour
	{

		[HideInInspector] public bool canControl = false;

		[Tooltip("Automatically switch between keyboard and mobile controls based on the running platform")]
		public bool autoSwitchPlatform = true;
		// Select control type => Touch or keyboard
		[Tooltip("Keyboard for pc and mobile for touch platforms")]
		public InputType controlType;


		EasyCarController controller;


		float motorInput, steerInput;
		bool handBrake;

		[Header("Components")]
		ALIyerEdon.Joystick vJoystick;
		bool sWheelControl;

		public GameObject joystick;
		public GameObject arrowKeys;

		// Accelerometer controlling
		[Header("Accelerometer")]
		public float accelSensibility = 10f;
		public float accelSmooth = 0.5f;
		Vector3 curAc;
		bool accelInput;

		[Header("Keyboard Keys")]
		public KeyCode CameraSwitch = KeyCode.C;
		public KeyCode HandBrake = KeyCode.Space;
		public KeyCode Nitro = KeyCode.LeftShift;
		public KeyCode Pause = KeyCode.Escape;

		IEnumerator Start()
		{
			if (PlayerPrefs.GetFloat("accelSensibility") == 0)
				PlayerPrefs.SetFloat("accelSensibility", 10f);


			vJoystick = joystick.GetComponent<ALIyerEdon.Joystick>();

			if (autoSwitchPlatform)
			{
#if UNITY_EDITOR || UNITY_WEBGL || UNITY_STANDALONE || UNITY_WSA || UNITY_64
				controlType = InputType.Keyboard;
#else
						controlType = InputType.Mobile;			
#endif
			}

			if (PlayerPrefs.GetInt("ControlType") == 0)
			{
				joystick.SetActive(false);
				arrowKeys.SetActive(true);
			}
			if (PlayerPrefs.GetInt("ControlType") == 1)
			{
				joystick.SetActive(true);
				arrowKeys.SetActive(false);
				sWheelControl = true;
			}
			if (PlayerPrefs.GetInt("ControlType") == 2)
			{
				joystick.SetActive(false);
				arrowKeys.SetActive(false);
				accelInput = true;
			}



			accelSensibility = PlayerPrefs.GetFloat("accelSensibility");

			yield return new WaitForEndOfFrame();

			controller = GameObject.FindGameObjectWithTag("Player")
				.GetComponent<EasyCarController>();

			GameObject.FindGameObjectWithTag("Player")
				.GetComponent<Car_AI>().enabled = false;
		}

		void Update()
		{

			if (!controller || !canControl)
				return;

			if (accelInput)
			{
				// Controll steering (mobile)	
				// 		
				if (Input.acceleration.x > 0.2f || Input.acceleration.x < -0.2f)
				{
					steerInput = Input.acceleration.x * Time.deltaTime * accelSensibility;
				}
				else
				{
					steerInput = 0;
				}

			}

			if (sWheelControl)
				steerInput = vJoystick.GetHorizontal(0) * Time.deltaTime * 23;


			if (controlType == InputType.Keyboard)
			{

				motorInput = Input.GetAxis("Vertical");

				steerInput = Input.GetAxis("Horizontal");

				if (Input.GetKey(HandBrake))
					handBrake = true;
				else
					handBrake = false;


				if (Input.GetKeyDown(CameraSwitch))
					FindObjectOfType<CameraSwitch>().NextCamera();

				if (Input.GetKeyDown(Pause))
					FindObjectOfType<Pause_Menu>().Pause();
			}

			controller.Move(motorInput, steerInput, handBrake);

		}

		public void Throttle()
		{
			if (controlType == InputType.Mobile)
				motorInput = 1f;
		}

		public void ThrottleRelease()
		{
			if (controlType == InputType.Mobile)
				motorInput = 0;
		}

		public void Steer(bool state)
		{
			if (controlType == InputType.Mobile)
			{
				if (state)
					steerInput = Mathf.Lerp(steerInput, 1f, Time.deltaTime * 25);
				else
					steerInput = Mathf.Lerp(steerInput, -1f, Time.deltaTime * 25);
			}
		}

		public void SteerRelease()
		{
			if (controlType == InputType.Mobile)
				steerInput = 0;

		}

		public void Brake(bool state)
		{
			if (controlType == InputType.Mobile)
			{
				if (state)
					motorInput = -1f;
				else
					motorInput = 0;
			}
		}

		public void Hand_Brake(bool state)
		{
			handBrake = state;
		}

		public void LoadLevel(string name)
		{
			UnityEngine.SceneManagement.SceneManager.LoadScene(name);
		}

		public void Pause_Game()
		{
			FindObjectOfType<Pause_Menu>().Pause();
		}

		public void Switch_Camera()
		{
			FindObjectOfType<CameraSwitch>().NextCamera();
		}
	}
}