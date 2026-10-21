using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// Drives lights, emissive surfaces and neon text together so a sign or lamp dims as one.
    /// Gentle = faint hum, NeonBuzz = mostly on with short stuttering dropouts,
    /// Broken = a dying lamp that spends a lot of its time dark.
    /// </summary>
    public class LightFlicker : MonoBehaviour
    {
        public enum Mode { Steady, Gentle, NeonBuzz, Broken }

        public Mode mode = Mode.NeonBuzz;
        public Light[] lights;
        public Renderer[] emissiveRenderers;
        public TextMesh[] textMeshes;
        [ColorUsage(false, true)] public Color emissionColor = Color.white;
        [Range(0f, 1f)] public float offLevel = 0.04f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        float[] baseIntensity;
        Color[] baseTextColor;
        MaterialPropertyBlock block;
        float seed;
        float stateUntil;
        bool on = true;
        float burstUntil;

        void Awake()
        {
            seed = Random.value * 100f;
            block = new MaterialPropertyBlock();
            lights ??= new Light[0];
            textMeshes ??= new TextMesh[0];
            emissiveRenderers ??= new Renderer[0];

            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            baseTextColor = new Color[textMeshes.Length];
            for (int i = 0; i < textMeshes.Length; i++) baseTextColor[i] = textMeshes[i] != null ? textMeshes[i].color : Color.white;
            stateUntil = Time.time + Random.Range(0.5f, 4f);
        }

        void Update()
        {
            Apply(Evaluate(Time.time));
        }

        float Evaluate(float t)
        {
            switch (mode)
            {
                case Mode.Gentle:
                    return 0.9f + 0.1f * Mathf.PerlinNoise(seed, t * 6f);

                case Mode.NeonBuzz:
                    if (t > stateUntil)
                    {
                        burstUntil = t + Random.Range(0.08f, 0.6f);
                        stateUntil = burstUntil + Random.Range(2f, 9f);
                    }
                    if (t < burstUntil)
                        return Mathf.PerlinNoise(seed, t * 40f) > 0.5f ? 1f : offLevel;
                    return 0.93f + 0.07f * Mathf.PerlinNoise(seed, t * 12f);

                case Mode.Broken:
                    if (t > stateUntil)
                    {
                        on = !on;
                        stateUntil = t + (on ? Random.Range(0.05f, 2.5f) : Random.Range(0.05f, 1.2f));
                    }
                    if (!on) return offLevel;
                    // Even when "on", a dying lamp stutters.
                    return Mathf.PerlinNoise(seed, t * 25f) > 0.25f ? 1f : 0.3f;

                default:
                    return 1f;
            }
        }

        void Apply(float level)
        {
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].intensity = baseIntensity[i] * level;

            for (int i = 0; i < textMeshes.Length; i++)
            {
                if (textMeshes[i] == null) continue;
                Color c = baseTextColor[i];
                textMeshes[i].color = Color.Lerp(c * 0.15f, c, level);
            }

            if (emissiveRenderers.Length > 0)
            {
                block.SetColor(EmissionColorId, emissionColor * level);
                foreach (var r in emissiveRenderers)
                    if (r != null) r.SetPropertyBlock(block);
            }
        }
    }
}
