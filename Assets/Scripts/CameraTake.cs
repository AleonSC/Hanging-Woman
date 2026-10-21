using System;
using System.Collections.Generic;
using UnityEngine;

namespace HangingWoman
{
    /// <summary>A recorded camera take, as saved to Assets/Recordings/*.json.</summary>
    [Serializable]
    public class CameraTake
    {
        public string name;
        public string scene;
        public string recordedAt;
        public float sampleRate;
        public List<CameraSample> samples = new List<CameraSample>();
        public List<CutsceneMarker> markers = new List<CutsceneMarker>();

        public float Duration => samples.Count > 0 ? samples[samples.Count - 1].t : 0f;
    }

    [Serializable]
    public struct CameraSample
    {
        public float t;
        public Vector3 position;
        public Quaternion rotation;
        public float fov;
    }

    [Serializable]
    public struct CutsceneMarker
    {
        public float time;
        public string label;
    }
}
