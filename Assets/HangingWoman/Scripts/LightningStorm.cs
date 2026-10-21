using System.Collections;
using UnityEngine;

namespace HangingWoman
{
    /// <summary>Occasional distant lightning: brightens the sky light and ambient, then thunder rolls in.</summary>
    public class LightningStorm : MonoBehaviour
    {
        public Light skyLight;
        public float firstFlashDelay = 14f;
        public float minInterval = 20f;
        public float maxInterval = 50f;
        public float flashIntensity = 1.4f;
        public Color flashColor = new Color(0.75f, 0.82f, 1f);
        [Range(0f, 1f)] public float thunderVolume = 0.8f;
        public AudioClip[] thunderClips;

        AudioSource source;
        float baseIntensity;
        Color baseColor;
        Color baseAmbient;

        void Start()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            if (thunderClips == null || thunderClips.Length == 0)
                thunderClips = new[] { ProceduralAudio.CreateThunder(7), ProceduralAudio.CreateThunder(19), ProceduralAudio.CreateThunder(42) };

            if (skyLight != null)
            {
                baseIntensity = skyLight.intensity;
                baseColor = skyLight.color;
            }
            baseAmbient = RenderSettings.ambientLight;
            StartCoroutine(StormLoop());
        }

        void OnDisable()
        {
            SetFlash(0f);
        }

        IEnumerator StormLoop()
        {
            yield return new WaitForSeconds(firstFlashDelay);
            while (true)
            {
                yield return Strike();
                yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
            }
        }

        public IEnumerator Strike()
        {
            int flashes = Random.Range(1, 4);
            for (int i = 0; i < flashes; i++)
            {
                SetFlash(Random.Range(0.5f, 1f));
                yield return new WaitForSeconds(Random.Range(0.03f, 0.1f));
                SetFlash(Random.Range(0f, 0.15f));
                yield return new WaitForSeconds(Random.Range(0.05f, 0.25f));
            }
            SetFlash(0f);

            // Distance of the strike: further away = longer delay and quieter thunder.
            float distance = Random.value;
            yield return new WaitForSeconds(Mathf.Lerp(0.4f, 3.5f, distance));
            var clip = thunderClips[Random.Range(0, thunderClips.Length)];
            source.pitch = Random.Range(0.8f, 1.05f);
            source.PlayOneShot(clip, thunderVolume * Mathf.Lerp(1f, 0.45f, distance));
        }

        void SetFlash(float k)
        {
            if (skyLight != null)
            {
                skyLight.intensity = Mathf.Lerp(baseIntensity, flashIntensity, k);
                skyLight.color = Color.Lerp(baseColor, flashColor, k);
            }
            RenderSettings.ambientLight = Color.Lerp(baseAmbient, flashColor * 0.35f, k);
        }
    }
}
