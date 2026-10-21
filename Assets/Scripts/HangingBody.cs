using System.Collections;
using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// A limp body hanging from the neck pivot (this transform). It twists slowly on the rope
    /// and sways a fraction of a degree, and can be turned to face something.
    /// </summary>
    public class HangingBody : MonoBehaviour
    {
        [Tooltip("Degrees of slow twisting either way around the rope.")]
        public float twistAmount = 4f;
        public float twistPeriod = 7f;
        [Tooltip("Degrees of pendulum sway.")]
        public float swayAmount = 0.6f;
        public float swayPeriod = 3.6f;

        public bool IsTurning { get; private set; }

        float baseYaw;
        float seed;

        void Awake()
        {
            baseYaw = transform.eulerAngles.y;
            seed = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time + seed;
            float twist = Mathf.Sin(t * 2f * Mathf.PI / twistPeriod) * twistAmount;
            float swayX = Mathf.Sin(t * 2f * Mathf.PI / swayPeriod) * swayAmount;
            float swayZ = Mathf.Sin(t * 2f * Mathf.PI / (swayPeriod * 1.37f)) * swayAmount * 0.5f;
            transform.rotation = Quaternion.Euler(swayX, baseYaw + twist, swayZ);
        }

        /// <summary>Slowly rotates on the rope until the front of the body faces the point.</summary>
        public IEnumerator TurnToFace(Vector3 worldPoint, float duration)
        {
            Vector3 to = worldPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) yield break;

            float from = baseYaw;
            float target = Quaternion.LookRotation(to).eulerAngles.y;
            IsTurning = true;
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / duration);
                baseYaw = Mathf.LerpAngle(from, target, k);
                yield return null;
            }
            baseYaw = target;
            IsTurning = false;
        }
    }
}
