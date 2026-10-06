using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 120f;

    [Header("Shoot Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float fireRate = 0.2f;

    private InputAction moveAction;
    private InputAction rotateAction;
    private InputAction shootAction;

    private float nextFireTime = 0f;

    private void Awake()
    {
        // Movimento frente/trás (setas ↑ ↓)
        moveAction = new InputAction(
            name: "Move",
            type: InputActionType.Value,
            binding: "<Keyboard>/upArrow"
        );

        moveAction.AddCompositeBinding("1DAxis")
            .With("Positive", "<Keyboard>/upArrow")
            .With("Negative", "<Keyboard>/downArrow");

        // Rotação esquerda/direita (setas ← →)
        rotateAction = new InputAction(
            name: "Rotate",
            type: InputActionType.Value
        );

        rotateAction.AddCompositeBinding("1DAxis")
            .With("Positive", "<Keyboard>/rightArrow")
            .With("Negative", "<Keyboard>/leftArrow");

        // Tiro (Espaço)
        shootAction = new InputAction(
            name: "Shoot",
            type: InputActionType.Button,
            binding: "<Keyboard>/space"
        );
    }

    private void OnEnable()
    {
        moveAction.Enable();
        rotateAction.Enable();
        shootAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        rotateAction.Disable();
        shootAction.Disable();
    }

    private void Update()
    {
        float moveValue = moveAction.ReadValue<float>();
        float rotateValue = rotateAction.ReadValue<float>();

        // Movimento frente/trás
        transform.Translate(Vector3.forward * moveValue * moveSpeed * Time.deltaTime);

        // Rotação
        transform.Rotate(Vector3.up * rotateValue * rotationSpeed * Time.deltaTime);

        // Tiro
        if (shootAction.IsPressed() && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("Player: Assign 'bulletPrefab' and 'firePoint' in the Inspector!");
            return;
        }

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = firePoint.forward * bulletSpeed;
        }

        Destroy(bullet, 3f);
    }
}