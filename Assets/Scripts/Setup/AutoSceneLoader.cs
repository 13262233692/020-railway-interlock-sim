using UnityEngine;

namespace RailwayInterlock.Setup
{
    public static class AutoSceneLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            var gm = Object.FindObjectOfType<Management.GameManager>();
            var bootstrapper = Object.FindObjectOfType<SceneBootstrapper>();

            if (gm == null && bootstrapper == null)
            {
                Debug.Log("[AutoSceneLoader] 检测到空场景，自动创建引导启动器...");
                GameObject boot = new GameObject("AutoBootstrapper");
                boot.AddComponent<SceneBootstrapper>();
            }
        }
    }
}
