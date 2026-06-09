using UnityEngine;
using System.Collections.Generic;

public class EnemyPos : MonoBehaviour
{
    [SerializeField] private GamePlayManager gameplayManager;
    [SerializeField] private List<GameObject> positionObjects = new List<GameObject>();
    [SerializeField] private Transform targetEnemyTransform;

    private int currentVisibleIndex = -1;

    private void Awake()
    {
        if (targetEnemyTransform == null)
            targetEnemyTransform = transform;

        if (targetEnemyTransform != null && targetEnemyTransform.root != null)
            targetEnemyTransform = targetEnemyTransform.root;
    }

    private void OnEnable()
    {
        ResolveGameplayManager();
        ApplyPlayerVisibilityRule();
    }

    private void Update()
    {
        ResolveGameplayManager();

        if (ApplyPlayerVisibilityRule())
            return;

        UpdateEnemyPositionObjects();
    }

    private void UpdateEnemyPositionObjects()
    {
        if (gameplayManager == null || targetEnemyTransform == null)
            return;

        int racerPosition = gameplayManager.GetRacerPosition(targetEnemyTransform);
        int targetIndex = racerPosition - 1;

        if (targetIndex == currentVisibleIndex)
            return;

        currentVisibleIndex = targetIndex;

        for (int i = 0; i < positionObjects.Count; i++)
        {
            GameObject numberObject = positionObjects[i];

            if (numberObject == null)
                continue;

            bool shouldShow = targetIndex >= 0 && i == targetIndex;

            if (numberObject.activeSelf != shouldShow)
                numberObject.SetActive(shouldShow);
        }
    }

    private void ResolveGameplayManager()
    {
        if (gameplayManager == null)
            gameplayManager = FindFirstObjectByType<GamePlayManager>(FindObjectsInactive.Include);
    }

    private bool ApplyPlayerVisibilityRule()
    {
        if (gameplayManager == null || targetEnemyTransform == null)
        {
            HideAllPositionObjects();
            return false;
        }

        bool isPlayerMarker = gameplayManager.IsPlayerRacer(targetEnemyTransform);

        if (isPlayerMarker)
        {
            HideAllPositionObjects();

            if (enabled)
                enabled = false;

            return true;
        }

        return false;
    }

    private void HideAllPositionObjects()
    {
        currentVisibleIndex = -1;

        for (int i = 0; i < positionObjects.Count; i++)
        {
            if (positionObjects[i] != null)
                positionObjects[i].SetActive(false);
        }
    }
}
