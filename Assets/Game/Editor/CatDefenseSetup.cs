using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatDefense.EditorTools
{
    public static class CatDefenseSetup
    {
        const string ScenePath = "Assets/Game/Scenes/Main.unity";

        [MenuItem("Cat Defense/Create Main Scene")]
        public static void CreateMainScene()
        {
            System.IO.Directory.CreateDirectory("Assets/Game/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var c = cam.AddComponent<Camera>();
            c.orthographic = true;
            c.orthographicSize = 5.31f;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.AddComponent<AudioListener>();

            new GameObject("Game").AddComponent<Game>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.productName = "Cat Defense";
            Debug.Log("Cat Defense: Main scene created at " + ScenePath);
        }
    }
}
