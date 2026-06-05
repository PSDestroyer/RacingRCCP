using System.Collections;
using System.Collections.Generic;
using HalvaStudio.Save;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Username : MonoBehaviour
{
    public Button StartPlay;
    public TMP_InputField inputF;
    private PlayerInput _playerInput;
    public TMP_Text Saving;
    void Start()
    {
        Saving.gameObject.SetActive(false);
        _playerInput = GetComponent<PlayerInput>();
        // inputF.Select();
        inputF.onSubmit.AddListener(deselecUI);
        // StartPlay.onClick.AddListener(() => LoadMenu());
        InputAction submitAction = GetAction("Submit");
        if (submitAction != null)
            submitAction.performed += SelectInput;

        InputAction cancelAction = GetAction("Cancel");
        if (cancelAction != null)
            cancelAction.performed += LoadMenuCTX;
    }

    private void deselecUI(string arg0)
    {
        EventSystem.current.SetSelectedGameObject(null);
    }
    private void SelectInput(InputAction.CallbackContext CTX)
    {
        inputF.Select();

    }
    private void LoadMenuCTX(InputAction.CallbackContext ctx) => LoadMenu();
    public void LoadMenu()
    {
        SaveManager.Instance.saveData.PlayerName = inputF.text;
        SaveManager.Instance.Save();
        StartCoroutine(LoadGame());

        InputAction submitAction = GetAction("Submit");
        if (submitAction != null)
            submitAction.performed -= SelectInput;

        InputAction cancelAction = GetAction("Cancel");
        if (cancelAction != null)
            cancelAction.performed -= LoadMenuCTX;
    }

    IEnumerator LoadGame()
    {
        Saving.text = "Saving .";
        Saving.gameObject.SetActive(true);
        yield return new WaitForSeconds(1);
        Saving.text = "Saving ..";
        yield return new WaitForSeconds(1);
        Saving.text = "Saving ...";
        yield return new WaitForSeconds(1);
        SaveManager.Instance.Save();
      
        LoadingManager.Instance.LoadScene("Menu");
        
    }

    private InputAction GetAction(string actionName)
    {
        if (_playerInput == null)
            _playerInput = GetComponent<PlayerInput>();

        if (_playerInput == null || _playerInput.actions == null)
            return null;

        return _playerInput.actions[actionName];
    }
}
