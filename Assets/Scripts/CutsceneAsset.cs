using System.Collections.Generic;
using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// A replayable camera cutscene made from a recorded take. Created and rebuilt from the
    /// editor (Hanging Woman > Cutscenes); played by a CutscenePlayer.
    /// </summary>
    [CreateAssetMenu(menuName = "Hanging Woman/Cutscene", fileName = "Cutscene")]
    public class CutsceneAsset : ScriptableObject
    {
        [Tooltip("The recording this was made from (Assets/Recordings/*.json).")]
        public TextAsset sourceRecording;
        [Tooltip("Seconds of smoothing on the camera path. 0 keeps every wobble of the original.")]
        [Range(0f, 1f)] public float positionSmoothing = 0.12f;
        [Range(0f, 1f)] public float rotationSmoothing = 0.15f;
        [Range(0f, 1f)] public float zoomSmoothing = 0.05f;

        [Header("Generated")]
        [Tooltip("Camera position, rotation and field of view over time. Editable in the Animation window.")]
        public AnimationClip cameraClip;
        public float duration;
        public List<CutsceneMarker> markers = new List<CutsceneMarker>();
    }
}
