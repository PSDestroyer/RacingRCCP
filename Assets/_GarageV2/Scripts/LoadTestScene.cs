using UnityEngine;

public class LoadTestScene : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Stasrt()
    {
        LoadingManager.Instance.LoadScene("RCCP_Scene_Blank");
    }

    // Update is called once per frame
   
}
