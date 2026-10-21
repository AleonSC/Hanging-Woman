using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HangingWoman
{
    /// <summary>
    /// The scare at the end of the alley. When the player gets close, the camera is taken over
    /// and tilts up the rope until she's out of frame. The rope creaks while she turns on it,
    /// and when the camera comes back down she's hanging there facing you.
    /// </summary>
    public class HangingWomanEvent : MonoBehaviour
    {
        [Header("References")]
        public FirstPersonController player;
        public HangingBody body;
        [Tooltip("Where the camera looks when it comes back down (her face).")]
        public Transform lookTarget;
        public AudioSource creakSource;
        [Tooltip("Leave empty to use a generated creak.")]
        public AudioClip creakClip;

        [Header("Trigger")]
        [Tooltip("Horizontal distance from the body that starts the event.")]
        public float triggerDistance = 3f;

        [Header("Timing")]
        public float lookUpDuration = 2.4f;
        [Tooltip("How far up the camera tilts. 90 = straight up.")]
        [Range(40f, 85f)] public float lookUpAngle = 78f;
        public float pauseBeforeCreak = 0.5f;
        public float turnDuration = 1.6f;
        public float holdAfterTurn = 0.7f;
        public float lookDownDuration = 2.0f;
        public float holdOnReveal = 2.5f;
        [Tooltip("Give control back afterwards. Turn off if the next beat takes over.")]
        public bool releaseControlAtEnd = true;

        [Header("Events")]
        public UnityEvent onTriggered;
        public UnityEvent onRevealed;

        public bool HasTriggered { get; private set; }

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<FirstPersonController>();
            if (creakClip == null) creakClip = ProceduralAudio.CreateRopeCreak();
            if (creakSource == null)
            {
                creakSource = gameObject.AddComponent<AudioSource>();
                creakSource.spatialBlend = 0.7f;
                creakSource.minDistance = 2f;
            }
            creakSource.playOnAwake = false;
        }

        void Update()
        {
            if (HasTriggered || player == null || body == null) return;
            Vector3 offset = player.transform.position - body.transform.position;
            offset.y = 0f;
            if (offset.magnitude <= triggerDistance) StartCoroutine(Sequence());
        }

        /// <summary>Starts the event immediately, wherever the player is.</summary>
        public void Trigger()
        {
            if (!HasTriggered) StartCoroutine(Sequence());
        }

        IEnumerator Sequence()
        {
            HasTriggered = true;
            player.InputLocked = true;
            onTriggered?.Invoke();

            // Up the rope, which pushes her out the bottom of the frame.
            float yawToHer = YawTowards(body.transform.position);
            yield return Look(yawToHer, -lookUpAngle, lookUpDuration);
            yield return new WaitForSeconds(pauseBeforeCreak);

            // Out of sight, she turns on the rope.
            if (creakClip != null) creakSource.PlayOneShot(creakClip, 0.8f);
            yield return body.TurnToFace(player.EyePosition, turnDuration);
            yield return new WaitForSeconds(holdAfterTurn);

            // Back down to her face.
            Vector3 target = lookTarget != null ? lookTarget.position : body.transform.position;
            yield return Look(YawTowards(target), PitchTowards(target), lookDownDuration);
            onRevealed?.Invoke();
            yield return new WaitForSeconds(holdOnReveal);

            if (releaseControlAtEnd) player.InputLocked = false;
        }

        IEnumerator Look(float yaw, float pitch, float duration)
        {
            float fromYaw = player.Yaw, fromPitch = player.Pitch;
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / duration);
                player.Yaw = Mathf.LerpAngle(fromYaw, yaw, k);
                player.Pitch = Mathf.Lerp(fromPitch, pitch, k);
                yield return null;
            }
            player.Yaw = yaw;
            player.Pitch = pitch;
        }

        float YawTowards(Vector3 point)
        {
            Vector3 d = point - player.EyePosition;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        float PitchTowards(Vector3 point)
        {
            Vector3 d = point - player.EyePosition;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            return -Mathf.Atan2(d.y, horizontal) * Mathf.Rad2Deg;
        }
    }
}
