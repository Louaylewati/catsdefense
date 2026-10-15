using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CatDefense.EditorTools
{
    // The speaker icon and the thin white lines in the Game view are editor gizmos
    // (AudioSource icon + the UI canvas outline). They never appear in a build; this hides them in the editor.
    [InitializeOnLoad]
    public static class HideGameViewGizmos
    {
        const string DoneKey = "CatDefense.GizmosHidden";

        static HideGameViewGizmos()
        {
            EditorApplication.delayCall += () =>
            {
                GizmoUtility.SetIconEnabled(typeof(AudioSource), false);
                GizmoUtility.SetIconEnabled(typeof(AudioListener), false);
                // only switch the Game view toggle off once, so turning it back on by hand sticks
                if (EditorPrefs.GetBool(DoneKey + Application.dataPath)) return;
                if (Hide()) EditorPrefs.SetBool(DoneKey + Application.dataPath, true);
            };
        }

        [MenuItem("Cat Defense/Hide Game View Gizmos")]
        public static bool Hide()
        {
            var t = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            var field = t?.GetField("m_Gizmos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return false;
            var views = Resources.FindObjectsOfTypeAll(t);
            foreach (var v in views)
            {
                field.SetValue(v, false);
                ((EditorWindow)v).Repaint();
            }
            return views.Length > 0;
        }
    }
}
