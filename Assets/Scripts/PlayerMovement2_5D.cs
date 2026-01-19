using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement2_5D : MonoBehaviour
{
    [Header("Input Actions (drag from your Input Actions asset)")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference dashAction;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 7f;

    [Header("Jump")]
    [SerializeField] private float jumpImpulse = 7.5f;
    [SerializeField] private bool enableDoubleJump = true;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.18f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Dash Mode")]
    [Tooltip("If enabled, dash uses the Strong parameters automatically.")]
    [SerializeField] private bool enableStrongDash = false;

    [Header("Dash (Weak)")]
    [SerializeField] private float weakDashMaxDistance = 1.0f;
    [SerializeField] private float weakDashSpeed = 12.0f;

    [Header("Dash (Strong)")]
    [SerializeField] private float strongDashMaxDistance = 2.0f;
    [SerializeField] private float strongDashSpeed = 18.0f;

    [Header("Dash Rules")]
    [SerializeField] private float dashCooldown = 0.15f;

    private Rigidbody rb;
    private Vector2 moveInput;

    private bool isGrounded;
    private bool wasGrounded;

    private int jumpsUsed;

    private bool isDashing;
    private bool dashHeld;

    private float lastNonZeroMoveX = 1f;
    private float nextDashTime;

    // ✅ levegőben csak 1 dash
    private bool airDashUsed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints = RigidbodyConstraints.FreezeRotation
                       | RigidbodyConstraints.FreezePositionZ;
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        dashAction.action.Enable();

        jumpAction.action.performed += OnJump;
        dashAction.action.started += OnDashStarted;
        dashAction.action.canceled += OnDashCanceled;
    }

    private void OnDisable()
    {
        jumpAction.action.performed -= OnJump;
        dashAction.action.started -= OnDashStarted;
        dashAction.action.canceled -= OnDashCanceled;

        moveAction.action.Disable();
        jumpAction.action.Disable();
        dashAction.action.Disable();
    }

    private void Update()
    {
        moveInput = moveAction.action.ReadValue<Vector2>();

        if (Mathf.Abs(moveInput.x) > 0.01f)
            lastNonZeroMoveX = Mathf.Sign(moveInput.x);

        wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        // Landing (false -> true): reset jump + air dash
        if (!wasGrounded && isGrounded && !isDashing)
        {
            jumpsUsed = 0;
            airDashUsed = false; // ✅ újratölt földet érés után
        }

        // Biztonsági reset: ha áll a talajon, legyen újra elérhető
        if (isGrounded && !isDashing)
        {
            airDashUsed = false;
        }
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        Vector3 v = rb.linearVelocity;
        v.x = moveInput.x * moveSpeed;
        v.z = 0f;
        rb.linearVelocity = v;
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (isDashing) return;

        int allowedJumps = enableDoubleJump ? 2 : 1;

        if (isGrounded)
        {
            DoJump();
            jumpsUsed = 1;
            return;
        }

        if (jumpsUsed < allowedJumps)
        {
            DoJump();
            jumpsUsed++;
        }
    }

    private void DoJump()
    {
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * jumpImpulse, ForceMode.Impulse);
    }

    private void OnDashStarted(InputAction.CallbackContext ctx)
    {
        dashHeld = true;

        if (Time.time < nextDashTime) return;
        if (isDashing) return;

        // ✅ Levegőben csak egyszer dash-elhet
        if (!isGrounded && airDashUsed) return;

        // Ha levegőben indítja, akkor “elfogyasztjuk”
        if (!isGrounded)
            airDashUsed = true;

        StartCoroutine(DashHoldRoutine());
    }

    private void OnDashCanceled(InputAction.CallbackContext ctx)
    {
        dashHeld = false;
    }

    private IEnumerator DashHoldRoutine()
    {
        isDashing = true;
        nextDashTime = Time.time + dashCooldown;

        float maxDistance = enableStrongDash ? strongDashMaxDistance : weakDashMaxDistance;
        float dashSpeed = enableStrongDash ? strongDashSpeed : weakDashSpeed;

        float dir = (Mathf.Abs(moveInput.x) > 0.01f) ? Mathf.Sign(moveInput.x) : lastNonZeroMoveX;

        bool oldUseGravity = rb.useGravity;
        rb.useGravity = false;

        rb.linearVelocity = new Vector3(dashSpeed * dir, 0f, 0f);

        float traveled = 0f;

        while (dashHeld && traveled < maxDistance)
        {
            float step = dashSpeed * Time.fixedDeltaTime;
            traveled += step;

            rb.linearVelocity = new Vector3(dashSpeed * dir, 0f, 0f);

            yield return new WaitForFixedUpdate();
        }

        rb.useGravity = oldUseGravity;

        Vector3 v = rb.linearVelocity;
        v.x = moveInput.x * moveSpeed;
        v.z = 0f;
        rb.linearVelocity = v;

        isDashing = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
#endif
}
