using System;
using System.Collections.Generic;
using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// One cross-section of a loft: an ellipse (or superellipse, Square &gt; 2) centered at C in
    /// the plane spanned by X (right) and Z (front). Angle 0 is the front (+Z), pi/2 is +X.
    /// </summary>
    public struct Ring
    {
        public Vector3 C, X, Z;
        public float Rx, Rz;
        /// <summary>Superellipse exponent: 2 = ellipse, 4..8 = rounded box.</summary>
        public float Square;

        public Ring(Vector3 c, float rx, float rz, float square = 2f)
        {
            C = c; X = Vector3.right; Z = Vector3.forward; Rx = rx; Rz = rz; Square = square;
        }

        public Ring(Vector3 c, Vector3 x, Vector3 z, float rx, float rz, float square = 2f)
        {
            C = c; X = x; Z = z; Rx = rx; Rz = rz; Square = square;
        }

        public Vector3 At(float angle, float inset = 0f)
        {
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            if (Square != 2f)
            {
                float e = 2f / Square;
                c = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
                s = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e);
            }
            return C + Z * (c * Mathf.Max(0.0005f, Rz - inset)) + X * (s * Mathf.Max(0.0005f, Rx - inset));
        }
    }

    /// <summary>
    /// Builds the hero's single skinned mesh: vertices in avatar-root space, one submesh per
    /// material, bone weights from a position callback. Every shape shares vertices along its
    /// surface so normals are smooth and the inverted-hull outline has no cracks. Triangles are
    /// oriented per quad against a known outward direction, so callers never think about winding.
    /// </summary>
    public sealed class MeshKit
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<BoneWeight> _w = new List<BoneWeight>();
        readonly List<List<int>> _subs = new List<List<int>>();
        readonly List<Material> _mats = new List<Material>();
        List<int> _tris;

        /// <summary>Applied to every vertex added (part-local → root space).</summary>
        public Matrix4x4 Xf = Matrix4x4.identity;
        /// <summary>Bone weights for a root-space position.</summary>
        public Func<Vector3, BoneWeight> Weigh = p => W1(0);

        public int VertexCount => _v.Count;
        public int TriangleCount { get { int n = 0; foreach (var s in _subs) n += s.Count; return n / 3; } }

        public static BoneWeight W1(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1f };

        public static BoneWeight W2(int a, int b, float t)
        {
            t = Mathf.Clamp01(t);
            if (a == b || t <= 0f) return W1(a);
            if (t >= 1f) return W1(b);
            // Heaviest influence first.
            return t <= 0.5f
                ? new BoneWeight { boneIndex0 = a, weight0 = 1f - t, boneIndex1 = b, weight1 = t }
                : new BoneWeight { boneIndex0 = b, weight0 = t, boneIndex1 = a, weight1 = 1f - t };
        }

        /// <summary>Mixes two weight sets (t = share of b), keeping the four heaviest influences.</summary>
        public static BoneWeight Blend(BoneWeight a, BoneWeight b, float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            var idx = new int[8];
            var w = new float[8];
            int n = 0;
            void Add(int bone, float weight)
            {
                if (weight <= 0f) return;
                for (int i = 0; i < n; i++) if (idx[i] == bone) { w[i] += weight; return; }
                idx[n] = bone; w[n] = weight; n++;
            }
            Add(a.boneIndex0, a.weight0 * (1f - t)); Add(a.boneIndex1, a.weight1 * (1f - t));
            Add(a.boneIndex2, a.weight2 * (1f - t)); Add(a.boneIndex3, a.weight3 * (1f - t));
            Add(b.boneIndex0, b.weight0 * t); Add(b.boneIndex1, b.weight1 * t);
            Add(b.boneIndex2, b.weight2 * t); Add(b.boneIndex3, b.weight3 * t);
            // Sort by weight, descending.
            for (int i = 1; i < n; i++)
                for (int j = i; j > 0 && w[j] > w[j - 1]; j--)
                {
                    float tw = w[j]; w[j] = w[j - 1]; w[j - 1] = tw;
                    int ti = idx[j]; idx[j] = idx[j - 1]; idx[j - 1] = ti;
                }
            var r = new BoneWeight();
            if (n > 0) { r.boneIndex0 = idx[0]; r.weight0 = w[0]; }
            if (n > 1) { r.boneIndex1 = idx[1]; r.weight1 = w[1]; }
            if (n > 2) { r.boneIndex2 = idx[2]; r.weight2 = w[2]; }
            if (n > 3) { r.boneIndex3 = idx[3]; r.weight3 = w[3]; }
            return r;
        }

        public void Use(Material m)
        {
            int i = _mats.IndexOf(m);
            if (i < 0) { _mats.Add(m); _subs.Add(new List<int>()); i = _mats.Count - 1; }
            _tris = _subs[i];
        }

        public void Rigid(int bone) { Weigh = p => W1(bone); }

        public int Vert(Vector3 local)
        {
            Vector3 p = Xf.MultiplyPoint3x4(local);
            _v.Add(p);
            _w.Add(Weigh(p));
            return _v.Count - 1;
        }

        public int Vert(Vector3 local, BoneWeight w)
        {
            _v.Add(Xf.MultiplyPoint3x4(local));
            _w.Add(w);
            return _v.Count - 1;
        }

        /// <summary>
        /// A cloth sheet with thickness (coat panels, collars, plates): an outer grid, an inner
        /// grid offset against <paramref name="outward"/>, and edge strips on all four borders, so
        /// it's closed and outlines cleanly. pos/outward/weight are evaluated per grid point (row i, col j).
        /// </summary>
        public void Sheet(int rows, int cols, Func<int, int, Vector3> pos, Func<int, int, Vector3> outward, float thickness, Func<int, int, BoneWeight> weight)
        {
            int n = (rows + 1) * (cols + 1);
            int outer = _v.Count;
            for (int i = 0; i <= rows; i++)
                for (int j = 0; j <= cols; j++)
                    Vert(pos(i, j), weight(i, j));
            int inner = _v.Count;
            for (int i = 0; i <= rows; i++)
                for (int j = 0; j <= cols; j++)
                    Vert(pos(i, j) - outward(i, j).normalized * thickness, weight(i, j));
            int Id(int baseIndex, int i, int j) => baseIndex + i * (cols + 1) + j;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    Vector3 o = outward(i, j) + outward(i + 1, j + 1);
                    Quad(Id(outer, i, j), Id(outer, i, j + 1), Id(outer, i + 1, j + 1), Id(outer, i + 1, j), o);
                    Quad(Id(inner, i, j), Id(inner, i, j + 1), Id(inner, i + 1, j + 1), Id(inner, i + 1, j), -o);
                }
            // Edges: outward = away from the sheet's interior along the surface.
            for (int i = 0; i < rows; i++)
            {
                Vector3 e0 = pos(i, 0) - pos(i, 1), e1 = pos(i, cols) - pos(i, cols - 1);
                Quad(Id(outer, i, 0), Id(outer, i + 1, 0), Id(inner, i + 1, 0), Id(inner, i, 0), e0);
                Quad(Id(outer, i, cols), Id(outer, i + 1, cols), Id(inner, i + 1, cols), Id(inner, i, cols), e1);
            }
            for (int j = 0; j < cols; j++)
            {
                Vector3 e0 = pos(0, j) - pos(1, j), e1 = pos(rows, j) - pos(rows - 1, j);
                Quad(Id(outer, 0, j), Id(outer, 0, j + 1), Id(inner, 0, j + 1), Id(inner, 0, j), e0);
                Quad(Id(outer, rows, j), Id(outer, rows, j + 1), Id(inner, rows, j + 1), Id(inner, rows, j), e1);
            }
        }

        /// <summary>Adds a triangle facing <paramref name="outward"/> (local space).</summary>
        public void Tri(int a, int b, int c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
            Vector3 o = Xf.MultiplyVector(outward);
            if (Vector3.Dot(n, o) < 0f) { int t = b; b = c; c = t; }
            _tris.Add(a); _tris.Add(b); _tris.Add(c);
        }

        public void Quad(int a, int b, int c, int d, Vector3 outward)
        {
            Tri(a, b, c, outward);
            Tri(a, c, d, outward);
        }

        /// <summary>
        /// Lofts rings into a tube. With an arc (a0..a1, not closed) it's an open sheet; set
        /// inward to face the inside (shell linings). Caps are fans to a pole (smooth, outline-safe).
        /// Returns the index of the first vertex (rings are laid out row by row, cols = sides + (closed ? 0 : 1)).
        /// </summary>
        public int Loft(IList<Ring> rings, int sides, bool capStart, bool capEnd, float a0 = 0f, float a1 = Mathf.PI * 2f, bool closed = true, bool inward = false, float inset = 0f)
        {
            int cols = closed ? sides : sides + 1;
            int first = _v.Count;
            for (int i = 0; i < rings.Count; i++)
                for (int j = 0; j < cols; j++)
                    Vert(rings[i].At(a0 + (a1 - a0) * j / sides, inset));
            for (int i = 0; i < rings.Count - 1; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int j1 = closed ? (j + 1) % cols : j + 1;
                    int a = first + i * cols + j, b = first + i * cols + j1;
                    int c = first + (i + 1) * cols + j1, d = first + (i + 1) * cols + j;
                    Vector3 mid = (Local(a) + Local(c)) * 0.5f;
                    Vector3 center = (rings[i].C + rings[i + 1].C) * 0.5f;
                    Vector3 o = mid - center;
                    Quad(a, b, c, d, inward ? -o : o);
                }
            }
            if (capStart) Cap(rings[0], first, cols, sides, closed, rings.Count > 1 ? rings[0].C - rings[1].C : -Vector3.up);
            if (capEnd)
            {
                int n = rings.Count;
                Cap(rings[n - 1], first + (n - 1) * cols, cols, sides, closed, n > 1 ? rings[n - 1].C - rings[n - 2].C : Vector3.up);
            }
            return first;
        }

        void Cap(Ring r, int rowStart, int cols, int sides, bool closed, Vector3 dir)
        {
            dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.up;
            float bulge = Mathf.Min(r.Rx, r.Rz) * 0.35f;
            int pole = Vert(r.C + dir * bulge);
            for (int j = 0; j < sides; j++)
            {
                int j1 = closed ? (j + 1) % cols : j + 1;
                Tri(rowStart + j, rowStart + j1, pole, dir);
            }
        }

        Vector3 Local(int index) => Xf.inverse.MultiplyPoint3x4(_v[index]);

        /// <summary>An ellipsoid (pole to pole along Y), optionally squashed into a rounded box.</summary>
        public void Ellipsoid(Vector3 c, Vector3 r, int sides = 12, int stacks = 8, float square = 2f)
        {
            var rings = new List<Ring>();
            for (int i = 1; i < stacks; i++)
            {
                float t = Mathf.PI * i / stacks;
                float y = -Mathf.Cos(t), k = Mathf.Sin(t);
                rings.Add(new Ring(c + Vector3.up * (y * r.y), r.x * k, r.z * k, square));
            }
            Loft(rings, sides, true, true);
        }

        /// <summary>A tapered tube between two points, with rounded caps.</summary>
        public void Tube(Vector3 a, Vector3 b, float ra, float rb, int sides = 8, bool caps = true, float flatten = 1f)
        {
            Vector3 axis = b - a;
            Frame(axis, out Vector3 x, out Vector3 z);
            var rings = new List<Ring> { new Ring(a, x, z, ra, ra * flatten), new Ring(b, x, z, rb, rb * flatten) };
            Loft(rings, sides, caps, caps);
        }

        /// <summary>A tube along a polyline with per-point radii (and an optional flatten along the frame's Z).</summary>
        public void Path(IList<Vector3> pts, IList<float> radii, int sides, float flatten = 1f, Vector3? up = null, bool caps = true, float square = 2f)
        {
            var rings = new List<Ring>();
            Vector3 hint = up ?? Vector3.forward;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 t = (i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : pts[i + 1] - pts[i - 1]).normalized;
                Vector3 x = Vector3.Cross(hint, t);
                if (x.sqrMagnitude < 1e-6f) x = Vector3.Cross(Vector3.right, t);
                x.Normalize();
                Vector3 z = Vector3.Cross(t, x).normalized;
                rings.Add(new Ring(pts[i], x, z, radii[i], radii[i] * flatten, square));
            }
            Loft(rings, sides, caps, caps);
        }

        /// <summary>Lathe around a vertical axis at base: profile = (radius, height) pairs, bottom to top.</summary>
        public void Lathe(Vector3 basePos, IList<Vector2> profile, int sides = 10, float square = 2f, float squashZ = 1f)
        {
            var rings = new List<Ring>();
            foreach (Vector2 p in profile) rings.Add(new Ring(basePos + Vector3.up * p.y, p.x, p.x * squashZ, square));
            Loft(rings, sides, true, true);
        }

        /// <summary>A rounded box (superellipse loft along Y).</summary>
        public void Box(Vector3 c, Vector3 size, float square = 6f)
        {
            Vector3 h = size * 0.5f;
            float bev = Mathf.Min(h.x, h.z) * 0.15f;
            var rings = new List<Ring>
            {
                new Ring(c + Vector3.up * -h.y, h.x - bev, h.z - bev, square),
                new Ring(c + Vector3.up * (-h.y + bev), h.x, h.z, square),
                new Ring(c + Vector3.up * (h.y - bev), h.x, h.z, square),
                new Ring(c + Vector3.up * h.y, h.x - bev, h.z - bev, square),
            };
            Loft(rings, 8, true, true);
        }

        /// <summary>A ring (torus) around axis, e.g. belts, cuffs, scarf wraps.</summary>
        public void Torus(Vector3 c, Vector3 axis, float radiusX, float radiusZ, float minor, int major = 16, int minorSides = 6, float flatten = 1f)
        {
            Frame(axis, out Vector3 x, out Vector3 z);
            Vector3 up = axis.normalized;
            int first = _v.Count;
            // A truly closed loop (no seam): rings wrap around, so normals are smooth all the way.
            for (int i = 0; i < major; i++)
            {
                float a = Mathf.PI * 2f * i / major;
                Vector3 radial = (z * Mathf.Cos(a) * radiusZ + x * Mathf.Sin(a) * radiusX);
                Vector3 center = c + radial;
                Vector3 outDir = radial.normalized;
                for (int j = 0; j < minorSides; j++)
                {
                    float b = Mathf.PI * 2f * j / minorSides;
                    Vert(center + outDir * (Mathf.Cos(b) * minor) + up * (Mathf.Sin(b) * minor * flatten));
                }
            }
            for (int i = 0; i < major; i++)
            {
                int i1 = (i + 1) % major;
                float a = Mathf.PI * 2f * (i + 0.5f) / major;
                Vector3 center = c + z * Mathf.Cos(a) * radiusZ + x * Mathf.Sin(a) * radiusX;
                for (int j = 0; j < minorSides; j++)
                {
                    int j1 = (j + 1) % minorSides;
                    int p0 = first + i * minorSides + j, p1 = first + i * minorSides + j1;
                    int p2 = first + i1 * minorSides + j1, p3 = first + i1 * minorSides + j;
                    Vector3 mid = (Local(p0) + Local(p2)) * 0.5f;
                    Quad(p0, p1, p2, p3, mid - center);
                }
            }
        }

        /// <summary>Catmull-Rom through pts with perSeg samples per segment (includes both ends).</summary>
        public static List<Vector3> Spline(IList<Vector3> pts, int perSeg)
        {
            var o = new List<Vector3>();
            int n = pts.Count;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(n - 1, i + 2)];
                for (int k = 0; k < perSeg; k++)
                {
                    float t = (float)k / perSeg, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            o.Add(pts[n - 1]);
            return o;
        }

        public static void Frame(Vector3 axis, out Vector3 x, out Vector3 z)
        {
            Vector3 t = axis.normalized;
            Vector3 hint = Mathf.Abs(Vector3.Dot(t, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward;
            x = Vector3.Cross(hint, t).normalized;
            z = Vector3.Cross(t, x).normalized;
        }

        public Mesh Build(string name, out Material[] materials)
        {
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.boneWeights = NormalizedWeights();
            mesh.subMeshCount = _subs.Count;
            for (int i = 0; i < _subs.Count; i++) mesh.SetTriangles(_subs[i], i, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            materials = _mats.ToArray();
            return mesh;
        }

        BoneWeight[] NormalizedWeights()
        {
            var arr = new BoneWeight[_w.Count];
            for (int i = 0; i < arr.Length; i++)
            {
                BoneWeight b = _w[i];
                float s = b.weight0 + b.weight1 + b.weight2 + b.weight3;
                if (s <= 1e-6f) b = W1(0);
                else { b.weight0 /= s; b.weight1 /= s; b.weight2 /= s; b.weight3 /= s; }
                arr[i] = b;
            }
            return arr;
        }
    }
}
