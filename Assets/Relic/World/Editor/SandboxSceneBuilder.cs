using Relic.World.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Relic.World.Editor
{
    /// <summary>
    /// Creates the sandbox's materials, player config and scene. The scene holds a single
    /// GameObject with TerrainSandbox; everything else is built from code at runtime.
    /// Menu: Relic > World > Build Terrain Sandbox. Batchmode:
    /// -executeMethod Relic.World.Editor.SandboxSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class SandboxSceneBuilder
    {
        public const string Root = "Assets/Relic/World";
        public const string MaterialsDir = Root + "/Materials";
        public const string SettingsDir = Root + "/Settings";
        public const string ScenePath = Root + "/Scenes/TerrainSandbox.unity";

        [MenuItem("Relic/World/Build Terrain Sandbox")]
        public static void BuildMenu()
        {
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            Build();
            Debug.Log("[Relic.World] Terrain sandbox built at " + ScenePath);
        }

        public static void Build()
        {
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Settings");
            EnsureFolder(Root, "Scenes");

            Material terrain = EnsureMaterial("ToonTerrain", "Relic/World/ToonTerrain");
            Material water = EnsureMaterial("Water", "Relic/World/Water");
            Material sky = EnsureMaterial("SkyGradient", "Relic/World/SkyGradient");
            Material toon = EnsureMaterial("ToonCharacter", "Relic/World/Toon");
            PlayerConfig config = EnsureAsset<PlayerConfig>(SettingsDir + "/PlayerConfig.asset");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("TerrainSandbox");
            var sandbox = go.AddComponent<TerrainSandbox>();
            sandbox.TerrainMaterial = terrain;
            sandbox.WaterMaterial = water;
            sandbox.SkyMaterial = sky;
            sandbox.ToonMaterial = toon;
            sandbox.PlayerConfig = config;

            // Scene-level lighting defaults that match the runtime sky (no baking, no probes needed).
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static Material EnsureMaterial(string name, string shaderName)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception("Shader not found: " + shaderName + " (did it fail to compile?)");
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                if (shaderName == "Relic/World/Toon") mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
