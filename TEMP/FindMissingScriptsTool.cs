#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class FindMissingScriptsTool
{
    [MenuItem("Tools/Chess Game/Find Missing Scripts/In Loaded Scenes")]
    private static void FindMissingScriptsInLoadedScenes()
    {
        int totalMissing = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                totalMissing += ScanGameObjectRecursive(root, $"Scene '{scene.name}'");
            }
        }

        if (totalMissing == 0)
            Debug.Log("FindMissingScriptsTool: No missing scripts found in loaded scenes.");
        else
            Debug.LogWarning($"FindMissingScriptsTool: Found {totalMissing} missing script reference(s) in loaded scenes.");
    }

    [MenuItem("Tools/Chess Game/Find Missing Scripts/In DontDestroyOnLoad")]
    private static void FindMissingScriptsInDontDestroyOnLoad()
    {
        int totalMissing = 0;
        List<GameObject> roots = GetDontDestroyOnLoadRoots();

        foreach (GameObject root in roots)
        {
            totalMissing += ScanGameObjectRecursive(root, "DontDestroyOnLoad");
        }

        if (totalMissing == 0)
            Debug.Log("FindMissingScriptsTool: No missing scripts found in DontDestroyOnLoad.");
        else
            Debug.LogWarning($"FindMissingScriptsTool: Found {totalMissing} missing script reference(s) in DontDestroyOnLoad.");
    }

    [MenuItem("Tools/Chess Game/Find Missing Scripts/In All Prefabs")]
    private static void FindMissingScriptsInAllPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int totalMissing = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            totalMissing += ScanGameObjectRecursive(prefab, $"Prefab '{path}'");
        }

        if (totalMissing == 0)
            Debug.Log("FindMissingScriptsTool: No missing scripts found in prefabs.");
        else
            Debug.LogWarning($"FindMissingScriptsTool: Found {totalMissing} missing script reference(s) in prefabs.");
    }

    [MenuItem("Tools/Chess Game/Find Missing Scripts/Everywhere")]
    private static void FindMissingScriptsEverywhere()
    {
        FindMissingScriptsInLoadedScenes();
        FindMissingScriptsInDontDestroyOnLoad();
        FindMissingScriptsInAllPrefabs();
    }

    [MenuItem("Tools/Chess Game/Remove Missing Scripts/From Selected Object And Children")]
    private static void RemoveMissingScriptsFromSelectedObjectAndChildren()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("FindMissingScriptsTool: No selected GameObject.");
            return;
        }

        int removedCount = RemoveMissingScriptsRecursive(Selection.activeGameObject);

        if (removedCount == 0)
            Debug.Log("FindMissingScriptsTool: No missing scripts removed.");
        else
            Debug.LogWarning($"FindMissingScriptsTool: Removed {removedCount} missing script reference(s).");
    }

    private static int ScanGameObjectRecursive(GameObject go, string sourceLabel)
    {
        int missingCount = 0;

        int localMissing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (localMissing > 0)
        {
            missingCount += localMissing;
            Debug.LogWarning($"{sourceLabel} -> Missing script on: {GetHierarchyPath(go)}", go);
        }

        foreach (Transform child in go.transform)
        {
            missingCount += ScanGameObjectRecursive(child.gameObject, sourceLabel);
        }

        return missingCount;
    }

    private static int RemoveMissingScriptsRecursive(GameObject go)
    {
        int removedCount = 0;

        int localMissingBefore = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (localMissingBefore > 0)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            removedCount += localMissingBefore;
            Debug.LogWarning($"Removed missing script(s) from: {GetHierarchyPath(go)}", go);
        }

        foreach (Transform child in go.transform)
        {
            removedCount += RemoveMissingScriptsRecursive(child.gameObject);
        }

        return removedCount;
    }

    private static List<GameObject> GetDontDestroyOnLoadRoots()
    {
        List<GameObject> roots = new List<GameObject>();
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject go in allObjects)
        {
            if (go == null)
                continue;

            if (EditorUtility.IsPersistent(go))
                continue;

            if (go.hideFlags != HideFlags.None)
                continue;

            if (go.scene.name != "DontDestroyOnLoad")
                continue;

            if (go.transform.parent != null)
                continue;

            roots.Add(go);
        }

        return roots;
    }

    private static string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        Transform current = go.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
#endif