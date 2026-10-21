using System.Collections;
using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// A physics ragdoll pinned by the neck to a rope. The rope's lower end is a kinematic
    /// anchor this script moves around; the body itself is never animated, it just hangs off
    /// the anchor, so anything it does is gravity and momentum.
    ///
    /// The rope is drawn from the neck up to a point high in the sky. When dragging, that top
    /// end swings toward the destination first, then the anchor follows and pulls her along.
    /// </summary>
    public class HangingRagdoll : MonoBehaviour
    {
        [Tooltip("Kinematic body at the end of the rope. The chest is jointed to it.")]
        public Rigidbody anchor;
        public Rigidbody chest;
        public Transform head;
        [Tooltip("Point on the neck where the rope leaves the noose.")]
        public Transform ropeAttach;
        [Tooltip("Cylinder stretched between the neck and the sky each frame.")]
        public Transform ropeVisual;
        public float ropeTopHeight = 140f;
        public float ropeRadius = 0.024f;
        public int solverIterations = 20;

        public bool IsDragging { get; private set; }
        public bool IsTurning { get; private set; }
        public Vector3 Position => chest != null ? chest.position : transform.position;

        Vector3 skyOffset;
        Rigidbody[] bodies;

        void Awake()
        {
            bodies = GetComponentsInChildren<Rigidbody>();
            foreach (var rb in bodies)
            {
                rb.solverIterations = solverIterations;
                rb.solverVelocityIterations = Mathf.Max(4, solverIterations / 3);
            }

            // Limbs overlap at the joints; let them pass through each other rather than fight.
            var colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                for (int j = i + 1; j < colliders.Length; j++)
                    Physics.IgnoreCollision(colliders[i], colliders[j]);
        }

        void LateUpdate()
        {
            if (ropeVisual == null || ropeAttach == null || anchor == null) return;
            Vector3 bottom = ropeAttach.position;
            Vector3 top = new Vector3(anchor.position.x + skyOffset.x, ropeTopHeight, anchor.position.z + skyOffset.z);
            Vector3 span = top - bottom;
            ropeVisual.position = bottom + span * 0.5f;
            ropeVisual.rotation = Quaternion.FromToRotation(Vector3.up, span);
            ropeVisual.localScale = new Vector3(ropeRadius, span.magnitude * 0.5f, ropeRadius);
        }

        void WakeAll()
        {
            foreach (var rb in bodies) rb.WakeUp();
        }

        /// <summary>Twists the rope until her front faces the point. The limp body turns with it.</summary>
        public IEnumerator TurnToFace(Vector3 point, float duration)
        {
            Vector3 to = point - anchor.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) yield break;

            IsTurning = true;
            Quaternion from = anchor.rotation;
            Quaternion target = Quaternion.LookRotation(to);
            for (float e = 0f; e < duration; e += Time.fixedDeltaTime)
            {
                anchor.MoveRotation(Quaternion.Slerp(from, target, Mathf.SmoothStep(0f, 1f, e / duration)));
                WakeAll();
                yield return new WaitForFixedUpdate();
            }
            anchor.MoveRotation(target);
            IsTurning = false;
        }

        /// <summary>
        /// Swings the top of the rope toward the point, then drags her along the ground until
        /// she's `stopDistance` away from it, then lets the rope settle back to vertical.
        /// </summary>
        public IEnumerator DragTowards(Vector3 point, float speed, float stopDistance, float ropeLead = 22f, float leadTime = 1.4f)
        {
            Vector3 dir = point - anchor.position;
            dir.y = 0f;
            float total = dir.magnitude - stopDistance;
            if (total <= 0f) yield break;
            dir.Normalize();
            IsDragging = true;

            // The rope moves first.
            for (float e = 0f; e < leadTime; e += Time.fixedDeltaTime)
            {
                skyOffset = dir * ropeLead * Mathf.SmoothStep(0f, 1f, e / leadTime);
                yield return new WaitForFixedUpdate();
            }

            // Then it takes her with it: a slow start, a steady pull, easing off at the end.
            float travelled = 0f, velocity = 0f;
            while (travelled < total)
            {
                float remaining = total - travelled;
                float targetSpeed = speed * Mathf.Clamp(remaining / 0.8f, 0.15f, 1f);
                velocity = Mathf.MoveTowards(velocity, targetSpeed, speed * 0.8f * Time.fixedDeltaTime);
                float step = Mathf.Min(velocity * Time.fixedDeltaTime, remaining);
                travelled += step;
                anchor.MovePosition(anchor.position + dir * step);
                WakeAll();
                yield return new WaitForFixedUpdate();
            }

            // The rope swings back upright above her.
            Vector3 lead = skyOffset;
            for (float e = 0f; e < 1.8f; e += Time.fixedDeltaTime)
            {
                skyOffset = Vector3.Lerp(lead, Vector3.zero, Mathf.SmoothStep(0f, 1f, e / 1.8f));
                yield return new WaitForFixedUpdate();
            }
            skyOffset = Vector3.zero;
            IsDragging = false;
        }
    }
}
