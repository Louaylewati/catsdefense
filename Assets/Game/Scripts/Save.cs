using UnityEngine;

namespace CatDefense
{
    /// <summary>Progress + settings (PlayerPrefs = localStorage on WebGL).</summary>
    public static class Save
    {
        public static int Unlocked
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt("cd_unlocked", 1));
            set { PlayerPrefs.SetInt("cd_unlocked", value); PlayerPrefs.Save(); }
        }

        public static string Cat
        {
            get => PlayerPrefs.GetString("cd_cat", "");
            set { PlayerPrefs.SetString("cd_cat", value); PlayerPrefs.Save(); }
        }

        public static int Coins
        {
            get => PlayerPrefs.GetInt("cd_coins", 0);
            set { PlayerPrefs.SetInt("cd_coins", value); PlayerPrefs.Save(); }
        }

        /// <summary>Weapon upgrade index (0 = base weapon).</summary>
        public static int WeaponLevel
        {
            get => PlayerPrefs.GetInt("cd_weapon", 0);
            set { PlayerPrefs.SetInt("cd_weapon", value); PlayerPrefs.Save(); }
        }

        public static bool Music { get => Flag("cd_music"); set => SetFlag("cd_music", value); }
        public static bool Sound { get => Flag("cd_sound"); set => SetFlag("cd_sound", value); }
        public static bool Vibration { get => Flag("cd_vibra"); set => SetFlag("cd_vibra", value); }

        static bool Flag(string k) => PlayerPrefs.GetInt(k, 1) == 1;
        static void SetFlag(string k, bool v) { PlayerPrefs.SetInt(k, v ? 1 : 0); PlayerPrefs.Save(); }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("cd_unlocked");
            PlayerPrefs.DeleteKey("cd_cat");
            PlayerPrefs.DeleteKey("cd_coins");
            PlayerPrefs.DeleteKey("cd_weapon");
            PlayerPrefs.Save();
        }
    }
}
