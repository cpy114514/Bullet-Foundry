using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class TitlePageGoblinDemo : MonoBehaviour
{
    [SerializeField]
    private Collider2D clickArea;

    [SerializeField]
    private SettingsMenuController settingsMenu;

    [SerializeField]
    private Animator goblinAnimator;

    [SerializeField]
    private GameObject speedGoblinObject;

    [SerializeField]
    private Animator speedGoblinAnimator;

    [SerializeField]
    private GameObject normalGoblinObject;

    [SerializeField]
    private Animator normalGoblinAnimator;

    [SerializeField]
    private Transform goblinTarget;

    [SerializeField]
    private Transform[] bulletTransforms = System.Array.Empty<Transform>();

    [SerializeField]
    private string deathStateName = "goblin_die";

    [SerializeField]
    private string speedDanceStateName = "speedGoblin_dance";

    [SerializeField]
    private string speedDeathStateName = "goblin_die";

    [SerializeField]
    private string normalDeathStateName = "goblin_die";

    [SerializeField, Min(0.01f)]
    private float bulletTravelDuration = 0.26f;

    [SerializeField, Min(0f)]
    private float bulletStagger = 0f;

    [SerializeField, Min(0f)]
    private float deathFreezeDelay = 1.1f;

    [SerializeField, Min(0f)]
    private float speedDeathDelay = 0.08f;

    private bool hasPlayed;

    private void Awake()
    {
        if (settingsMenu == null)
        {
            settingsMenu = FindFirstObjectByType<SettingsMenuController>();
        }

        PrepareTitleEnemyState();
        DisableRuntimeEnemyBehaviour();
        DisableRuntimeBulletBehaviour();
    }

    private void Update()
    {
        if (hasPlayed || clickArea == null || !WasPointerPressedThisFrame(out Vector2 screenPosition))
        {
            return;
        }

        if (!TryGetWorldPointerPosition(screenPosition, out Vector2 worldPosition))
        {
            return;
        }

        if (clickArea.OverlapPoint(worldPosition) && !ShouldIgnorePointer(worldPosition))
        {
            StartCoroutine(PlayDemoRoutine());
        }
    }

    private IEnumerator PlayDemoRoutine()
    {
        hasPlayed = true;
        PlaySpeedDeathOnce();
        StartCoroutine(FinishDeathSequenceRoutine());

        int validBulletCount = 0;
        for (int i = 0; i < bulletTransforms.Length; i++)
        {
            if (bulletTransforms[i] == null)
            {
                continue;
            }

            StartCoroutine(FlyBulletRoutine(bulletTransforms[i], validBulletCount * bulletStagger));
            validBulletCount++;
        }

        float finalBulletDelay = bulletTravelDuration;
        if (validBulletCount > 0)
        {
            finalBulletDelay += (validBulletCount - 1) * bulletStagger;
        }

        float finalDeathDelay = speedDeathDelay + deathFreezeDelay;
        yield return new WaitForSeconds(Mathf.Max(finalBulletDelay, finalDeathDelay));
        enabled = false;
    }

    private IEnumerator FlyBulletRoutine(Transform bulletTransform, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (bulletTransform == null)
        {
            yield break;
        }

        Vector3 startPosition = bulletTransform.position;
        Vector3 targetPosition = GetStraightRightTargetPosition(startPosition);

        float elapsed = 0f;
        while (elapsed < bulletTravelDuration)
        {
            if (bulletTransform == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bulletTravelDuration);
            float easedT = t * t * (3f - 2f * t);
            Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, easedT);
            RotateBulletToward(bulletTransform, nextPosition - bulletTransform.position);
            bulletTransform.position = nextPosition;
            yield return null;
        }

        if (bulletTransform == null)
        {
            yield break;
        }

        bulletTransform.position = targetPosition;
        HideBullet(bulletTransform.gameObject);
    }

    private void PlayGoblinDeathOnce()
    {
        if (goblinAnimator == null || string.IsNullOrWhiteSpace(deathStateName))
        {
            return;
        }

        goblinAnimator.speed = 1f;
        goblinAnimator.Play(deathStateName, 0, 0f);
    }

    private void PlaySpeedDeathOnce()
    {
        Animator firstAnimator = speedGoblinAnimator != null ? speedGoblinAnimator : goblinAnimator;
        if (firstAnimator != null && !string.IsNullOrWhiteSpace(speedDeathStateName))
        {
            firstAnimator.speed = 1f;
            firstAnimator.Play(speedDeathStateName, 0, 0f);
        }
        else
        {
            PlayGoblinDeathOnce();
        }
    }

    private IEnumerator FinishDeathSequenceRoutine()
    {
        if (speedDeathDelay > 0f)
        {
            yield return new WaitForSeconds(speedDeathDelay);
        }

        GameObject speedObject = GetAnimatorObject(speedGoblinObject, speedGoblinAnimator);
        if (speedObject != null)
        {
            speedObject.SetActive(false);
        }

        GameObject normalObject = GetAnimatorObject(normalGoblinObject, normalGoblinAnimator);
        if (normalObject != null)
        {
            normalObject.SetActive(true);
        }

        if (normalGoblinAnimator != null && !string.IsNullOrWhiteSpace(normalDeathStateName))
        {
            normalGoblinAnimator.speed = 1f;
            normalGoblinAnimator.Play(normalDeathStateName, 0, 0f);
        }

        if (deathFreezeDelay > 0f)
        {
            yield return new WaitForSeconds(deathFreezeDelay);
        }

        FreezeAnimator(normalGoblinAnimator);
        FreezeAnimator(goblinAnimator);
    }

    private bool ShouldIgnorePointer(Vector2 worldPosition)
    {
        if (settingsMenu == null)
        {
            settingsMenu = FindFirstObjectByType<SettingsMenuController>();
        }

        if (settingsMenu != null && settingsMenu.IsOpen)
        {
            return true;
        }

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == clickArea)
            {
                continue;
            }

            if (hit.GetComponentInParent<TitlePageSpriteButton>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void PrepareTitleEnemyState()
    {
        GameObject normalObject = GetAnimatorObject(normalGoblinObject, normalGoblinAnimator);
        if (normalObject != null)
        {
            normalObject.SetActive(false);
        }

        Animator firstAnimator = speedGoblinAnimator != null ? speedGoblinAnimator : goblinAnimator;
        GameObject speedObject = GetAnimatorObject(speedGoblinObject, speedGoblinAnimator);
        if (speedObject != null)
        {
            speedObject.SetActive(true);
        }

        if (firstAnimator != null && !string.IsNullOrWhiteSpace(speedDanceStateName))
        {
            firstAnimator.speed = 1f;
            firstAnimator.Play(speedDanceStateName, 0, 0f);
        }
    }

    private void FreezeAnimator(Animator animator)
    {
        if (animator != null)
        {
            animator.speed = 0f;
        }
    }

    private Vector3 GetStraightRightTargetPosition(Vector3 startPosition)
    {
        Vector3 targetPosition = startPosition;
        if (goblinTarget != null)
        {
            targetPosition.x = goblinTarget.position.x;
            return targetPosition;
        }

        if (goblinAnimator != null)
        {
            targetPosition.x = goblinAnimator.transform.position.x;
            return targetPosition;
        }

        targetPosition.x += 3f;
        return targetPosition;
    }

    private void DisableRuntimeBulletBehaviour()
    {
        for (int i = 0; i < bulletTransforms.Length; i++)
        {
            Transform bulletTransform = bulletTransforms[i];
            if (bulletTransform == null)
            {
                continue;
            }

            if (bulletTransform.TryGetComponent(out Bullet bullet))
            {
                bullet.enabled = false;
            }

            if (bulletTransform.TryGetComponent(out BulletImpactEffect impactEffect))
            {
                impactEffect.enabled = false;
            }
        }
    }

    private void DisableRuntimeEnemyBehaviour()
    {
        DisableRuntimeEnemyBehaviour(GetAnimatorObject(speedGoblinObject, speedGoblinAnimator));
        DisableRuntimeEnemyBehaviour(GetAnimatorObject(normalGoblinObject, normalGoblinAnimator));
    }

    private static GameObject GetAnimatorObject(GameObject explicitObject, Animator animator)
    {
        if (explicitObject != null)
        {
            return explicitObject;
        }

        return animator != null ? animator.gameObject : null;
    }

    private static void DisableRuntimeEnemyBehaviour(GameObject enemyObject)
    {
        if (enemyObject == null)
        {
            return;
        }

        GoblinEnemy[] enemies = enemyObject.GetComponentsInChildren<GoblinEnemy>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i].enabled = false;
        }

        SpeedGoblinEnemy[] speedEnemies = enemyObject.GetComponentsInChildren<SpeedGoblinEnemy>(true);
        for (int i = 0; i < speedEnemies.Length; i++)
        {
            speedEnemies[i].enabled = false;
        }

        Rigidbody2D[] bodies = enemyObject.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].simulated = false;
        }

        Collider2D[] colliders = enemyObject.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private static void RotateBulletToward(Transform bulletTransform, Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        bulletTransform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static void HideBullet(GameObject bulletObject)
    {
        if (bulletObject == null)
        {
            return;
        }

        SpriteRenderer[] renderers = bulletObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }

        Collider2D[] colliders = bulletObject.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }

    private static bool WasPointerPressedThisFrame(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            screenPosition = Pointer.current.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }

    private static bool TryGetWorldPointerPosition(Vector2 screenPosition, out Vector2 worldPosition)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            worldPosition = default;
            return false;
        }

        Vector3 screenPoint = new Vector3(
            screenPosition.x,
            screenPosition.y,
            Mathf.Abs(camera.transform.position.z));
        Vector3 worldPoint = camera.ScreenToWorldPoint(screenPoint);
        worldPosition = worldPoint;
        return true;
    }
}
