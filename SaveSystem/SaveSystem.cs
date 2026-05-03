using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private const string SavesFolderName = "saves";
    private const string SaveFileExtension = ".json";

    private static string SavesFolderPath => Path.Combine(Application.persistentDataPath, SavesFolderName);

    public static string SaveGame(SavedGameData saveData, string fileName = null)
    {
        if (saveData == null)
        {
            Debug.LogError("SaveGame failed: saveData is null.");
            return null;
        }

        EnsureSaveDirectoryExists();

        string resolvedFileName = string.IsNullOrWhiteSpace(fileName)
            ? CreateFileName(saveData.WhitePlayerName, saveData.BlackPlayerName)
            : fileName;

        saveData.FileName = resolvedFileName;
        saveData.SavedAtTicks = DateTime.Now.Ticks;
        saveData.SavedAtDisplay = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        saveData.EnsureCollections();

        string fullPath = GetFullPath(resolvedFileName);
        string json = JsonUtility.ToJson(saveData, true);

        File.WriteAllText(fullPath, json);
        Debug.Log($"Game saved to: {fullPath}");

        return resolvedFileName;
    }

    public static SavedGameData LoadGame(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Debug.LogError("LoadGame failed: fileName is empty.");
            return null;
        }

        string fullPath = GetFullPath(fileName);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"LoadGame failed: file not found: {fullPath}");
            return null;
        }

        if (!TryReadSaveData(fullPath, fileName, out SavedGameData saveData))
        {
            Debug.LogError($"LoadGame failed: invalid or empty save file: {fullPath}");
            return null;
        }

        return saveData;
    }

    public static List<SaveFileInfo> GetAllSaves()
    {
        EnsureSaveDirectoryExists();

        List<SaveFileInfo> saves = new List<SaveFileInfo>();
        string[] files = Directory.GetFiles(SavesFolderPath, $"*{SaveFileExtension}");

        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileName(files[i]);

            if (!TryLoadGameSilently(fileName, out SavedGameData saveData))
                continue;

            saves.Add(new SaveFileInfo
            {
                FileName = fileName,
                WhitePlayerName = saveData.WhitePlayerName,
                BlackPlayerName = saveData.BlackPlayerName,
                SavedAtDisplay = saveData.SavedAtDisplay,
                SavedAtTicks = saveData.SavedAtTicks,
                GameEnded = saveData.GameEnded
            });
        }

        saves.Sort((a, b) => b.SavedAtTicks.CompareTo(a.SavedAtTicks));
        return saves;
    }

    public static bool DeleteSave(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        string fullPath = GetFullPath(fileName);
        if (!File.Exists(fullPath))
            return false;

        File.Delete(fullPath);
        return true;
    }

    private static bool TryLoadGameSilently(string fileName, out SavedGameData saveData)
    {
        saveData = null;

        try
        {
            string fullPath = GetFullPath(fileName);
            if (!File.Exists(fullPath))
                return false;

            return TryReadSaveData(fullPath, fileName, out saveData);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadSaveData(string fullPath, string fileName, out SavedGameData saveData)
    {
        saveData = null;

        string json = File.ReadAllText(fullPath);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        saveData = JsonUtility.FromJson<SavedGameData>(json);
        if (saveData == null)
            return false;

        NormalizeLoadedSaveData(saveData, fileName);
        return true;
    }

    private static void NormalizeLoadedSaveData(SavedGameData saveData, string fileName)
    {
        if (string.IsNullOrWhiteSpace(saveData.FileName))
            saveData.FileName = fileName;

        saveData.EnsureCollections();
    }

    private static void EnsureSaveDirectoryExists()
    {
        if (!Directory.Exists(SavesFolderPath))
            Directory.CreateDirectory(SavesFolderPath);
    }

    private static string GetFullPath(string fileName)
    {
        EnsureSaveDirectoryExists();
        return Path.Combine(SavesFolderPath, fileName);
    }

    private static string CreateFileName(string whiteName, string blackName)
    {
        string safeWhite = SanitizeFileNamePart(string.IsNullOrWhiteSpace(whiteName) ? "White" : whiteName);
        string safeBlack = SanitizeFileNamePart(string.IsNullOrWhiteSpace(blackName) ? "Black" : blackName);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        return $"{safeWhite}_vs_{safeBlack}_{timestamp}{SaveFileExtension}";
    }

    private static string SanitizeFileNamePart(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();

        for (int i = 0; i < invalidChars.Length; i++)
            value = value.Replace(invalidChars[i].ToString(), "_");

        value = value.Replace(" ", "_");
        return value;
    }
}