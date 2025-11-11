using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// AIMBOT-X: Reinforcement Learning–Based Shooting Agent
/// This script defines the RL agent's observations, actions, and reward logic.
/// Attach this script to the AI player (agent) in your Unity scene.
/// </summary>
public class AimbotAgent : Agent
{
    [Header("Agent Settings")]
    public Transform target;                // The target or enemy player
    public float moveSpeed = 3f;            // Movement speed
    public float rotateSpeed = 200f;        // Rotation speed
    public GameObject bulletPrefab;         // Bullet object for shooting
    public Transform shootPoint;            // Firing position
    public float shootingCooldown = 1.0f;   // Delay between shots
    public float health = 100f;             // Health of the agent
    public float maxHealth = 100f;

    private float timeSinceLastShot = 0f;
    private Rigidbody rb;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        ResetAgent();
    }

    public override void OnEpisodeBegin()
    {
        ResetAgent();
    }

    private void ResetAgent()
    {
        // Reset agent position and health
        transform.localPosition = new Vector3(Random.Range(-4f, 4f), 0.5f, Random.Range(-4f, 4f));
        transform.localRotation = Quaternion.identity;
        health = maxHealth;

        // Randomize target position
        if (target != null)
        {
            target.localPosition = new Vector3(Random.Range(-4f, 4f), 0.5f, Random.Range(-4f, 4f));
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (target == null)
        {
            sensor.AddObservation(Vector3.zero);
            return;
        }

        // Observe relative position and distance to the target
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        // Add observations
        sensor.AddObservation(directionToTarget.x);
        sensor.AddObservation(directionToTarget.z);
        sensor.AddObservation(distanceToTarget);

        // Add agent health
        sensor.AddObservation(health / maxHealth);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var continuousActions = actions.ContinuousActions;
        var discreteActions = actions.DiscreteActions;

        // Movement actions
        float moveZ = continuousActions.Length > 0 ? continuousActions[0] : 0;
        float moveX = continuousActions.Length > 1 ? continuousActions[1] : 0;

        Vector3 move = new Vector3(moveX, 0, moveZ) * moveSpeed * Time.deltaTime;
        rb.MovePosition(transform.position + move);

        // Rotate toward target
        if (target != null)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, rotateSpeed * Time.deltaTime);
        }

        // Shooting action (discrete)
        if (discreteActions.Length > 0 && discreteActions[0] == 1)
        {
            TryShoot();
        }

        // Reward: survival bonus
        AddReward(0.001f);

        // Negative reward for being idle or too far
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > 8f) AddReward(-0.001f);
    }

    private void TryShoot()
    {
        timeSinceLastShot += Time.deltaTime;
        if (timeSinceLastShot < shootingCooldown) return;

        timeSinceLastShot = 0f;
        if (bulletPrefab && shootPoint)
        {
            GameObject bullet = Instantiate(bulletPrefab, shootPoint.position, shootPoint.rotation);
            bullet.GetComponent<Rigidbody>().AddForce(shootPoint.forward * 10f, ForceMode.VelocityChange);
        }

        // Shooting has a small cost to avoid spamming
        AddReward(-0.005f);
    }

    public void RegisterHit(bool hitTarget)
    {
        if (hitTarget)
        {
            AddReward(+1.0f); // reward for hitting the target
        }
        else
        {
            AddReward(-0.2f); // penalty for missing
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        AddReward(-0.5f);
        if (health <= 0)
        {
            AddReward(-1.0f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;

        continuousActionsOut[0] = Input.GetAxis("Vertical");
        continuousActionsOut[1] = Input.GetAxis("Horizontal");
        discreteActionsOut[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void Update()
    {
        timeSinceLastShot += Time.deltaTime;
    }
}
