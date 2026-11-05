using UnityEngine;

namespace FlowerEscape.UI
{
    // Fits this RectTransform to Screen.safeArea (notch, rounded corners, home indicator).
    // The Figma frame already leaves a status-bar strip at the top, so only the part of the
    // top inset that is taller than that strip pushes the layout down.
    // Runtime only: in edit mode Screen.* reports whichever editor view is repainting.
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Height of the status-bar strip the design already keeps free, in canvas units (Figma points).")]
        float designTopInset = 40f;

        RectTransform _rect;
        Canvas _canvas;
        Rect _lastSafeArea;
        Vector2Int _lastScreenSize;
        float _lastScaleFactor;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
        }

        void OnEnable()
        {
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _lastSafeArea
                || Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y
                || !Mathf.Approximately(_canvas.scaleFactor, _lastScaleFactor))
                Apply();
        }

        void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastScaleFactor = _canvas.scaleFactor;
            if (Screen.width <= 0 || Screen.height <= 0 || _lastScaleFactor <= 0f)
                return;

            float left = _lastSafeArea.xMin / _lastScaleFactor;
            float right = (Screen.width - _lastSafeArea.xMax) / _lastScaleFactor;
            float bottom = _lastSafeArea.yMin / _lastScaleFactor;
            float top = (Screen.height - _lastSafeArea.yMax) / _lastScaleFactor;

            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.one;
            _rect.offsetMin = new Vector2(left, bottom);
            _rect.offsetMax = new Vector2(-right, -Mathf.Max(0f, top - designTopInset));
        }
    }
}
