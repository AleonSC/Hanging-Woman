using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HangingWoman.EditorTools
{
    /// <summary>
    /// Turns camera recordings (Assets/Recordings/*.json, made with the R key in play mode)
    /// into replayable cutscenes: a smoothed AnimationClip of the camera, a CutsceneAsset that
    /// holds it with the markers, and a CutscenePlayer in the open scene.
    /// </summary>
    public static class CutsceneTools
    {
        const string RecordingsDir = "Assets/Recordings";
        const string CutscenesDir = "Assets/Cutscenes";

        [MenuItem("Hanging Woman/Cutscenes/Create Cutscene From Latest Recording", priority = 40)]
        static void CreateFromLatest()
        {
            var latest = Directory.Exists(RecordingsDir)
                ? Directory.GetFiles(RecordingsDir, "*.json").OrderBy(File.GetLastWriteTimeUtc).LastOrDefault()
                : null;
            if (latest == null)
            {
                EditorUtility.DisplayDialog("Hanging Woman", "No recordings yet.\n\nPress Play, then R to start and stop recording the camera.", "OK");
                return;
            }
            CreateFromRecording(AssetDatabase.LoadAssetAtPath<TextAsset>(latest.Replace('\\', '/')));
        }

        [MenuItem("Assets/Hanging Woman/Create Cutscene From Recording", false, 40)]
        static void CreateFromSelected()
        {
            foreach (var text in Selection.GetFiltered<TextAsset>(SelectionMode.Assets)) CreateFromRecording(text);
        }

        [MenuItem("Assets/Hanging Woman/Create Cutscene From Recording", true)]
        static bool CanCreateFromSelected() =>
            Selection.GetFiltered<TextAsset>(SelectionMode.Assets).Any(t => AssetDatabase.GetAssetPath(t).EndsWith(".json"));

        [MenuItem("Hanging Woman/Cutscenes/Open Recordings Folder", priority = 41)]
        static void ShowRecordings()
        {
            Directory.CreateDirectory(RecordingsDir);
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(RecordingsDir));
        }

        public static CutsceneAsset CreateFromRecording(TextAsset recording)
        {
            if (recording == null) return null;
            Directory.CreateDirectory(CutscenesDir);
            string name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(recording));
            string assetPath = $"{CutscenesDir}/{name}.asset";

            var cutscene = AssetDatabase.LoadAssetAtPath<CutsceneAsset>(assetPath);
            if (cutscene == null)
            {
                cutscene = ScriptableObject.CreateInstance<CutsceneAsset>();
                cutscene.sourceRecording = recording;
                AssetDatabase.CreateAsset(cutscene, assetPath);
            }
            cutscene.sourceRecording = recording;
            if (!Rebuild(cutscene)) return null;

            AddToScene(cutscene, makeDefault: true);
            Selection.activeObject = cutscene;
            EditorGUIUtility.PingObject(cutscene);
            Debug.Log($"[Hanging Woman] Created cutscene {assetPath} ({cutscene.duration:0.0}s). Press Play, then P to watch it.");
            return cutscene;
        }

        /// <summary>Regenerates the camera clip from the recording using the cutscene's smoothing settings.</summary>
        public static bool Rebuild(CutsceneAsset cutscene)
        {
            if (cutscene.sourceRecording == null)
            {
                Debug.LogWarning($"[Hanging Woman] {cutscene.name} has no source recording.");
                return false;
            }
            var take = JsonUtility.FromJson<CameraTake>(cutscene.sourceRecording.text);
            if (take == null || take.samples == null || take.samples.Count < 2)
            {
                Debug.LogWarning($"[Hanging Woman] {cutscene.sourceRecording.name} doesn't contain a usable recording.");
                return false;
            }

            var samples = take.samples;
            float t0 = samples[0].t;
            int n = samples.Count;
            var times = new float[n];
            for (int i = 0; i < n; i++) times[i] = samples[i].t - t0;

            var positions = Smooth(times, samples.Select(s => s.position).ToArray(), cutscene.positionSmoothing);
            var rotations = SmoothRotations(times, samples.Select(s => s.rotation).ToArray(), cutscene.rotationSmoothing);
            var fovs = Smooth(times, samples.Select(s => new Vector3(s.fov, 0f, 0f)).ToArray(), cutscene.zoomSmoothing);

            string clipPath = AssetDatabase.GetAssetPath(cutscene).Replace(".asset", ".anim");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            clip.ClearCurves();
            clip.frameRate = Mathf.Max(30f, take.sampleRate);

            SetCurve(clip, typeof(Transform), "m_LocalPosition.x", times, i => positions[i].x);
            SetCurve(clip, typeof(Transform), "m_LocalPosition.y", times, i => positions[i].y);
            SetCurve(clip, typeof(Transform), "m_LocalPosition.z", times, i => positions[i].z);
            SetCurve(clip, typeof(Transform), "m_LocalRotation.x", times, i => rotations[i].x);
            SetCurve(clip, typeof(Transform), "m_LocalRotation.y", times, i => rotations[i].y);
            SetCurve(clip, typeof(Transform), "m_LocalRotation.z", times, i => rotations[i].z);
            SetCurve(clip, typeof(Transform), "m_LocalRotation.w", times, i => rotations[i].w);
            SetCurve(clip, typeof(Camera), "field of view", times, i => fovs[i].x);
            EditorUtility.SetDirty(clip);

            cutscene.cameraClip = clip;
            cutscene.duration = times[n - 1];
            cutscene.markers = (take.markers ?? new List<CutsceneMarker>())
                .Select(m => new CutsceneMarker { time = Mathf.Max(0f, m.time - t0), label = m.label })
                .OrderBy(m => m.time).ToList();
            EditorUtility.SetDirty(cutscene);
            AssetDatabase.SaveAssets();
            return true;
        }

        static void SetCurve(AnimationClip clip, System.Type type, string property, float[] times, System.Func<int, float> value)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < times.Length; i++) curve.AddKey(new Keyframe(times[i], value(i)));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", type, property), curve);
        }

        /// <summary>Gaussian smoothing over time (handles uneven sample spacing).</summary>
        static Vector3[] Smooth(float[] times, Vector3[] values, float sigma)
        {
            if (sigma <= 0.0001f) return values;
            var result = new Vector3[values.Length];
            int lo = 0;
            for (int i = 0; i < values.Length; i++)
            {
                while (times[lo] < times[i] - sigma * 3f) lo++;
                Vector3 sum = Vector3.zero;
                float weight = 0f;
                for (int j = lo; j < values.Length && times[j] <= times[i] + sigma * 3f; j++)
                {
                    float d = times[j] - times[i];
                    float w = Mathf.Exp(-(d * d) / (2f * sigma * sigma));
                    sum += values[j] * w;
                    weight += w;
                }
                result[i] = sum / weight;
            }
            return result;
        }

        static Quaternion[] SmoothRotations(float[] times, Quaternion[] values, float sigma)
        {
            var result = new Quaternion[values.Length];
            int lo = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (sigma <= 0.0001f)
                {
                    result[i] = values[i];
                }
                else
                {
                    while (times[lo] < times[i] - sigma * 3f) lo++;
                    Vector4 sum = Vector4.zero;
                    Quaternion reference = values[i];
                    for (int j = lo; j < values.Length && times[j] <= times[i] + sigma * 3f; j++)
                    {
                        float d = times[j] - times[i];
                        float w = Mathf.Exp(-(d * d) / (2f * sigma * sigma));
                        Quaternion q = values[j];
                        // q and -q are the same rotation; average them on the same side.
                        if (Quaternion.Dot(q, reference) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        sum += new Vector4(q.x, q.y, q.z, q.w) * w;
                    }
                    sum.Normalize();
                    result[i] = new Quaternion(sum.x, sum.y, sum.z, sum.w);
                }
                // Keep neighbouring keys on the same side so the curves don't jump.
                if (i > 0 && Quaternion.Dot(result[i], result[i - 1]) < 0f)
                    result[i] = new Quaternion(-result[i].x, -result[i].y, -result[i].z, -result[i].w);
            }
            return result;
        }

        /// <summary>Adds (or refreshes) a CutscenePlayer for the cutscene in the open scene.</summary>
        public static CutscenePlayer AddToScene(CutsceneAsset cutscene, bool makeDefault)
        {
            var existing = Object.FindObjectsByType<CutscenePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(p => p.cutscene == cutscene);
            CutscenePlayer player = existing;
            if (player == null)
            {
                // Clip curves are local to this object, so it sits at the root, unparented and untransformed.
                var go = new GameObject("Cutscene " + cutscene.name);
                var cam = go.AddComponent<Camera>();
                CopyCameraSettings(cam);
                cam.depth = 10f;
                cam.enabled = false;
                player = go.AddComponent<CutscenePlayer>();
                player.cutscene = cutscene;
                player.cutsceneCamera = cam;
            }

            if (makeDefault)
            {
                // Only the newest cutscene answers to P, and it's placed last so it registers last.
                foreach (var other in Object.FindObjectsByType<CutscenePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (other != player) { other.playWithKey = false; EditorUtility.SetDirty(other); }
                player.playWithKey = true;
                player.transform.SetAsLastSibling();
            }
            cutscene.cameraClip.SampleAnimation(player.gameObject, 0f);
            EditorUtility.SetDirty(player);

            var scene = player.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path) && !EditorApplication.isPlaying) EditorSceneManager.SaveScene(scene);
            return player;
        }

        static void CopyCameraSettings(Camera cam)
        {
            var source = Object.FindFirstObjectByType<FirstPersonController>()?.playerCamera ?? Camera.main;
            if (source == null) return;
            cam.clearFlags = source.clearFlags;
            cam.backgroundColor = source.backgroundColor;
            cam.nearClipPlane = source.nearClipPlane;
            cam.farClipPlane = source.farClipPlane;
            cam.allowHDR = source.allowHDR;
            cam.fieldOfView = source.fieldOfView;
        }

        /// <summary>Called by the scene builder so cutscenes survive a rebuild of NightStreet.</summary>
        public static void AddAllToScene()
        {
            var cutscenes = AssetDatabase.FindAssets("t:CutsceneAsset", new[] { CutscenesDir })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CutsceneAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(c => c != null && c.cameraClip != null)
                .OrderBy(c => File.GetLastWriteTimeUtc(AssetDatabase.GetAssetPath(c)))
                .ToList();
            for (int i = 0; i < cutscenes.Count; i++) AddToScene(cutscenes[i], makeDefault: i == cutscenes.Count - 1);
        }
    }

    [CustomEditor(typeof(CutsceneAsset))]
    class CutsceneAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var cutscene = (CutsceneAsset)target;
            EditorGUILayout.HelpBox(
                "Made from a camera recording. Change the smoothing, then Rebuild. " +
                "The camera clip can also be edited by hand in the Animation window (but Rebuild overwrites it).",
                MessageType.Info);
            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Camera Path")) CutsceneTools.Rebuild(cutscene);
                if (GUILayout.Button("Add To Open Scene")) CutsceneTools.AddToScene(cutscene, makeDefault: true);
            }

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button(EditorApplication.isPlaying ? "Play Now" : "Play Now (enter Play mode first)"))
                {
                    var player = Object.FindObjectsByType<CutscenePlayer>(FindObjectsSortMode.None).FirstOrDefault(p => p.cutscene == cutscene);
                    if (player != null) player.Play();
                    else Debug.LogWarning("[Hanging Woman] This cutscene isn't in the scene. Use Add To Open Scene first.");
                }
            }
        }
    }
}
