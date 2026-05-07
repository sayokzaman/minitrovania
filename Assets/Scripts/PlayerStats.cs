using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerStats : MonoBehaviour
{
    private Animator animator;
    private Coroutine DeathCoroutine;
    private PlayerInput playerInput;
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina = 100f;

    [Header("Bools")]
    [SerializeField] private bool isDead = false;

    // Events: pass the new current value (not normalized)
    public UnityEvent<float> onHealthChanged;
    public UnityEvent<float> onStaminaChanged;
    public UnityEvent onDeath;

    // Read-only properties
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;

    // Normalized helpers (0..1)
    public float HealthPercent => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    public float StaminaPercent => maxStamina > 0f ? currentStamina / maxStamina : 0f;
    public bool IsDead => isDead;

    private void Start()
    {
        animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();
    }
    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        if (onHealthChanged == null) onHealthChanged = new UnityEvent<float>();
        if (onStaminaChanged == null) onStaminaChanged = new UnityEvent<float>();
        if (onDeath == null) onDeath = new UnityEvent();
    }

    private void Update()
    {
        if (currentHealth <= 0f)
        {
            Death();
        }
    }

    // Health
    public void SetMaxHealth(float value, bool keepCurrentPercent = true)
    {
        if (value <= 0f) return;
        float prevPercent = HealthPercent;
        maxHealth = value;
        currentHealth = keepCurrentPercent ? maxHealth * prevPercent : Mathf.Clamp(currentHealth, 0f, maxHealth);
        onHealthChanged.Invoke(currentHealth);
    }

    public void ModifyHealth(float delta)
    {
        if (delta == 0f) return;
        currentHealth = Mathf.Clamp(currentHealth + delta, 0f, maxHealth);
        onHealthChanged.Invoke(currentHealth);
        if (currentHealth <= 0f) onDeath.Invoke();
    }

    public void RestoreToMaxHealth()
    {
        currentHealth = maxHealth;
        onHealthChanged.Invoke(currentHealth);
    }

    // Stamina
    public void SetMaxStamina(float value, bool keepCurrentPercent = true)
    {
        if (value <= 0f) return;
        float prevPercent = StaminaPercent;
        maxStamina = value;
        currentStamina = keepCurrentPercent ? maxStamina * prevPercent : Mathf.Clamp(currentStamina, 0f, maxStamina);
        onStaminaChanged.Invoke(currentStamina);
    }

    public void ModifyStamina(float delta)
    {
        if (delta == 0f) return;
        currentStamina = Mathf.Clamp(currentStamina + delta, 0f, maxStamina);
        onStaminaChanged.Invoke(currentStamina);
    }

    public void RestoreToMaxStamina()
    {
        currentStamina = maxStamina;
        onStaminaChanged.Invoke(currentStamina);
    }


    public void Death()
    {
        animator.SetTrigger("Death");

        DeathCoroutine = StartCoroutine(DeathDelay());

        isDead = true;

        IEnumerator DeathDelay()
        {
            yield return new WaitForSeconds(0.2f);
            this.enabled = false;
            playerInput.enabled = false;
            animator.SetTrigger("DeathPose");
        }
    }
}
