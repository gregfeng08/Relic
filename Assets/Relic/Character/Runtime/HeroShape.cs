using UnityEngine;

namespace Relic.Character
{
    /// <summary>
    /// Final proportions. Parts are authored at "sheet" proportions (about 6.7 heads); this maps
    /// them to a Genshin "medium" body (about 6.2 heads: a slightly bigger head, a slender body),
    /// scaled by the appearance's height and build. Applied to vertices and to bone rest positions
    /// alike, so skinning stays exact. Below the neck the map is linear; the head is scaled
    /// uniformly about the neck; the two blend smoothly through the neck and scarf.
    /// </summary>
    public sealed class HeroShape
    {
        const float BlendLow = 1.40f, BlendHigh = 1.50f;
        static readonly Vector3 Neck = new Vector3(0f, HeroRig.NeckY, 0f);

        public readonly float BodyHeight, BodyWidth, BodyDepth, HeadScale, Scale;

        public HeroShape(BodySpec body)
        {
            body ??= new BodySpec();
            float build = Mathf.Clamp01(body.Build);
            Scale = Mathf.Clamp(body.Height, AppearanceRules.MinHeight, AppearanceRules.MaxHeight);
            BodyHeight = 0.96f;
            BodyWidth = Mathf.Lerp(0.84f, 0.96f, build);
            BodyDepth = Mathf.Lerp(0.90f, 0.98f, build);
            HeadScale = 1.06f;
        }

        public Vector3 Body(Vector3 p) => new Vector3(p.x * BodyWidth, p.y * BodyHeight, p.z * BodyDepth) * Scale;

        public Vector3 HeadMap(Vector3 p) => Body(Neck) + (p - Neck) * (HeadScale * Scale);

        public Vector3 Remap(Vector3 p)
        {
            float t = Mathf.Clamp01((p.y - BlendLow) / (BlendHigh - BlendLow));
            if (t <= 0f) return Body(p);
            if (t >= 1f) return HeadMap(p);
            t = t * t * (3f - 2f * t);
            return Vector3.Lerp(Body(p), HeadMap(p), t);
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
    }
}
