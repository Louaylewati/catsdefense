using UnityEngine;
using UnityEngine.EventSystems;

namespace CatDefense
{
    /// <summary>
    /// The player's cat: moves (WASD / arrows) inside the defense zone in front of the barricade, hops (Space),
    /// aims at the pointer, fires from a magazine while held, reloads (R or when empty), one special on a cooldown.
    /// Zombies that touch it hit it; after a hit it is invincible for a moment and flashes.
    /// </summary>
    public class CatShooter : MonoBehaviour
    {
        const float JumpTime = 0.55f, JumpHeight = 0.9f;
#if UNITY_EDITOR
        public static bool AutoFire;
#endif

        // Defense zone: from just in front of the barricade, 480 px deep (design doc), across the road lanes.
        public static float ZoneLeft => Battle.BarricadeFront + 0.45f;
        public static float ZoneRight => ZoneLeft + 480f * GameData.Px;

        public CatDef Def { get; private set; }
        public float SpecialCooldownLeft { get; private set; }
        public float SpecialActiveLeft { get; private set; }
        public bool PierceReady { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public int Ammo { get; private set; }
        public float ReloadLeft { get; private set; }
        public bool Reloading => ReloadLeft > 0;
        public bool Grounded => jumpT <= 0;

        public int Magazine => Mathf.Max(1, Mathf.RoundToInt(Def.magazine * GameData.Weapon.magazine));
        public Vector2 Center => (Vector2)transform.position + Vector2.up * (unit.cy * Def.scale + hop);
        public float Radius => unit.r * Def.scale;

        Battle b;
        Transform body;
        SpriteRenderer sr;
        SpriteAnim anim;
        MaterialPropertyBlock mpb;
        Art.UnitInfo unit;
        float fireTimer, jumpT, hop, invT, bobT, facing = 1;
        bool pointerBlocked;
        Vector2 aimPoint;
        // last aim point on the battlefield (not over a HUD button): where Big Blast lands
        Vector2 fieldAim;

        public void Init(Battle battle, CatDef def, Vector3 pos)
        {
            b = battle;
            Def = def;
            unit = Art.Unit(def.sprite);
            MaxHp = Hp = GameData.CatHp;
            Ammo = Magazine;
            transform.position = pos;
            fieldAim = new Vector2(ZoneRight + 2f, pos.y);

            // the sprite lives on a child so hops and flips never move the cat's feet / logic position
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            sr = body.gameObject.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = Art.FlashMaterial;
            mpb = new MaterialPropertyBlock();
            anim = body.gameObject.AddComponent<SpriteAnim>();
            Idle();
            UpdateBody(false);
        }

        void Idle() => anim.Play(Def.sprite + "_idle", true);

        Vector2 Muzzle => (Vector2)body.position + new Vector2(Def.muzzleX * facing, Def.muzzleY) * Def.scale;

        float FireRate
        {
            get
            {
                float r = Def.fireRate * GameData.Weapon.fireRate * b.FireMul;
                if (Def.special == "rapid" && SpecialActiveLeft > 0) r *= 2f;
                return r;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (SpecialCooldownLeft > 0) SpecialCooldownLeft -= dt;
            if (SpecialActiveLeft > 0) SpecialActiveLeft -= dt;
            if (fireTimer > 0) fireTimer -= dt;
            if (invT > 0) invT -= dt;
            if (!b.Playing || b.Paused) { UpdateBody(false); return; }

            // move inside the zone (invisible walls on every side)
            var move = new Vector2(
                (Key(KeyCode.D, KeyCode.RightArrow) ? 1 : 0) - (Key(KeyCode.A, KeyCode.LeftArrow) ? 1 : 0),
                (Key(KeyCode.W, KeyCode.UpArrow) ? 1 : 0) - (Key(KeyCode.S, KeyCode.DownArrow) ? 1 : 0));
            bool moving = move.sqrMagnitude > 0;
            if (moving)
            {
                var p = (Vector2)transform.position + move.normalized * GameData.MoveSpeed * GameData.Px * dt;
                p.x = Mathf.Clamp(p.x, ZoneLeft, ZoneRight);
                p.y = Mathf.Clamp(p.y, Battle.LaneBottom, Battle.LaneTop);
                transform.position = p;
            }

            // hop: zombies can't touch the cat while it's in the air
            if (jumpT > 0) jumpT -= dt;
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                jumpT = JumpTime;
                Sfx.Play("jump", 0.6f);
            }
            hop = jumpT > 0 ? Mathf.Sin(Mathf.PI * (1 - jumpT / JumpTime)) * JumpHeight : 0;

            var cam = Camera.main;
            aimPoint = cam.ScreenToWorldPoint(Input.mousePosition);
            if (!PointerOverUI()) fieldAim = aimPoint;

            // presses that start on a HUD button never fire
            if (Input.GetMouseButtonDown(0)) pointerBlocked = PointerOverUI();
            if (Input.GetMouseButtonUp(0)) pointerBlocked = false;

            bool trigger = Input.GetMouseButton(0) && !pointerBlocked;
#if UNITY_EDITOR
            // editor-only playtest helper: aims at the closest zombie and holds the trigger
            if (AutoFire && b.Enemies.Count > 0)
            {
                Enemy best = null;
                foreach (var e in b.Enemies) if (best == null || e.transform.position.x < best.transform.position.x) best = e;
                aimPoint = fieldAim = best.Center;
                trigger = true;
                if (SpecialCooldownLeft <= 0) TrySpecial();
            }
#endif
            // the sprite faces the cursor
            facing = aimPoint.x < transform.position.x ? -1 : 1;

            // reload: R, or automatically when the magazine is empty; the cat can move but not shoot
            if (Reloading)
            {
                ReloadLeft -= dt;
                if (ReloadLeft <= 0)
                {
                    ReloadLeft = 0;
                    Ammo = Magazine;
                }
            }
            else if (Input.GetKeyDown(KeyCode.R) && Ammo < Magazine) StartReload();

            if (trigger && fireTimer <= 0 && !Reloading && Ammo > 0)
            {
                Fire();
                fireTimer = 1f / FireRate;
                if (--Ammo <= 0) StartReload();
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Q)) TrySpecial();

            UpdateBody(moving);
        }

