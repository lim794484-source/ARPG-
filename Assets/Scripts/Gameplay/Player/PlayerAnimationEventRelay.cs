using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerBow bow;

    private void Awake()
    {
        if (combat == null)
            combat = GetComponentInChildren<PlayerCombat>(true);
        if (bow == null)
            bow = GetComponentInChildren<PlayerBow>(true);
    }

    public void DealDamage()
    {
        if (combat != null && combat.IsActive)
            combat.DealDamage();
    }

    public void FinishCombat()
    {
        if (combat != null && combat.IsActive)
            combat.FinishCombat();
    }

    public void HandleShootingAiming()
    {
        if (bow != null && bow.IsActive)
            bow.HandleShootingAiming();
    }

    public void Shoot()
    {
        if (bow != null && bow.IsActive)
            bow.Shoot();
    }

    public void ShootingDone()
    {
        if (bow != null && bow.IsActive)
            bow.ShootingDone();
    }
}
