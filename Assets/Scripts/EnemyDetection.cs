using System.Collections.Generic;
using UnityEngine;

public class EnemyDetection : MonoBehaviour
{
    [SerializeField] private EnemyManager enemyManager;
    private PlayerController movementInput;
    private CombatScript combatScript;

    public LayerMask layerMask;

    [SerializeField] Vector3 inputDirection;
    [SerializeField] private float detectionRadius = 3f;
    [SerializeField] private float detectionDistance = 6f;
    [SerializeField] private float detectionConeAngle = 90f;  // Cone angle in degrees (e.g., 90 = half-circle)
    [SerializeField] private EnemyScript currentTarget;

    public GameObject cam;

    private SphereCollider detectorCollider;

    private void Start()
    {
        movementInput = GetComponentInParent<PlayerController>();
        combatScript = GetComponentInParent<CombatScript>();

        // Cache own SphereCollider to exclude it from detection
        detectorCollider = GetComponent<SphereCollider>();

        // Sync SphereCollider radius with detectionRadius
        if (detectorCollider != null)
            detectorCollider.radius = detectionRadius;
    }

    private void Update()
    {
        var camera = Camera.main;
        var forward = camera.transform.forward;
        var right = camera.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        inputDirection = forward * movementInput.moveAxis.y + right * movementInput.moveAxis.x;
        inputDirection = inputDirection.normalized;

        // Use OverlapSphere to match SphereCollider trigger detection
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, layerMask);

        EnemyScript closest = null;
        float closestDist = float.MaxValue;

        foreach (Collider col in colliders)
        {
            // Skip self
            if (col == detectorCollider)
                continue;

            var enemy = col.GetComponent<EnemyScript>();
            if (enemy != null && enemy.IsAttackable())
            {
                Vector3 dirToEnemy = (col.transform.position - transform.position).normalized;

                // Check if enemy is within cone direction
                float angleToEnemy = Vector3.Angle(inputDirection, dirToEnemy);
                if (angleToEnemy <= detectionConeAngle * 0.5f)  // Half angle for full cone coverage
                {
                    float dist = Vector3.Distance(transform.position, col.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = enemy;
                    }
                }
            }
        }

        if (closest != null)
            currentTarget = closest;
        else
            currentTarget = null;  // Clear target if no enemy found
    }


    public EnemyScript CurrentTarget()
    {
        return currentTarget;
    }

    public void SetCurrentTarget(EnemyScript target)
    {
        currentTarget = target;
    }

    public float InputMagnitude()
    {
        return inputDirection.magnitude;
    }

    private void OnDrawGizmos()
    {
        // Draw detection cone
        var dir = inputDirection;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        dir.Normalize();

        // Yellow wire-sphere = detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw cone boundaries (thin lines showing the detection angle)
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);  // Semi-transparent yellow
        float halfAngle = detectionConeAngle * 0.5f * Mathf.Deg2Rad;

        // Get perpendicular vectors to create cone boundaries
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 up = Vector3.Cross(dir, right).normalized;

        // Draw left and right cone boundaries
        Vector3 leftBound = Quaternion.AngleAxis(detectionConeAngle * 0.5f, up) * dir * detectionRadius;
        Vector3 rightBound = Quaternion.AngleAxis(-detectionConeAngle * 0.5f, up) * dir * detectionRadius;

        Gizmos.DrawLine(transform.position, transform.position + leftBound);
        Gizmos.DrawLine(transform.position, transform.position + rightBound);
        Gizmos.DrawLine(transform.position, transform.position + dir * detectionRadius);

        // Draw any SphereCollider on this object (for comparison)
        var sc = GetComponent<SphereCollider>();
        if (sc != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
            var centerWorld = transform.TransformPoint(sc.center);
            var scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            var worldRadius = sc.radius * scale;
            Gizmos.DrawWireSphere(centerWorld, worldRadius);
        }

        // Highlight current target (GREEN)
        if (CurrentTarget() != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(CurrentTarget().transform.position, 0.5f);
        }
    }
}
