using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatDefense
{
    /// <summary>One level: world, wave spawner, barricade, player cat, hostage, HUD and end-of-level pop-ups.</summary>
    public class Battle : MonoBehaviour
    {
        enum State { Playing, Won, Lost }

        // Positions measured on the kit backgrounds (2143x1062 at 100 px per unit, centred on 0,0).
        public const float BarricadeFront = -3.1f;
        static readonly Vector3 BarricadePos = new Vector3(-4.34f, 0.36f, 0);
        static readonly Vector3 CatPos = new Vector3(BarricadeFront + 1.4f, -0.45f, 0);
        public const float LaneTop = 1.7f, LaneBottom = -3.1f;
        const float GaugeX = -3.905f, GaugeBottom = -1.44f, GaugeHeight = 3.95f, GaugeWidth = 0.17f;

        public List<Enemy> Enemies { get; } = new List<Enemy>();
        public bool Playing => state == State.Playing;
        public bool Paused { get; private set; }
        public float DamageMul { get; private set; } = 1f;
        public float FireMul { get; private set; } = 1f;
        public float BarricadeHp => barricadeHp;
        public CatShooter Cat => shooter;
        public Hostage Hostage => hostage;
        public float VisibleRight => cam.orthographicSize * cam.aspect;

        LevelDef level;
        CatDef cat;
        HashSet<string> boosts;
        State state;
        Coroutine run;
        Camera cam;
        float spawnX;

        Transform world;
        SpriteRenderer barricadeSr, gaugeSr;
        MaterialPropertyBlock mpb;
        float barricadeHp, barricadeMax, barricadeFlash, barricadeShake;
        CatShooter shooter;
        Enemy boss;
        Hostage hostage;

        // coins and weapon level when the level started: a loss (or quitting) goes back to these
        int startCoins, startWeapon;

        int coinsEarned, waveIndex, waveTotal, waveLeft;
        float shakeT, shakeAmp;

        // HUD
        RectTransform hud, popup;
        Text coinText, waveText, hpText, specialText, catHpText, ammoText, helpText, breakText;
        Image waveFill, hpFill, bossFill, specialCd, catHpFill, reloadFill, hostageFill;
        GameObject waveGroup, bossGroup, hostageRow, breakGroup;
        bool upgradeAsked;

        public void Begin(LevelDef def, CatDef catDef, HashSet<string> activeBoosts, RectTransform screen)
        {
            level = def;
            cat = catDef;
            boosts = activeBoosts;
            Time.timeScale = 1f;
            DamageMul = boosts.Contains("sharp") ? 1.25f : 1f;
            FireMul = boosts.Contains("quick") ? 1.25f : 1f;
            barricadeMax = GameData.BarricadeHp + (boosts.Contains("reinforce") ? 100 : 0);
            barricadeHp = barricadeMax;
            mpb = new MaterialPropertyBlock();
            startCoins = Save.Coins;
            startWeapon = Save.WeaponLevel;

            cam = Camera.main;
            cam.orthographicSize = 5.31f;
            cam.transform.position = new Vector3(0, 0, -10);
            spawnX = cam.orthographicSize * cam.aspect + 1.3f;

            BuildWorld();
            BuildHud(screen);
            Sfx.Music("music_battle");
            run = StartCoroutine(Run());
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (cam != null) cam.transform.position = new Vector3(0, 0, -10);
        }

        // ---------------- world ----------------

        void BuildWorld()
        {
            world = new GameObject("World").transform;
            world.SetParent(transform, false);

            var bg = new GameObject("Background").AddComponent<SpriteRenderer>();
            bg.transform.SetParent(world, false);
            bg.sprite = Art.Bg(level.background);
            bg.sortingOrder = -100;

            barricadeSr = new GameObject("Barricade").AddComponent<SpriteRenderer>();
            barricadeSr.transform.SetParent(world, false);
            barricadeSr.transform.position = BarricadePos;
            barricadeSr.sprite = Art.Bg(level.barricade);
            barricadeSr.sharedMaterial = Art.FlashMaterial;
            barricadeSr.sortingOrder = 0;

            // the barricade's own gauge (drawn into the kit art) shows its HP
            gaugeSr = new GameObject("Gauge").AddComponent<SpriteRenderer>();
            gaugeSr.transform.SetParent(barricadeSr.transform, false);
            gaugeSr.sprite = Art.White;
            gaugeSr.sortingOrder = 1;
            UpdateGauge();

            var c = new GameObject("Cat");
            c.transform.SetParent(world, false);
            shooter = c.AddComponent<CatShooter>();
            shooter.Init(this, cat, CatPos);
        }

        void UpdateGauge()
        {
            float f = Mathf.Clamp01(barricadeHp / barricadeMax);
            var t = gaugeSr.transform;
            t.localScale = new Vector3(GaugeWidth, Mathf.Max(0.001f, GaugeHeight * f), 1);
            // local to the barricade, so the gauge shakes with it
            t.localPosition = new Vector3(GaugeX, GaugeBottom + GaugeHeight * f * 0.5f, 0) - BarricadePos;
            gaugeSr.color = HpColor(f);
        }

        static Color HpColor(float f) =>
            f < 0.25f ? new Color(0.93f, 0.25f, 0.2f) : f < 0.5f ? new Color(1f, 0.6f, 0.15f) : new Color(0.55f, 0.85f, 0.2f);

        // ---------------- waves ----------------

        IEnumerator Run()
        {
            for (int w = 0; w < level.TotalWaves; w++)
            {
                waveIndex = w;
                bool bossWave = level.bossWave && w == level.waves.Length;
                string sub = w == 0 ? "Level " + level.id + ": " + level.name : null;
                yield return Banner(bossWave ? "BOSS WAVE!" : "WAVE " + (w + 1), sub, GameData.WaveBanner);

                if (bossWave)
                {
                    yield return BossWave();
                }
                else
                {
                    int count = level.waves[w];
                    bool mini = w == level.waves.Length - 1 && !string.IsNullOrEmpty(level.miniBoss);
                    waveTotal = waveLeft = count + (mini ? 1 : 0);
                    int spawned = 0;
                    while (spawned < count)
                    {
                        // small groups of 1-3 so they never walk in single file
                        int g = Mathf.Min(Random.Range(1, 4), count - spawned);
                        float baseY = Random.Range(LaneBottom + 0.6f, LaneTop - 0.6f);
                        for (int i = 0; i < g; i++)
                            Spawn(PickType(), 1f, true, Mathf.Clamp(baseY + Random.Range(-1.2f, 1.2f), LaneBottom, LaneTop), i * 0.6f);
                        spawned += g;
                        yield return new WaitForSeconds(level.spawnGap * g * 0.8f);
                    }
                    if (mini)
                    {
                        yield return new WaitForSeconds(level.spawnGap);
                        Spawn(level.miniBoss, level.miniBossHp, true, Random.Range(LaneBottom + 0.8f, LaneTop - 0.8f), 0, mini: true);
                    }
                    while (Enemies.Count > 0) yield return null;
                }
                if (w < level.TotalWaves - 1) yield return Break();
            }
            Win();
        }

        /// <summary>Between waves: the cat heals a bit, and the player can open the upgrade pop-up (the break waits for it).</summary>
        IEnumerator Break()
        {
            shooter.Heal(GameData.WaveHeal);
            breakGroup.SetActive(true);
            upgradeAsked = false;
            float t = GameData.WaveBreak;
            while (t > 0)
            {
                // the pop-up sets timeScale to 0, so the countdown waits while it's open
                t -= Time.deltaTime;
                if (Input.GetKeyDown(KeyCode.U)) upgradeAsked = true;
                if (upgradeAsked && !Paused)
                {
                    upgradeAsked = false;
                    OpenUpgrade();
                }
                breakText.text = "Next wave in " + Mathf.CeilToInt(Mathf.Max(0, t));
                yield return null;
            }
            breakGroup.SetActive(false);
        }

        IEnumerator BossWave()
        {
            waveTotal = waveLeft = 1;
            Sfx.Play("roar");
            Shake(0.2f, 0.8f);
            boss = Spawn(GameData.Boss.enemy, 1f, true, (LaneTop + LaneBottom) * 0.5f, 0, boss: true);
            if (level.hostage) SpawnHostage();
            waveGroup.SetActive(false);
            bossGroup.SetActive(true);
            hostageRow.SetActive(hostage != null);
            float escortT = level.escortEvery;
            while (boss != null && boss.Alive)
            {
                if (level.escortEvery > 0 && !string.IsNullOrEmpty(level.escort))
                {
                    escortT -= Time.deltaTime;
                    if (escortT <= 0)
                    {
                        escortT = level.escortEvery;
                        Spawn(level.escort, 1f, false, Random.Range(LaneBottom, LaneTop), 0);
                    }
                }
                yield return null;
            }
            bossGroup.SetActive(false);
            if (hostage != null) hostage.Free();
            // the boss's 3 death explosions, then everything left falls with it
            yield return new WaitForSeconds(0.6f);
            foreach (var e in Enemies.ToArray()) e.Kill();
            yield return new WaitForSeconds(hostage != null ? 2f : 1.2f);
        }

        /// <summary>The captured cat: one of the cats the player didn't pick.</summary>
        void SpawnHostage()
        {
            CatDef look = null;
            foreach (var c in GameData.Cats) if (c.id != cat.id) { look = c; break; }
            var go = new GameObject("Hostage");
            go.transform.SetParent(world, false);
            hostage = go.AddComponent<Hostage>();
            hostage.Init(this, boss, look ?? cat, GameData.Boss.hostageHp);
        }

        string PickType()
        {
            float total = 0;
            foreach (var m in level.mix) total += m.weight;
            float r = Random.value * total;
            foreach (var m in level.mix)
            {
                r -= m.weight;
                if (r <= 0) return m.type;
            }
            return level.mix[0].type;
        }

        Enemy Spawn(string type, float hpMul, bool countsForWave, float y, float xOffset, bool mini = false, bool boss = false)
        {
            return Create(type, hpMul, countsForWave, new Vector3(spawnX + xOffset + Random.Range(0f, 0.4f), y, 0), mini, boss);
        }

        /// <summary>Boss minions appear at the boss's position (they don't count toward the wave bar).</summary>
        public void SpawnAt(string type, Vector3 pos)
        {
            if (!Playing) return;
            pos.y = Mathf.Clamp(pos.y, LaneBottom, LaneTop);
            Create(type, 1f, false, pos, false, false);
        }

        Enemy Create(string type, float hpMul, bool countsForWave, Vector3 pos, bool mini, bool boss)
        {
            if (!GameData.Enemies.TryGetValue(type, out var def)) { Debug.LogError("Unknown enemy type " + type); return null; }
            var go = new GameObject(def.name + (mini ? " (mini-boss)" : ""));
            go.transform.SetParent(world, false);
            var e = go.AddComponent<Enemy>();
            e.Init(this, def, def.hp * GameData.HpMultiplier(level.id) * hpMul, pos, countsForWave, boss, mini);
            Enemies.Add(e);
            return e;
        }

        public void OnEnemyKilled(Enemy e)
        {
            Enemies.Remove(e);
            int coins = e.Def.coins * (e.IsMini ? 3 : 1);
            coinsEarned += coins;
            Sfx.Play("coin", 0.5f, 0.06f);
            if (e.CountsForWave) waveLeft = Mathf.Max(0, waveLeft - 1);
        }

        public void Splash(Vector2 pos, float radius, float damage, Enemy except)
        {
            // explosions don't care who's a hostage
            if (hostage != null && hostage.Alive && Vector2.Distance(hostage.Center, pos) <= radius + hostage.Radius * 0.5f)
                hostage.Hit(damage);
            foreach (var e in Enemies.ToArray())
                if (e != except && e.Alive && Vector2.Distance(e.Center, pos) <= radius + e.Radius * 0.5f)
                    e.Hit(damage);
        }

        public void DamageBarricade(float d)
        {
            if (!Playing) return;
            barricadeHp = Mathf.Max(0, barricadeHp - d);
            barricadeFlash = 0.1f;
            barricadeShake = 0.15f;
            Sfx.Play("barricade", 0.6f, 0.08f);
            if (barricadeHp <= 0) Lose("The barricade fell!");
        }

        /// <summary>Is this zombie touching the player's cat? (same lane, overlapping, cat on the ground)</summary>
        public bool TouchesCat(Enemy e)
        {
            if (shooter == null || !shooter.Grounded || !Playing) return false;
            float dy = Mathf.Abs(e.transform.position.y - shooter.transform.position.y);
            if (dy > (e.IsBoss ? 1.0f : 0.55f)) return false;
            // only from the front: zombies that already walked past keep going to the barricade
            float dx = e.Center.x - shooter.Center.x;
            return dx > -0.1f && dx < e.Radius * 0.6f + shooter.Radius;
        }

        public void DamageCat(float d)
        {
            if (Playing) shooter.TakeHit(d);
        }

        public void CatDown() => Lose("Your cat was knocked out!");

        public void HostageDown() => Lose("The hostage cat was hit!");

        public void Shake(float amp, float dur)
        {
            shakeAmp = shakeT > 0 ? Mathf.Max(shakeAmp, amp) : amp;
            shakeT = Mathf.Max(shakeT, dur);
        }

        // ---------------- frame update ----------------

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && Playing)
            {
                if (Paused) Resume(); else Pause();
            }

            float dt = Time.deltaTime;

            // camera shake
            if (shakeT > 0)
            {
                shakeT -= dt;
                var o = Random.insideUnitCircle * shakeAmp;
                cam.transform.position = new Vector3(o.x, o.y, -10);
            }
            else cam.transform.position = new Vector3(0, 0, -10);

            // barricade hit feedback
            barricadeFlash -= dt;
            barricadeShake -= dt;
            barricadeSr.transform.position = BarricadePos + (barricadeShake > 0 ? (Vector3)(Random.insideUnitCircle * 0.05f) : Vector3.zero);
            barricadeSr.GetPropertyBlock(mpb);
            mpb.SetFloat("_Flash", barricadeFlash > 0 ? 0.6f : 0f);
            mpb.SetColor("_FlashColor", new Color(1f, 0.3f, 0.25f));
            barricadeSr.SetPropertyBlock(mpb);
            UpdateGauge();

            UpdateHud();
        }

        // ---------------- HUD ----------------

        void BuildHud(RectTransform screen)
        {
            hud = UIKit.Stretch(screen, "HUD");
            var tl = new Vector2(0, 1);
            var tr = new Vector2(1, 1);

            // coins (top left)
            UIKit.Img(hud, "CoinBar", new Vector2(130, -45), 1.6f, tl);
            UIKit.Img(hud, "CoinIcon", new Vector2(58, -45), 1.5f, tl);
            coinText = UIKit.Label(hud, "0", new Vector2(150, -45), 30, null, TextAnchor.MiddleCenter, new Vector2(140, 40), tl);

            // wave / level bar (top centre)
            waveGroup = UIKit.Rect(hud, "WaveGroup", new Vector2(0, -50), new Vector2(300, 90), new Vector2(0.5f, 1)).gameObject;
            var wb = UIKit.Img(waveGroup.transform, "WaveBar", new Vector2(0, 8), 1.7f);
            var fillRt = UIKit.Rect(wb.transform, "Fill", new Vector2(wb.rectTransform.sizeDelta.x * 0.12f, 0),
                new Vector2(wb.rectTransform.sizeDelta.x * 0.62f, wb.rectTransform.sizeDelta.y * 0.36f));
            waveFill = fillRt.gameObject.AddComponent<Image>();
            waveFill.sprite = Art.White;
            waveFill.type = Image.Type.Filled;
            waveFill.fillMethod = Image.FillMethod.Horizontal;
            waveFill.color = new Color(0.95f, 0.35f, 0.25f, 0.9f);
            waveFill.raycastTarget = false;
            waveText = UIKit.Label(waveGroup.transform, "", new Vector2(0, -42), 24);

            // boss bar (replaces the wave bar while the boss is alive)
            bossGroup = UIKit.Rect(hud, "BossGroup", new Vector2(0, -55), new Vector2(560, 90), new Vector2(0.5f, 1)).gameObject;
            UIKit.Label(bossGroup.transform, GameData.Enemies[GameData.Boss.enemy].name.ToUpper(), new Vector2(0, 22), 30, new Color(1f, 0.85f, 0.3f));
            bossFill = UIKit.Bar(bossGroup.transform, new Vector2(0, -14), new Vector2(520, 34), new Color(0.9f, 0.2f, 0.2f));
            hostageRow = UIKit.Rect(bossGroup.transform, "Hostage", new Vector2(0, -52), new Vector2(520, 30)).gameObject;
            UIKit.Label(hostageRow.transform, "HOSTAGE", new Vector2(-150, 0), 22, new Color(0.7f, 0.95f, 1f), TextAnchor.MiddleRight, new Vector2(160, 30));
            hostageFill = UIKit.Bar(hostageRow.transform, new Vector2(60, 0), new Vector2(240, 24), new Color(0.35f, 0.75f, 1f));
            bossGroup.SetActive(false);

            // barricade HP + cat HP (top right) + pause
            UIKit.Img(hud, "WallIcon", new Vector2(-355, -45), 0.85f, tr);
            hpFill = UIKit.Bar(hud, new Vector2(-225, -45), new Vector2(210, 36), HpColor(1), tr);
            hpText = UIKit.Label(hpFill.transform.parent, "", Vector2.zero, 22, null, TextAnchor.MiddleCenter, new Vector2(210, 36));
            UIKit.Img(hud, "Icon_Cat", new Vector2(-355, -92), 0.6f, tr);
            catHpFill = UIKit.Bar(hud, new Vector2(-225, -92), new Vector2(210, 32), HpColor(1), tr);
            catHpText = UIKit.Label(catHpFill.transform.parent, "", Vector2.zero, 20, null, TextAnchor.MiddleCenter, new Vector2(210, 32));
            UIKit.Btn(hud, "SettingBtn", null, new Vector2(-60, -48), Pause, 0.9f, null, 30, tr);

            // ammo counter that follows the cat
            ammoText = UIKit.Label(hud, "", Vector2.zero, 20, null, TextAnchor.MiddleCenter, new Vector2(120, 26));
            reloadFill = UIKit.Bar(hud, Vector2.zero, new Vector2(80, 16), new Color(1f, 0.85f, 0.3f));
            helpText = UIKit.Label(hud, "HELP!", Vector2.zero, 26, new Color(0.7f, 0.95f, 1f), TextAnchor.MiddleCenter, new Vector2(200, 32));
            helpText.gameObject.SetActive(false);

            // between-wave break: countdown + upgrade button
            breakGroup = UIKit.Rect(hud, "Break", new Vector2(0, 40), new Vector2(500, 200)).gameObject;
            UIKit.Label(breakGroup.transform, "WAVE CLEARED!  +" + GameData.WaveHeal + " HP", new Vector2(0, 60), 40, new Color(0.7f, 1f, 0.5f));
            breakText = UIKit.Label(breakGroup.transform, "", new Vector2(0, 15), 28);
            UIKit.Btn(breakGroup.transform, "BtnGreen", "Upgrade (U)", new Vector2(0, -55), () => upgradeAsked = true, 0.55f, "BtnGreenPressed", 26);
            breakGroup.SetActive(false);

            // controls reminder
            UIKit.Label(hud, "WASD move  -  SPACE jump  -  CLICK shoot  -  R reload  -  ESC pause", new Vector2(0, 18), 16,
                new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, null, new Vector2(0.5f, 0));

            // special button (bottom right) with radial cooldown
            var sp = UIKit.Btn(hud, "YellowBox", null, new Vector2(-85, 95), () => shooter.TrySpecial(), 0.95f, null, 30, new Vector2(1, 0));
            UIKit.Img(sp.transform, SpecialIcon(cat.special), Vector2.zero, 1.1f).raycastTarget = false;
            specialCd = UIKit.Rect(sp.transform, "Cooldown", Vector2.zero, ((RectTransform)sp.transform).sizeDelta).gameObject.AddComponent<Image>();
            specialCd.sprite = Art.UI("YellowBox");
            specialCd.color = new Color(0, 0, 0, 0.6f);
            specialCd.type = Image.Type.Filled;
            specialCd.fillMethod = Image.FillMethod.Radial360;
            specialCd.fillOrigin = (int)Image.Origin360.Top;
            specialCd.fillClockwise = false;
            specialCd.raycastTarget = false;
            specialText = UIKit.Label(hud, cat.specialName, new Vector2(-85, 175), 22, null, TextAnchor.MiddleCenter, null, new Vector2(1, 0));
            UIKit.Label(hud, "E / RIGHT CLICK", new Vector2(-85, 22), 14, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, null, new Vector2(1, 0));

            // active boosters (bottom left)
            float bx = 50;
            foreach (var bd in GameData.Boosters)
            {
                if (!boosts.Contains(bd.id)) continue;
                UIKit.Img(hud, bd.icon, new Vector2(bx, 45), 0.7f, Vector2.zero).raycastTarget = false;
                bx += 70;
            }
            UpdateHud();
        }

        static string SpecialIcon(string special)
        {
            switch (special)
            {
                case "rapid": return "AddonIcon1";
                case "pierce": return "AddonIcon3";
                case "spread": return "Up3";
                default: return "AddonIcon6";
            }
        }

        void UpdateHud()
        {
            if (hud == null) return;
            coinText.text = (Save.Coins + coinsEarned).ToString();
            waveText.text = "Level " + level.id + " - Wave " + (waveIndex + 1) + "/" + level.TotalWaves;
            waveFill.fillAmount = waveTotal > 0 ? (float)waveLeft / waveTotal : 1f;
            float f = barricadeHp / barricadeMax;
            hpFill.fillAmount = f;
            hpFill.color = HpColor(f);
            hpText.text = Mathf.CeilToInt(barricadeHp) + " / " + barricadeMax;
            if (boss != null) bossFill.fillAmount = Mathf.Clamp01(boss.Hp / boss.MaxHp);

            float cf = shooter.Hp / shooter.MaxHp;
            catHpFill.fillAmount = cf;
            catHpFill.color = HpColor(cf);
            catHpText.text = Mathf.CeilToInt(shooter.Hp) + " / " + shooter.MaxHp;

            var under = HudPos(shooter.transform.position + Vector3.down * 0.25f);
            ammoText.rectTransform.anchoredPosition = under;
            ammoText.text = shooter.Reloading ? "RELOAD" : shooter.Ammo + " / " + shooter.Magazine;
            ammoText.color = shooter.Reloading || shooter.Ammo <= shooter.Magazine / 4 ? new Color(1f, 0.6f, 0.3f) : Color.white;
            var reloadBar = reloadFill.transform.parent.gameObject;
            reloadBar.SetActive(shooter.Reloading);
            ((RectTransform)reloadBar.transform).anchoredPosition = under + new Vector2(0, -22);
            reloadFill.fillAmount = 1f - shooter.ReloadLeft / GameData.ReloadTime;

            if (hostage != null)
            {
                hostageFill.fillAmount = Mathf.Clamp01(hostage.Hp / hostage.MaxHp);
                helpText.gameObject.SetActive(hostage.Alive || hostage.Freed);
                helpText.text = hostage.Freed ? "THANK YOU!" : "HELP!";
                helpText.rectTransform.anchoredPosition = HudPos(hostage.Top + Vector3.up * 0.3f) + Vector2.up * Mathf.Sin(Time.time * 8f) * 4f;
            }
            specialCd.fillAmount = shooter.Def.specialCooldown > 0 ? Mathf.Clamp01(shooter.SpecialCooldownLeft / shooter.Def.specialCooldown) : 0;
            bool active = shooter.SpecialActiveLeft > 0 || shooter.PierceReady;
            specialText.color = active ? new Color(1f, 0.9f, 0.3f) : Color.white;
        }

        /// <summary>World position -> HUD (canvas) position, for labels that follow things on the battlefield.</summary>
        Vector2 HudPos(Vector3 world)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, cam.WorldToScreenPoint(world), null, out var lp);
            return lp;
        }

        IEnumerator Banner(string title, string sub, float duration)
        {
            Sfx.Play("wave");
            var g = UIKit.Rect(hud, "Banner", new Vector2(0, 150), new Vector2(600, 200));
            UIKit.Img(g, "TitleRibbon", Vector2.zero, 1.35f).raycastTarget = false;
            UIKit.Label(g, title, new Vector2(0, 14), 50);
            if (sub != null) UIKit.Label(g, sub, new Vector2(0, -80), 30, new Color(1f, 0.9f, 0.55f));
            g.gameObject.AddComponent<PopIn>();
            yield return new WaitForSeconds(duration);
            if (g != null) Destroy(g.gameObject);
        }

        // ---------------- pause / win / lose ----------------

        void Pause()
        {
            if (!Playing || Paused) return;
            Paused = true;
            Time.timeScale = 0f;
            popup = Popup();
            UIKit.Label(popup, "PAUSED GAME", new Vector2(0, 150), 52);
            Game.SettingsRow(popup, new Vector2(0, 50));
            UIKit.Btn(popup, "BtnOrange", "Restart", new Vector2(-210, -90), Restart, 0.6f, "BtnOrangePressed");
            UIKit.Btn(popup, "BtnGreen", "Resume", new Vector2(0, -90), Resume, 0.6f, "BtnGreenPressed");
            UIKit.Btn(popup, "BtnOrange", "Home", new Vector2(210, -90), Home, 0.6f, "BtnOrangePressed");
        }

        /// <summary>Weapon upgrade pop-up (between waves). Coins earned this level can be spent right away.</summary>
        void OpenUpgrade()
        {
            if (!Playing || Paused) return;
            Paused = true;
            Time.timeScale = 0f;
            popup = Popup();
            UIKit.Label(popup, "UPGRADE WEAPON", new Vector2(0, 130), 52);
            var coins = UIKit.Label(popup, "", new Vector2(0, 75), 28, new Color(1f, 0.9f, 0.5f));
            System.Action refresh = null;
            System.Action showCoins = () => coins.text = "Coins: " + (Save.Coins + coinsEarned);
            refresh = Game.WeaponRow(popup, new Vector2(0, 0), cat, () => Save.Coins + coinsEarned, cost =>
            {
                if (Save.Coins + coinsEarned < cost) return false;
                BankCoins();
                Save.Coins -= cost;
                return true;
            }, () =>
            {
                shooter.OnWeaponUpgraded();
                refresh();
                showCoins();
            });
            showCoins();
            UIKit.Btn(popup, "BtnGreen", "Back to fight", new Vector2(0, -110), Resume, 0.6f, "BtnGreenPressed", 28);
        }

        void Resume()
        {
            if (popup != null) Destroy(popup.gameObject);
            Paused = false;
            Time.timeScale = 1f;
        }

        void Home()
        {
            RestoreStart();
            Game.I.ShowLevelSelect();
        }

        void Restart()
        {
            RestoreStart();
            Game.I.StartLevel(level.id);
        }

        /// <summary>Losing or quitting a level puts coins and weapon back to how they were when it started.</summary>
        void RestoreStart()
        {
            coinsEarned = 0;
            Save.Coins = startCoins;
            Save.WeaponLevel = startWeapon;
        }

        void BankCoins()
        {
            Save.Coins += coinsEarned;
            coinsEarned = 0;
        }

        RectTransform Popup()
        {
            var p = UIKit.Stretch(hud, "Popup");
            UIKit.Dim(p);
            p.gameObject.AddComponent<PopIn>();
            return p;
        }

        void Win()
        {
            if (!Playing) return;
            state = State.Won;
            int earned = coinsEarned;
            BankCoins();
            if (level.id >= Save.Unlocked) Save.Unlocked = level.id + 1;
            Sfx.Play("win");
            bool last = level.id >= GameData.Levels.Length;

            popup = Popup();
            UIKit.Img(popup, "WinPopUp", new Vector2(0, 70), 0.78f);
            UIKit.Label(popup, "Coins earned", new Vector2(0, 10), 30, new Color(1f, 0.92f, 0.7f));
            UIKit.Img(popup, "CoinBar", new Vector2(0, -40), 1.7f);
            UIKit.Img(popup, "CoinIcon", new Vector2(-78, -40), 1.6f);
            UIKit.Label(popup, "+" + earned, new Vector2(12, -40), 32);
            UIKit.Btn(popup, "BtnGreen", last ? "Finish" : "Continue", new Vector2(0, -150), () =>
            {
                if (last) Game.I.ShowComplete();
                else Game.I.ShowUpgrades();
            }, 0.7f, "BtnGreenPressed", 34);
        }

        void Lose(string reason)
        {
            if (!Playing) return;
            state = State.Lost;
            if (run != null) StopCoroutine(run);
            if (breakGroup != null) breakGroup.SetActive(false);
            RestoreStart();
            Sfx.Play("lose");
            Sfx.Vibrate();

            popup = Popup();
            UIKit.Img(popup, "LosePopUp", new Vector2(0, 60), 0.85f);
            UIKit.Label(popup, reason, new Vector2(0, -40), 30, new Color(1f, 0.85f, 0.75f));
            UIKit.Btn(popup, "BtnGreen", "Retry", new Vector2(-130, -140), () => Game.I.StartLevel(level.id), 0.65f, "BtnGreenPressed", 32);
            UIKit.Btn(popup, "BtnOrange", "Home", new Vector2(130, -140), () => Game.I.ShowLevelSelect(), 0.65f, "BtnOrangePressed", 32);
        }
    }
}
