using UnityEngine;

public class LoadTestScene : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Start()
    {
        LoadingManager.Instance.LoadScene("GameplayTestScene");
    }

    // Update is called once per frame
   
}
