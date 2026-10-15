using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatDefense
{
    /// <summary>
    /// Entry point + screen flow:
    /// Title -> Cat select (once) -> Level select -> Battle -> You Win -> Upgrades (weapon + boosters) -> next level ... -> Game complete.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        Canvas canvas;
        RectTransform screen;
        Battle battle;

        // boosters bought on the booster screen apply to the next battle (and its retries)
        readonly HashSet<string> boosts = new HashSet<string>();
        int boostsForLevel;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            GameData.Load();
            Sfx.Init(gameObject);
            if (Camera.main == null)
            {
                var c = new GameObject("Main Camera").AddComponent<Camera>();
                c.tag = "MainCamera";
                c.orthographic = true;
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = Color.black;
                c.transform.position = new Vector3(0, 0, -10);
            }
            Camera.main.orthographicSize = 5.31f;
            BuildCanvas();
        }

        void Start() => ShowTitle();

        void BuildCanvas()
        {
            var go = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var cs = go.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1280, 720);
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight = 1f;

            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);
        }

        RectTransform NewScreen()
        {
            Time.timeScale = 1f;
            if (battle != null) Destroy(battle.gameObject);
            battle = null;
            if (screen != null) Destroy(screen.gameObject);
            screen = UIKit.Stretch(canvas.transform, "Screen");
            return screen;
        }

        // ---------------- Title ----------------

        public void ShowTitle()
        {
            var r = NewScreen();
            Sfx.Music("music_menu");
            UIKit.Background(r, "LandingScreen");
            var tap = UIKit.Stretch(r, "Tap").gameObject.AddComponent<Button>();
            tap.gameObject.AddComponent<Image>().color = Color.clear;
            tap.onClick.AddListener(() =>
            {
                Sfx.Play("click");
                if (GameData.Cat(Save.Cat) == null) ShowCatSelect();
                else ShowLevelSelect();
            });
            UIKit.Img(r, "Logo", new Vector2(0, 60), 0.95f).raycastTarget = false;
            UIKit.Label(r, "Tap To Play", new Vector2(0, -215), 44).gameObject.AddComponent<Pulse>();
            UIKit.Btn(r, "SettingBtn", null, new Vector2(-60, 60), () => ShowSettings(r), 0.9f, null, 30, new Vector2(1, 0));
        }

        void ShowSettings(RectTransform parent)
        {
            var p = UIKit.Stretch(parent, "Settings");
            UIKit.Dim(p);
            p.gameObject.AddComponent<PopIn>();
            UIKit.Label(p, "SETTINGS", new Vector2(0, 150), 52);
            SettingsRow(p, new Vector2(0, 50));
            UIKit.Btn(p, "BtnOrange", "Reset progress", new Vector2(-130, -90), () =>
            {
                Save.ResetProgress();
                boosts.Clear();
                ShowTitle();
            }, 0.7f, "BtnOrangePressed", 26);
            UIKit.Btn(p, "BtnGreen", "Close", new Vector2(130, -90), () => Destroy(p.gameObject), 0.7f, "BtnGreenPressed", 30);
        }

        /// <summary>The kit's paused-bar with music / sound / vibration toggles.</summary>
        public static void SettingsRow(RectTransform parent, Vector2 pos)
        {
            UIKit.Img(parent, "BgPaused", pos, 1.05f);
            Toggle(parent, pos + new Vector2(-150, 0), "BtnMusic", "BtnMusicOff", () => Save.Music, v => { Save.Music = v; Sfx.RefreshMusic(); });
            Toggle(parent, pos, "BtnSound", "BtnSoundOff", () => Save.Sound, v => Save.Sound = v);
            Toggle(parent, pos + new Vector2(150, 0), "BtnVibra", "BtnVibraOff", () => Save.Vibration, v => Save.Vibration = v);
        }

        static void Toggle(RectTransform parent, Vector2 pos, string on, string off, System.Func<bool> get, System.Action<bool> set)
        {
            Button b = null;
            b = UIKit.Btn(parent, get() ? on : off, null, pos, () =>
            {
                set(!get());
                b.image.sprite = Art.UI(get() ? on : off);
            }, 0.62f);
        }

        // ---------------- Cat select ----------------

        public void ShowCatSelect()
        {
            var r = NewScreen();
            Sfx.Music("music_menu");
            UIKit.Background(r, "CatUpgradeScreen");
            SideTab(r, "Icon_home", 150, ShowTitle);
            Ribbon(r, "Choose Your Cat");

            string selected = GameData.Cat(Save.Cat) != null ? Save.Cat : GameData.Cats[0].id;
            var cards = new Dictionary<string, Image>();
            var preview = UIKit.Rect(r, "Preview", new Vector2(330, -30), new Vector2(380, 420));

            void Select(CatDef c)
            {
                selected = c.id;
                foreach (var kv in cards) kv.Value.enabled = kv.Key == c.id;
                for (int i = preview.childCount - 1; i >= 0; i--) Destroy(preview.GetChild(i).gameObject);

                var a = Art.Anim(c.sprite + "_idle");
                var size = new Vector2(a[0].rect.width, a[0].rect.height) * 1.65f;
                var animImg = UIKit.Rect(preview, "Cat", new Vector2(0, 118), size).gameObject.AddComponent<Image>();
                animImg.preserveAspect = true;
                animImg.raycastTarget = false;
                var sa = animImg.gameObject.AddComponent<SpriteAnim>();
                sa.Unscaled = true;
                sa.Play(c.sprite + "_idle", true);

                UIKit.Label(preview, c.name, new Vector2(0, 2), 44, new Color(1f, 0.85f, 0.3f));
                UIKit.Label(preview, c.role, new Vector2(0, -34), 24);
                StatRow(preview, "Damage", c.barDamage, -70);
                StatRow(preview, "Speed", c.barSpeed, -102);
                StatRow(preview, "Range", c.barRange, -134);
                UIKit.Label(preview, "Special: " + c.specialName + " - " + c.specialDesc, new Vector2(0, -166), 18, new Color(1f, 0.95f, 0.8f));
            }

            for (int i = 0; i < GameData.Cats.Length; i++)
            {
                var c = GameData.Cats[i];
                var pos = new Vector2(-235 + (i % 2) * 240, 70 - (i / 2) * 170);
                var card = UIKit.Box(r, "Card " + c.name, pos, new Vector2(220, 150), new Color(0.36f, 0.41f, 0.36f));
                var btn = card.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => { Sfx.Play("click"); Select(c); });
                var border = UIKit.Rect(card.transform, "Selected", Vector2.zero, new Vector2(236, 166)).gameObject.AddComponent<Image>();
                border.sprite = Art.UI("SelectedBorder");
                border.raycastTarget = false;
                border.enabled = false;
                cards[c.id] = border;
                var idle = Art.Anim(c.sprite + "_idle")[0];
                var pic = UIKit.Rect(card.transform, "Pic", new Vector2(0, 18), new Vector2(idle.rect.width, idle.rect.height) * 0.85f).gameObject.AddComponent<Image>();
                pic.sprite = idle;
                pic.preserveAspect = true;
                pic.raycastTarget = false;
                UIKit.Label(card.transform, c.name, new Vector2(0, -52), 28);
            }

            UIKit.Btn(r, "BtnGreen", "Select", new Vector2(330, -232), () =>
            {
                Save.Cat = selected;
                ShowLevelSelect();
            }, 0.6f, "BtnGreenPressed", 32);
            UIKit.Label(r, "You keep this cat for the whole run", new Vector2(-115, -225), 20, new Color(1, 1, 1, 0.8f));

            Select(GameData.Cat(selected));
        }

        static void StatRow(RectTransform parent, string label, int value, float y)
        {
            UIKit.Label(parent, label, new Vector2(-90, y), 22, null, TextAnchor.MiddleRight, new Vector2(150, 30));
            UIKit.Img(parent, Mathf.Clamp(value, 0, 5) + "Bar", new Vector2(60, y), 0.8f);
        }

        // ---------------- Level select ----------------

        public void ShowLevelSelect()
        {
            if (GameData.Cat(Save.Cat) == null) { ShowCatSelect(); return; }
            var r = NewScreen();
            Sfx.Music("music_menu");
            UIKit.Background(r, "LevelMap_Bg_Screen");
            SideTab(r, "Icon_home", 150, ShowTitle);
            SideTab(r, "Icon_Addon", 40, ShowUpgrades);
            Ribbon(r, "Level Select");
            CoinCounter(r, new Vector2(-260, 245));

            var cat = GameData.Cat(Save.Cat);
            var catIcon = Art.Anim(cat.sprite + "_idle")[0];
            var ci = UIKit.Rect(r, "YourCat", new Vector2(-545, -30), new Vector2(catIcon.rect.width, catIcon.rect.height) * 1.1f).gameObject.AddComponent<Image>();
            ci.sprite = catIcon;
            ci.preserveAspect = true;
            UIKit.Label(r, cat.name, new Vector2(-545, -110), 26);

            var map = UIKit.Img(r, "LevelMapPart", new Vector2(95, -40), 0.79f);
            map.raycastTarget = false;
            var s = map.sprite.rect.size;
            var size = map.rectTransform.sizeDelta;
            Vector2 MapPos(float nx, float ny) => new Vector2((nx / s.x - 0.5f) * size.x, (0.5f - ny / s.y) * size.y);
            var nodes = new[] { new Vector2(420, 560), new Vector2(1100, 560), new Vector2(1480, 230) };

            int unlocked = Save.Unlocked;
            for (int i = 0; i < GameData.Levels.Length; i++)
            {
                var lv = GameData.Levels[i];
                var np = nodes[Mathf.Min(i, nodes.Length - 1)] + (i >= nodes.Length ? new Vector2(150 * (i - nodes.Length + 1), 0) : Vector2.zero);
                bool locked = lv.id > unlocked;
                bool done = lv.id < unlocked;
                string sprite = locked ? "LockedLevel" : done ? "GreenLevel" : "OrangeLvl";
                var b = UIKit.Btn(map.transform, sprite, locked ? null : lv.id.ToString(), MapPos(np.x, np.y) + new Vector2(0, 40), () =>
                {
                    if (!locked) StartLevel(lv.id);
                }, 0.8f, null, 40);
                b.interactable = !locked;
                if (locked) b.transition = Selectable.Transition.None;
                if (!locked) b.gameObject.AddComponent<Pulse>().Amount = done ? 0f : 0.05f;
                UIKit.Label(map.transform, lv.name, MapPos(np.x, np.y) + new Vector2(0, -35), 24);
            }
            if (unlocked > GameData.Levels.Length)
                UIKit.Label(r, "All levels cleared! Replay any level.", new Vector2(95, -275), 24, new Color(1f, 0.9f, 0.5f));
        }

        // ---------------- Upgrades (weapon + boosters) ----------------

        public void ShowUpgrades()
        {
            var r = NewScreen();
            Sfx.Music("music_menu");
            UIKit.Background(r, "AddonScreen");
            SideTab(r, "Icon_home", 150, ShowLevelSelect);
            Ribbon(r, "Upgrades", 190);
            var coins = CoinCounter(r, new Vector2(-170, 245));
            int next = Mathf.Min(Save.Unlocked, GameData.Levels.Length);
            if (boostsForLevel != next) { boosts.Clear(); boostsForLevel = next; }
            var refreshers = new List<System.Action>();
            void RefreshAll()
            {
                coins.text = Save.Coins.ToString();
                foreach (var rf in refreshers) rf();
            }

            var cat = GameData.Cat(Save.Cat);
            UIKit.Label(r, "Weapon - kept for the whole run", new Vector2(190, 172), 24, new Color(1f, 0.92f, 0.7f));
            refreshers.Add(WeaponRow(r, new Vector2(190, 128), cat, () => Save.Coins, cost =>
            {
                if (Save.Coins < cost) return false;
                Save.Coins -= cost;
                return true;
            }, RefreshAll));

            UIKit.Label(r, "Boosters - Level " + next + " only", new Vector2(190, 76), 24, new Color(1f, 0.92f, 0.7f));
            for (int i = 0; i < GameData.Boosters.Length; i++)
            {
                var bd = GameData.Boosters[i];
                ShopRowUI row = null;
                row = ShopRow(r, new Vector2(190, 32 - i * 84), bd.icon, () =>
                {
                    if (boosts.Contains(bd.id) || Save.Coins < bd.cost) return;
                    Save.Coins -= bd.cost;
                    boosts.Add(bd.id);
                    Sfx.Play("coin");
                    RefreshAll();
                });
                row.Name.text = bd.name;
                row.Desc.text = bd.desc;
                refreshers.Add(() =>
                {
                    bool owned = boosts.Contains(bd.id);
                    row.Border.enabled = owned;
                    row.Buy.image.sprite = Art.UI(owned || Save.Coins < bd.cost ? "BtnShopDisable" : "BtnShopYellow");
                    row.Price.text = owned ? "Ready!" : bd.cost + " coins";
                });
            }
            RefreshAll();

            UIKit.Btn(r, "BtnGreen", "Play Level " + next, new Vector2(190, -248), () => StartLevel(next), 0.6f, "BtnGreenPressed", 28);
        }

        public class ShopRowUI { public Image Border; public Text Name, Desc, Price; public Button Buy; }

        /// <summary>One shop line: icon, name, description and a buy button.</summary>
        public static ShopRowUI ShopRow(RectTransform parent, Vector2 pos, string icon, System.Action onBuy)
        {
            var ui = new ShopRowUI();
            var row = UIKit.Box(parent, "Row", pos, new Vector2(640, 78), new Color(0.33f, 0.38f, 0.33f));
            ui.Border = UIKit.Rect(row.transform, "Owned", Vector2.zero, new Vector2(656, 94)).gameObject.AddComponent<Image>();
            ui.Border.sprite = Art.UI("YellowBorderAddon");
            ui.Border.raycastTarget = false;
            ui.Border.enabled = false;
            UIKit.Img(row.transform, icon, new Vector2(-265, 0), 0.7f).raycastTarget = false;
            ui.Name = UIKit.Label(row.transform, "", new Vector2(-20, 15), 28, null, TextAnchor.MiddleLeft, new Vector2(420, 34));
            ui.Desc = UIKit.Label(row.transform, "", new Vector2(-20, -17), 20, new Color(1f, 0.92f, 0.75f), TextAnchor.MiddleLeft, new Vector2(420, 28));
            ui.Buy = UIKit.Btn(row.transform, "BtnShopYellow", null, new Vector2(240, 0), onBuy, 0.8f);
            ui.Price = UIKit.Label(ui.Buy.transform, "", Vector2.zero, 21, null, TextAnchor.MiddleCenter, ((RectTransform)ui.Buy.transform).sizeDelta);
            return ui;
        }

        /// <summary>
        /// The weapon upgrade line (damage, fire rate and magazine all go up together).
        /// spend(cost) takes the coins if there are enough; bought runs after a level-up. Returns its refresh action.
        /// </summary>
        public static System.Action WeaponRow(RectTransform parent, Vector2 pos, CatDef cat, System.Func<int> coins, System.Func<int, bool> spend, System.Action bought)
        {
            var levels = GameData.WeaponLevels;
            ShopRowUI ui = null;
            void Refresh()
            {
                int lv = Mathf.Clamp(Save.WeaponLevel, 0, levels.Length - 1);
                var cur = levels[lv];
                bool max = lv >= levels.Length - 1;
                ui.Name.text = cat.name + "'s weapon  Lv " + (lv + 1) + "/" + levels.Length;
                ui.Border.enabled = max;
                if (max)
                {
                    ui.Desc.text = "Damage " + Stat(cat.damage * cur.damage) + "  -  Rate " + Stat(cat.fireRate * cur.fireRate) + "/s  -  Mag " + Mag(cat, cur) + "  (max)";
                    ui.Price.text = "MAX";
                    ui.Buy.image.sprite = Art.UI("BtnShopDisable");
                    return;
                }
                var nx = levels[lv + 1];
                ui.Desc.text = "Damage " + Stat(cat.damage * cur.damage) + ">" + Stat(cat.damage * nx.damage)
                    + "  -  Rate " + Stat(cat.fireRate * cur.fireRate) + ">" + Stat(cat.fireRate * nx.fireRate)
                    + "/s  -  Mag " + Mag(cat, cur) + ">" + Mag(cat, nx);
                ui.Price.text = nx.cost + " coins";
                ui.Buy.image.sprite = Art.UI(coins() >= nx.cost ? "BtnShopYellow" : "BtnShopDisable");
            }
            ui = ShopRow(parent, pos, "Up3", () =>
            {
                int lv = Save.WeaponLevel;
                if (lv >= levels.Length - 1 || !spend(levels[lv + 1].cost)) return;
                Save.WeaponLevel = lv + 1;
                Sfx.Play("coin");
                Sfx.Play("special");
                bought?.Invoke();
                Refresh();
            });
            Refresh();
            return Refresh;
        }

        static string Stat(float v) => v.ToString(v >= 10 ? "0" : "0.#");
        static int Mag(CatDef c, WeaponLevel w) => Mathf.Max(1, Mathf.RoundToInt(c.magazine * w.magazine));

        // ---------------- Battle ----------------

        public void StartLevel(int id)
        {
            LevelDef lv = null;
            foreach (var l in GameData.Levels) if (l.id == id) lv = l;
            if (lv == null) { ShowLevelSelect(); return; }
            var r = NewScreen();
            battle = new GameObject("Battle").AddComponent<Battle>();
            var active = boostsForLevel == id ? new HashSet<string>(boosts) : new HashSet<string>();
            battle.Begin(lv, GameData.Cat(Save.Cat), active, r);
        }

        // ---------------- Game complete ----------------

        public void ShowComplete()
        {
            var r = NewScreen();
            boosts.Clear();
            Sfx.Music("music_menu");
            UIKit.Background(r, "LandingScreen");
            UIKit.Dim(r, 0.55f);
            UIKit.Img(r, "Logo", new Vector2(0, 140), 0.7f);
            UIKit.Label(r, "YOU SAVED THE STREET!", new Vector2(0, -40), 60, new Color(1f, 0.85f, 0.3f));
            UIKit.Label(r, "The " + GameData.Enemies[GameData.Boss.enemy].name + " is down and the hostage cat is free. Thanks for playing!", new Vector2(0, -100), 28);
            CoinCounter(r, new Vector2(0, -160));
            UIKit.Btn(r, "BtnGreen", "Level Select", new Vector2(-150, -255), ShowLevelSelect, 0.6f, "BtnGreenPressed", 28);
            UIKit.Btn(r, "BtnOrange", "Title", new Vector2(150, -255), ShowTitle, 0.6f, "BtnOrangePressed", 28);
        }

        // ---------------- shared bits ----------------

        static void Ribbon(RectTransform r, string title, float x = 95)
        {
            UIKit.Img(r, "TitleRibbon", new Vector2(x, 238), 1.0f).raycastTarget = false;
            UIKit.Label(r, title, new Vector2(x, 246), 32);
        }

        static void SideTab(RectTransform r, string icon, float y, System.Action onClick)
        {
            var b = UIKit.Btn(r, "BtnTab_Active", null, new Vector2(-520, y), onClick, 0.85f);
            UIKit.Img(b.transform, icon, Vector2.zero, 0.85f).raycastTarget = false;
        }

        static Text CoinCounter(RectTransform r, Vector2 pos)
        {
            UIKit.Img(r, "CoinBar", pos + new Vector2(20, 0), 1.5f);
            UIKit.Img(r, "CoinIcon", pos + new Vector2(-48, 0), 1.4f);
            return UIKit.Label(r, Save.Coins.ToString(), pos + new Vector2(32, 0), 28);
        }
    }

    /// <summary>Scale pop on appear (unscaled time, works while paused).</summary>
    public class PopIn : MonoBehaviour
    {
        float t;
        void OnEnable() { t = 0; transform.localScale = Vector3.one * 0.7f; }
        void Update()
        {
            if (t >= 1) return;
            t = Mathf.Min(1, t + Time.unscaledDeltaTime / 0.22f);
            // ease from 70% up to a slight overshoot, settling at 100%
            float overshoot = t < 1 ? 1 + 0.12f * Mathf.Sin(t * Mathf.PI) : 1;
            transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, t) * overshoot;
        }
    }

    /// <summary>Gentle breathing scale for call-to-action elements.</summary>
    public class Pulse : MonoBehaviour
    {
        public float Amount = 0.06f, Speed = 3f;
        void Update() => transform.localScale = Vector3.one * (1 + Amount * Mathf.Sin(Time.unscaledTime * Speed));
    }
}
