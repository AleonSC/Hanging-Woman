using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HangingWoman
{
    /// <summary>
    /// Plays a CutsceneAsset through its own camera, taking over from the player while it runs.
    /// Press P in play mode to play the most recently added cutscene, Space to skip; or call
    /// Play() from any UnityEvent (e.g. Alley Trigger's onPlayerEnter).
    /// </summary>
    public class CutscenePlayer : MonoBehaviour
    {
        [System.Serializable] public class MarkerEvent : UnityEvent<string> { }

        public CutsceneAsset cutscene;
        public Camera cutsceneCamera;
        public bool playOnStart;
        [Tooltip("P plays this cutscene (if several have this on, the last one added wins).")]
        public bool playWithKey = true;
        public bool allowSkip = true;
        public bool loop;
        [Tooltip("Freeze the player and hide their camera while the cutscene runs.")]
        public bool takeOverPlayer = true;

        public UnityEvent onStarted;
        public UnityEvent onFinished;
        [Tooltip("Fired as each recorded marker is reached, with its label.")]
        public MarkerEvent onMarker;

        public bool IsPlaying { get; private set; }

        static readonly List<CutscenePlayer> keyPlayers = new List<CutscenePlayer>();

        float time;
        int nextMarker;
        FirstPersonController player;
        Camera playerCamera;
        bool playerWasLocked;

        void OnEnable()
        {
            if (cutsceneCamera != null) cutsceneCamera.enabled = false;
            keyPlayers.Add(this);
        }

        void OnDisable()
        {
            keyPlayers.Remove(this);
            if (IsPlaying) Stop();
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        void Update()
        {
            if (!IsPlaying)
            {
                if (playWithKey && GameInput.PlayCutscenePressed() && LastKeyPlayer() == this) Play();
                return;
            }

            if (allowSkip && GameInput.SkipPressed())
            {
                Stop();
                return;
            }

            time += Time.deltaTime;
            while (nextMarker < cutscene.markers.Count && cutscene.markers[nextMarker].time <= time)
            {
                string label = cutscene.markers[nextMarker].label;
                Debug.Log($"[Hanging Woman] Cutscene marker: {label}");
                onMarker?.Invoke(label);
                nextMarker++;
            }

            if (time >= cutscene.duration)
            {
                if (loop) { time = 0f; nextMarker = 0; }
                else { Sample(cutscene.duration); Stop(); return; }
            }
        }

        void LateUpdate()
        {
            if (IsPlaying) Sample(time);
        }

        static CutscenePlayer LastKeyPlayer()
        {
            for (int i = keyPlayers.Count - 1; i >= 0; i--)
                if (keyPlayers[i] != null && keyPlayers[i].playWithKey) return keyPlayers[i];
            return null;
        }

        public void Play()
        {
            if (cutscene == null || cutscene.cameraClip == null || cutsceneCamera == null)
            {
                Debug.LogWarning($"[Hanging Woman] {name} has no cutscene to play.");
                return;
            }
            if (IsPlaying) Stop();

            time = 0f;
            nextMarker = 0;
            IsPlaying = true;

            if (takeOverPlayer)
            {
                player = FindFirstObjectByType<FirstPersonController>();
                if (player != null)
                {
                    playerWasLocked = player.InputLocked;
                    player.InputLocked = true;
                    playerCamera = player.playerCamera;
                    if (playerCamera != null) playerCamera.enabled = false;
                }
            }
            cutsceneCamera.enabled = true;
            Sample(0f);
            onStarted?.Invoke();
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            cutsceneCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            if (player != null) player.InputLocked = playerWasLocked;
            player = null;
            playerCamera = null;
            onFinished?.Invoke();
        }

        void Sample(float t)
        {
            cutscene.cameraClip.SampleAnimation(cutsceneCamera.gameObject, Mathf.Clamp(t, 0f, cutscene.duration));
        }
    }
}
