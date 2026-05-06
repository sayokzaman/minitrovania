using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private Animator anim;
    private Camera cam;
    private CharacterController controller;

    private Vector3 desiredMoveDirection;
    private Vector3 moveVector;

    public Vector2 moveAxis;
    private float verticalVel;
    private bool jumpQueued;
    private float currentInputMagnitude;
    private bool sprintInputPressed;
    private float animatorVerticalSpeed;
    private float runBlend;
    [SerializeField] float runBlendSpeed = 8f;

    [Header("Settings")]
    [SerializeField] float maxStamina = 5f;
    [SerializeField] float staminaDrainRate = 1f;
    [SerializeField] float staminaRegenRate = 0.5f;
    [SerializeField] float stamina = 0f;
    [SerializeField] float startStaminaToRun = 2.0f;
    [SerializeField] float stopStaminaToRun = 0.1f;
    private bool exhausted;
    [SerializeField] float walkSpeed;
    [SerializeField] float runSpeed;
    [SerializeField] float rotationSpeed = 0.1f;
    [SerializeField] float jumpHeight = 1.6f;
    [SerializeField] float gravity = -25f;
    public float acceleration = 1;

    [Header("Booleans")]
    [SerializeField] bool blockRotationPlayer;
    private bool isGrounded;
    private bool isRunning;


    void Start()
    {
        anim = this.GetComponent<Animator>();
        cam = Camera.main;
        controller = this.GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        stamina = maxStamina;
    }

    void Update()
    {

        // Determine running permission with hysteresis so start/stop thresholds differ
        bool desiredRunning;
        if (isRunning)
            desiredRunning = sprintInputPressed && !exhausted && stamina > stopStaminaToRun;
        else
            desiredRunning = sprintInputPressed && !exhausted && stamina > startStaminaToRun;

        isRunning = desiredRunning;

        // Smoothly blend between walk and run to avoid abrupt animation/movement jumps
        runBlend = Mathf.MoveTowards(runBlend, desiredRunning ? 1f : 0f, runBlendSpeed * Time.deltaTime);

        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVel < 0f)
            verticalVel = -2f;

        if (jumpQueued && isGrounded)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpQueued = false;
            anim.SetTrigger("Jump");
        }

        // Only apply gravity when in the air to avoid small negative accumulation while grounded
        if (!isGrounded)
            verticalVel += gravity * Time.deltaTime;

        moveVector = new Vector3(0f, verticalVel, 0f);
        controller.Move(moveVector * Time.deltaTime);

        anim.SetBool("IsGrounded", isGrounded);
        // Smooth VerticalSpeed to avoid frame jitter in transitions
        float targetV = isGrounded ? 0f : verticalVel;
        animatorVerticalSpeed = Mathf.Lerp(animatorVerticalSpeed, targetV, 15f * Time.deltaTime);
        anim.SetFloat("VerticalSpeed", animatorVerticalSpeed);

        InputMagnitude();

        // Drain or regenerate stamina
        if (isRunning && currentInputMagnitude > 0.1f)
        {
            stamina -= staminaDrainRate * Time.deltaTime;
        }
        else
        {
            stamina += staminaRegenRate * Time.deltaTime;
        }

        stamina = Mathf.Clamp(stamina, 0f, maxStamina);

        // When stamina depletes, mark exhausted and disallow running until recovered above threshold
        if (stamina <= 0f)
        {
            stamina = 0f;
            exhausted = true;
            isRunning = false;
            runBlend = 0f;
        }

        // Recover from exhausted state only when stamina reaches the minimum to start running
        if (exhausted && stamina >= startStaminaToRun)
        {
            exhausted = false;
        }
    }

    void PlayerMoveAndRotation()
    {
        var camera = Camera.main;
        var forward = cam.transform.forward;
        var right = cam.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        desiredMoveDirection = forward * moveAxis.y + right * moveAxis.x;

        float currentSpeed = Mathf.Lerp(walkSpeed, runSpeed, runBlend);

        if (blockRotationPlayer == false && desiredMoveDirection != Vector3.zero)
        {
            //Camera
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), rotationSpeed);
            controller.Move(desiredMoveDirection * Time.deltaTime * (currentSpeed * acceleration));
        }
        else
        {
            //Strafe
            controller.Move((transform.forward * moveAxis.y + transform.right * moveAxis.y) * Time.deltaTime * (currentSpeed * acceleration));
        }
    }

    public void LookAt(Vector3 pos)
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(pos), rotationSpeed);
    }

    public void RotateToCamera(Transform t)
    {
        var forward = cam.transform.forward;

        desiredMoveDirection = forward;
        Quaternion lookAtRotation = Quaternion.LookRotation(desiredMoveDirection);
        Quaternion lookAtRotationOnly_Y = Quaternion.Euler(transform.rotation.eulerAngles.x, lookAtRotation.eulerAngles.y, transform.rotation.eulerAngles.z);

        t.rotation = Quaternion.Slerp(transform.rotation, lookAtRotationOnly_Y, rotationSpeed);
    }

    void InputMagnitude()
    {
        //Calculate the Input Magnitude
        float inputMagnitude = new Vector2(moveAxis.x, moveAxis.y).sqrMagnitude;
        currentInputMagnitude = inputMagnitude;

        //Physically move player
        if (inputMagnitude > 0.1f)
        {
            float targetAnimValue = Mathf.Lerp(1.0f, 2.0f, runBlend);

            anim.SetFloat("InputMagnitude", inputMagnitude * targetAnimValue, .1f, Time.deltaTime);

            PlayerMoveAndRotation();
        }
        else
        {
            anim.SetFloat("InputMagnitude", 0, 0.1f, Time.deltaTime);
        }
    }

    #region Input

    public void OnMove(InputValue value)
    {
        moveAxis.x = value.Get<Vector2>().x;
        moveAxis.y = value.Get<Vector2>().y;
    }

    public void OnSprint(InputValue value)
    {
        // store the sprint input; actual `isRunning` is gated by stamina in Update
        sprintInputPressed = value.isPressed;
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            jumpQueued = true;
    }

    #endregion

    private void OnDisable()
    {
        anim.SetFloat("InputMagnitude", 0);
    }
}
