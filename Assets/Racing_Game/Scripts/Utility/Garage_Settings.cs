//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ALIyerEdon;

namespace ALIyerEdon
{
    public class Garage_Settings : MonoBehaviour
    {
        public GameObject[] particleEffects;

        // Start is called before the first frame update
        void Awake()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Garage"))
            {

                // Disable particle effects in the garage scene
                foreach (GameObject g in particleEffects)
                    g.SetActive(false);

                GetComponent<Car_Position>().enabled = false;

                GetComponent<Car_Health>().enabled = false;

                GetComponent<Car_AI>().enabled = false;
                
                GetComponent<EasyCarAudio>().Stop_Effects();
                GetComponent<EasyCarAudio>().enabled = false;

                if (GetComponent<Random_Car_Speed>())
                    GetComponent<Random_Car_Speed>().enabled = false;

                GetComponent<EasyCarController>().Clutch = true;
            }
        }
    }
}