using Relic.World.Terrain;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Procedural low-poly toon meshes for every PropKind, built once in code: a near mesh and a
    /// cheap far mesh (trees and boulders only; smaller props are culled before they'd need one).
    /// Sizes are for scale 1; instances scale them.
    /// </summary>
    internal sealed class PropMeshes
    {
        public readonly Mesh[] Near = new Mesh[PropKinds.Count];
        public readonly Mesh[] Far = new Mesh[PropKinds.Count];

        static readonly Color Bark = new Color(0.40f, 0.28f, 0.19f);
        static readonly Color Leaf = new Color(0.30f, 0.56f, 0.22f);
        static readonly Color PineLeaf = new Color(0.15f, 0.38f, 0.25f);
        static readonly Color SnowWhite = new Color(0.93f, 0.95f, 1.0f);

        public PropMeshes()
        {
            for (int k = 0; k < PropKinds.Count; k++)
            {
                var kind = (PropKind)k;
                Near[k] = Build(kind, false).ToMesh("Prop " + kind);
                if (PropKinds.ClassOf(kind) == PropClass.Large) Far[k] = Build(kind, true).ToMesh("Prop " + kind + " far");
            }
        }

        public void Destroy()
        {
            foreach (Mesh m in Near) if (m != null) Object.Destroy(m);
            foreach (Mesh m in Far) if (m != null) Object.Destroy(m);
        }

        static PropMeshBuilder Build(PropKind kind, bool far)
        {
            var b = new PropMeshBuilder();
            switch (kind)
            {
                case PropKind.Broadleaf: Broadleaf(b, far, Leaf, 1f); break;
                case PropKind.Birch: Birch(b, far); break;
                case PropKind.Pine: Pine(b, far, false); break;
                case PropKind.SnowPine: Pine(b, far, true); break;
                case PropKind.DeadTree: DeadTree(b, far); break;
                case PropKind.Palm: Palm(b, far); break;
                case PropKind.Boulder: b.Rock(new Vector3(0f, 0.55f, 0f), new Vector3(1.25f, 0.85f, 1.05f), far ? 0 : 1, new Color(0.52f, 0.51f, 0.49f), 3); break;
                case PropKind.Bush: Bush(b); break;
                case PropKind.Rock: b.Rock(new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.35f, 0.45f), 0, new Color(0.55f, 0.53f, 0.5f), 7); break;
                case PropKind.DeadShrub: DeadShrub(b); break;
                case PropKind.Succulent: Succulent(b); break;
                case PropKind.Grass: Grass(b, new Color(0.22f, 0.42f, 0.14f), new Color(0.52f, 0.78f, 0.30f), 18, 0.7f); break;
                case PropKind.DryGrass: Grass(b, new Color(0.50f, 0.42f, 0.22f), new Color(0.86f, 0.76f, 0.46f), 14, 0.6f); break;
                case PropKind.Flowers: Flowers(b); break;
            }
            return b;
        }

        static void Broadleaf(PropMeshBuilder b, bool far, Color leaf, float size)
        {
            b.Prism(Vector3.zero, new Vector3(0f, 2.9f * size, 0f), 0.24f * size, 0.13f * size, far ? 4 : 6, Bark * 0.85f, Bark, 0f, 0.05f);
            Vector3 c = new Vector3(0f, 3.8f * size, 0f);
            if (far)
            {
                b.Blob(c, Vector3.one * 2.1f * size, 0, leaf, 0.3f, 1, c, 0f);
                return;
            }
            b.Prism(new Vector3(0f, 2.2f, 0f) * size, new Vector3(0.9f, 3.2f, 0.3f) * size, 0.09f * size, 0.04f * size, 4, Bark, Bark, 0.05f, 0.2f);
            b.Blob(c, Vector3.one * 1.75f * size, 1, leaf, 0.35f, 1, c);
            b.Blob(c + new Vector3(0.95f, -0.5f, 0.4f) * size, Vector3.one * 1.2f * size, 1, leaf * 1.06f, 0.45f, 2, c);
            b.Blob(c + new Vector3(-0.85f, -0.4f, -0.5f) * size, Vector3.one * 1.25f * size, 1, leaf * 0.94f, 0.45f, 3, c);
            b.Blob(c + new Vector3(0.1f, 0.75f, -0.2f) * size, Vector3.one * 1.15f * size, 1, leaf * 1.1f, 0.55f, 4, c);
        }

        static void Birch(PropMeshBuilder b, bool far)
        {
            Color white = new Color(0.90f, 0.88f, 0.82f), mark = new Color(0.22f, 0.2f, 0.18f);
            float h = 4.4f;
            int segs = far ? 1 : 5;
            for (int i = 0; i < segs; i++)
            {
                float y0 = h * i / segs, y1 = h * (i + 1) / segs;
                float r0 = Mathf.Lerp(0.15f, 0.07f, y0 / h), r1 = Mathf.Lerp(0.15f, 0.07f, y1 / h);
                Color c = i % 2 == 1 ? Color.Lerp(white, mark, 0.35f) : white;
                b.Prism(new Vector3(0f, y0, 0f), new Vector3(0f, y1, 0f), r0, r1, far ? 4 : 6, c, c, 0f, 0.05f * y1 / h);
            }
            Color leaf = new Color(0.55f, 0.72f, 0.26f);
            Vector3 c0 = new Vector3(0f, 4.5f, 0f);
            if (far) { b.Blob(c0, new Vector3(1.3f, 1.6f, 1.3f), 0, leaf, 0.4f, 5, c0, 0f); return; }
            b.Blob(c0, new Vector3(1.1f, 1.3f, 1.1f), 1, leaf, 0.45f, 5, c0);
            b.Blob(c0 + new Vector3(0.6f, -0.9f, 0.2f), Vector3.one * 0.8f, 1, leaf * 1.08f, 0.55f, 6, c0);
            b.Blob(c0 + new Vector3(-0.55f, -0.7f, -0.35f), Vector3.one * 0.85f, 1, leaf * 0.93f, 0.55f, 7, c0);
        }

        static void Pine(PropMeshBuilder b, bool far, bool snowy)
        {
            b.Prism(Vector3.zero, new Vector3(0f, 5.2f, 0f), 0.2f, 0.07f, far ? 4 : 6, Bark * 0.8f, Bark, 0f, 0.05f);
            Color tip = snowy ? SnowWhite : PineLeaf * 1.25f;
            Color baseC = snowy ? Color.Lerp(PineLeaf, SnowWhite, 0.15f) : PineLeaf;
            if (far)
            {
                b.Cone(new Vector3(0f, 1.1f, 0f), 5.2f, 1.75f, 6, baseC, tip, 0.2f, 0.1f);
                return;
            }
            float[] y = { 1.2f, 2.4f, 3.5f, 4.5f };
            float[] r = { 1.8f, 1.45f, 1.08f, 0.7f };
            float[] h = { 2.3f, 2.1f, 1.9f, 1.6f };
            for (int i = 0; i < 4; i++)
                b.Cone(new Vector3(0f, y[i], 0f), h[i], r[i], 8, baseC, snowy ? Color.Lerp(baseC, SnowWhite, 0.85f) : tip, 0.25f + 0.1f * i);
        }

        static void DeadTree(PropMeshBuilder b, bool far)
        {
            Color wood = new Color(0.44f, 0.38f, 0.32f);
            b.Prism(Vector3.zero, new Vector3(0.1f, 3.6f, 0f), 0.2f, 0.06f, far ? 4 : 5, wood * 0.85f, wood, 0f, 0.1f);
            int n = far ? 2 : 5;
            for (int i = 0; i < n; i++)
            {
                float a = i * 2.4f, y = 1.6f + i * 0.4f;
                var from = new Vector3(0.04f * i, y, 0f);
                var to = from + new Vector3(Mathf.Cos(a) * 1.1f, 0.9f, Mathf.Sin(a) * 1.1f);
                b.Prism(from, to, 0.08f, 0.02f, 4, wood, wood * 1.1f, 0.05f, 0.25f);
                if (!far) b.Prism(to, to + new Vector3(Mathf.Cos(a + 0.8f) * 0.5f, 0.5f, Mathf.Sin(a + 0.8f) * 0.5f), 0.03f, 0.01f, 3, wood, wood, 0.25f, 0.4f);
            }
        }

        static void Palm(PropMeshBuilder b, bool far)
        {
            Color trunk = new Color(0.62f, 0.49f, 0.32f);
            int segs = far ? 2 : 6;
            Vector3 p = Vector3.zero;
            for (int i = 0; i < segs; i++)
            {
                float t = (i + 1) / (float)segs;
                Vector3 q = new Vector3(0.9f * t * t, 5.6f * t, 0f);
                b.Prism(p, q, Mathf.Lerp(0.24f, 0.14f, i / (float)segs), Mathf.Lerp(0.24f, 0.14f, t), far ? 4 : 6, trunk * (i % 2 == 0 ? 0.9f : 1f), trunk, 0.15f * i / segs, 0.15f * t);
                p = q;
            }
            Color frond = new Color(0.30f, 0.60f, 0.20f);
            int leaves = far ? 5 : 8;
            for (int i = 0; i < leaves; i++)
            {
                float a = i * Mathf.PI * 2f / leaves + 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a)).normalized;
                b.Blade(p, dir, 2.6f, 0.55f, new Vector3(0f, -1.3f, 0f), frond * 0.8f, frond * 1.15f, Vector3.up, 0.2f, 1f);
            }
        }

        static void Bush(PropMeshBuilder b)
        {
            Color leaf = new Color(0.26f, 0.50f, 0.20f);
            Vector3 c = new Vector3(0f, 0.5f, 0f);
            b.Blob(c, new Vector3(0.8f, 0.6f, 0.8f), 1, leaf, 0.25f, 11, c);
            b.Blob(c + new Vector3(0.55f, -0.1f, 0.2f), Vector3.one * 0.55f, 1, leaf * 1.08f, 0.3f, 12, c);
            b.Blob(c + new Vector3(-0.5f, -0.12f, -0.25f), Vector3.one * 0.55f, 1, leaf * 0.92f, 0.3f, 13, c);
        }

        static void DeadShrub(PropMeshBuilder b)
        {
            Color twig = new Color(0.50f, 0.40f, 0.28f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 0.785f + (i % 2) * 0.3f;
                var to = new Vector3(Mathf.Cos(a) * 0.55f, 0.55f + (i % 3) * 0.15f, Mathf.Sin(a) * 0.55f);
                b.Prism(Vector3.zero, to, 0.035f, 0.01f, 3, twig * 0.85f, twig, 0f, 0.4f);
                b.Prism(to * 0.6f, to * 0.6f + new Vector3(Mathf.Cos(a + 1f) * 0.25f, 0.25f, Mathf.Sin(a + 1f) * 0.25f), 0.02f, 0.005f, 3, twig, twig, 0.25f, 0.5f);
            }
        }

        static void Succulent(PropMeshBuilder b)
        {
            Color green = new Color(0.33f, 0.54f, 0.30f);
            b.Prism(Vector3.zero, new Vector3(0f, 1.7f, 0f), 0.22f, 0.2f, 8, green * 0.9f, green, 0f, 0f);
            b.Blob(new Vector3(0f, 1.7f, 0f), Vector3.one * 0.2f, 0, green, 0f, 21, new Vector3(0f, 1.7f, 0f), 0f, 0.02f);
            // Two arms: out, then up.
            for (int s = -1; s <= 1; s += 2)
            {
                var elbow = new Vector3(0.45f * s, 0.85f + 0.15f * s, 0f);
                b.Prism(new Vector3(0f, 0.75f + 0.15f * s, 0f), elbow, 0.12f, 0.12f, 6, green, green, 0f, 0f);
                b.Prism(elbow, elbow + new Vector3(0f, 0.55f, 0f), 0.12f, 0.11f, 6, green, green, 0f, 0f);
            }
        }

        static void Grass(PropMeshBuilder b, Color baseC, Color tipC, int blades, float height)
        {
            for (int i = 0; i < blades; i++)
            {
                float a = i * 2.39996f; // golden angle spreads the blades evenly
                float r = 0.1f + 0.75f * ((i * 37 % 11) / 11f); // a ~1.7 m patch
                var root = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.25f;
                float h = height * (0.7f + 0.3f * ((i * 53 % 7) / 7f));
                b.Blade(root, Vector3.up, h, 0.09f, lean, baseC, tipC, Vector3.up);
            }
        }

        static void Flowers(PropMeshBuilder b)
        {
            Grass(b, new Color(0.22f, 0.42f, 0.14f), new Color(0.50f, 0.76f, 0.30f), 10, 0.45f);
            Color[] petals = { new Color(1f, 0.85f, 0.25f), new Color(0.98f, 0.97f, 0.93f), new Color(0.95f, 0.52f, 0.72f), new Color(0.62f, 0.48f, 0.92f) };
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.7f + 0.4f, r = 0.15f + 0.08f * i;
                var root = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float h = 0.4f + 0.06f * (i % 3);
                b.Prism(root, root + Vector3.up * h, 0.012f, 0.01f, 3, new Color(0.25f, 0.5f, 0.18f), new Color(0.3f, 0.55f, 0.2f), 0f, 0.9f);
                Vector3 head = root + Vector3.up * (h + 0.04f);
                b.Blob(head, new Vector3(0.07f, 0.04f, 0.07f), 0, petals[(i + 1) % petals.Length], 1f, 30 + i, head, 0f, 0.05f);
            }
        }
    }
}
