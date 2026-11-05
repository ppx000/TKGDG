using TMPro;
using UnityEngine;

namespace FlowerEscape.UI
{
    // Figma-style outside stroke for a TextMeshPro label.
    // TMP outlines are drawn glyph by glyph, so with tight letter spacing one glyph's outline
    // covers the face of its neighbour. Instead this object (the label's previous sibling)
    // renders the same text with a dilated face in the stroke colour, right behind the label,
    // and keeps mirroring the label's text and layout. Change the label; the stroke follows.
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public class TextStroke : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        TMP_Text _stroke;
        RectTransform _rect;

        public TMP_Text Label => label;

        void OnEnable()
        {
            _stroke = GetComponent<TMP_Text>();
            _rect = (RectTransform)transform;
            Sync();
        }

        void LateUpdate()
        {
            Sync();
        }

        void Sync()
        {
            if (label == null)
                return;

            bool visible = label.isActiveAndEnabled;
            if (_stroke.enabled != visible)
                _stroke.enabled = visible;
            if (!visible)
                return;

            if (_stroke.text != label.text)
                _stroke.text = label.text;
            if (!Mathf.Approximately(_stroke.fontSize, label.fontSize))
                _stroke.fontSize = label.fontSize;
            if (_stroke.alignment != label.alignment)
                _stroke.alignment = label.alignment;

            var color = _stroke.color;
            if (!Mathf.Approximately(color.a, label.color.a))
            {
                color.a = label.color.a;
                _stroke.color = color;
            }

            var source = label.rectTransform;
            if (_rect.anchoredPosition != source.anchoredPosition || _rect.sizeDelta != source.sizeDelta
                || _rect.anchorMin != source.anchorMin || _rect.anchorMax != source.anchorMax)
            {
                _rect.anchorMin = source.anchorMin;
                _rect.anchorMax = source.anchorMax;
                _rect.pivot = source.pivot;
                _rect.sizeDelta = source.sizeDelta;
                _rect.anchoredPosition = source.anchoredPosition;
            }
        }
    }
}
