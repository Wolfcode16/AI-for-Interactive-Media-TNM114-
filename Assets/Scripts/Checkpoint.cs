using UnityEngine;
public class Checkpoint : MonoBehaviour
{
    // ===== CHECKPOINT IDENTITY =====
    public int checkpointIndex;                 // Index of this checkpoint (0 = start/goal line)

    // ===== TRACK REFERENCE =====
    private TrackCheckpoints trackCheckpoints;  // Reference to the track manager

    public void SetTrackCheckpoints(TrackCheckpoints trackCheckpoints)
    {
        this.trackCheckpoints = trackCheckpoints;
    }
}
