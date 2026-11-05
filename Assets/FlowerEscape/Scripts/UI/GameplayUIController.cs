using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FlowerEscape.UI
{
    public enum GameplayState
    {
        Combat,
        BuyGear
    }

    // Switches the gameplay screen between its two layouts:
    // BuyGear (arrange and buy gear between waves) and Combat (wave running).
    // The HUD and the banner are shared and stay visible in both states.
    public class GameplayUIController : MonoBehaviour
    {
        [SerializeField] GameplayState initialState = GameplayState.BuyGear;
        [SerializeField] CanvasGroup combatRoot;
        [SerializeField] CanvasGroup buyGearRoot;
        [SerializeField] Button startButton;
        [SerializeField, Tooltip("Debug shortcut that flips the state in Play mode.")]
        KeyCode toggleKey = KeyCode.Tab;
        [SerializeField, Min(0f)] float fadeDuration = 0.2f;

        Coroutine _fade;

        public GameplayState State { get; private set; }
        public CanvasGroup CombatRoot => combatRoot;
        public CanvasGroup BuyGearRoot => buyGearRoot;

        public event Action<GameplayState> StateChanged;

        void Awake()
        {
            if (startButton != null)
                startButton.onClick.AddListener(ShowCombat);
        }

        void Start()
        {
            SetState(initialState, instant: true);
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                SetState(State == GameplayState.Combat ? GameplayState.BuyGear : GameplayState.Combat);
        }

        public void ShowCombat() => SetState(GameplayState.Combat);

        public void ShowBuyGear() => SetState(GameplayState.BuyGear);

        public void SetState(GameplayState state, bool instant = false)
        {
            State = state;
            ApplyStateImmediate(state);

            if (_fade != null)
                StopCoroutine(_fade);
            if (!instant && fadeDuration > 0f)
                _fade = StartCoroutine(FadeIn(RootFor(state)));

            StateChanged?.Invoke(state);
        }

        // Shows one layout and hides the other without animation or events.
        // Also used by the editor tools to preview a state in Edit mode.
        public void ApplyStateImmediate(GameplayState state)
        {
            combatRoot.gameObject.SetActive(state == GameplayState.Combat);
            buyGearRoot.gameObject.SetActive(state == GameplayState.BuyGear);
            RootFor(state).alpha = 1f;
        }

        CanvasGroup RootFor(GameplayState state) => state == GameplayState.Combat ? combatRoot : buyGearRoot;

        IEnumerator FadeIn(CanvasGroup group)
        {
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / fadeDuration;
                yield return null;
            }
            group.alpha = 1f;
            _fade = null;
        }
    }
}
