//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ALIyerEdon
{
    public class Shake_Utility : MonoBehaviour
    {
        public float shakeIntensity = 5f;

        public bool collisionShaking;
        public bool offRoadShaking;
        public bool delayShaking;

        Quaternion originalRotation;

        [HideInInspector] public float currentSpeed;

        void Start()
        {
            originalRotation = transform.localRotation;
        }

        // Update is called once per frame
        void Update()
        {
            if (collisionShaking)
            {
                float angle;
                angle = Mathf.Sin(Time.time * 30) * (shakeIntensity / 10);

                transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            else if(offRoadShaking)
            {
                float angle;
                angle = Mathf.Sin(Time.time * 30) * ((shakeIntensity * currentSpeed) / 1523);

                transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            else if(delayShaking)
            {
                float angle;
                angle = Mathf.Sin(Time.time * 30) * (shakeIntensity / 10);

                transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            else
                transform.localRotation = originalRotation;
        }

        [HideInInspector] public bool isShaking;
        public void Shake_Now(float duration, float Intensity)
        {
            if (!isShaking)
                StartCoroutine(Do_Shake(duration, Intensity));
        }

        IEnumerator Do_Shake(float duration, float Intensity)
        {
            isShaking = true;
            delayShaking = true;
            shakeIntensity = Intensity;

            yield return new WaitForSeconds(duration);

            isShaking = false;
            delayShaking = false;
        }
    }
}