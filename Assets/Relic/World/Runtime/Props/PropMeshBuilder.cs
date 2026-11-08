using System.Collections.Generic;
using UnityEngine;

namespace Relic.World.Runtime
{
    /// <summary>
    /// Tiny procedural mesh kit for low-poly toon props: prisms, cones, lumpy blobs, faceted
    /// rocks and double-sided blades. Vertex color rgb is the albedo (sRGB) and alpha is how much
    /// the vertex sways in the wind (0 = rooted, 1 = free). Main thread or worker, no Unity API
    /// until ToMesh.
    /// </summary>
    internal sealed class PropMeshBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Color32> _c = new List<Color32>();
        readonly List<int> _t = new List<int>();

        public int VertexCount => _v.Count;

        static Color32 C(Color c, float sway) => new Color32(
            (byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(sway) * 255f));

        int Vert(Vector3 p, Vector3 n, Color c, float sway)
        {
            _v.Add(p);
            _n.Add(n);
            _c.Add(C(c, sway));
            return _v.Count - 1;
        }

        void Tri(int a, int b, int c) { _t.Add(a); _t.Add(b); _t.Add(c); }

        /// <summary>A tapered prism from a to b (smooth sides, top cap).</summary>
        public void Prism(Vector3 a, Vector3 b, float r0, float r1, int sides, Color c0, Color c1, float sway0, float sway1, float twist = 0f)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 fwd = Vector3.Cross(side, axis);
            int start = _v.Count;
            for (int i = 0; i <= sides; i++)
            {
                float ang = (i / (float)sides) * Mathf.PI * 2f + twist;
                Vector3 dir = side * Mathf.Cos(ang) + fwd * Mathf.Sin(ang);
                Vert(a + dir * r0, dir, c0, sway0);
                Vert(b + dir * r1, dir, c1, sway1);
            }
            for (int i = 0; i < sides; i++)
            {
                int k = start + i * 2;
                Tri(k, k + 1, k + 3);
                Tri(k, k + 3, k + 2);
            }
            if (r1 > 0.001f)
            {
                int center = Vert(b, axis, c1, sway1);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = (i / (float)sides) * Mathf.PI * 2f + twist, a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f + twist;
                    int p0 = Vert(b + (side * Mathf.Cos(a0) + fwd * Mathf.Sin(a0)) * r1, axis, c1, sway1);
                    int p1 = Vert(b + (side * Mathf.Cos(a1) + fwd * Mathf.Sin(a1)) * r1, axis, c1, sway1);
                    Tri(center, p1, p0);
                }
            }
        }

        /// <summary>A cone (pine tier): base ring at y0, apex above; normals lean outward-up.</summary>
        public void Cone(Vector3 baseCenter, float height, float radius, int sides, Color baseColor, Color tipColor, float sway, float droop = 0.25f)
        {
            Vector3 apex = baseCenter + Vector3.up * height;
            float slant = radius / height;
            int start = _v.Count;
            for (int i = 0; i <= sides; i++)
            {
                float ang = (i / (float)sides) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 n = (dir + Vector3.up * slant).normalized;
                Vert(baseCenter + dir * radius - Vector3.up * droop, n, baseColor, sway * 0.7f);
                Vert(apex, n, tipColor, sway);
            }
            for (int i = 0; i < sides; i++)
            {
                int k = start + i * 2;
                Tri(k, k + 1, k + 2);
            }
            // Underside so the tier isn't hollow from below.
            int c = Vert(baseCenter + Vector3.up * (height * 0.15f), Vector3.down, baseColor * 0.7f, sway * 0.5f);
            for (int i = 0; i < sides; i++) Tri(c, start + i * 2, start + (i + 1) * 2);
        }

        /// <summary>
        /// A lumpy blob (canopy, bush): an icosahedron (detail 0 = 20 faces, 1 = 80) scaled and
        /// displaced. Normals are spherical (blended toward `normalCenter`) for soft toon shading.
        /// </summary>
        public void Blob(Vector3 center, Vector3 radius, int detail, Color color, float sway, int seed, Vector3 normalCenter, float normalBlend = 0.4f, float lump = 0.14f)
        {
            foreach (Vector3[] f in Ico(detail))
            {
                int a = BlobVert(f[0], center, radius, color, sway, seed, normalCenter, normalBlend, lump);
                int b = BlobVert(f[1], center, radius, color, sway, seed, normalCenter, normalBlend, lump);
                int c = BlobVert(f[2], center, radius, color, sway, seed, normalCenter, normalBlend, lump);
                Tri(a, b, c);
            }
        }

        int BlobVert(Vector3 dir, Vector3 center, Vector3 radius, Color color, float sway, int seed, Vector3 normalCenter, float normalBlend, float lump)
        {
            float d = 1f + lump * Lump(dir, seed);
            Vector3 p = center + Vector3.Scale(dir * d, radius);
            Vector3 n = Vector3.Lerp(dir, (p - normalCenter).normalized, normalBlend).normalized;
            float shade = 0.9f + 0.1f * dir.y; // a touch darker underneath
            return Vert(p, n, color * shade, sway);
        }

        /// <summary>A faceted rock: displaced icosahedron with flat normals.</summary>
        public void Rock(Vector3 center, Vector3 radius, int detail, Color color, int seed)
        {
            foreach (Vector3[] f in Ico(detail))
            {
                Vector3 a = center + Vector3.Scale(f[0] * (1f + 0.22f * Lump(f[0], seed)), radius);
                Vector3 b = center + Vector3.Scale(f[1] * (1f + 0.22f * Lump(f[1], seed)), radius);
                Vector3 c = center + Vector3.Scale(f[2] * (1f + 0.22f * Lump(f[2], seed)), radius);
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                float shade = 0.92f + 0.08f * n.y;
                Tri(Vert(a, n, color * shade, 0f), Vert(b, n, color * shade, 0f), Vert(c, n, color * shade, 0f));
            }
        }

        /// <summary>A double-sided blade (grass, palm frond segment): base to tip, bending by `bend`.</summary>
        public void Blade(Vector3 root, Vector3 dir, float length, float width, Vector3 bend, Color baseColor, Color tipColor, Vector3 normal, float swayBase = 0f, float swayTip = 1f)
        {
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side = side.normalized * (width * 0.5f);
            Vector3 mid = root + dir * (length * 0.5f) + bend * 0.35f;
            Vector3 tip = root + dir * length + bend;
            int a = Vert(root - side, normal, baseColor, swayBase), b = Vert(root + side, normal, baseColor, swayBase);
            int c = Vert(mid - side * 0.7f, normal, Color.Lerp(baseColor, tipColor, 0.5f), (swayBase + swayTip) * 0.5f);
            int d = Vert(mid + side * 0.7f, normal, Color.Lerp(baseColor, tipColor, 0.5f), (swayBase + swayTip) * 0.5f);
            int e = Vert(tip, normal, tipColor, swayTip);
            // Both windings, so it shows from either side even with back-face culling.
            Tri(a, c, b); Tri(b, c, d); Tri(c, e, d);
            Tri(a, b, c); Tri(b, d, c); Tri(c, d, e);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetColors(_c);
            m.SetTriangles(_t, 0, true);
            m.UploadMeshData(true);
            return m;
        }

        // --- Icosahedron ---------------------------------------------------------------------

        static readonly List<Vector3[]>[] IcoCache = new List<Vector3[]>[3];

        static List<Vector3[]> Ico(int detail)
        {
            detail = Mathf.Clamp(detail, 0, 2);
            if (IcoCache[detail] != null) return IcoCache[detail];
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] p =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var faces = new List<Vector3[]>();
            for (int i = 0; i < f.Length; i += 3) faces.Add(new[] { p[f[i]].normalized, p[f[i + 1]].normalized, p[f[i + 2]].normalized });
            for (int d = 0; d < detail; d++)
            {
                var next = new List<Vector3[]>();
                foreach (Vector3[] tri in faces)
                {
                    Vector3 ab = (tri[0] + tri[1]).normalized, bc = (tri[1] + tri[2]).normalized, ca = (tri[2] + tri[0]).normalized;
                    next.Add(new[] { tri[0], ab, ca });
                    next.Add(new[] { tri[1], bc, ab });
                    next.Add(new[] { tri[2], ca, bc });
                    next.Add(new[] { ab, bc, ca });
                }
                faces = next;
            }
            // Unity front faces have cross(b - a, c - a) pointing at the viewer: make every face point out.
            foreach (Vector3[] tri in faces)
            {
                if (Vector3.Dot(Vector3.Cross(tri[1] - tri[0], tri[2] - tri[0]), tri[0] + tri[1] + tri[2]) < 0f) { Vector3 tmp = tri[1]; tri[1] = tri[2]; tri[2] = tmp; }
            }
            IcoCache[detail] = faces;
            return faces;
        }

        /// <summary>Deterministic smooth lump in about [-1, 1] for a unit direction (same input, same output).</summary>
        static float Lump(Vector3 d, int seed)
        {
            float s = seed * 1.618f;
            return 0.55f * Mathf.Sin(d.x * 4.1f + s) * Mathf.Cos(d.z * 3.7f - s * 0.7f)
                 + 0.45f * Mathf.Sin(d.y * 5.3f + d.x * 2.1f + s * 1.3f);
        }
    }
}
