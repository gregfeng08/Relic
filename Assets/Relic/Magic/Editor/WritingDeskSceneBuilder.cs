using System.IO;
using Relic.Magic.Desk;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Relic.Magic.DeskEditor
{
    /// <summary>
    /// Creates Assets/Relic/Magic/Scenes/WritingDesk.unity: one GameObject with
    /// WritingDeskBootstrap, which builds everything else at runtime.
    /// Batchmode: Unity.exe -batchmode -quit -projectPath . -executeMethod Relic.Magic.DeskEditor.WritingDeskSceneBuilder.Build
    /// </summary>
    public static class WritingDeskSceneBuilder
    {
        public const string ScenePath = "Assets/Relic/Magic/Scenes/WritingDesk.unity";

        [MenuItem("Relic/Magic/Rebuild Writing Desk Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Writing Desk");
            go.AddComponent<WritingDeskBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Relic.Magic] Wrote " + ScenePath);
        }

        [MenuItem("Relic/Magic/Open Writing Desk")]
        public static void Open()
        {
            if (!File.Exists(ScenePath)) Build();
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
