using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// Put on a foot (or the bone the foot belongs to). Whenever the sole scrapes along the
    /// ground it leaves a smear of blood behind it.
    /// </summary>
    public class FootBloodTrail : MonoBehaviour
    {
        public Material smearMaterial;
        [Tooltip("Sole position in this transform's local space.")]
        public Vector3 soleLocalPoint = new Vector3(0f, -0.46f, 0.05f);
        public float groundHeight;
        [Tooltip("How close to the ground the sole must be to count as touching.")]
        public float contactHeight = 0.08f;
        [Tooltip("Distance the foot must drag before laying down another smear.")]
        public float spacing = 0.1f;
        public Vector2 widthRange = new Vector2(0.07f, 0.11f);
        public int maxSmears = 300;

        static int stagger;
        Vector3 last;
        bool touching;
        int count;
        Transform container;

        void Start()
        {
            container = new GameObject(name + " Blood").transform;
        }

        void FixedUpdate()
        {
            if (smearMaterial == null || count >= maxSmears) return;

            Vector3 sole = transform.TransformPoint(soleLocalPoint);
            if (sole.y > groundHeight + contactHeight)
            {
                touching = false;
                return;
            }

            Vector3 flat = new Vector3(sole.x, 0f, sole.z);
            if (!touching)
            {
                touching = true;
                last = flat;
                return;
            }

            Vector3 delta = flat - last;
            float length = delta.magnitude;
            if (length < spacing) return;

            Smear(last + delta * 0.5f, delta, length);
            last = flat;
        }

        void Smear(Vector3 center, Vector3 along, float length)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Drag Smear";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(container, false);

            // After Euler(90, yaw, 0) a quad's local X points along (cos yaw, 0, -sin yaw).
            float yaw = Mathf.Atan2(-along.z, along.x) * Mathf.Rad2Deg;
            float y = groundHeight + 0.007f + (stagger++ % 12) * 0.0004f;
            quad.transform.SetPositionAndRotation(new Vector3(center.x, y, center.z), Quaternion.Euler(90f, yaw, 0f));
            quad.transform.localScale = new Vector3(length * 1.5f, Random.Range(widthRange.x, widthRange.y), 1f);

            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = smearMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            count++;
        }
    }
}
