using UnityEngine;

[DisallowMultipleComponent]
public sealed class BulletImpactEffect : MonoBehaviour
{
    private const string ImpactMaterialResourcePath = "Materials/BulletImpactBlack";

    [SerializeField, Min(1)]
    private int particleCount = 6;

    [SerializeField, Min(0f)]
    private float minSpeed = 1.5f;

    [SerializeField, Min(0f)]
    private float maxSpeed = 4f;

    [SerializeField]
    private Color particleColor = Color.black;

    [SerializeField, Min(0f)]
    private float destroyAfterSeconds = 0.7f;

    private ParticleSystem particles;
    private static Material impactMaterial;

    public static void Spawn(Vector3 position, Vector2 incomingDirection)
    {
        GameObject effectObject = new GameObject("Bullet Impact Effect");
        effectObject.transform.position = position;

        BulletImpactEffect effect = effectObject.AddComponent<BulletImpactEffect>();
        effect.Play(incomingDirection);
    }

    private void Awake()
    {
        particles = gameObject.AddComponent<ParticleSystem>();
        ConfigureParticleSystem();
    }

    private void Play(Vector2 incomingDirection)
    {
        if (particles == null)
        {
            particles = gameObject.AddComponent<ParticleSystem>();
            ConfigureParticleSystem();
        }

        Vector2 baseDirection = incomingDirection.sqrMagnitude > Mathf.Epsilon
            ? -incomingDirection.normalized
            : Vector2.left;

        for (int i = 0; i < particleCount; i++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector2 velocityDirection = (baseDirection + randomDirection * 0.8f).normalized;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = Vector3.zero,
                velocity = velocityDirection * Random.Range(minSpeed, maxSpeed),
                startColor = particleColor,
                startSize = Random.Range(0.1f, 0.2f),
                startLifetime = Random.Range(0.18f, 0.38f)
            };

            particles.Emit(emitParams, 1);
        }

        Destroy(gameObject, destroyAfterSeconds);
    }

    private void ConfigureParticleSystem()
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.4f;
        main.loop = false;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startSize = 0.15f;
        main.startLifetime = 0.25f;
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = particleCount;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = 5;
        particleRenderer.sharedMaterial = GetImpactMaterial();
    }

    private static Material GetImpactMaterial()
    {
        if (impactMaterial == null)
        {
            impactMaterial = Resources.Load<Material>(ImpactMaterialResourcePath);
        }

        return impactMaterial;
    }
}
