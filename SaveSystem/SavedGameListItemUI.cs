using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SavedGameListItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button deleteButton;

    private SaveFileInfo currentInfo;
    private Action<SaveFileInfo> onLoadClicked;
    private Action<SaveFileInfo> onDeleteClicked;

    public void Initialize(
        SaveFileInfo saveInfo,
        Action<SaveFileInfo> loadCallback,
        Action<SaveFileInfo> deleteCallback)
    {
        currentInfo = saveInfo;
        onLoadClicked = loadCallback;
        onDeleteClicked = deleteCallback;

        UpdateTexts();
        BindButtons();
    }

    private void UpdateTexts()
    {
        if (currentInfo == null)
            return;

        if (titleText != null)
            titleText.text = $"{GetDisplayName(currentInfo.WhitePlayerName, "White")} vs {GetDisplayName(currentInfo.BlackPlayerName, "Black")}";

        if (subtitleText != null)
            subtitleText.text = $"{currentInfo.SavedAtDisplay} • {(currentInfo.GameEnded ? "finished" : "in progress")}";
    }

    private void BindButtons()
    {
        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(HandleLoadClicked);
            loadButton.onClick.AddListener(HandleLoadClicked);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(HandleDeleteClicked);
            deleteButton.onClick.AddListener(HandleDeleteClicked);
        }
    }

    private static string GetDisplayName(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private void HandleLoadClicked()
    {
        if (currentInfo == null)
            return;

        onLoadClicked?.Invoke(currentInfo);
    }

    private void HandleDeleteClicked()
    {
        if (currentInfo == null)
            return;

        onDeleteClicked?.Invoke(currentInfo);
    }
}