using System;
using System.Collections.Generic;
using System.IO;
using Relic.Items;
using Relic.World.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Relic.Character.Editor
{
    /// <summary>
    /// Review renders of the procedural hero, no play mode needed:
    /// Unity -batchmode -quit -projectPath . -executeMethod Relic.Character.Editor.HeroCapture.Run [-captureDir DIR]
    /// Writes, per kit, front / three-quarter / back views side by side (kitN_sheet.png), a head
    /// close-up, and a motion sheet (idle, sprint, dash, jump, climb). Default output is the
    /// gitignored art-ref/renders folder next to the main checkout.
    /// </summary>
    public static class HeroCapture
    {
        const int W = 600, H = 900;

        [MenuItem("Relic/Character/Capture Hero Renders")]
        public static void RunMenu() => Capture(DefaultDir());

        public static void Run()
        {
            try { Capture(Arg("-captureDir") ?? DefaultDir()); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static string DefaultDir()
        {
            // <repo>/.wt/<stream>/Assets → <repo>/art-ref/renders; a plain checkout → <repo>/art-ref/renders.
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repo = Path.GetFileName(Path.GetDirectoryName(project)) == ".wt" ? Path.GetFullPath(Path.Combine(project, "../..")) : project;
            return Path.Combine(repo, "art-ref", "renders");
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        struct Kit { public string Name; public BaseType Weapon; public BaseType Armor; public int Roll; public bool Drawn; }

        public static void Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var toon = AssetDatabase.LoadAssetAtPath<Material>("Assets/Relic/World/Materials/ToonCharacter.mat");
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Relic/World/Materials/SkyGradient.mat");

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            var sky = sunGo.AddComponent<SkyController>();
            sky.Sun = sun;
            sky.SkyMaterial = skyMat != null ? new Material(skyMat) : null;
            sky.Apply();
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.45f, -0.6f, -0.65f));
            Shader.SetGlobalVector("_RelicWorldOffset", Vector4.zero);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.localScale = Vector3.one * 2f;
            var groundMat = new Material(toon);
            groundMat.SetColor("_BaseColor", new Color(0.62f, 0.62f, 0.62f));
            groundMat.SetFloat("_OutlineWidth", 0f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            var heroGo = new GameObject("Hero");
            var hero = heroGo.AddComponent<HeroAvatar>();
            hero.ToonMaterial = toon;

            var cam = MakeCamera();
            var roller = new KitRoller(1337);
            var kits = new List<Kit>
            {
                new Kit { Name = "kit1_light_staff", Weapon = BaseType.Staff, Armor = BaseType.LightArmor, Roll = 1 },
                new Kit { Name = "kit2_medium_sword", Weapon = BaseType.Sword, Armor = BaseType.MediumArmor, Roll = 2 },
                new Kit { Name = "kit3_heavy_greatsword", Weapon = BaseType.Greatsword, Armor = BaseType.HeavyArmor, Roll = 3 },
            };
            foreach (Kit k in kits)
            {
                hero.Kit = roller.Roll(3, k.Roll, k.Weapon, k.Armor);
                hero.WeaponDrawn = k.Drawn;
                hero.Input = new HeroAnimInput { Motion = HeroMotion.Idle };
                hero.Rebuild();
                Settle(hero, Vector3.zero, 90);
                Debug.Log("[Relic.Character] " + k.Name + ": " + hero.TriangleCount + " triangles, " + hero.MaterialCount + " materials [" + hero.Breakdown + "], items: " + string.Join(" | ", hero.Look.ItemNames));
                var sheet = new Texture2D(W * 3, H, TextureFormat.RGB24, false);
                float[] yaws = { 180f, 215f, 0f };
                for (int v = 0; v < 3; v++)
                {
                    Orbit(cam, yaws[v], 0.95f, 4.3f, 4f, 30f);
                    Blit(cam, sheet, v * W);
                }
                Save(sheet, Path.Combine(dir, k.Name + "_sheet.png"));
                if (k.Roll == 1)
                {
                    Orbit(cam, 180f, 1.58f, 0.85f, 2f, 22f);
                    var head = new Texture2D(W, H, TextureFormat.RGB24, false);
                    Blit(cam, head, 0);
                    Save(head, Path.Combine(dir, "head_closeup.png"));
                    Orbit(cam, 160f, 1.55f, 1.0f, 2f, 22f);
                    Blit(cam, head, 0);
                    Save(head, Path.Combine(dir, "head_threequarter.png"));
                    MotionSheet(hero, cam, Path.Combine(dir, "motion_sheet.png"));
                }
            }
            // A drawn weapon for good measure.
            hero.Kit = roller.Roll(3, 4, BaseType.Bow, BaseType.MediumArmor);
            hero.WeaponDrawn = true;
            hero.Input = new HeroAnimInput { Motion = HeroMotion.Idle, WeaponDrawn = true };
            hero.Rebuild();
            Settle(hero, Vector3.zero, 90);
            var drawn = new Texture2D(W * 2, H, TextureFormat.RGB24, false);
            Orbit(cam, 200f, 0.95f, 3.6f, 5f, 30f); Blit(cam, drawn, 0);
            Orbit(cam, 120f, 0.95f, 3.6f, 5f, 30f); Blit(cam, drawn, W);
            Save(drawn, Path.Combine(dir, "kit4_bow_drawn.png"));

            Object.DestroyImmediate(heroGo);
            Debug.Log("[Relic.Character] Captures written to " + dir);
        }

        static void MotionSheet(HeroAvatar hero, Camera cam, string file)
        {
            var poses = new (string name, HeroAnimInput input, Vector3 vel)[]
            {
                ("idle", new HeroAnimInput { Motion = HeroMotion.Idle }, Vector3.zero),
                ("run", new HeroAnimInput { Motion = HeroMotion.Run, Speed = 6f }, new Vector3(0f, 0f, 6f)),
                ("sprint", new HeroAnimInput { Motion = HeroMotion.Run, Speed = 9.5f, Sprint = true }, new Vector3(0f, 0f, 9.5f)),
                ("dash", new HeroAnimInput { Motion = HeroMotion.Run, Speed = 14f, Dash = true }, new Vector3(0f, 0f, 14f)),
                ("jump", new HeroAnimInput { Motion = HeroMotion.Air, VerticalSpeed = 5f, Speed = 5f }, new Vector3(0f, 5f, 5f)),
                ("climb", new HeroAnimInput { Motion = HeroMotion.Climb, ClimbSpeed = 2f }, new Vector3(0f, 1.5f, 0f)),
            };
            var sheet = new Texture2D(W * poses.Length, H, TextureFormat.RGB24, false);
            for (int i = 0; i < poses.Length; i++)
            {
                hero.Input = poses[i].input;
                hero.Rebuild();
                // Move the hero along its velocity so the chains feel it.
                Vector3 p = Vector3.zero;
                for (int f = 0; f < 50; f++)
                {
                    p += poses[i].vel * (1f / 60f);
                    hero.transform.position = new Vector3(0f, 0f, p.z);
                    hero.Animate(1f / 60f, poses[i].vel);
                }
                hero.transform.position = Vector3.zero;
                // Keep the chain tails where they were relative to the hero.
                hero.Chains.Step(0f);
                Orbit(cam, 250f, 0.9f, 3.8f, 5f, 30f);
                Blit(cam, sheet, i * W);
            }
            Save(sheet, file);
            hero.transform.position = Vector3.zero;
            hero.Input = new HeroAnimInput { Motion = HeroMotion.Idle };
        }

        static void Settle(HeroAvatar hero, Vector3 vel, int frames)
        {
            for (int f = 0; f < frames; f++) hero.Animate(1f / 60f, vel);
        }

        static Camera MakeCamera()
        {
            var go = new GameObject("CaptureCamera");
            var cam = go.AddComponent<Camera>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.64f, 0.64f, 0.64f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            return cam;
        }

        /// <summary>yaw 180 = looking at the hero's face (camera on +Z).</summary>
        static void Orbit(Camera cam, float yaw, float targetY, float distance, float pitch, float fov)
        {
            cam.fieldOfView = fov;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 target = new Vector3(0f, targetY, 0f);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }

        static void Blit(Camera cam, Texture2D into, int x)
        {
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            into.ReadPixels(new Rect(0, 0, W, H), x, 0);
            into.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }

        static void Save(Texture2D tex, string file)
        {
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Debug.Log("[Relic.Character] Wrote " + file);
        }
    }
}
