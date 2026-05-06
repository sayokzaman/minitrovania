using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using Unity.Cinemachine;

public class CombatScript : MonoBehaviour
{
    private EnemyManager enemyManager;
    private EnemyDetection enemyDetection;
    private PlayerController movementInput;
    private Animator animator;
    private CinemachineImpulseSource impulseSource;

    [Header("Target")]
    private EnemyScript lockedTarget;

    [Header("Combat Settings")]
    [SerializeField] private bool inCombat = false;
    [SerializeField] private float attackCooldown;

    [Header("States")]
    public bool isAttackingEnemy = false;
    public bool isCountering = false;

    [Header("SFX")]
    private AudioSource audioSource;
    [SerializeField] private AudioClip punchSound;

    [Header("Public References")]
    [SerializeField] private Transform punchPosition;
    [SerializeField] private ParticleSystemScript punchParticle;
    [SerializeField] private GameObject lastHitCamera;
    [SerializeField] private Transform lastHitFocusObject;

    //Coroutines
    private Coroutine counterCoroutine;
    private Coroutine attackCoroutine;
    private Coroutine damageCoroutine;

    [Space]

    //Events
    public UnityEvent<EnemyScript> OnTrajectory;
    public UnityEvent<EnemyScript> OnHit;
    public UnityEvent<EnemyScript> OnCounterAttack;

    int animationCount = 0;
    string[] attacks;

    void Start()
    {
        enemyManager = FindFirstObjectByType<EnemyManager>();
        animator = GetComponent<Animator>();
        enemyDetection = GetComponentInChildren<EnemyDetection>();
        movementInput = GetComponent<PlayerController>();
        impulseSource = GetComponentInChildren<CinemachineImpulseSource>();
        audioSource = GetComponent<AudioSource>();
    }

    //This function gets called whenever the player inputs the punch action
    void AttackCheck()
    {
        if (isAttackingEnemy)
            return;

        if (enemyManager == null || enemyManager.AliveEnemyCount() == 0)
        {
            Attack(null, 0);
            return;
        }

        //Check to see if the detection behavior has an enemy set
        if (enemyDetection.CurrentTarget() == null)
        {
            if (enemyManager.AliveEnemyCount() == 0)
            {
                Attack(null, 0);
                return;
            }
            else
            {
                lockedTarget = enemyManager.RandomEnemy();
            }
        }

        //If the player is moving the movement input, use the "directional" detection to determine the enemy
        if (enemyDetection.InputMagnitude() > .2f)
            lockedTarget = enemyDetection.CurrentTarget();

        //Extra check to see if the locked target was set
        if (lockedTarget == null)
            lockedTarget = enemyManager.RandomEnemy();

        //AttackTarget
        Attack(lockedTarget, TargetDistance(lockedTarget));
    }

    public void Attack(EnemyScript target, float distance)
    {
        //Types of attack animation
        attacks = new string[] { "GroundPunch", "Kick" };

        //Attack nothing in case target is null
        if (target == null)
        {
            AttackType("GroundPunch", attackCooldown, null, 0);
            return;
        }

        if (distance < 15)
        {
            animationCount = (int)Mathf.Repeat((float)animationCount + 1, (float)attacks.Length);
            string attackString = isLastHit() ? attacks[Random.Range(0, attacks.Length)] : attacks[animationCount];
            AttackType(attackString, attackCooldown, target, .65f);
        }
        else
        {
            lockedTarget = null;
            AttackType("GroundPunch", attackCooldown, null, 0);
        }

        //Change impulse
        impulseSource.ImpulseDefinition.AmplitudeGain = Mathf.Max(3, 1 * distance);
    }

    void AttackType(string attackTrigger, float cooldown, EnemyScript target, float movementDuration)
    {
        animator.SetTrigger(attackTrigger);

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);
        attackCoroutine = StartCoroutine(AttackCoroutine(isLastHit() ? 2f : cooldown));

        //Check if last enemy
        if (isLastHit())
            StartCoroutine(FinalBlowCoroutine());

        if (target == null)
            return;

        target.StopMoving();
        MoveTorwardsTarget(target, movementDuration);

        IEnumerator AttackCoroutine(float duration)
        {
            isAttackingEnemy = true;
            if (target != null)
            {
                movementInput.acceleration = 0;
                movementInput.enabled = false;
            }
            yield return new WaitForSeconds(duration);
            isAttackingEnemy = false;
            yield return new WaitForSeconds(.2f);
            movementInput.enabled = true;
            LerpCharacterAcceleration(target);
        }

