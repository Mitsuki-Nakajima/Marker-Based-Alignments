using UnityEngine;

public class FakeMarkerProvider : MonoBehaviour
{
    public Transform fakeMarker;

    public bool TryGetMarkerPose(out Vector3 position, out Quaternion rotation)
    {
        if (fakeMarker == null)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        position = fakeMarker.position;
        rotation = fakeMarker.rotation;

        Debug.Log(
            $"Detected Marker → Position: {position}, Rotation: {rotation.eulerAngles}"
        );

        return true;
    }

    void Update()
    {
        TryGetMarkerPose(out _, out _);
    }
}