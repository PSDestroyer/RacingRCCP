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
        if (_playerInput == null || _playerInput.actions == null)
            return;

        _playerInput.actions["Submit"].performed += SelectInput;
        _playerInput.actions["Cancel"].performed += LoadMenuCTX;
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
    void LoadMenu()
    {
        SaveManager.Instance.saveData.PlayerName = inputF.text;
        SaveManager.Instance.Save();
        StartCoroutine(LoadGame());

        if (_playerInput == null || _playerInput.actions == null)
            return;

        _playerInput.actions["Submit"].performed -= SelectInput;
        _playerInput.actions["Cancel"].performed -= LoadMenuCTX;
    }

    IEnumerator LoadGame()
    {
        string savingText = UILocalization.Get("Saving", "Saving");
        Saving.text = savingText + " .";
        Saving.gameObject.SetActive(true);
        yield return new WaitForSeconds(1);
        Saving.text = savingText + " ..";
        yield return new WaitForSeconds(1);
        Saving.text = savingText + " ...";
        yield return new WaitForSeconds(1);
        SaveManager.Instance.Save();
      
        LoadingManager.Instance.LoadScene("Menu");
        
    }
}
