using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuGameSettingsUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject startGameSetupPanel;
    [SerializeField] private GameObject mainMenuButtonsPanel;

    [Header("Player Names")]
    [SerializeField] private TMP_InputField whiteNicknameInput;
    [SerializeField] private TMP_InputField blackNicknameInput;

    [Header("Timer Settings")]
    [SerializeField] private Toggle useTimerToggle;
    [SerializeField] private TMP_InputField baseMinutesInput;
    [SerializeField] private TMP_InputField incrementSecondsInput;

    [Header("Load Menu")]
    [SerializeField] private LoadGameMenuUI loadGameMenuUI;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    private void Awake()
    {
        ShowMainMenuButtons();
        HideStartGameSetup();

        if (loadGameMenuUI != null)
            loadGameMenuUI.Close();
    }

    private void Start()
    {
        LoadCurrentSettingsToUI();

        if (useTimerToggle != null)
            useTimerToggle.onValueChanged.AddListener(OnTimerToggleChanged);

        UpdateTimerInputsState();
    }

    private void OnDestroy()
    {
        if (useTimerToggle != null)
            useTimerToggle.onValueChanged.RemoveListener(OnTimerToggleChanged);
    }

    public void OpenStartGameSetup()
    {
        CloseSavedGamesMenuIfExists();
        HideMainMenuButtons();
        ShowStartGameSetup();

        LoadCurrentSettingsToUI();
        UpdateTimerInputsState();
    }

    public void CloseStartGameSetup()
    {
        HideStartGameSetup();
        ShowMainMenuButtons();
        CloseSavedGamesMenuIfExists();
    }

    public void OnTimerToggleChanged(bool isOn)
    {
        UpdateTimerInputsState();
    }

    public void StartGame()
    {
        GameSettings.WhitePlayerName = GetValidatedPlayerName(whiteNicknameInput, "WhitePlayer");
        GameSettings.BlackPlayerName = GetValidatedPlayerName(blackNicknameInput, "BlackPlayer");
        GameSettings.UseTimer = useTimerToggle != null && useTimerToggle.isOn;
        GameSettings.BaseMinutes = Mathf.Clamp(ParseFloat(GetInputText(baseMinutesInput), 10f), 1f, 180f);
        GameSettings.IncrementSeconds = Mathf.Clamp(ParseFloat(GetInputText(incrementSecondsInput), 0f), 0f, 180f);

        PendingGameLoad.Clear();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSavedGamesStub()
    {
        OpenSavedGamesMenu();
    }

    public void OpenSavedGamesMenu()
    {
        if (loadGameMenuUI == null)
        {
            Debug.LogError("MainMenuGameSettingsUI: LoadGameMenuUI is not assigned.");
            return;
        }

        HideStartGameSetup();
        HideMainMenuButtons();
        loadGameMenuUI.Open();
    }

    public void CloseSavedGamesMenu()
    {
        CloseSavedGamesMenuIfExists();
        HideStartGameSetup();
        ShowMainMenuButtons();
    }

    private void LoadCurrentSettingsToUI()
    {
        if (whiteNicknameInput != null)
            whiteNicknameInput.text = GameSettings.WhitePlayerName;

        if (blackNicknameInput != null)
            blackNicknameInput.text = GameSettings.BlackPlayerName;

        if (useTimerToggle != null)
            useTimerToggle.isOn = GameSettings.UseTimer;

        if (baseMinutesInput != null)
            baseMinutesInput.text = GameSettings.BaseMinutes.ToString("0.##", CultureInfo.InvariantCulture);

        if (incrementSecondsInput != null)
            incrementSecondsInput.text = GameSettings.IncrementSeconds.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void UpdateTimerInputsState()
    {
        bool enabled = useTimerToggle != null && useTimerToggle.isOn;

        if (baseMinutesInput != null)
            baseMinutesInput.interactable = enabled;

        if (incrementSecondsInput != null)
            incrementSecondsInput.interactable = enabled;
    }

    private string GetValidatedPlayerName(TMP_InputField inputField, string fallback)
    {
        string value = GetInputText(inputField).Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private string GetInputText(TMP_InputField inputField)
    {
        return inputField != null ? inputField.text : string.Empty;
    }

    private void ShowStartGameSetup()
    {
        if (startGameSetupPanel != null)
            startGameSetupPanel.SetActive(true);
    }

    private void HideStartGameSetup()
    {
        if (startGameSetupPanel != null)
            startGameSetupPanel.SetActive(false);
    }

    private void ShowMainMenuButtons()
    {
        if (mainMenuButtonsPanel != null)
            mainMenuButtonsPanel.SetActive(true);
    }

    private void HideMainMenuButtons()
    {
        if (mainMenuButtonsPanel != null)
            mainMenuButtonsPanel.SetActive(false);
    }

    private void CloseSavedGamesMenuIfExists()
    {
        if (loadGameMenuUI != null)
            loadGameMenuUI.Close();
    }

    private float ParseFloat(string input, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(input))
            return defaultValue;

        input = input.Replace(',', '.');

        if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            return result;

        return defaultValue;
    }
}