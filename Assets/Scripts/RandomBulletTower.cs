using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RandomBulletTower : MonoBehaviour
{
    [SerializeField]
    private BulletElement[] possibleOutputs =
    {
        BulletElement.Normal,
        BulletElement.Fire,
        BulletElement.Ice,
        BulletElement.Lightning,
        BulletElement.Homing
    };

    [SerializeField]
    private Sprite[] randomFrames = System.Array.Empty<Sprite>();

    [SerializeField, Min(0.01f)]
    private float frameDuration = 0.08f;

    [SerializeField]
    private Vector2 randomBulletScale = new(0.75f, 0.75f);

    private readonly HashSet<Bullet> convertedBullets = new();
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        BulletTowerUtility.EnsureTriggerCollider(gameObject, spriteRenderer);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryConvert(other.GetComponentInParent<Bullet>());
    }

    private void Update()
    {
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
        for (int i = 0; i < bullets.Length; i++)
        {
            Bullet bullet = bullets[i];
            if (bullet != null
                && bullet.isActiveAndEnabled
                && BulletTowerUtility.IsInsideTower(transform, spriteRenderer, bullet.transform.position))
            {
                TryConvert(bullet);
            }
        }
    }

    private void TryConvert(Bullet bullet)
    {
        if (bullet == null || !convertedBullets.Add(bullet))
        {
            return;
        }

        BulletElement output = GetRandomOutput();
        if (output == BulletElement.Normal)
        {
            bullet.ResetToNormal();
        }
        else
        {
            bullet.ApplyElement(output, randomFrames, frameDuration, randomBulletScale);
            bullet.SetVisualColor(GetColorFor(output));
        }

        if (output == BulletElement.Homing && bullet.GetComponent<HomingBullet>() == null)
        {
            bullet.gameObject.AddComponent<HomingBullet>();
        }
        else if (output == BulletElement.Lightning && bullet.GetComponent<LightningBulletEffect>() == null)
        {
            bullet.gameObject.AddComponent<LightningBulletEffect>();
        }

        bullet.SpawnConversionEffect(output);
    }

    private BulletElement GetRandomOutput()
    {
        if (possibleOutputs == null || possibleOutputs.Length == 0)
        {
            return BulletElement.Normal;
        }

        return possibleOutputs[Random.Range(0, possibleOutputs.Length)];
    }

    private static Color GetColorFor(BulletElement element)
    {
        return element switch
        {
            BulletElement.Fire => new Color(0.15f, 0.15f, 0.15f, 1f),
            BulletElement.Ice => new Color(0.75f, 0.75f, 0.75f, 1f),
            BulletElement.Lightning => new Color(0.9f, 0.9f, 0.9f, 1f),
            BulletElement.Homing => new Color(0.45f, 0.45f, 0.45f, 1f),
            _ => Color.white
        };
    }
}
