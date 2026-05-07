using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using System.Linq;

public class EnemyScript : MonoBehaviour
{
    //Declarations
    private Animator animator;
    private CombatScript playerCombat;
    private EnemyManager enemyManager;
    private EnemyDetection enemyDetection;
    private CharacterController characterController;
    private PlayerStats playerStats;

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;
    private int _currentWaypointIndex = 0;
    private int _direction = 1;
    [SerializeField] private float waitTimeAtWaypoint = 2f;

    [Header("Stats")]
    public int health = 3;
    private float moveSpeed = 4f;
    private Vector3 moveDirection;
    [SerializeField] private float stunDuration = 0.1f;

    [Header("States")]
    [SerializeField] private bool isPreparingAttack;
    [SerializeField] private bool isMoving;
    [SerializeField] private bool isRetreating;
    [SerializeField] private bool isLockedTarget;
    [SerializeField] private bool isStunned;
    [SerializeField] private bool isWaiting = true;

    [Header("Polish")]
    [SerializeField] private ParticleSystem counterParticle;
    [SerializeField] private ParticleSystemScript punchParticle;
    [SerializeField] private Transform punchPosition;
    [Header("SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip punchSound;

    private Coroutine PatrolCoroutine;
    private Coroutine PrepareAttackCoroutine;
    private Coroutine RetreatCoroutine;
    private Coroutine DeathCoroutine;
    private Coroutine DamageCoroutine;
    private Coroutine MovementCoroutine;

    //Events
    public UnityEvent<EnemyScript> OnDamage;
    public UnityEvent<EnemyScript> OnStopMoving;
    public UnityEvent<EnemyScript> OnRetreat;

    void Start()
    {
        enemyManager = GetComponentInParent<EnemyManager>();

        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();

        playerCombat = FindFirstObjectByType<CombatScript>();
        if (playerCombat != null)
        {
            enemyDetection = playerCombat.GetComponentInChildren<EnemyDetection>();
        }

        playerStats = playerCombat.GetComponentInParent<PlayerStats>();

        playerCombat.OnHit.AddListener((x) => OnPlayerHit(x));
        playerCombat.OnCounterAttack.AddListener((x) => OnPlayerCounter(x));
        playerCombat.OnTrajectory.AddListener((x) => OnPlayerTrajectory(x));

        MovementCoroutine = StartCoroutine(EnemyMovement());
    }

    IEnumerator EnemyMovement()
    {
        //Waits until the enemy is not assigned to no action like attacking or retreating
        yield return new WaitUntil(() => isWaiting == true);

        int randomChance = Random.Range(0, 2);

        if (randomChance == 1)
        {
            int randomDir = Random.Range(0, 2);
            moveDirection = randomDir == 1 ? Vector3.right : Vector3.left;
            isMoving = true;
        }
        else
        {
            StopMoving();
        }

        yield return new WaitForSeconds(1);

        MovementCoroutine = StartCoroutine(EnemyMovement());
    }

    void Update()
    {
        if (!enemyManager.mainAIEnabled)
        {
            // Only start if it's not already running
            if (PatrolCoroutine == null && waypoints.Length > 0)
            {
                PatrolCoroutine = StartCoroutine(PatrolRoutine());
            }
        }
        else
        {
            // If AI is disabled (combat mode), stop the patrol
            if (PatrolCoroutine != null)
            {
                StopCoroutine(PatrolCoroutine);
                PatrolCoroutine = null;
            }

            if (playerStats.IsDead)
            {
                StopMoving();
            }

            // Constantly look at player
            transform.LookAt(new Vector3(playerCombat.transform.position.x, transform.position.y, playerCombat.transform.position.z));
            MoveEnemy(moveDirection);
        }
    }

    private IEnumerator PatrolRoutine()
    {
        while (true)
        {
            Transform target = waypoints[_currentWaypointIndex];

            while (GetFlatDistance(transform.position, target.position) > 0.2f)
            {
                Vector3 direction = (target.position - transform.position);
                Vector3 moveDir = new Vector3(direction.x, 0, direction.z).normalized;

                animator.SetFloat("InputMagnitude", moveDir.magnitude, .2f, Time.deltaTime);
                characterController.Move(moveDir * moveSpeed * Time.deltaTime);

                if (moveDir != Vector3.zero)
                {
                    transform.forward = Vector3.Slerp(transform.forward, moveDir, 8f * Time.deltaTime);
                }

                yield return null;
            }

            animator.SetFloat("InputMagnitude", 0);
            yield return new WaitForSeconds(waitTimeAtWaypoint);

            // --- PING-PONG LOGIC ---

            // If we are at the last waypoint, start going backwards
            if (_currentWaypointIndex >= waypoints.Length - 1)
            {
                _direction = -1;
            }
            // If we reached the first waypoint again, start going forwards
            else if (_currentWaypointIndex <= 0)
            {
                _direction = 1;
            }

            _currentWaypointIndex += _direction;

            // Safety check to keep index within bounds
            _currentWaypointIndex = Mathf.Clamp(_currentWaypointIndex, 0, waypoints.Length - 1);
        }
    }

    private float GetFlatDistance(Vector3 a, Vector3 b)
    {
        Vector2 aFlat = new Vector2(a.x, a.z);
        Vector2 bFlat = new Vector2(b.x, b.z);
        return Vector2.Distance(aFlat, bFlat);
    }

    //Listened event from Player Animation
    void OnPlayerHit(EnemyScript target)
    {
        if (target == this)
        {
            StopEnemyCoroutines();
            DamageCoroutine = StartCoroutine(HitCoroutine());

            enemyDetection.SetCurrentTarget(null);
            isLockedTarget = false;
            OnDamage.Invoke(this);

            health--;

            if (health <= 0)
            {
                Death();
                return;
            }

            animator.SetTrigger("Hit");
            Vector3 knockbackTarget = transform.position - (transform.forward * 0.6f);
            DOVirtual.Float(0, 1, .3f, (v) =>
            {
                if (characterController.enabled)
                {
                    Vector3 nextPos = Vector3.Lerp(transform.position, knockbackTarget, v);
                    characterController.Move(nextPos - transform.position);
                }
            });

            StopMoving();
        }

        IEnumerator HitCoroutine()
        {
            isStunned = true;
            yield return new WaitForSeconds(stunDuration);
            isStunned = false;
        }
    }

    void OnPlayerCounter(EnemyScript target)
    {
        if (target == this)
        {
            PrepareAttack(false);
        }
    }

    void OnPlayerTrajectory(EnemyScript target)
    {
        if (target == this)
        {
            StopEnemyCoroutines();
            isLockedTarget = true;
            PrepareAttack(false);
            StopMoving();
        }
    }

    void Death()
    {
        StopEnemyCoroutines();

        animator.SetTrigger("Death");

        DeathCoroutine = StartCoroutine(DeathDelay());

        IEnumerator DeathDelay()
        {
            yield return new WaitForSeconds(0.2f);
            this.enabled = false;
            characterController.enabled = false;
            animator.SetTrigger("DeathPose");
            enemyManager.SetEnemyAvailiability(this, false);
        }
    }

    public void SetRetreat()
    {
        StopEnemyCoroutines();

        RetreatCoroutine = StartCoroutine(PrepRetreat());

        IEnumerator PrepRetreat()
        {
            yield return new WaitForSeconds(1.4f);
            OnRetreat.Invoke(this);
            isRetreating = true;
            moveDirection = -Vector3.forward;
            isMoving = true;
            yield return new WaitUntil(() => Vector3.Distance(transform.position, playerCombat.transform.position) > 4);
            isRetreating = false;
            StopMoving();

            //Free 
            isWaiting = true;
            MovementCoroutine = StartCoroutine(EnemyMovement());
        }
    }

    public void SetAttack()
    {
        isWaiting = false;

        PrepareAttackCoroutine = StartCoroutine(PrepAttack());

        IEnumerator PrepAttack()
        {
            PrepareAttack(true);
            yield return new WaitForSeconds(.2f);
            moveDirection = Vector3.forward;
            isMoving = true;
        }
    }


    void PrepareAttack(bool active)
    {
        isPreparingAttack = active;

        if (active)
        {
            counterParticle.Play();
        }
        else
        {
            StopMoving();
            counterParticle.Clear();
            counterParticle.Stop();
        }
    }

    void MoveEnemy(Vector3 direction)
    {
        //Set movespeed based on direction
        moveSpeed = 2f;

        if (direction == Vector3.forward)
            moveSpeed = 8f;
        if (direction == -Vector3.forward)
            moveSpeed = 4f;

        //Set Animator values
        animator.SetFloat("InputMagnitude", (characterController.velocity.normalized.magnitude * direction.z) / (5 / moveSpeed), .2f, Time.deltaTime);
        animator.SetBool("Strafe", (direction == Vector3.right || direction == Vector3.left));
        animator.SetFloat("StrafeDirection", direction.normalized.x, .2f, Time.deltaTime);

        //Don't do anything if isMoving is false
        if (!isMoving)
            return;

        Vector3 dir = (playerCombat.transform.position - transform.position).normalized;
        Vector3 pDir = Quaternion.AngleAxis(90, Vector3.up) * dir; //Vector perpendicular to direction
        Vector3 movedir = Vector3.zero;

        Vector3 finalDirection = Vector3.zero;

        if (direction == Vector3.forward)
            finalDirection = dir;
        if (direction == Vector3.right || direction == Vector3.left)
            finalDirection = (pDir * direction.normalized.x);
        if (direction == -Vector3.forward)
            finalDirection = -transform.forward;

        // if (direction == Vector3.right || direction == Vector3.left)
        //     moveSpeed /= 1.5f;

        movedir += finalDirection * moveSpeed * Time.deltaTime;

        characterController.Move(movedir);

        if (!isPreparingAttack)
            return;

        if (Vector3.Distance(transform.position, playerCombat.transform.position) < 2)
        {
            StopMoving();
            if (!playerCombat.isCountering && !playerCombat.isAttackingEnemy)
                Attack();
            else
                PrepareAttack(false);
        }
    }

    private void Attack()
    {
        Vector3 attackDash = transform.position + (transform.forward / 1);
        DOVirtual.Float(0, 1, .5f, (v) =>
        {
            if (characterController.enabled)
            {
                Vector3 nextPos = Vector3.Lerp(transform.position, attackDash, v);
                characterController.Move(nextPos - transform.position);
            }
        });
        animator.SetTrigger("AirPunch");
    }

    public void HitEvent()
    {
        if (!playerCombat.isCountering && !playerCombat.isAttackingEnemy)
            playerCombat.DamageEvent();

        punchParticle.PlayParticleAtPosition(punchPosition.position);
        audioSource.clip = punchSound;
        audioSource.Play();

        PrepareAttack(false);
    }

    public void StopMoving()
    {
        isMoving = false;
        moveDirection = Vector3.zero;
        if (characterController.enabled)
            characterController.Move(moveDirection);
    }

    void StopEnemyCoroutines()
    {
        PrepareAttack(false);

        if (isRetreating)
        {
            if (RetreatCoroutine != null)
                StopCoroutine(RetreatCoroutine);
        }

        if (PrepareAttackCoroutine != null)
            StopCoroutine(PrepareAttackCoroutine);

        if (DamageCoroutine != null)
            StopCoroutine(DamageCoroutine);

        if (MovementCoroutine != null)
            StopCoroutine(MovementCoroutine);
    }

    #region Public Booleans

    public bool IsAttackable()
    {
        return health > 0;
    }

    public bool IsPreparingAttack()
    {
        return isPreparingAttack;
    }

    public bool IsRetreating()
    {
        return isRetreating;
    }

    public bool IsLockedTarget()
    {
        return isLockedTarget;
    }

    public bool IsStunned()
    {
        return isStunned;
    }

    #endregion
}