        IEnumerator FinalBlowCoroutine()
        {
            Time.timeScale = .5f;
            lastHitCamera.SetActive(true);
            lastHitFocusObject.position = lockedTarget.transform.position;
            yield return new WaitForSecondsRealtime(2);
            lastHitCamera.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    void MoveTorwardsTarget(EnemyScript target, float duration)
    {
        OnTrajectory.Invoke(target);
        transform.DOLookAt(target.transform.position, .2f);

        // Instead of DOMove, we calculate the path and move via the controller
        Vector3 startPos = transform.position;
        Vector3 endPos = TargetOffset(target.transform);

        DOVirtual.Float(0, 1, duration, (value) =>
        {
            Vector3 targetPos = Vector3.Lerp(startPos, endPos, value);
            Vector3 moveDiff = targetPos - transform.position;
            movementInput.GetComponent<CharacterController>().Move(moveDiff);
        }).SetEase(Ease.OutCubic);
    }

    void CounterCheck()
    {
        //Initial check
        if (isCountering || isAttackingEnemy || !enemyManager.AnEnemyIsPreparingAttack())
            return;

        lockedTarget = ClosestCounterEnemy();
        OnCounterAttack.Invoke(lockedTarget);

        if (TargetDistance(lockedTarget) > 2)
        {
            Attack(lockedTarget, TargetDistance(lockedTarget));
            return;
        }

        float duration = .2f;
        animator.SetTrigger("Dodge");
        transform.DOLookAt(lockedTarget.transform.position, .2f);
        transform.DOMove(transform.position + lockedTarget.transform.forward, duration);

        if (counterCoroutine != null)
            StopCoroutine(counterCoroutine);
        counterCoroutine = StartCoroutine(CounterCoroutine(duration));

        IEnumerator CounterCoroutine(float duration)
        {
            isCountering = true;
            movementInput.enabled = false;
            yield return new WaitForSeconds(duration);
            Attack(lockedTarget, TargetDistance(lockedTarget));
            isCountering = false;

        }
    }

    float TargetDistance(EnemyScript target)
    {
        return Vector3.Distance(transform.position, target.transform.position);
    }

    public Vector3 TargetOffset(Transform target)
    {
        Vector3 position;
        position = target.position;
        return Vector3.MoveTowards(position, transform.position, .95f);
    }

    public void HitEvent()
    {
        if (lockedTarget == null || enemyManager.AliveEnemyCount() == 0)
            return;

        if (enemyManager.mainAIEnabled == false)
        {
            enemyManager.mainAIEnabled = true;
            enemyManager.StartAI();
        }

        OnHit.Invoke(lockedTarget);

        //Polish
        punchParticle.PlayParticleAtPosition(punchPosition.position);
        audioSource.clip = punchSound;
        audioSource.Play();
    }

    public void DamageEvent()
    {
        animator.SetTrigger("Hit");

        if (damageCoroutine != null)
            StopCoroutine(damageCoroutine);
        damageCoroutine = StartCoroutine(DamageCoroutine());

        IEnumerator DamageCoroutine()
        {
            movementInput.enabled = false;
            yield return new WaitForSeconds(.5f);
            movementInput.enabled = true;
            LerpCharacterAcceleration(null);
        }
    }

    EnemyScript ClosestCounterEnemy()
    {
        float minDistance = 100;
        int finalIndex = 0;

        for (int i = 0; i < enemyManager.allEnemies.Length; i++)
        {
            EnemyScript enemy = enemyManager.allEnemies[i].enemyScript;

            if (enemy.IsPreparingAttack())
            {
                if (Vector3.Distance(transform.position, enemy.transform.position) < minDistance)
                {
                    minDistance = Vector3.Distance(transform.position, enemy.transform.position);
                    finalIndex = i;
                }
            }
        }

        return enemyManager.allEnemies[finalIndex].enemyScript;

    }

    void LerpCharacterAcceleration(EnemyScript target)
    {
        if (target != null)
        {
            movementInput.acceleration = 0;
            DOVirtual.Float(0, 1, .6f, ((acceleration) => movementInput.acceleration = acceleration));
        }
    }

    bool isLastHit()
    {
        if (lockedTarget == null)
            return false;

        return enemyManager.AliveEnemyCount() == 1 && lockedTarget.health <= 1;
    }

    #region Input

    private void OnCounter()
    {
        CounterCheck();
    }

    private void OnAttack()
    {
        AttackCheck();
    }

    #endregion

}
