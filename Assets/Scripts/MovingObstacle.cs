using UnityEngine;

// =================================================================================
// OBSTACLE - Static obstacle with randomized offset positioning
// =================================================================================
// Creates unpredictable hazards that the AI must learn to avoid
// Randomly spawns at original position or offset position each episode
// =================================================================================
public class MovingObstacle : MonoBehaviour
{
    // ==========================================================
    // SETTINGS
    // ==========================================================
    [SerializeField] private float penalty = 10f;            // Penalty to give car on collision

    // ==========================================================
    // OFFSET SETTINGS
    // ==========================================================
    [SerializeField] private bool randomize = true;         // True = enable randomization/movement, False = stay static
    [SerializeField] private bool offsetOnXAxis = true;     // True = offset on X axis, False = offset on Z axis
    [SerializeField] private float offsetDistance = 2f;     // Offset distance from original position
    [SerializeField] private bool inverseOffset = false;    // True = apply offset in negative direction
    [SerializeField] private bool useLerp = false;          // True = lerp back and forth, False = static spawn
    [SerializeField] private float lerpSpeed = 1f;          // Speed of lerp movement (higher = faster)

    // Private variables
    private Vector3 originalPosition;   // The obstacle's initial position in the scene
    private Vector3 targetPosition;     // Target position for lerping
    private float lerpProgress = 0f;    // Current lerp progress (0 to 1)
    private bool lerpForward = true;    // True = lerping toward offset, False = lerping toward original

    // ==========================================================
    // INITIALIZATION
    // ==========================================================
    void Start()
    {
        // Remember the original position from the scene
        originalPosition = transform.position;

        // If randomize is disabled, stay at original position
        if (!randomize)
        {
            return;
        }

        // Calculate target position
        float offsetValue;
        if (inverseOffset)
        {
            offsetValue = -offsetDistance;
        }
        else
        {
            offsetValue = offsetDistance;
        }

        Vector3 offset;
        if (offsetOnXAxis)
        {
            offset = new Vector3(offsetValue, 0, 0);
        }
        else
        {
            offset = new Vector3(0, 0, offsetValue);
        }
        targetPosition = originalPosition + offset;

        // Initialize with random position if not lerping
        if (!useLerp)
        {
            RandomizeObstacle();
        }
        else
        {
            // Start at random position along the path
            lerpProgress = Random.value;

            if (Random.value > 0.5f)
            {
                lerpForward = true;
            }
            else
            {
                lerpForward = false;
            }

            transform.position = Vector3.Lerp(originalPosition, targetPosition, lerpProgress);
        }
    }

    // ==========================================================
    // UPDATE - Handle lerp movement
    // ==========================================================
    void Update()
    {
        if (!randomize || !useLerp) return;

        // Update lerp progress
        if (lerpForward)
        {
            lerpProgress += lerpSpeed * Time.deltaTime;
            if (lerpProgress >= 1f)
            {
                lerpProgress = 1f;
                lerpForward = false;
            }
        }
        else
        {
            lerpProgress -= lerpSpeed * Time.deltaTime;
            if (lerpProgress <= 0f)
            {
                lerpProgress = 0f;
                lerpForward = true;
            }
        }

        // Apply lerp position
        transform.position = Vector3.Lerp(originalPosition, targetPosition, lerpProgress);
    }

    // ==========================================================
    // RANDOMIZATION - Called at episode start to randomly set obstacle offset
    // ==========================================================
    public void RandomizeObstacle()
    {
        // If randomize is disabled, stay at current position
        if (!randomize) return;

        // If lerping is enabled, don't reset position - let it continue moving
        if (useLerp) return;

        // Randomly decide whether to use offset (50/50 chance)
        bool useOffset = Random.value > 0.5f;

        if (useOffset)
        {
            // Apply offset on selected axis
            float offsetValue;
            if (inverseOffset)
            {
                offsetValue = -offsetDistance;
            }
            else
            {
                offsetValue = offsetDistance;
            }

            Vector3 offset;
            if (offsetOnXAxis)
            {
                offset = new Vector3(offsetValue, 0, 0);
            }
            else
            {
                offset = new Vector3(0, 0, offsetValue);
            }

            transform.position = originalPosition + offset;
        }
        else
        {
            // Use original position
            transform.position = originalPosition;
        }
    }

    // ==========================================================
    // COLLISION - End episode when car hits obstacle
    // ==========================================================
    private void OnCollisionEnter(Collision collision)
    {
        // Check if car hit the obstacle
        CarAgent agent = collision.gameObject.GetComponent<CarAgent>();
        if (agent != null)
        {
            // Give penalty and end episode
            agent.AddReward(-penalty);
            agent.EndEpisode();
        }
    }
}
