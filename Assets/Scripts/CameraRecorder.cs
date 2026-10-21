using System;
using System.IO;
using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// Records the camera while you play so it can be turned into a cutscene
    /// (Hanging Woman > Cutscenes in the editor).
    /// R starts/stops recording, M drops a marker, the scroll wheel zooms smoothly.
    /// Takes are saved to Assets/Recordings/take_NN.json (persistentDataPath in a build).
    /// </summary>
    public class CameraRecorder : MonoBehaviour
    {
        public Camera targetCamera;
        [Tooltip("Samples per second. The cutscene is smoothed afterwards, so 30 is plenty.")]
        public float sampleRate = 30f;
        public bool showHud = true;

        [Header("Zoom")]
        public bool zoomOnlyWhileRecording = true;
        [Tooltip("Ease back to the normal view when a recording stops.")]
        public bool resetZoomWhenStopped = true;
        public float degreesPerNotch = 5f;
        public float minFov = 12f;
        public float maxFov = 90f;
        [Tooltip("Seconds the zoom takes to catch up with the wheel.")]
        public float zoomSmoothTime = 0.18f;

        public bool IsRecording { get; private set; }

        CameraTake take;
        float startTime, nextSample, baseFov, targetFov, fovVelocity;
        string lastSaved;
        float lastSavedShownUntil;

        void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) { enabled = false; return; }
            baseFov = targetFov = targetCamera.fieldOfView;
        }

        void Update()
        {
            if (GameInput.RecordPressed())
            {
                if (IsRecording) StopRecording();
                else StartRecording();
            }
            if (IsRecording && GameInput.MarkerPressed()) AddMarker();

            float scroll = GameInput.Scroll();
            if (Mathf.Abs(scroll) > 0.001f && (IsRecording || !zoomOnlyWhileRecording))
                targetFov = Mathf.Clamp(targetFov - scroll * degreesPerNotch, minFov, maxFov);

            targetCamera.fieldOfView = Mathf.SmoothDamp(targetCamera.fieldOfView, targetFov, ref fovVelocity, zoomSmoothTime);
        }

        void LateUpdate()
        {
            // After the controller has placed the camera for this frame.
            if (!IsRecording) return;
            float t = Time.time - startTime;
            if (t < nextSample) return;
            nextSample = t + 1f / Mathf.Max(1f, sampleRate);
            var cam = targetCamera.transform;
            take.samples.Add(new CameraSample { t = t, position = cam.position, rotation = cam.rotation, fov = targetCamera.fieldOfView });
        }

        public void StartRecording()
        {
            take = new CameraTake
            {
                scene = gameObject.scene.name,
                recordedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sampleRate = sampleRate,
            };
            startTime = Time.time;
            nextSample = 0f;
            IsRecording = true;
        }

        public void AddMarker(string label = null)
        {
            if (!IsRecording) return;
            take.markers.Add(new CutsceneMarker { time = Time.time - startTime, label = label ?? $"Marker {take.markers.Count + 1}" });
        }

        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
            if (resetZoomWhenStopped) targetFov = baseFov;
            if (take.samples.Count < 2) return;
            lastSaved = Save(take);
            lastSavedShownUntil = Time.time + 4f;
        }

        void OnDisable()
        {
            StopRecording();
        }

        static string Save(CameraTake take)
        {
#if UNITY_EDITOR
            string folder = Path.Combine(Application.dataPath, "Recordings");
#else
            string folder = Path.Combine(Application.persistentDataPath, "Recordings");
#endif
            Directory.CreateDirectory(folder);
            int n = 1;
            while (File.Exists(Path.Combine(folder, $"take_{n:00}.json"))) n++;
            take.name = $"take_{n:00}";
            string path = Path.Combine(folder, take.name + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(take, true));
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.ImportAsset("Assets/Recordings/" + take.name + ".json");
#endif
            Debug.Log($"[Hanging Woman] Saved {take.samples.Count} samples ({take.Duration:0.0}s, {take.markers.Count} markers) to {path}");
            return take.name;
        }

        void OnGUI()
        {
            if (!showHud) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            const float x = 12f, y = 10f;

            if (IsRecording)
            {
                bool blink = Mathf.Repeat(Time.time, 1f) < 0.6f;
                style.normal.textColor = new Color(1f, 0.25f, 0.2f, 1f);
                GUI.Label(new Rect(x, y, 420, 24), $"{(blink ? "●" : "  ")} REC  {Time.time - startTime:0.0}s   markers {take.markers.Count}   zoom {targetCamera.fieldOfView:0}°", style);
                style.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
                GUI.Label(new Rect(x, y + 20, 420, 24), "R stop · M marker · scroll zoom", style);
            }
            else if (Time.time < lastSavedShownUntil)
            {
                GUI.Label(new Rect(x, y, 520, 24), $"Saved {lastSaved}. Make a cutscene: Hanging Woman > Cutscenes", style);
            }
            else
            {
                style.normal.textColor = new Color(1f, 1f, 1f, 0.35f);
                GUI.Label(new Rect(x, y, 420, 24), "R record camera", style);
            }
        }
    }
}
