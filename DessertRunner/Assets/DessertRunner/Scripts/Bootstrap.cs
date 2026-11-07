using UnityEngine;

namespace DessertRunner
{
    /// <summary>Creates the game automatically in ANY scene when Play starts - no setup needed.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            #if UNITY_2023_1_OR_NEWER
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
#else
            if (Object.FindObjectOfType<GameManager>() != null) return;
#endif
            var go = new GameObject("DessertRunner_GameManager");
            go.AddComponent<GameManager>();
        }
    }
}
