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
    [SerializeField] private InputActionReference attackAction;

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

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float runThreshold = 0.05f;

    [Header("Facing (3D)")]
    [SerializeField] private Transform visualPivot;
    [Tooltip("Turning speed in degrees/second. 0 = instant flip. Try 360..1080.")]
    [SerializeField] private float turnSmoothSpeed = 720f;

    [Header("Attack")]
    [SerializeField] private bool lockMovementDuringAttack = true;
    [SerializeField] private float attackCooldown = 0.35f;
    [Tooltip("How long the attack state locks movement (seconds). Should roughly match the clip length.")]
    [SerializeField] private float attackLockDuration = 0.25f;

    [Header("Attack Hit (Prototype)")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 0.6f;
    [Tooltip("When to apply the hit check after starting attack (seconds).")]
    [SerializeField] private float hitTime = 0.12f;
    [SerializeField] private LayerMask hittableLayers;

    private static readonly int RunHash = Animator.StringToHash("Run");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private Rigidbody rb;
    private Vector2 moveInput;

    private bool isGrounded;
    private bool wasGrounded;

    private int jumpsUsed;

    private bool isDashing;
    private bool dashHeld;

    private float lastNonZeroMoveX = 1f;
    private float nextDashTime;

    private bool airDashUsed;

    // Facing state
    private Quaternion rightRot;
    private Quaternion leftRot;
    private Quaternion targetRot;
    private bool facingRight = true;

    // Attack state
    private bool isAttacking;
    private float nextAttackTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (visualPivot == null)
        {
            if (animator != null && animator.transform.parent != null)
                visualPivot = animator.transform.parent;
            else
                visualPivot = transform;
        }

        // jobbra = induló rotáció
        rightRot = visualPivot.localRotation;
        leftRot = rightRot * Quaternion.Euler(0f, 180f, 0f);
        facingRight = true;
        targetRot = rightRot;
        if (turnSmoothSpeed <= 0f) visualPivot.localRotation = targetRot;
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (jumpAction != null) jumpAction.action.Enable();
        if (dashAction != null) dashAction.action.Enable();
        if (attackAction != null) attackAction.action.Enable();

        if (jumpAction != null) jumpAction.action.performed += OnJump;

        if (dashAction != null)
        {
            dashAction.action.started += OnDashStarted;
            dashAction.action.canceled += OnDashCanceled;
        }

        if (attackAction != null)
            attackAction.action.performed += OnAttack;
    }

    private void OnDisable()
    {
        if (jumpAction != null) jumpAction.action.performed -= OnJump;

        if (dashAction != null)
        {
            dashAction.action.started -= OnDashStarted;
            dashAction.action.canceled -= OnDashCanceled;
        }

        if (attackAction != null)
            attackAction.action.performed -= OnAttack;

        if (moveAction != null) moveAction.action.Disable();
        if (jumpAction != null) jumpAction.action.Disable();
        if (dashAction != null) dashAction.action.Disable();
        if (attackAction != null) attackAction.action.Disable();
    }

    private void Update()
    {
        moveInput = (moveAction != null) ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        if (Mathf.Abs(moveInput.x) > 0.01f)
            lastNonZeroMoveX = Mathf.Sign(moveInput.x);

        wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        if (!wasGrounded && isGrounded && !isDashing)
        {
            jumpsUsed = 0;
            airDashUsed = false;
        }

        if (isGrounded && !isDashing)
            airDashUsed = false;

        UpdateAnimator();
        UpdateFacingTarget();
        SmoothRotateToTarget();
        UpdateAttackPointFacing();
    }

    private void FixedUpdate()
    {
        if (isDashing) return;

        if (lockMovementDuringAttack && isAttacking)
        {
            // támadás alatt álljunk meg vízszintesen (prototípus)
            Vector3 vv = rb.linearVelocity;
            vv.x = 0f;
            vv.z = 0f;
            rb.linearVelocity = vv;
            return;
        }

        Vector3 v = rb.linearVelocity;
        v.x = moveInput.x * moveSpeed;
        v.z = 0f;
        rb.linearVelocity = v;
    }

    // ---------------- Jump ----------------
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

    // ---------------- Dash (hold) ----------------
    private void OnDashStarted(InputAction.CallbackContext ctx)
    {
        dashHeld = true;

        if (Time.time < nextDashTime) return;
        if (isDashing) return;

        if (!isGrounded && airDashUsed) return;
        if (!isGrounded) airDashUsed = true;

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

    // ---------------- Attack ----------------
    private void OnAttack(InputAction.CallbackContext ctx)
    {
        if (Time.time < nextAttackTime) return;
        if (isDashing) return; // protó: dash alatt ne támadjon

        nextAttackTime = Time.time + attackCooldown;

        // anim trigger
        if (animator != null)
            animator.SetTrigger(AttackHash);

        // lock (prototípus)
        if (lockMovementDuringAttack)
            StartCoroutine(AttackLockRoutine());

        // hit check (prototípus, 1 pillanatban)
        StartCoroutine(AttackHitRoutine());
    }

    private IEnumerator AttackLockRoutine()
    {
        isAttacking = true;
        yield return new WaitForSeconds(attackLockDuration);
        isAttacking = false;
    }

    private IEnumerator AttackHitRoutine()
    {
        yield return new WaitForSeconds(hitTime);

        if (attackPoint == null) yield break;

        Collider[] hits = Physics.OverlapSphere(
            attackPoint.position,
            attackRange,
            hittableLayers,
            QueryTriggerInteraction.Ignore
        );

        // Prototípus: csak logolunk
        for (int i = 0; i < hits.Length; i++)
        {
            Debug.Log($"Hit: {hits[i].name}");
            // Később: ITakeDamage / Health komponens meghívása
        }
    }

    private void UpdateAttackPointFacing()
    {
        if (attackPoint == null) return;

        // AttackPointot jobbra/balra toljuk X-ben (a local X előjele alapján)
        Vector3 lp = attackPoint.localPosition;
        float absX = Mathf.Abs(lp.x);
        lp.x = facingRight ? absX : -absX;
        attackPoint.localPosition = lp;
    }

    // ---------------- Animation + Facing ----------------
    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool isRunning = Mathf.Abs(moveInput.x) > runThreshold;
        if (isDashing) isRunning = false;
        if (lockMovementDuringAttack && isAttacking) isRunning = false;

        animator.SetBool(RunHash, isRunning);
    }

    private void UpdateFacingTarget()
    {
        if (Mathf.Abs(moveInput.x) < 0.01f) return;

        bool wantRight = moveInput.x > 0f;
        if (wantRight != facingRight)
        {
            facingRight = wantRight;
            targetRot = facingRight ? rightRot : leftRot;
        }
    }

    private void SmoothRotateToTarget()
    {
        if (visualPivot == null) return;

        if (turnSmoothSpeed <= 0f)
        {
            visualPivot.localRotation = targetRot;
            return;
        }

        float maxDegrees = turnSmoothSpeed * Time.deltaTime;
        visualPivot.localRotation = Quaternion.RotateTowards(visualPivot.localRotation, targetRot, maxDegrees);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

        if (attackPoint != null)
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
#endif
}
