using UnityEngine;

namespace HangingWoman
{
    /// <summary>
    /// Rain that follows the player: a box of falling streaks above their head, splashes
    /// wherever a drop hits a collider (street, awnings, dumpsters), and a looping rain sound.
    /// Particles simulate in world space, so walking doesn't drag the rain along with you.
    /// </summary>
    public class RainSystem : MonoBehaviour
    {
        public Transform follow;
        public Material rainMaterial;
        public Material splashMaterial;

        [Header("Rain")]
        public float dropsPerSecond = 4500f;
        public float areaSize = 34f;
        public float spawnHeight = 13f;
        public float fallSpeed = 14f;
        [Tooltip("Tilt of the rain in degrees; positive slants it toward +Z (down the street).")]
        public float windTilt = 6f;
        [Tooltip("Rain won't splash on these layers (the player sits on Ignore Raycast).")]
        public LayerMask splashLayers = ~(1 << 2);

        [Header("Audio")]
        public AudioClip rainLoop;
        [Range(0f, 1f)] public float volume = 0.55f;

        ParticleSystem rain;
        AudioSource audioSource;

        void Start()
        {
            if (follow == null && Camera.main != null) follow = Camera.main.transform.root;
            rain = CreateRain();
            CreateSplashes(rain);
            rain.Play();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = rainLoop != null ? rainLoop : ProceduralAudio.CreateRainLoop();
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.volume = volume;
            audioSource.Play();
        }

        void LateUpdate()
        {
            if (follow == null || rain == null) return;
            // Bias the emitter slightly ahead so you're always walking into fresh rain.
            Vector3 ahead = Vector3.ProjectOnPlane(follow.forward, Vector3.up) * 4f;
            rain.transform.position = follow.position + ahead + Vector3.up * spawnHeight;
        }

        public void SetVolume(float v)
        {
            volume = v;
            if (audioSource != null) audioSource.volume = v;
        }

        ParticleSystem CreateRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float lifetime = (spawnHeight + 3f) / fallSpeed;

            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(fallSpeed * 0.9f, fallSpeed * 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.03f);
            main.startColor = new Color(0.75f, 0.8f, 0.9f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.CeilToInt(dropsPerSecond * lifetime * 1.2f);
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = ps.emission;
            emission.rateOverTime = dropsPerSecond;

            // Box shape emits along its local +Z; rotate so that points (nearly) straight down.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(90f - windTilt, 0f, 0f);
            shape.scale = new Vector3(areaSize, areaSize, 0.1f);

            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.collidesWith = splashLayers;
            collision.lifetimeLoss = 1f;
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.radiusScale = 0.5f;
            collision.enableDynamicColliders = false;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1.5f;
            r.sharedMaterial = rainMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        void CreateSplashes(ParticleSystem parent)
        {
            var go = new GameObject("Splashes");
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
            main.startColor = new Color(0.8f, 0.85f, 0.95f, 0.5f);
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4000;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1, 3) });

            // Cone emits along local +Z; rotate it to spray upward.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = splashMaterial != null ? splashMaterial : rainMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var sub = parent.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(ps, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
        }
    }
}
