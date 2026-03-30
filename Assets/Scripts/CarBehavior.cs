// =================================================================================
// NOTES
// =================================================================================
// - Attempt 1: Car realised that crashing early led to less penalty accumulation so it just steered into walls quickly.
// - Attempt 2: Penalty still big, probably due to rays being to far detecting walls constantly
// - Attempt 3: Trying small reward for going forward instead of penalty for time to encourage survival. 
//              Also trying to increase reward so it dominates over the penalty.
// - Attempt 4: Before:
//              - Progress toward checkpoint: 0.1/frame × 100 frames = 10 points (dominated everything)
//              - Reaching checkpoint: 1 point (insignificant!)
//              - Near wall: -0.001/frame (ignored)
//              Now:
//              - Progress toward checkpoint: 0.01/frame × 100 frames = 1 point (gentle guidance)
//              - Reaching checkpoint: 5 points (clear goal!)
//              - Near wall: -0.01/frame (noticeable pain, teaches avoidance)
// - Attempt 5: Duplicated assets, now have 4 tracks and cars running parallel.


using Unity.MLAgents;              // Core ML-Agents library for AI training
using Unity.MLAgents.Actuators;   // Handles actions (steering, acceleration)
using Unity.MLAgents.Sensors;     // Handles observations (what the AI "sees")
using UnityEngine;                 // Unity game engine functionality

// =================================================================================
// CAR AGENT
// =================================================================================
public class CarAgent : Agent
{
    // ==========================================================
    // REFERENCES & BASIC SETTINGS
    // ========================================================== 
    public Rigidbody carRb;              // Reference to the car's Rigidbody for physics
    public float speed = 10f;
    public float turnSpeed = 300f;
    public Transform startTransform;

    // ==========================================================
    // CHECKPOINT TRACKING
    // ==========================================================
    private int nextCheckpointIndex = 0;
    private int totalCheckpoints = -1;
    private bool finalCpReached = false;
    private Transform nextCheckpointTransform;
    private float previousCheckpointDistance;

    // ==========================================================
    // MOVEMENT & PHYSICS
    // ==========================================================
    private float currentVelocity = 0f;

    // ==========================================================
    // REWARD SYSTEM PARAMETERS
    // ==========================================================

    [SerializeField] private float dragDeceleration = 8f;

    // REWARDS (positive reinforcement - encourages behavior):
    [SerializeField] private float checkpointReward = 5f;
    [SerializeField] private float goalReward = 50f;
    [SerializeField] private float progressRewardMultiplier = 0.01f;
    [SerializeField] private float stepReward = 0.001f;

    // PENALTIES (negative reinforcement - discourages behavior):
    [SerializeField] private float wallPenalty = 1f;
    [SerializeField] private float obstacleDistancePenalty = 0.02f;
    [SerializeField] private float safetyDistanceReward = 0.02f;

    // ==========================================================
    // RAY SENSORS - Separate sensors for walls and obstacles
    // ==========================================================
    [SerializeField] private RayPerceptionSensorComponent3D wallSensor;
    [SerializeField] private RayPerceptionSensorComponent3D obstacleSensor;
    [SerializeField] private Transform obstacleParent;
    [SerializeField] private float obstacleProximityDistance = 4f;

    // =================================================================================
    // EPISODE BEGIN 
    // =================================================================================
    public override void OnEpisodeBegin()
    {
        // Reset checkpoint tracking to start of lap
        nextCheckpointIndex = 0;
        finalCpReached = false;
        nextCheckpointTransform = null;
        previousCheckpointDistance = float.MaxValue;

        // Reset car's physics (stop all movement)
        currentVelocity = 0f;
        carRb.linearVelocity = Vector3.zero;
        carRb.angularVelocity = Vector3.zero;

        // Move car back to starting position and rotation
        if (startTransform != null)
        {
            transform.position = startTransform.position;
            transform.rotation = startTransform.rotation;
        }

        // Randomize only the obstacles on THIS car's track (not all tracks)
        if (obstacleParent != null)
        {
            // Get all MovingObstacle components from children of the parent
            MovingObstacle[] trackObstacles = obstacleParent.GetComponentsInChildren<MovingObstacle>();

            foreach (var obstacle in trackObstacles)
            {
                if (obstacle != null)
                {
                    obstacle.RandomizeObstacle();
                }
            }
        }

        // Clear console
        Debug.ClearDeveloperConsole();
    }

