using UnityEngine;
using UnityEngine.UI;

namespace Pentomino.View
{
    /// <summary>ナビゲーションの文字とボタンを組み立てる小道具。</summary>
    public static class UiFactory
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        /// <summary>
        /// ボタンの背景に使うスプライト。9 スライス済みのものを想定している。
        ///
        /// Unity 組み込みのスプライトはエディタ専用リソースで、実行時にも
        /// バッチ実行時にも Resources からは取り出せない。そのため取得はエディタ側
        /// （UiPrefabBuilder）で行い、ここには結果だけを預けてもらう。
        /// プレハブに焼き込まれた参照は実行時にもそのまま使える。
        /// </summary>
        public static Sprite DefaultButtonSprite { get; set; }

        /// <summary>ボタンの背景にスプライトを当てる。9 スライスで伸ばす。</summary>
        public static void ApplyButtonSprite(Image image)
        {
            if (image == null || DefaultButtonSprite == null) return;

            image.sprite = DefaultButtonSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return rect;
        }

        public static Text CreateLabel(Transform parent, string name, string content, int fontSize,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            var text = go.GetComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = new Color(0.12f, 0.12f, 0.12f);
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string content, int fontSize,
            Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.94f, 0.94f, 0.94f);
            ApplyButtonSprite(image);

            var label = CreateLabel(go.transform, "Label", content, fontSize, TextAnchor.MiddleCenter);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }
    }
}
