using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        public event Action<float> OnLoadProgress;
        public event Action OnLoadComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Load(string sceneName, bool showLoading = true)
        {
            StartCoroutine(LoadAsync(sceneName, showLoading));
        }

        private IEnumerator LoadAsync(string sceneName, bool showLoading)
        {
            if (showLoading)
                EventBus.Raise(new SceneLoadStartedEvent { SceneName = sceneName });

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                OnLoadProgress?.Invoke(op.progress);
                yield return null;
            }

            OnLoadProgress?.Invoke(1f);
            op.allowSceneActivation = true;
            yield return op;

            OnLoadComplete?.Invoke();
            EventBus.Raise(new SceneLoadCompleteEvent { SceneName = sceneName });
        }
    }

    public struct SceneLoadStartedEvent  { public string SceneName; }
    public struct SceneLoadCompleteEvent { public string SceneName; }
}