    // =================================================================================
    // COLLECT OBSERVATIONS
    // =================================================================================
    public override void CollectObservations(VectorSensor sensor)
    {
        // OBSERVATION 1: Current speed of the car
        sensor.AddObservation(carRb.linearVelocity.magnitude);

        // OBSERVATION 2: How far through the lap we are (0% to 100%)
        if (totalCheckpoints > 0)
        {
            sensor.AddObservation((float)nextCheckpointIndex / totalCheckpoints);
        }
        else
        {
            sensor.AddObservation(0f);  // Haven't started yet
        }
    }

    // =================================================================================
    // ON ACTION RECEIVED
    // =================================================================================
    public override void OnActionReceived(ActionBuffers actions)
    {
        // ========== GET AI ACTIONS ==========
        float steer = actions.ContinuousActions[0];  // Steering: -1 = left, 0 = straight, +1 = right
        float accel = actions.ContinuousActions[1];  // Acceleration: -1 = brake, 0 = neutral, +1 = full throttle

        // ========== MOVEMENT LOGIC ==========
        // Only allow forward movement (no reverse)
        accel = Mathf.Max(0f, accel);  // Clamp to 0 or positive

        // Update the car's velocity based on throttle input
        if (accel > 0f)
        {
            // Accelerating: velocity increases based on throttle
            currentVelocity = accel * speed;
        }
        else
        {
            // Not accelerating: car slows down naturally (drag/friction)
            currentVelocity = Mathf.Max(0f, currentVelocity - dragDeceleration * Time.fixedDeltaTime);
        }

        // Move the car forward in the direction it's facing
        Vector3 move = transform.forward * currentVelocity * Time.fixedDeltaTime;
        carRb.MovePosition(carRb.position + move);

        // ========== STEERING LOGIC ==========
        // Only allow turning when the car is moving
        if (currentVelocity > 0.1f)
        {
            float turn = steer * turnSpeed * Time.fixedDeltaTime;
            transform.Rotate(0f, turn, 0f);  // Rotate around Y-axis (up)
        }

        // ========== PENALTIES & REWARDS ==========

        // SURVIVAL REWARD: Small reward each frame for continuing to drive
        AddReward(stepReward);

        // PROGRESS REWARD: Reward for moving closer to the next checkpoint
        ApplyProgressReward();

        // OBSTACLE AVOIDANCE REWARD: Reward for maintaining distance from obstacles
        ApplyObstacleAvoidanceReward();
    }

    // =================================================================================
    // CHECKPOINT PROGRESS REWARD - Encourage moving toward next checkpoint
    // =================================================================================
    // This rewards the agent for getting closer to the next checkpoint each frame
    // Helps guide the agent in the right direction between checkpoints
    // =================================================================================
    private void ApplyProgressReward()
    {
        // Make sure we have a checkpoint to target
        if (nextCheckpointTransform == null)
        {
            // Try to find the next checkpoint if we don't have it yet
            UpdateNextCheckpointTransform();
            if (nextCheckpointTransform == null) return;
        }

        // Calculate current distance to the next checkpoint
        float currentDistance = Vector3.Distance(transform.position, nextCheckpointTransform.position);

        // Check if we're getting closer (distance decreased since last frame)
        if (previousCheckpointDistance != float.MaxValue)
        {
            float distanceChange = previousCheckpointDistance - currentDistance;

            // If distance decreased (positive change), reward the agent
            if (distanceChange > 0)
            {
                // Moving closer = reward proportional to how much closer we got
                AddReward(distanceChange * progressRewardMultiplier);
            }
        }

        // Store current distance for next frame's comparison
        previousCheckpointDistance = currentDistance;
    }

    // Helper method to find and cache the next checkpoint's Transform
    private void UpdateNextCheckpointTransform()
    {
        // Find the track manager
        var track = FindFirstObjectByType<TrackCheckpoints>();
        if (track != null)
        {
            // Get the next checkpoint Transform from the track
            nextCheckpointTransform = track.GetCheckpointTransform(nextCheckpointIndex);
        }
    }