        static bool Key(KeyCode a, KeyCode b) => Input.GetKey(a) || Input.GetKey(b);

        void UpdateBody(bool moving)
        {
            bobT = moving && Grounded ? bobT + Time.deltaTime : 0;
            float bob = Mathf.Abs(Mathf.Sin(bobT * 14f)) * 0.06f;
            body.localPosition = new Vector3(0, hop + bob, 0);
            body.localScale = new Vector3(Def.scale * facing, Def.scale, 1);
            sr.sortingOrder = 1000 + Mathf.RoundToInt(-transform.position.y * 100f) + 5;

            // red blink while invincible after a hit
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat("_Flash", invT > 0 && Mathf.Repeat(invT, 0.16f) > 0.08f ? 0.55f : 0f);
            mpb.SetColor("_FlashColor", new Color(1f, 0.25f, 0.2f));
            sr.SetPropertyBlock(mpb);
        }

        void StartReload()
        {
            ReloadLeft = GameData.ReloadTime;
            Sfx.Play("reload", 0.7f);
        }

        /// <summary>A zombie (or the boss) hits the cat. Ignored while invincible or in the air.</summary>
        public void TakeHit(float damage)
        {
            if (invT > 0 || !Grounded || Hp <= 0) return;
            Hp = Mathf.Max(0, Hp - damage);
            invT = GameData.Invincible;
            Sfx.Play("hurt", 0.8f);
            b.Shake(0.08f, 0.15f);
            if (Hp <= 0) b.CatDown();
        }

        public void Heal(float amount) => Hp = Mathf.Min(MaxHp, Hp + amount);

        /// <summary>After a weapon upgrade the bigger magazine comes full.</summary>
        public void OnWeaponUpgraded()
        {
            ReloadLeft = 0;
            Ammo = Magazine;
        }

        static bool PointerOverUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return es.IsPointerOverGameObject();
        }

        Vector2 AimDir()
        {
            Vector2 d = aimPoint - Muzzle;
            // cursor right on top of the gun: shoot the way the cat is facing
            if (d.sqrMagnitude < 0.01f || Mathf.Sign(d.x) != facing) d = new Vector2(facing, d.y);
            return d.normalized;
        }

        void Fire()
        {
            Vector2 dir = AimDir();
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float dmg = Def.damage * GameData.Weapon.damage * b.DamageMul;
            float splashDmg = Def.splashDamage * GameData.Weapon.damage * b.DamageMul;
            float splashR = Def.splashRadius * GameData.Px;

            bool pierce = PierceReady;
            PierceReady = false;

            if (Def.special == "spread" && SpecialActiveLeft > 0)
            {
                for (int i = -2; i <= 2; i++)
                {
                    float a = (ang + i * 9f) * Mathf.Deg2Rad;
                    Projectile.Fire(b, Muzzle, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), Def.projectile, dmg, false, splashDmg, splashR);
                }
            }
            else
            {
                Projectile.Fire(b, Muzzle, dir, Def.projectile, dmg, pierce, splashDmg, splashR);
            }

            Fx.Muzzle(Muzzle, ang, 0.9f);
            Sfx.Play("shot" + Def.projectile, 0.45f, 0.05f);

            // attack anim once per shot; restart from frame 0 when firing faster than the anim
            var key = Def.sprite + "_shoot";
            float len = Art.Anim(key).Length / Art.Fps(key);
            anim.Play(key, false, Mathf.Max(1f, len * FireRate), Idle);
        }

        public void TrySpecial()
        {
            if (!b.Playing || b.Paused || SpecialCooldownLeft > 0) return;
            SpecialCooldownLeft = Def.specialCooldown;
            Sfx.Play("special");
            switch (Def.special)
            {
                case "rapid":
                case "spread":
                    SpecialActiveLeft = Def.specialDuration;
                    break;
                case "pierce":
                    PierceReady = true;
                    break;
                case "blast":
                    Vector2 p = fieldAim;
                    p.x = Mathf.Max(p.x, Battle.BarricadeFront + 0.5f);
                    float r = Def.specialRadius * GameData.Px;
                    b.Splash(p, r, Def.specialDamage * GameData.Weapon.damage * b.DamageMul, null);
                    Fx.Explosion(p, r * 2f, 1.2f);
                    Sfx.Play("boom");
                    b.Shake(0.25f, 0.35f);
                    break;
            }
        }
    }
}
