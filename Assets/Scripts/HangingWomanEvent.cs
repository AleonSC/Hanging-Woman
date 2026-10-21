using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HangingWoman
{
    /// <summary>
    /// The scare at the end of the alley.
    /// 1. Get close and the camera is taken over and tilts up the rope until she's out of frame.
    /// 2. The rope creaks as it twists her around.
    /// 3. The camera comes back down: she's facing you. You back away a few steps.
    /// 4. The rope swings toward you, then drags her limp body after you, feet scraping
    ///    through the blood, knocking over whatever is in the way.
    /// </summary>
    public class HangingWomanEvent : MonoBehaviour
    {
        [Header("References")]
        public FirstPersonController player;
        public HangingRagdoll ragdoll;
        [Tooltip("Where the camera looks once she's turned (her face).")]
        public Transform lookTarget;
        public AudioSource creakSource;
        [Tooltip("Leave empty to use a generated creak.")]
        public AudioClip creakClip;

        [Header("Trigger")]
        [Tooltip("Horizontal distance from her that starts the event.")]
        public float triggerDistance = 3f;

        [Header("Look up / turn / look down")]
        public float lookUpDuration = 2.4f;
        [Range(40f, 85f)] public float lookUpAngle = 78f;
        public float pauseBeforeCreak = 0.5f;
        public float turnDuration = 1.8f;
        public float holdAfterTurn = 0.7f;
        public float lookDownDuration = 2.0f;

        [Header("Back away")]
        public float pauseBeforeBackingUp = 0.8f;
        [Tooltip("Direction (flat) the player backs away in, from her toward the alley mouth.")]
        public Vector3 retreatDirection = Vector3.right;
        [Tooltip("How far from her the player ends up after backing away.")]
        public float backUpTo = 4.6f;
        public float backUpDuration = 1.8f;

        [Header("Dragged toward you")]
        public float pauseBeforeDrag = 1.2f;
        public float dragSpeed = 0.5f;
        [Tooltip("She stops this far from the player.")]
        public float stopDistance = 1.1f;
        public float creakInterval = 2.6f;
        public float holdAtEnd = 3f;
        [Tooltip("Give control back afterwards. Turn off if the next beat takes over.")]
        public bool releaseControlAtEnd = true;

        [Header("Events")]
        public UnityEvent onTriggered;
        public UnityEvent onRevealed;
        public UnityEvent onArrived;

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
            if (HasTriggered || player == null || ragdoll == null) return;
            Vector3 offset = player.transform.position - ragdoll.Position;
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
            yield return Look(YawTowards(ragdoll.Position), -lookUpAngle, lookUpDuration);
            yield return new WaitForSeconds(pauseBeforeCreak);

            // Out of sight, the rope twists her around.
            Creak(1f);
            yield return ragdoll.TurnToFace(player.EyePosition, turnDuration);
            yield return new WaitForSeconds(holdAfterTurn);

            // Back down to her face.
            yield return Look(YawTowards(Face), PitchTowards(Face), lookDownDuration);
            onRevealed?.Invoke();

            // A few steps back, still staring at her.
            yield return Hold(pauseBeforeBackingUp);
            yield return BackAway();
            yield return Hold(pauseBeforeDrag);

            // The rope swings toward you and drags her after it.
            Creak(1f);
            StartCoroutine(ragdoll.DragTowards(player.transform.position, dragSpeed, stopDistance));
            float nextCreak = Time.time + creakInterval;
            while (ragdoll.IsDragging)
            {
                TrackFace(3f);
                if (Time.time >= nextCreak)
                {
                    Creak(Random.Range(0.5f, 0.85f));
                    nextCreak = Time.time + creakInterval * Random.Range(0.7f, 1.3f);
                }
                yield return null;
            }
            onArrived?.Invoke();
            yield return Hold(holdAtEnd);

            if (releaseControlAtEnd) player.InputLocked = false;
        }

        Vector3 Face => lookTarget != null ? lookTarget.position : ragdoll.Position;

        void Creak(float volume)
        {
            if (creakClip == null) return;
            creakSource.pitch = Random.Range(0.85f, 1.1f);
            creakSource.PlayOneShot(creakClip, volume);
        }

        IEnumerator BackAway()
        {
            Vector3 dir = retreatDirection;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;

            Vector3 her = ragdoll.Position;
            Vector3 start = player.transform.position;
            Vector3 end = new Vector3(her.x, start.y, her.z) + dir * backUpTo;
            for (float e = 0f; e < backUpDuration; e += Time.deltaTime)
            {
                Vector3 desired = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, e / backUpDuration));
                Vector3 delta = desired - player.transform.position;
                delta.y = 0f;
                player.ScriptedMove(delta);
                TrackFace(4f);
                yield return null;
            }
        }

        /// <summary>Waits while keeping the camera on her face.</summary>
        IEnumerator Hold(float seconds)
        {
            for (float e = 0f; e < seconds; e += Time.deltaTime)
            {
                TrackFace(3f);
                yield return null;
            }
        }

        void TrackFace(float sharpness)
        {
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            player.Yaw = Mathf.LerpAngle(player.Yaw, YawTowards(Face), k);
            player.Pitch = Mathf.Lerp(player.Pitch, PitchTowards(Face), k);
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
