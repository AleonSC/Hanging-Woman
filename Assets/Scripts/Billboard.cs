using UnityEngine;

namespace HangingWoman
{
    /// <summary>Keeps a quad turned toward the camera (used for lamp glows).</summary>
    public class Billboard : MonoBehaviour
    {
        Camera cam;

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            // Quads face -Z, so point +Z away from the camera.
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
        }
    }
}
