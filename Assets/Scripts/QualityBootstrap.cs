using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// The street is lit by lots of small lights (lamps, neon, shop windows). The built-in
    /// forward renderer only gives each object a few per-pixel lights by default, so raise it.
    /// Has no effect under URP, which manages its own light limits.
    /// </summary>
    public class QualityBootstrap : MonoBehaviour
    {
        public int pixelLightCount = 8;
        public float shadowDistance = 40f;

        void Awake()
        {
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, pixelLightCount);
            QualitySettings.shadowDistance = shadowDistance;
        }
    }
}
