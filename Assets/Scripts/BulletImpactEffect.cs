using UnityEngine;

[DisallowMultipleComponent]
public sealed class BulletImpactEffect : MonoBehaviour
{
    private const string ImpactMaterialResourcePath = "Materials/BulletImpactBlack";

    private ParticleSystem particles;
    private static Material impactMaterial;

    public float PlayImpact(
        Vector3 worldPosition,
        Vector2 incomingDirection,
        BulletElement element,
        float worldSize)
    {
        EnsureParticleSystem();
        EffectProfile profile = GetProfile(element, false);
        ApplyProfile(profile);
        worldSize = Mathf.Clamp(worldSize, 0.08f, 0.6f);

        Vector2 baseDirection = incomingDirection.sqrMagnitude > Mathf.Epsilon
            ? -incomingDirection.normalized
            : Vector2.left;

        for (int i = 0; i < profile.ParticleCount; i++)
        {
            Vector2 velocityDirection = GetImpactDirection(
                element,
                baseDirection,
                i,
                profile.ParticleCount,
                profile.DirectionSpread);
            EmitParticle(profile, velocityDirection, worldSize, worldPosition, element, i);
        }

        return profile.DestroyDelay;
    }

    public void PlayConversion(
        Vector3 worldPosition,
        BulletElement element,
        float worldSize)
    {
        EnsureParticleSystem();
        EffectProfile profile = GetProfile(element, true);
        ApplyProfile(profile);
        worldSize = Mathf.Clamp(worldSize, 0.08f, 0.6f);

        for (int i = 0; i < profile.ParticleCount; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            EmitParticle(profile, direction, worldSize, worldPosition, element, i);
        }
    }

    private void EmitParticle(
        EffectProfile profile,
        Vector2 direction,
        float worldSize,
        Vector3 worldPosition,
        BulletElement element,
        int particleIndex)
    {
        float sizeMultiplier = element switch
        {
            BulletElement.Fire => Random.Range(0.8f, 1.35f),
            BulletElement.Ice => particleIndex % 2 == 0 ? 1.25f : 0.65f,
            BulletElement.Lightning => particleIndex % 2 == 0 ? 1.45f : 0.45f,
            BulletElement.Missile => Random.Range(1.2f, 1.8f),
            _ => Random.Range(0.8f, 1.1f)
        };

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = worldPosition,
            velocity = direction *
                (Random.Range(profile.MinSpeed, profile.MaxSpeed) * worldSize),
            startColor = profile.Color,
            startSize = Random.Range(profile.MinSize, profile.MaxSize) * worldSize * sizeMultiplier,
            startLifetime = Random.Range(profile.MinLifetime, profile.MaxLifetime),
            rotation = element == BulletElement.Ice
                ? Mathf.Atan2(direction.y, direction.x)
                : Random.Range(0f, Mathf.PI * 2f)
        };

