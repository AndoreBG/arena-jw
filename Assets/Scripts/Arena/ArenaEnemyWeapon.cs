using UnityEngine;

/// <summary>Turret aiming and ranged fire. Uses pooled shots, explicit ownership and faction.</summary>
public sealed class ArenaEnemyWeapon : MonoBehaviour
{
    [SerializeField] private Transform turret, barrel, muzzle;
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float turnSpeed = 90f, elevationSpeed = 40f;
    [SerializeField, Min(0f)] private float fireRange = 22f, fireRate = 1.6f, projectileSpeed = 28f, projectileLifetime = 4f, damage = 20f;
    [SerializeField, Min(0f)] private float aimTolerance = 3f;
    [SerializeField] private TankProjectilePool projectilePool;
    private Collider[] ownerColliders;
    private float nextFire;
    private bool canFire;
    private void Awake() { ownerColliders = GetComponentsInChildren<Collider>(true); if (projectilePool == null) projectilePool = TankProjectilePool.Instance; }
    public void SetTarget(Transform value) => target = value;
    public void SetCanFire(bool value) => canFire = value;
    private void Update()
    {
        if (target == null || turret == null || barrel == null || muzzle == null) return;
        Vector3 delta = target.position + Vector3.up - muzzle.position;
        Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
        if (flat.sqrMagnitude > 0.001f)
        {
            Quaternion desired = Quaternion.LookRotation(flat, Vector3.up);
            turret.rotation = Quaternion.RotateTowards(turret.rotation, desired, turnSpeed * Time.deltaTime);
        }
        Vector3 localAim = barrel.parent != null ? barrel.parent.InverseTransformDirection(delta.normalized) : transform.InverseTransformDirection(delta.normalized);
        float pitch = -Mathf.Atan2(localAim.y, new Vector2(localAim.x, localAim.z).magnitude) * Mathf.Rad2Deg;
        Quaternion barrelDesired = Quaternion.Euler(pitch, barrel.localEulerAngles.y, 0f);
        barrel.localRotation = Quaternion.RotateTowards(barrel.localRotation, barrelDesired, elevationSpeed * Time.deltaTime);
        if (!canFire || !ArenaManager.IsPlaying || Time.time < nextFire || delta.magnitude > fireRange) return;
        if (Vector3.Angle(muzzle.forward, delta) > aimTolerance) return;
        if (projectilePool == null) projectilePool = TankProjectilePool.Instance;
        if (projectilePool == null) return;
        TankShotData shot = new TankShotData(gameObject, TankShotFaction.Enemy, damage);
        TankProjectile p = projectilePool.RequestProjectile(muzzle.position, muzzle.forward, projectileSpeed, projectileLifetime, ownerColliders, shot, out _);
        if (p != null) nextFire = Time.time + Mathf.Max(0.05f, fireRate);
    }
}
