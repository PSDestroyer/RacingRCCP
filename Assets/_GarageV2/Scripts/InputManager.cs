using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class InputManager : Singleton<InputManager>
{
   public PlayerInput playerInput;

   public override void AwakeInit()
   {
      RefreshPlayerInput();
   }

   private void OnEnable()
   {
      SceneManager.sceneLoaded += HandleSceneLoaded;
   }

   private void OnDisable()
   {
      SceneManager.sceneLoaded -= HandleSceneLoaded;
   }

   private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
   {
      RefreshPlayerInput();
   }

   public PlayerInput GetPlayerInput()
   {
      RefreshPlayerInput();

      return playerInput;
   }

   public void RefreshPlayerInput()
   {
      playerInput = FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);
   }
}
