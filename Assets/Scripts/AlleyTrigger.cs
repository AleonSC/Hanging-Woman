using UnityEngine;
using UnityEngine.Events;

namespace HangingWoman
{
    /// <summary>
    /// Trigger volume covering the alley. Slows the player while inside and exposes events
    /// so you can hook up whatever happens at the end of the blood trail.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AlleyTrigger : MonoBehaviour
    {
        [Range(0.1f, 1f)] public float playerSpeedMultiplier = 0.75f;
        public bool fireEnterOnce = true;
        public UnityEvent onPlayerEnter;
        public UnityEvent onPlayerExit;

        bool fired;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<FirstPersonController>();
            if (player == null) return;
            player.SpeedMultiplier = playerSpeedMultiplier;
            if (fireEnterOnce && fired) return;
            fired = true;
            onPlayerEnter?.Invoke();
        }

        void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<FirstPersonController>();
            if (player == null) return;
            player.SpeedMultiplier = 1f;
            onPlayerExit?.Invoke();
        }
    }
}
