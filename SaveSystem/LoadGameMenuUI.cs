using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadGameMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject savedGameItemPrefab;
    [SerializeField] private TMP_Text emptyText;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    private readonly List<GameObject> spawnedItems = new List<GameObject>();

    public void Open()
    {
        if (rootPanel == null)
        {
            Debug.LogError("LoadGameMenuUI: Root Panel is not assigned.");
            return;
        }

        rootPanel.SetActive(true);
        rootPanel.transform.SetAsLastSibling();

        RefreshList();
        ForceRebuildLayouts();
        Canvas.ForceUpdateCanvases();
    }

    public void Close()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void RefreshList()
    {
        ClearList();

        if (!HasValidReferences())
            return;

        List<SaveFileInfo> saves = SaveSystem.GetAllSaves();
        UpdateEmptyState(saves.Count == 0);

        for (int i = 0; i < saves.Count; i++)
            SpawnSaveItem(saves[i]);
    }

    private void OnLoadClicked(SaveFileInfo saveInfo)
    {
        if (saveInfo == null)
            return;

        PendingGameLoad.Set(saveInfo.FileName);
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnDeleteClicked(SaveFileInfo saveInfo)
    {
        if (saveInfo == null)
            return;

        SaveSystem.DeleteSave(saveInfo.FileName);
        RefreshList();
        ForceRebuildLayouts();
    }

    private void SpawnSaveItem(SaveFileInfo saveInfo)
    {
        GameObject itemObject = Instantiate(savedGameItemPrefab, contentRoot, false);
        itemObject.name = $"SaveItem_{saveInfo.FileName}";

        if (!itemObject.TryGetComponent(out SavedGameListItemUI itemUI))
        {
            Debug.LogError("LoadGameMenuUI: SavedGameItemPrefab does not have SavedGameListItemUI.");
            Destroy(itemObject);
            return;
        }

        spawnedItems.Add(itemObject);
        itemUI.Initialize(saveInfo, OnLoadClicked, OnDeleteClicked);
    }

    private void UpdateEmptyState(bool isEmpty)
    {
        if (emptyText == null)
            return;

        emptyText.gameObject.SetActive(isEmpty);

        if (isEmpty)
            emptyText.text = "No saved games yet.";
    }

    private bool HasValidReferences()
    {
        bool isValid = true;

        if (contentRoot == null)
        {
            Debug.LogError("LoadGameMenuUI: Content Root is not assigned.");
            isValid = false;
        }

        if (savedGameItemPrefab == null)
        {
            Debug.LogError("LoadGameMenuUI: Saved Game Item Prefab is not assigned.");
            isValid = false;
        }

        return isValid;
    }

    private void ClearList()
    {
        for (int i = spawnedItems.Count - 1; i >= 0; i--)
        {
            if (spawnedItems[i] != null)
                Destroy(spawnedItems[i]);
        }

        spawnedItems.Clear();
    }

    private void ForceRebuildLayouts()
    {
        if (contentRoot is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        if (rootPanel != null && rootPanel.TryGetComponent(out RectTransform rootRect))
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
    }
}