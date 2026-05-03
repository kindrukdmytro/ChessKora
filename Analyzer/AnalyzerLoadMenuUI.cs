using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnalyzerLoadMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AnalyzerManager analyzerManager;
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject savedGameItemPrefab;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private Button closeButton;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();

    private void Awake()
    {
        BindCloseButton();

        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        UnbindCloseButton();
    }

    public void Open()
    {
        if (rootPanel == null)
        {
            Debug.LogWarning("AnalyzerLoadMenuUI: Root Panel is not assigned.");
            return;
        }

        rootPanel.SetActive(true);
        rootPanel.transform.SetAsLastSibling();
        RefreshList();
    }

    public void Close()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void RefreshList()
    {
        ClearList();

        if (!HasValidListReferences())
            return;

        List<SaveFileInfo> saves = SaveSystem.GetAllSaves();
        UpdateEmptyState(saves.Count == 0);

        for (int i = 0; i < saves.Count; i++)
            SpawnSaveItem(saves[i]);

        Canvas.ForceUpdateCanvases();
    }

    private void OnLoadClicked(SaveFileInfo saveInfo)
    {
        if (saveInfo == null)
            return;

        if (analyzerManager == null)
        {
            Debug.LogWarning("AnalyzerLoadMenuUI: AnalyzerManager is not assigned.");
            return;
        }

        Close();
        Canvas.ForceUpdateCanvases();
        analyzerManager.LoadSavedGameByFileName(saveInfo.FileName);
    }

    private void OnDeleteClicked(SaveFileInfo saveInfo)
    {
        if (saveInfo == null)
            return;

        SaveSystem.DeleteSave(saveInfo.FileName);
        RefreshList();
    }

    private void SpawnSaveItem(SaveFileInfo saveInfo)
    {
        GameObject itemObject = Instantiate(savedGameItemPrefab, contentRoot, false);

        if (!itemObject.TryGetComponent(out SavedGameListItemUI itemUI))
        {
            Debug.LogError("AnalyzerLoadMenuUI: SavedGameItemPrefab does not have SavedGameListItemUI.");
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

    private bool HasValidListReferences()
    {
        bool isValid = true;

        if (contentRoot == null)
        {
            Debug.LogWarning("AnalyzerLoadMenuUI: Content Root is not assigned.");
            isValid = false;
        }

        if (savedGameItemPrefab == null)
        {
            Debug.LogWarning("AnalyzerLoadMenuUI: Saved Game Item Prefab is not assigned.");
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

    private void BindCloseButton()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
    }

    private void UnbindCloseButton()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveListener(Close);
    }
}