using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightningBulletEffect : MonoBehaviour
{
    [SerializeField, Min(0.01f)]
    private float refreshInterval = 0.045f;

    [SerializeField, Min(0.01f)]
    private float boltLength = 0.5f;

    [SerializeField, Min(0.001f)]
    private float boltWidth = 0.03f;

    [SerializeField, Min(0f)]
    private float jitterAmount = 0.085f;

    [SerializeField, Range(1, 6)]
    private int arcCount = 2;

    [SerializeField, Range(2, 8)]
    private int segmentCount = 4;

    [SerializeField]
    private Color boltColor = new(0f, 0f, 0f, 0.95f);

    private Bullet bullet;
    private LineRenderer[] arcRenderers = System.Array.Empty<LineRenderer>();
    private float nextRefreshTime;
    private static Material lineMaterial;

    private void Awake()
    {
        bullet = GetComponent<Bullet>();
        EnsureLineRenderer();
    }

    private void OnEnable()
    {
        EnsureLineRenderer();
        nextRefreshTime = 0f;
    }

    private void Update()
    {
        if (arcRenderers.Length == 0)
        {
            return;
        }

        if (Time.time < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.time + refreshInterval;
        RefreshBolt();
    }

    private void OnDisable()
    {
        for (int i = 0; i < arcRenderers.Length; i++)
        {
            if (arcRenderers[i] != null)
            {
                arcRenderers[i].enabled = false;
            }
        }
    }

    private void EnsureLineRenderer()
    {
        int desiredArcCount = Mathf.Max(1, arcCount);
        if (arcRenderers.Length == desiredArcCount && AllRenderersExist())
        {
            SetRenderersEnabled(true);
            return;
        }

        arcRenderers = new LineRenderer[desiredArcCount];
        for (int i = 0; i < desiredArcCount; i++)
        {
            LineRenderer renderer = i == 0
                ? GetComponent<LineRenderer>()
                : FindChildRenderer(i);

            if (renderer == null)
            {
                if (i == 0)
                {
                    renderer = gameObject.AddComponent<LineRenderer>();
                }
                else
                {
                    GameObject arcObject = new($"LightningArc_{i}");
                    arcObject.transform.SetParent(transform, false);
                    renderer = arcObject.AddComponent<LineRenderer>();
                }
            }

            ConfigureRenderer(renderer, i);
            arcRenderers[i] = renderer;
        }
    }

    private void ConfigureRenderer(LineRenderer renderer, int index)
    {
        renderer.enabled = true;
        renderer.useWorldSpace = true;
        renderer.loop = false;
        renderer.positionCount = Mathf.Max(2, segmentCount);
        renderer.widthMultiplier = index == 0 ? boltWidth : boltWidth * Random.Range(0.75f, 1.15f);
        renderer.startColor = boltColor;
        renderer.endColor = new Color(boltColor.r, boltColor.g, boltColor.b, 0.75f);
        renderer.sortingOrder = 9;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            lineMaterial ??= new Material(shader);
            renderer.sharedMaterial = lineMaterial;
        }
    }

    private void RefreshBolt()
    {
        Vector2 forward = bullet != null && bullet.Direction.sqrMagnitude > Mathf.Epsilon
            ? bullet.Direction
            : Vector2.right;
        forward.Normalize();
        Vector2 perpendicular = new(-forward.y, forward.x);
        int count = Mathf.Max(2, segmentCount);

        for (int arcIndex = 0; arcIndex < arcRenderers.Length; arcIndex++)
        {
            LineRenderer renderer = arcRenderers[arcIndex];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = Random.value > 0.25f;
            renderer.positionCount = count;

            bool frontArc = Random.value < 0.14f;
            float centerAlong = frontArc
                ? Random.Range(0.02f, 0.14f) * boltLength
                : Random.Range(-0.12f, -0.38f) * boltLength;
            float sideA = Random.Range(-0.42f, 0.42f) * boltLength;
            float sideB = -sideA + Random.Range(-0.12f, 0.12f) * boltLength;
            float forwardSpan = Random.Range(0.02f, 0.1f) * boltLength;

            Vector2 arcCenter = forward * centerAlong;
            Vector2 startOffset = arcCenter
                + perpendicular * sideA
                + forward * Random.Range(-forwardSpan, forwardSpan);
            Vector2 endOffset = arcCenter
                + perpendicular * sideB
                + forward * Random.Range(-forwardSpan, forwardSpan);
            Vector2 arcDirection = (endOffset - startOffset).sqrMagnitude > 0.001f
                ? (endOffset - startOffset).normalized
                : perpendicular;
            Vector2 arcNormal = new(-arcDirection.y, arcDirection.x);

            for (int pointIndex = 0; pointIndex < count; pointIndex++)
            {
                float t = (float)pointIndex / (count - 1);
                Vector2 offset = Vector2.Lerp(startOffset, endOffset, t);
                if (pointIndex > 0 && pointIndex < count - 1)
                {
                    offset += arcNormal * Random.Range(-jitterAmount, jitterAmount);
                    offset += forward * Random.Range(-jitterAmount * 0.45f, jitterAmount * 0.45f);
                }

                Vector3 position = transform.position + new Vector3(offset.x, offset.y, 0f);
                position.z = transform.position.z;
                renderer.SetPosition(pointIndex, position);
            }
        }
    }

    private bool AllRenderersExist()
    {
        for (int i = 0; i < arcRenderers.Length; i++)
        {
            if (arcRenderers[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        for (int i = 0; i < arcRenderers.Length; i++)
        {
            if (arcRenderers[i] != null)
            {
                arcRenderers[i].enabled = enabled;
            }
        }
    }

    private LineRenderer FindChildRenderer(int index)
    {
        Transform child = transform.Find($"LightningArc_{index}");
        return child != null ? child.GetComponent<LineRenderer>() : null;
    }
}
