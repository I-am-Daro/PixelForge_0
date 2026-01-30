using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private bool destroyOnHit = false;

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;

        if (other.TryGetComponent<IDamageable>(out var dmg))
        {
            dmg.TakeDamage(damage);

            if (destroyOnHit)
                Destroy(gameObject);
        }
    }
}
