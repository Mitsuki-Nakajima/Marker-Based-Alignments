using UnityEngine;

public class SharedPoseDebug : MonoBehaviour
{
    public AlignmentManager alignmentManager;
    public Transform target;

    void Update()
    {
        if (!alignmentManager.IsAligned)
            return;

        Vector3 sharedPosition =
            alignmentManager.WorldToSharedPosition(
                target.position
            );

        Quaternion sharedRotation =
            alignmentManager.WorldToSharedRotation(
                target.rotation
            );

        Debug.Log(
            $"Shared Pose → Position: {sharedPosition}, " +
            $"Rotation: {sharedRotation.eulerAngles}"
        );
    }
}