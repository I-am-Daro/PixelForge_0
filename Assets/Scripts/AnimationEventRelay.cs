using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerMovement2_5D receiver;

    private void Awake()
    {
        // Ha nincs kézzel megadva, keresse meg a parentek között
        if (receiver == null)
            receiver = GetComponentInParent<PlayerMovement2_5D>();
    }

    // Animation Events ide jönnek, és továbbadjuk
    public void Anim_SummonWeapon() => receiver?.Anim_SummonWeapon();
    public void Anim_DespawnWeapon() => receiver?.Anim_DespawnWeapon();
    public void Anim_DoHit() => receiver?.Anim_DoHit();
}
