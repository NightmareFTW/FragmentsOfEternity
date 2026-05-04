using UnityEngine;

namespace Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private GameState _currentState = GameState.Boot;
        public GameState CurrentState => _currentState;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetState(GameState newState)
        {
            _currentState = newState;
            EventBus.Raise(new GameStateChangedEvent { State = newState });
        }
    }

    public enum GameState { Boot, MainMenu, Combat, Results }

    public struct GameStateChangedEvent { public GameState State; }
}
