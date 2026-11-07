#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DessertRunner.EditorTools
{
    /// <summary>
    /// On opening the project: creates Assets/Scenes/DessertRunner.unity (once), opens it and enters Play mode.
    /// The game itself is built at runtime by DessertRunner.Bootstrap, so the scene can stay empty.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoRun
    {
        const string ScenePath = "Assets/Scenes/DessertRunner.unity";
        const string Flag = "DessertRunner_AutoRan";

        static AutoRun()
        {
            if (SessionState.GetBool(Flag, false)) return;
            SessionState.SetBool(Flag, true);
            EditorApplication.delayCall += () => { EnsureScene(); if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true; };
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
                var sc = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(sc, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            }
            else if (EditorSceneManager.GetActiveScene().path != ScenePath && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        [MenuItem("Dessert Runner/Play Game")]
        static void PlayGame() { EnsureScene(); EditorApplication.isPlaying = true; }

        [MenuItem("Dessert Runner/Create Scene")]
        static void CreateScene() { EnsureScene(); }
    }
}
#endif
