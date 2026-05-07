using UnityEngine;

namespace Core
{
    [DefaultExecutionOrder(-1000)]
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private string _firstScene = "Combat";

        private void Awake()
        {
            EventBus.Clear();
            EnsureSingleton<GameManager>("GameManager");
            EnsureSingleton<SceneLoader>("SceneLoader");
        }

        private void Start()
        {
            GameManager.Instance.SetState(GameState.Boot);
            SceneLoader.Instance.Load(_firstScene);
        }

        private static void EnsureSingleton<T>(string goName) where T : MonoBehaviour
        {
            if (Object.FindAnyObjectByType<T>() == null)
                new GameObject(goName).AddComponent<T>();
        }
    }
}
