using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySlowEffect : MonoBehaviour
{
    private const string ImpactMaterialResourcePath = "Materials/BulletImpactBlack";

    private ParticleSystem particles;
    private GameObject effectObject;
    private float endTime;
    private static Material effectMaterial;

    public static void Apply(GoblinEnemy enemy, float duration)
    {
        if (enemy == null || enemy.IsDead || duration <= 0f)
        {
            return;
        }

        EnemySlowEffect effect = enemy.GetComponent<EnemySlowEffect>();
        if (effect == null)
        {
            effect = enemy.gameObject.AddComponent<EnemySlowEffect>();
        }

        effect.Refresh(duration);
    }

    private void Awake()
    {
        Bounds bounds = CalculateBounds();
        effectObject = new GameObject("Ice Slow Effect");
        effectObject.transform.position = new Vector3(
            bounds.center.x,
            bounds.min.y + bounds.size.y * 0.18f,
            transform.position.z);
        effectObject.transform.SetParent(transform, true);
        KeepWorldSized(effectObject.transform);

        particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureParticleSystem(bounds);
    }

    private void Update()
    {
        if (Time.time < endTime)
        {
            return;
        }

        if (effectObject != null)
        {
            Destroy(effectObject);
        }

        Destroy(this);
    }

    private void Refresh(float duration)
    {
        endTime = Mathf.Max(endTime, Time.time + duration);
        if (particles != null && !particles.isPlaying)
        {
            particles.Play();
        }
    }

    private void ConfigureParticleSystem(Bounds bounds)
    {
        float radius = Mathf.Clamp(bounds.extents.x * 1.15f, 0.16f, 0.75f);
        float particleSize = Mathf.Clamp(bounds.size.x * 0.12f, 0.025f, 0.09f);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            particleSize * 0.65f,
            particleSize * 1.2f);
        main.startColor = new Color(0.72f, 0.72f, 0.72f, 0.85f);
        main.gravityModifier = 0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 32;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 10f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.15f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 6;
        renderer.sharedMaterial = GetEffectMaterial();
    }

    private Bounds CalculateBounds()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private void KeepWorldSized(Transform effectTransform)
    {
        Vector3 scale = transform.lossyScale;
        effectTransform.localScale = new Vector3(
            Mathf.Abs(scale.x) > Mathf.Epsilon ? 1f / Mathf.Abs(scale.x) : 1f,
            Mathf.Abs(scale.y) > Mathf.Epsilon ? 1f / Mathf.Abs(scale.y) : 1f,
            1f);
    }

    private static Material GetEffectMaterial()
    {
        if (effectMaterial == null)
        {
            effectMaterial = Resources.Load<Material>(ImpactMaterialResourcePath);
        }

        return effectMaterial;
    }
}
