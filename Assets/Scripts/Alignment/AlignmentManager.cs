using UnityEngine;

public class AlignmentManager : MonoBehaviour
{
    public Transform sharedSpace;
    public FakeMarkerProvider markerProvider;

    public bool IsAligned { get; private set; }

    void Update()
    {
        UpdateAlignment();
    }

    void UpdateAlignment()
    {
        if (!markerProvider.TryGetMarkerPose(
                out Vector3 markerPosition,
                out Quaternion markerRotation))
        {
            IsAligned = false;
            return;
        }

        sharedSpace.SetPositionAndRotation(
            markerPosition,
            markerRotation
        );

        IsAligned = true;
    }

    // World / Tracking → Shared
    public Vector3 WorldToSharedPosition(Vector3 worldPosition)
    {
        return sharedSpace.InverseTransformPoint(worldPosition);
    }

    public Quaternion WorldToSharedRotation(Quaternion worldRotation)
    {
        return Quaternion.Inverse(sharedSpace.rotation)
               * worldRotation;
    }

    // Shared → World / Tracking
    public Vector3 SharedToWorldPosition(Vector3 sharedPosition)
    {
        return sharedSpace.TransformPoint(sharedPosition);
    }

    public Quaternion SharedToWorldRotation(Quaternion sharedRotation)
    {
        return sharedSpace.rotation
               * sharedRotation;
    }
}