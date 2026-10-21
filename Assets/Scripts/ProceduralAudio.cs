using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// Generates placeholder audio at runtime so the scene has rain, thunder and footsteps
    /// without any imported sound files. Swap in real recordings whenever you have them.
    /// </summary>
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;

        /// <summary>Seamless loop of steady rain: pink noise bed plus individual droplet ticks.</summary>
        public static AudioClip CreateRainLoop(float seconds = 6f, int seed = 1234)
        {
            var rng = new System.Random(seed);
            int fade = SampleRate / 2;
            int n = (int)(seconds * SampleRate) + fade;
            var data = new float[n];

            // Pink noise (Paul Kellet's filter) for the hiss of rain.
            float b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0, low = 0;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                b0 = 0.99886f * b0 + white * 0.0555179f;
                b1 = 0.99332f * b1 + white * 0.0750759f;
                b2 = 0.96900f * b2 + white * 0.1538520f;
                b3 = 0.86650f * b3 + white * 0.3104856f;
                b4 = 0.55000f * b4 + white * 0.5329522f;
                b5 = -0.7616f * b5 - white * 0.0168980f;
                float pink = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f) * 0.11f;
                b6 = white * 0.115926f;
                low += (pink - low) * 0.05f;
                data[i] = pink * 0.45f + low * 0.9f;
            }

            // Droplets hitting pavement, awnings and puddles.
            int drops = (int)(seconds * 140);
            for (int d = 0; d < drops; d++)
            {
                int start = rng.Next(n);
                int len = 80 + rng.Next(400);
                float amp = (float)(0.05 + rng.NextDouble() * 0.25);
                float prev = 0f;
                for (int j = 0; j < len && start + j < n; j++)
                {
                    float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                    float hp = white - prev;
                    prev = white;
                    float env = Mathf.Exp(-j / (len * 0.18f));
                    data[start + j] += hp * amp * env;
                }
            }

            return Finish("Rain Loop", CrossfadeLoop(data, fade), 0.7f, loop: true);
        }

        /// <summary>A crack followed by a long rolling rumble.</summary>
        public static AudioClip CreateThunder(int seed, float seconds = 7f)
        {
            var rng = new System.Random(seed);
            int n = (int)(seconds * SampleRate);
            var data = new float[n];

            int bumps = 4 + rng.Next(4);
            var bumpTime = new float[bumps];
            var bumpAmp = new float[bumps];
            var bumpWidth = new float[bumps];
            for (int b = 0; b < bumps; b++)
            {
                bumpTime[b] = 0.1f + (float)rng.NextDouble() * seconds * 0.55f;
                bumpAmp[b] = 0.4f + (float)rng.NextDouble() * 0.6f;
                bumpWidth[b] = 0.25f + (float)rng.NextDouble() * 1.1f;
            }

            float brown = 0f, lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                brown = (brown + 0.02f * white) / 1.02f;
                lp1 += (brown - lp1) * 0.1f;
                lp2 += (lp1 - lp2) * 0.1f;

                float rumble = 0f;
                for (int b = 0; b < bumps; b++)
                {
                    float x = (t - bumpTime[b]) / bumpWidth[b];
                    rumble += bumpAmp[b] * Mathf.Exp(-x * x);
                }
                float tail = Mathf.Exp(-t * 0.45f);
                float crack = white * Mathf.Exp(-t * 14f) * Mathf.Min(1f, t * 300f) * 0.06f;
                float edges = Mathf.Min(1f, t * 40f) * Mathf.Min(1f, (seconds - t) * 2f);
                data[i] = (lp2 * rumble * tail + crack) * edges;
            }
            return Finish("Thunder " + seed, data, 0.95f);
        }

        /// <summary>A short wet footstep on pavement.</summary>
        public static AudioClip CreateFootstep(int seed)
        {
            var rng = new System.Random(seed);
            int n = (int)(0.24f * SampleRate);
            var data = new float[n];
            float thumpFreq = 60f + (float)rng.NextDouble() * 30f;
            float splashDelay = 0.015f + (float)rng.NextDouble() * 0.03f;
            float lp = 0f, prev = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * 0.15f;
                float hp = white - prev;
                prev = white;

                float thump = Mathf.Sin(2f * Mathf.PI * thumpFreq * t) * Mathf.Exp(-t * 45f) * 0.8f;
                float scuff = lp * Mathf.Exp(-t * 28f) * 1.2f;
                float s = (t - splashDelay) / 0.035f;
                float splash = hp * Mathf.Exp(-s * s) * 0.18f;
                data[i] = (thump + scuff + splash) * Mathf.Min(1f, t * 2000f);
            }
            return Finish("Footstep " + seed, data, 0.9f);
        }

        /// <summary>
        /// A taut rope creaking under load: a continuous stick-slip groan that swells in and
        /// fades out smoothly, with the tension wavering so it never sounds mechanical.
        /// </summary>
        public static AudioClip CreateRopeCreak(int seed = 3, float seconds = 2.2f)
        {
            var data = RopeFriction(seed, seconds, t =>
            {
                // One smooth swell (no dips to silence), wavering a little in strength.
                // sin(π) rounds to a tiny negative number; a fractional power of that is NaN.
                float swell = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.7f);
                return swell * (0.8f + 0.2f * Mathf.Sin(t * seconds * 2f * Mathf.PI * 1.7f));
            });
            return Finish("Rope Creak " + seed, data, 0.8f);
        }

        /// <summary>
        /// Seamless loop of a rope straining as it drags a weight: steady stick-slip friction
        /// with the tension swelling and easing in a slow rhythm.
        /// </summary>
        public static AudioClip CreateRopeStrainLoop(int seed = 11, float seconds = 4f)
        {
            int fade = SampleRate / 2;
            float total = seconds + fade / (float)SampleRate;
            // Whole number of tension cycles over the loop so it joins up.
            var data = RopeFriction(seed, total, t => 0.65f + 0.35f * Mathf.Sin(t * total / seconds * 2f * Mathf.PI * 3f));
            return Finish("Rope Strain Loop " + seed, CrossfadeLoop(data, fade), 0.7f, loop: true);
        }

        /// <summary>
        /// Stick-slip friction: a pulse train whose rate follows the tension, plus a constant
        /// fibre rasp, both ringing woody resonances. `tension(t)` takes t in 0..1.
        /// </summary>
        static float[] RopeFriction(int seed, float seconds, System.Func<float, float> tension)
        {
            var rng = new System.Random(seed);
            int n = (int)(seconds * SampleRate);
            var data = new float[n];

            float[] freqs = { 430f, 980f, 2100f };
            float[] gains = { 1f, 0.6f, 0.25f };
            var y1 = new float[freqs.Length];
            var y2 = new float[freqs.Length];
            var a1 = new float[freqs.Length];
            var a2 = new float[freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
            {
                // Wider bandwidth on the low mode so pulses smear into a groan instead of clicks.
                float bandwidth = k == 0 ? 45f : 90f;
                float r = Mathf.Exp(-Mathf.PI * bandwidth / SampleRate);
                a1[k] = 2f * r * Mathf.Cos(2f * Mathf.PI * freqs[k] / SampleRate);
                a2[k] = -r * r;
            }

            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float env = Mathf.Max(0f, tension(t));
                float rate = 45f + 90f * env + (float)rng.NextDouble() * 12f;
                phase += rate / SampleRate;

                float impulse = 0f;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    impulse = env * (0.7f + (float)rng.NextDouble() * 0.3f);
                }

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * 0.2f;
                float rasp = lp * 0.08f * env;

                float sample = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    float y = a1[k] * y1[k] + a2[k] * y2[k] + impulse + rasp;
                    y2[k] = y1[k];
                    y1[k] = y;
                    sample += y * gains[k];
                }
                data[i] = sample;
            }
            return data;
        }

        static float[] CrossfadeLoop(float[] data, int fade)
        {
            int length = data.Length - fade;
            var result = new float[length];
            System.Array.Copy(data, result, length);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                result[i] = data[i] * k + data[length + i] * (1f - k);
            }
            return result;
        }

        static AudioClip Finish(string name, float[] data, float peak, bool loop = false)
        {
            // A single NaN or infinite sample reaching the audio device can make it buzz,
            // even for other apps sharing it. Never let one through.
            for (int i = 0; i < data.Length; i++)
                if (float.IsNaN(data[i]) || float.IsInfinity(data[i])) data[i] = 0f;

            float max = 0.0001f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            float gain = peak / max;

            // Short fades so one-shots never start or stop on a click (loops are already seamless).
            int fade = loop ? 0 : Mathf.Min(data.Length / 4, 220);
            for (int i = 0; i < data.Length; i++)
            {
                float edge = fade == 0 ? 1f : Mathf.Min(1f, Mathf.Min(i, data.Length - 1 - i) / (float)fade);
                data[i] = Mathf.Clamp(data[i] * gain * edge, -1f, 1f);
            }

            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
