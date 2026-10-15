using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatDefense
{
    /// <summary>Loads the packed sprite sheets (Resources/Game/Anim) and single sprites.</summary>
    public static class Art
    {
        [Serializable] class AnimInfo { public string key; public int w, h, cols, count; public float fps, px, py, ppu; }
        [Serializable] public class UnitInfo { public string key; public float cx, cy, r, top; }
        [Serializable] class AnimFile { public AnimInfo[] anims; public UnitInfo[] units; }

        static Dictionary<string, AnimInfo> infos;
        static Dictionary<string, UnitInfo> units;
        static readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, Sprite> singles = new Dictionary<string, Sprite>();
        static Font font;
        static Sprite white;
        static Material flashMat;

        static void Init()
        {
            if (infos != null) return;
            infos = new Dictionary<string, AnimInfo>();
            var f = JsonUtility.FromJson<AnimFile>(Resources.Load<TextAsset>("Game/anims").text);
            foreach (var a in f.anims) infos[a.key] = a;
            units = new Dictionary<string, UnitInfo>();
            foreach (var u in f.units) units[u.key] = u;
        }

        /// <summary>Body centre / radius (world units, relative to the feet pivot) of a packed character.</summary>
        public static UnitInfo Unit(string key)
        {
            Init();
            return units.TryGetValue(key, out var u) ? u : new UnitInfo { key = key, cy = 0.5f, r = 0.4f, top = 1f };
        }

        public static bool HasAnim(string key) { Init(); return infos.ContainsKey(key); }

        public static Sprite[] Anim(string key)
        {
            Init();
            if (frames.TryGetValue(key, out var cached)) return cached;
            if (!infos.TryGetValue(key, out var a)) { Debug.LogError("Missing anim " + key); return new Sprite[0]; }
            var tex = Resources.Load<Texture2D>("Game/Anim/" + key);
            var list = new Sprite[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int c = i % a.cols, r = i / a.cols;
                var rect = new Rect(c * a.w, tex.height - (r + 1) * a.h, a.w, a.h);
                list[i] = Sprite.Create(tex, rect, new Vector2(a.px, a.py), a.ppu, 0, SpriteMeshType.FullRect);
                list[i].name = key + "_" + i;
            }
            frames[key] = list;
            return list;
        }

        public static float Fps(string key) { Init(); return infos.TryGetValue(key, out var a) ? a.fps : 30f; }

        public static Sprite UI(string name) => Single("Game/UI/" + name);
        public static Sprite Bg(string name) => Single("Game/Bg/" + name);
        public static Sprite Fx(string name) => Single("Game/Fx/" + name);

        static Sprite Single(string path)
        {
            if (singles.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(path);
            if (s == null) Debug.LogError("Missing sprite " + path);
            singles[path] = s;
            return s;
        }

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("Game/Fonts/PassionOne-Regular");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>1x1 world-unit white square, used for bars and fills.</summary>
        public static Sprite White
        {
            get
            {
                if (white == null)
                    white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                return white;
            }
        }

        public static Material FlashMaterial
        {
            get
            {
                if (flashMat == null)
                {
                    var sh = Resources.Load<Shader>("Game/SpriteFlash");
                    flashMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                }
                return flashMat;
            }
        }
    }
}
