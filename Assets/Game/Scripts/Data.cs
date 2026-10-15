using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatDefense
{
    [Serializable]
    public class CatDef
    {
        public string id, name, role, sprite, portrait;
        public float damage, fireRate;
        public int projectile;
        public float splashDamage, splashRadius;
        public string special, specialName, specialDesc;
        public float specialDuration, specialCooldown, specialDamage, specialRadius;
        public float muzzleX, muzzleY, scale;
        public int barDamage, barSpeed, barRange;
        public int magazine;
    }

    /// <summary>One weapon upgrade step: multipliers on the cat's base damage / fire rate / magazine.</summary>
    [Serializable]
    public class WeaponLevel { public int cost; public float damage, fireRate, magazine; }

    [Serializable]
    public class EnemyDef
    {
        public string id, name;
        public string[] designs;
        public float hp, speed, damage, hitEvery;
        public int coins;
        public float stopDistance;
        public bool knockback;
        public float scale;
    }

    [Serializable]
    public class BossDef
    {
        public string enemy;
        public string phase1Spawn; public int phase1Count; public float phase1Every;
        public float phase2Speed;
        public string phase2Spawn; public int phase2Count; public float phase2Every;
        public float slamEvery, slamDamage, slamWarning;
        public float hostageHp;
    }

    [Serializable]
    public class MixEntry { public string type; public float weight; }

    [Serializable]
    public class LevelDef
    {
        public int id;
        public string name, background, barricade;
        public int[] waves;
        public float spawnGap;
        public MixEntry[] mix;
        public string miniBoss;
        public float miniBossHp;
        public bool bossWave, hostage;
        public string escort;
        public float escortEvery;

        public int TotalWaves => waves.Length + (bossWave ? 1 : 0);
    }

    [Serializable]
    public class BoosterDef { public string id, name, desc, icon; public int cost; }

    [Serializable] class CatFile { public CatDef[] cats; public WeaponLevel[] weaponLevels; public float reloadTime, catHp, moveSpeed, invincible; }
    [Serializable] class EnemyFile { public float levelHpScale; public EnemyDef[] enemies; public BossDef boss; }
    [Serializable] class LevelFile { public int barricadeHp; public float waveBanner, waveBreak, waveHeal; public LevelDef[] levels; public BoosterDef[] boosters; }

    /// <summary>All balancing numbers, loaded from Resources/Data/*.json.</summary>
    public static class GameData
    {
        // The design doc gives distances in pixels of a 1280-wide screen; this converts them to world units.
        public const float Px = 18.88f / 1280f;

        public static CatDef[] Cats;
        public static Dictionary<string, EnemyDef> Enemies;
        public static BossDef Boss;
        public static float LevelHpScale;
        public static LevelDef[] Levels;
        public static BoosterDef[] Boosters;
        public static int BarricadeHp;
        public static float WaveBanner, WaveBreak, WaveHeal;
        public static WeaponLevel[] WeaponLevels;
        public static float ReloadTime, CatHp, MoveSpeed, Invincible;

        public static void Load()
        {
            if (Cats != null) return;
            var cf = JsonUtility.FromJson<CatFile>(Text("cats"));
            Cats = cf.cats;
            WeaponLevels = cf.weaponLevels;
            ReloadTime = cf.reloadTime;
            CatHp = cf.catHp;
            MoveSpeed = cf.moveSpeed;
            Invincible = cf.invincible;
            var ef = JsonUtility.FromJson<EnemyFile>(Text("enemies"));
            Enemies = new Dictionary<string, EnemyDef>();
            foreach (var e in ef.enemies) Enemies[e.id] = e;
            Boss = ef.boss;
            LevelHpScale = ef.levelHpScale;
            var lf = JsonUtility.FromJson<LevelFile>(Text("levels"));
            Levels = lf.levels;
            Boosters = lf.boosters;
            BarricadeHp = lf.barricadeHp;
            WaveBanner = lf.waveBanner;
            WaveBreak = lf.waveBreak;
            WaveHeal = lf.waveHeal;
        }

        static string Text(string name)
        {
            var ta = Resources.Load<TextAsset>("Data/" + name);
            if (ta == null) throw new Exception("Missing Resources/Data/" + name + ".json");
            return ta.text;
        }

        public static CatDef Cat(string id)
        {
            foreach (var c in Cats) if (c.id == id) return c;
            return null;
        }

        /// <summary>Current weapon upgrade (index into WeaponLevels, from save data).</summary>
        public static WeaponLevel Weapon => WeaponLevels[Mathf.Clamp(Save.WeaponLevel, 0, WeaponLevels.Length - 1)];

        public static float HpMultiplier(int level) => 1f + LevelHpScale * (level - 1);
    }
}
