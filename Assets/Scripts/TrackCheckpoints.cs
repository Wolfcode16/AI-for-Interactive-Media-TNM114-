using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TrackCheckpoints : MonoBehaviour
{
    // ===== EVENTS =====
    public event EventHandler onPlayerCorrectCheckpoint;    // Fired when correct checkpoint is hit
    public event EventHandler onPlayerWrongCheckpoint;      // Fired when wrong checkpoint is hit

    // ===== CHECKPOINT MANAGEMENT =====
    private List<Checkpoint> checkpointList;    // List of all checkpoints in order

    private void Awake()
    {
        // Find the "Checkpoints" parent object
        Transform checkpointsTransform = transform.Find("Checkpoints");

        // Initialize checkpoint list
        checkpointList = new List<Checkpoint>();

        // Register each checkpoint
        foreach (Transform checkpointTransform in checkpointsTransform)
        {
            Checkpoint checkpoint = checkpointTransform.GetComponent<Checkpoint>();
            checkpoint.SetTrackCheckpoints(this);
            checkpointList.Add(checkpoint);
        }
    }

    public int GetCheckpointCount()
    {
        return checkpointList.Count;
    }

    public Transform GetCheckpointTransform(int index)
    {
        if (index >= 0 && index < checkpointList.Count)
            return checkpointList[index].transform;
        return null;
    }
}