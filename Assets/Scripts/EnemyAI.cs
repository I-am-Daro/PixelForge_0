using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyAI : MonoBehaviour
{
    [Header("Health (pips)")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth = 3;

    [Header("Contact Damage")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float contactHitCooldown = 0.6f;
    [Tooltip("Which layers can be damaged by touching this enemy (usually Player).")]
    [SerializeField] private LayerMask damageableLayers;

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath = true;

    private float nextContactHitTime;

    private void Awake()
    {
        if (maxHealth < 1) maxHealth = 1;
        currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
    }

    // ---- Called by player attack (prototype) ----
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (currentHealth <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);

        if (currentHealth == 0)
            Die();
    }

    private void Die()
    {
        // Késõbb: anim, loot, stb.
        if (destroyOnDeath)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    // ---- Contact damage (works with trigger OR collision) ----
    private void OnTriggerStay(Collider other)
    {
        TryContactDamage(other);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryContactDamage(collision.collider);
    }

    private void TryContactDamage(Collider other)
    {
        if (Time.time < nextContactHitTime) return;

        if (((1 << other.gameObject.layer) & damageableLayers.value) == 0)
            return;

        // Player Health keresése: lehet a root-on van, de collider child-on
        var playerHealth = other.GetComponentInParent<Health>();
        if (playerHealth == null) return;

        playerHealth.TakeDamage(contactDamage);
        nextContactHitTime = Time.time + contactHitCooldown;
    }

/*#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Debug: jelenítsük meg kb. hol van az enemy (nem kötelezõ)
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, 0.25f);
    }
#endif*/
}