    // =================================================================================
    // OBSTACLE AVOIDANCE REWARD - Encourage maintaining safe distance from obstacles
    // =================================================================================
    private void ApplyObstacleAvoidanceReward()
    {
        // Make sure we have the obstacle sensor to work with
        if (obstacleSensor == null) return;

        // Get the output from all rays
        var rayOutputs = obstacleSensor.RaySensor.RayPerceptionOutput?.RayOutputs;
        if (rayOutputs == null) return;

        // Get the maximum length each ray can reach
        float maxRayLength = obstacleSensor.RayLength;

        // Find the closest obstacle detected by ANY ray
        float closestObstacleDistance = float.MaxValue;
        bool obstacleDetected = false;

        // Check each ray to find the closest obstacle
        foreach (var rayOutput in rayOutputs)
        {
            if (rayOutput.HasHit)
            {
                // Calculate actual distance to the obstacle
                float hitDistance = rayOutput.HitFraction * maxRayLength;

                // Track the closest obstacle
                if (hitDistance < closestObstacleDistance)
                {
                    closestObstacleDistance = hitDistance;
                    obstacleDetected = true;
                }
            }
        }

        // // Debugging: Log the closest obstacle distance
        // if (obstacleDetected)
        // {
        //     Debug.Log($"[{gameObject.name}] Obstacle detected at distance: {closestObstacleDistance:F2} units");
        // }

        // Apply small reward if maintaining safe distance from obstacles
        if (obstacleDetected && closestObstacleDistance > obstacleProximityDistance)
        {
            float safetyReward = safetyDistanceReward * (closestObstacleDistance / maxRayLength);
            AddReward(safetyReward);
        }
        else if (obstacleDetected && closestObstacleDistance <= obstacleProximityDistance)
        {
            // Small penalty for getting too close to obstacles (but not as harsh as walls)
            float dangerPenalty = obstacleDistancePenalty * (1f - closestObstacleDistance / obstacleProximityDistance);
            AddReward(-dangerPenalty);
        }
    }

    // =================================================================================
    // HEURISTIC - Allows manual control for testing (keyboard input)
    // =================================================================================
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;

        float horizontal = 0f;  // Steering value
        float vertical = 0f;    // Acceleration value

        // Check keyboard input
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.leftArrowKey.isPressed) horizontal = -1f;   // Turn left
            if (UnityEngine.InputSystem.Keyboard.current.rightArrowKey.isPressed) horizontal = 1f;   // Turn right
            if (UnityEngine.InputSystem.Keyboard.current.upArrowKey.isPressed) vertical = 1f;        // Accelerate
            if (UnityEngine.InputSystem.Keyboard.current.downArrowKey.isPressed) vertical = 0f;      // Brake/coast
        }

        // Set the action outputs
        cont[0] = horizontal;  // Steering action
        cont[1] = vertical;    // Acceleration action
    }

    // Helper function to get the next checkpoint index (used for debugging/logging)
    // public int GetNextCheckpointIndex()
    // {
    //     return nextCheckpointIndex;
    // }

    // =================================================================================
    // COLLISION HANDLING - Detect when car touches checkpoints or walls
    // =================================================================================
    private void OnTriggerEnter(Collider other)
    {
        // ========== CHECKPOINT COLLISION ==========
        // Did we drive through a checkpoint?
        if (other.CompareTag("Checkpoint"))
        {
            var cp = other.GetComponent<Checkpoint>();

            if (cp != null)
            {
                // First time hitting a checkpoint? Count how many total checkpoints exist
                var track = FindFirstObjectByType<TrackCheckpoints>();
                if (totalCheckpoints < 0 && track != null)
                {
                    totalCheckpoints = track.GetCheckpointCount();
                }

                // Check if this is the CORRECT checkpoint (in the right order)
                bool correct = cp.checkpointIndex == nextCheckpointIndex;

                if (correct)
                {
                    AddReward(checkpointReward);

                    // Is it the final checkpoint?
                    if (cp.checkpointIndex == totalCheckpoints - 1)
                    {
                        finalCpReached = true;
                    }

                    // Move to next checkpoint (wraps back to 0 after the last one)
                    nextCheckpointIndex = (nextCheckpointIndex + 1) % totalCheckpoints;

                    // Update the checkpoint Transform reference for progress tracking
                    UpdateNextCheckpointTransform();
                    previousCheckpointDistance = float.MaxValue;  // Reset distance for new checkpoint

                    // Lap is complete when we reach checkpoint 0 AFTER passing the final checkpoint
                    if (cp.checkpointIndex == 0 && finalCpReached)
                    {
                        AddReward(goalReward);
                        EndEpisode();
                    }
                }
            }
        }


        // ========== WALL COLLISION ==========
        // Did we crash
        if (other.gameObject.CompareTag("Wall"))
        {
            // Give penalty and end this episode (restart)
            AddReward(-wallPenalty);
            EndEpisode();  // Episode over - car will reset and try again
        }
    }
}