        particles.Emit(emitParams, 1);
    }

    private static Vector2 GetImpactDirection(
        BulletElement element,
        Vector2 baseDirection,
        int particleIndex,
        int particleCount,
        float directionSpread)
    {
        if (element == BulletElement.Ice)
        {
            float angle = particleIndex * Mathf.PI * 2f / Mathf.Max(1, particleCount);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        if (element == BulletElement.Lightning)
        {
            float zigZag = particleIndex % 2 == 0 ? 1f : -1f;
            Vector2 perpendicular = new(-baseDirection.y, baseDirection.x);
            return (baseDirection + perpendicular * zigZag * 0.8f).normalized;
        }

        if (element == BulletElement.Missile)
        {
            return Random.insideUnitCircle.normalized;
        }

        if (element == BulletElement.Fire)
        {
            Vector2 radial = Random.insideUnitCircle.normalized;
            return (radial + Vector2.up * Random.Range(0.1f, 0.65f)).normalized;
        }

        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        return (baseDirection + randomDirection * directionSpread).normalized;
    }

    private void Awake()
    {
        EnsureParticleSystem();
    }

    private void EnsureParticleSystem()
    {
        if (particles != null)
        {
            return;
        }

        particles = GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = gameObject.AddComponent<ParticleSystem>();
        }

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureParticleSystem();
    }

    private void ApplyProfile(EffectProfile profile)
    {
        ParticleSystem.MainModule main = particles.main;
        main.gravityModifier = profile.Gravity;
        main.maxParticles = Mathf.Max(32, profile.ParticleCount);
    }

    private void ConfigureParticleSystem()
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startSize = 0.15f;
        main.startLifetime = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = 6;
        particleRenderer.sharedMaterial = GetImpactMaterial();
    }

    private static EffectProfile GetProfile(BulletElement element, bool conversion)
    {
        if (conversion)
        {
            return element switch
            {
                BulletElement.Fire => new EffectProfile(
                    8, 2f, 4.5f, 0.2f, 0.45f, 0.16f, 0.32f,
                    new Color(0.15f, 0.15f, 0.15f, 1f), -0.05f, 1f, 0.55f),
                BulletElement.Ice => new EffectProfile(
                    6, 1.2f, 2.6f, 0.06f, 0.14f, 0.16f, 0.3f,
                    new Color(0.75f, 0.75f, 0.75f, 1f), 0.12f, 1f, 0.5f),
                BulletElement.Lightning => new EffectProfile(
                    9, 2.5f, 6f, 0.08f, 0.22f, 0.08f, 0.18f,
                    Color.black, 0f, 0.25f, 0.35f),
                BulletElement.Homing => new EffectProfile(
                    6, 1.5f, 3.5f, 0.1f, 0.2f, 0.14f, 0.28f,
                    new Color(0.9f, 0.9f, 0.9f, 1f), 0.05f, 0.75f, 0.4f),
                _ => new EffectProfile(
                    5, 1.5f, 3f, 0.15f, 0.32f, 0.14f, 0.28f,
                    Color.black, 0.1f, 1f, 0.5f)
            };
        }

        return element switch
        {
            BulletElement.Fire => new EffectProfile(
                12, 4f, 9f, 0.35f, 0.75f, 0.18f, 0.38f,
                new Color(0.12f, 0.12f, 0.12f, 1f), -0.08f, 1.15f, 0.8f),
            BulletElement.Ice => new EffectProfile(
                8, 1.8f, 4f, 0.07f, 0.18f, 0.16f, 0.34f,
                new Color(0.72f, 0.72f, 0.72f, 1f), 0.35f, 0.9f, 0.65f),
            BulletElement.Lightning => new EffectProfile(
                14, 6f, 13f, 0.08f, 0.28f, 0.08f, 0.2f,
                Color.black, 0f, 0.2f, 0.45f),
            BulletElement.Homing => new EffectProfile(
                10, 4f, 8f, 0.12f, 0.32f, 0.14f, 0.28f,
                new Color(0.9f, 0.9f, 0.9f, 1f), 0.08f, 0.8f, 0.55f),
            BulletElement.Missile => new EffectProfile(
                18, 5f, 12f, 0.45f, 1.05f, 0.18f, 0.42f,
                Color.black, 0.25f, 1.6f, 0.8f),
            _ => new EffectProfile(
                12, 7f, 14f, 0.6f, 1.3f, 0.25f, 0.5f,
                Color.black, 0.3f, 1.4f, 0.9f)
        };
    }

    private static Material GetImpactMaterial()
    {
        if (impactMaterial == null)
        {
            impactMaterial = Resources.Load<Material>(ImpactMaterialResourcePath);
        }

        return impactMaterial;
    }

    private readonly struct EffectProfile
    {
        public EffectProfile(
            int particleCount,
            float minSpeed,
            float maxSpeed,
            float minSize,
            float maxSize,
            float minLifetime,
            float maxLifetime,
            Color color,
            float gravity,
            float directionSpread,
            float destroyDelay)
        {
            ParticleCount = particleCount;
            MinSpeed = minSpeed;
            MaxSpeed = maxSpeed;
            MinSize = minSize;
            MaxSize = maxSize;
            MinLifetime = minLifetime;
            MaxLifetime = maxLifetime;
            Color = color;
            Gravity = gravity;
            DirectionSpread = directionSpread;
            DestroyDelay = destroyDelay;
        }

        public int ParticleCount { get; }
        public float MinSpeed { get; }
        public float MaxSpeed { get; }
        public float MinSize { get; }
        public float MaxSize { get; }
        public float MinLifetime { get; }
        public float MaxLifetime { get; }
        public Color Color { get; }
        public float Gravity { get; }
        public float DirectionSpread { get; }
        public float DestroyDelay { get; }
    }
}
