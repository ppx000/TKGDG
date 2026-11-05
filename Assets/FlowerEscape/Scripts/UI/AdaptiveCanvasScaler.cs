using UnityEngine;
using UnityEngine.UI;

namespace FlowerEscape.UI
{
    // Keeps the whole reference frame on screen on any aspect ratio:
    // taller than the reference (e.g. 9:21) locks width, so the extra height becomes free space;
    // wider than the reference (e.g. 3:4) locks height, so the extra width becomes side margins.
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public class AdaptiveCanvasScaler : MonoBehaviour
    {
        Canvas _canvas;
        CanvasScaler _scaler;

        void OnEnable()
        {
            _canvas = GetComponent<Canvas>();
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        void Update()
        {
            Apply();
        }

        void Apply()
        {
            var screen = _canvas.renderingDisplaySize;
            if (screen.x <= 0f || screen.y <= 0f)
                return;

            var reference = _scaler.referenceResolution;
            float match = screen.x / screen.y < reference.x / reference.y ? 0f : 1f;
            if (!Mathf.Approximately(_scaler.matchWidthOrHeight, match))
                _scaler.matchWidthOrHeight = match;
        }
    }
}
