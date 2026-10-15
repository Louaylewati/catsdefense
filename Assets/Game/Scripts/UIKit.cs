using System;
using UnityEngine;
using UnityEngine.UI;

namespace CatDefense
{
    /// <summary>Small helpers to build uGUI from code with the kit's sprites (1280x720 reference canvas).</summary>
    public static class UIKit
    {
        // Kit art is authored for a ~2143 px wide screen; this maps native sprite size to the 1280x720 canvas.
        public const float K = 720f / 1063f;

        public static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size, Vector2? anchor = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var a = anchor ?? new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Image at the sprite's native size times scale (times K).</summary>
        public static Image Img(Transform parent, string sprite, Vector2 pos, float scale = 1f, Vector2? anchor = null)
        {
            var s = Art.UI(sprite);
            var size = s != null ? new Vector2(s.rect.width, s.rect.height) * K * scale : new Vector2(100, 100);
            var img = Rect(parent, sprite, pos, size, anchor).gameObject.AddComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            return img;
        }

        public static Image Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Vector2? anchor = null)
        {
            var img = Rect(parent, name, pos, size, anchor).gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Image Dim(Transform parent, float alpha = 0.65f)
        {
            var img = Stretch(parent, "Dim").gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, alpha);
            return img;
        }

        public static Text Label(Transform parent, string text, Vector2 pos, int size = 32, Color? color = null,
            TextAnchor align = TextAnchor.MiddleCenter, Vector2? box = null, Vector2? anchor = null, bool outline = true)
        {
            var rt = Rect(parent, "Text", pos, box ?? new Vector2(600, size * 1.6f), anchor);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Art.Font;
            t.fontSize = size;
            t.text = text;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.25f, 0.12f, 0.08f, 0.9f);
                o.effectDistance = new Vector2(2, -2);
            }
            return t;
        }

        public static Button Btn(Transform parent, string sprite, string label, Vector2 pos, Action onClick,
            float scale = 1f, string pressed = null, int fontSize = 30, Vector2? anchor = null)
        {
            var img = Img(parent, sprite, pos, scale, anchor);
            img.preserveAspect = false;
            var b = img.gameObject.AddComponent<Button>();
            if (pressed != null)
            {
                b.transition = Selectable.Transition.SpriteSwap;
                b.spriteState = new SpriteState { pressedSprite = Art.UI(pressed) };
            }
            else
            {
                var cb = b.colors;
                cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                cb.highlightedColor = new Color(1.05f, 1.05f, 1.05f);
                b.colors = cb;
            }
            b.onClick.AddListener(() => { Sfx.Play("click"); onClick?.Invoke(); });
            if (!string.IsNullOrEmpty(label))
                Label(img.transform, label, new Vector2(0, 2), fontSize, null, TextAnchor.MiddleCenter, img.rectTransform.sizeDelta);
            return b;
        }

        /// <summary>A horizontal fill bar: returns the fill image (Filled, horizontal).</summary>
        public static Image Bar(Transform parent, Vector2 pos, Vector2 size, Color fill, Vector2? anchor = null, string bg = "GemsBarBg")
        {
            var back = Rect(parent, "Bar", pos, size, anchor);
            var bimg = back.gameObject.AddComponent<Image>();
            bimg.sprite = Art.UI(bg);
            bimg.type = Image.Type.Simple;
            var f = Rect(back, "Fill", Vector2.zero, size - new Vector2(10, 10));
            var fimg = f.gameObject.AddComponent<Image>();
            fimg.sprite = Art.White;
            fimg.type = Image.Type.Filled;
            fimg.fillMethod = Image.FillMethod.Horizontal;
            fimg.fillOrigin = 0;
            fimg.color = fill;
            fimg.raycastTarget = false;
            return fimg;
        }

        /// <summary>Full-screen background art centred at its native aspect (height = 720).</summary>
        public static Image Background(Transform parent, string sprite)
        {
            var s = Art.UI(sprite);
            float w = s.rect.width * 720f / s.rect.height;
            var img = Rect(parent, "Bg", Vector2.zero, new Vector2(w, 720)).gameObject.AddComponent<Image>();
            img.sprite = s;
            return img;
        }
    }
}
