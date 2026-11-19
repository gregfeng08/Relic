using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// Final proportions. Parts are authored at "sheet" proportions (about 6.7 heads); this maps
    /// them to a Genshin "medium" body (about 6.2 heads: a slightly bigger head, a slender body),
    /// scaled by the appearance's height and build. Applied to vertices and to bone rest positions
    /// alike, so skinning stays exact. Below the neck the map is linear; the head is scaled
    /// uniformly about the neck; the two blend smoothly through the neck and scarf.
    /// The small proportions (the blank unwritten body) use a piecewise vertical map instead:
    /// short legs and torso, no neck, and a big round head, about 3.5 heads and 1.27 m tall.
    /// </summary>
    public sealed class HeroShape
    {
        const float BlendLow = 1.40f, BlendHigh = 1.50f;
        static readonly Vector3 Neck = new Vector3(0f, HeroRig.NeckY, 0f);

        // Small proportions: authoring height → final height at the rig's key joints
        // (ground, ankle, knee, hip joint, hips, spine, chest, shoulder, neck).
        static readonly float[] SmallFrom = { 0f, HeroRig.AnkleY, HeroRig.KneeY, HeroRig.HipJointY, HeroRig.HipY, HeroRig.SpineY, HeroRig.ChestY, HeroRig.ShoulderY, HeroRig.NeckY };
        static readonly float[] SmallTo = { 0f, 0.045f, 0.235f, 0.455f, 0.48f, 0.56f, 0.70f, 0.82f, 0.865f };
        /// <summary>Head scale of the small proportions (an authoring head of 0.26 m becomes about 0.36 m).</summary>
        public const float SmallHead = 1.385f;

        public readonly float BodyHeight, BodyWidth, BodyDepth, HeadScale, Scale;
        public readonly bool Small;

        public HeroShape(BodySpec body, bool small = false)
        {
            body ??= new BodySpec();
            Small = small;
            float build = Mathf.Clamp01(body.Build);
            Scale = Mathf.Clamp(body.Height, AppearanceRules.MinHeight, AppearanceRules.MaxHeight);
            BodyHeight = 0.96f;
            BodyWidth = small ? 1f : Mathf.Lerp(0.84f, 0.96f, build);
            BodyDepth = small ? 1f : Mathf.Lerp(0.90f, 0.98f, build);
            float head = Mathf.Clamp(body.HeadSize, AppearanceRules.MinHeadSize, AppearanceRules.MaxHeadSize);
            HeadScale = (small ? SmallHead : 1.06f) * head;
        }

        public Vector3 Body(Vector3 p)
        {
            if (!Small) return new Vector3(p.x * BodyWidth, p.y * BodyHeight, p.z * BodyDepth) * Scale;
            return new Vector3(p.x, SmallY(p.y), p.z) * Scale;
        }

        static float SmallY(float y)
        {
            int n = SmallFrom.Length;
            if (y <= SmallFrom[1]) return y * (SmallTo[1] / SmallFrom[1]);
            for (int i = 1; i < n - 1; i++)
                if (y <= SmallFrom[i + 1])
                    return Mathf.Lerp(SmallTo[i], SmallTo[i + 1], (y - SmallFrom[i]) / (SmallFrom[i + 1] - SmallFrom[i]));
            float slope = (SmallTo[n - 1] - SmallTo[n - 2]) / (SmallFrom[n - 1] - SmallFrom[n - 2]);
            return SmallTo[n - 1] + (y - SmallFrom[n - 1]) * slope;
        }

        public Vector3 HeadMap(Vector3 p) => Body(Neck) + (p - Neck) * (HeadScale * Scale);

        public Vector3 Remap(Vector3 p)
        {
            float t = Mathf.Clamp01((p.y - BlendLow) / (BlendHigh - BlendLow));
            if (t <= 0f) return Body(p);
            if (t >= 1f) return HeadMap(p);
            t = t * t * (3f - 2f * t);
            return Vector3.Lerp(Body(p), HeadMap(p), t);
        }

        /// <summary>
        /// The authoring height whose remapped (final) height on the body axis is finalY: lets a
        /// builder author shapes directly in final proportions (the map is monotonic in y).
        /// </summary>
        public float AuthorY(float finalY)
        {
            float lo = -1f, hi = 3f;
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Remap(new Vector3(0f, mid, 0f)).y < finalY) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>The map for a rigid part on this bone: never bends a prop across the neck blend.</summary>
        public System.Func<Vector3, Vector3> RigidMap(int bone)
        {
            if (bone == HeroRig.Head) return HeadMap;
            return Body;
        }

        /// <summary>Heads tall, for reports (authoring: chin 1.477, crown 1.733 + hair).</summary>
        public float HeadsTall
        {
            get
            {
                float chin = HeroRig.HeadCenterY - HeroRig.HeadHalfH, crown = HeroRig.HeadCenterY + HeroRig.HeadHalfH;
                float head = HeadMap(new Vector3(0f, crown, 0f)).y - HeadMap(new Vector3(0f, chin, 0f)).y;
                return HeadMap(new Vector3(0f, crown, 0f)).y / head;
            }
        }

        /// <summary>Standing height (crown), meters.</summary>
        public float Height => HeadMap(new Vector3(0f, HeroRig.HeadCenterY + HeroRig.HeadHalfH, 0f)).y;
    }
}
