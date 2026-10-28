using UnityEngine;

public class SharedCoordinateDebug : MonoBehaviour
{
    public Transform markerOrigin;
    public Transform arPlayer;
    public Transform vrPlayer;

    void Update()
    {
        Vector3 arLocalPosition =
            markerOrigin.InverseTransformPoint(arPlayer.position);

        Quaternion arLocalRotation =
            Quaternion.Inverse(markerOrigin.rotation) * arPlayer.rotation;

        Vector3 vrLocalPosition =
            markerOrigin.InverseTransformPoint(vrPlayer.position);

        Quaternion vrLocalRotation =
            Quaternion.Inverse(markerOrigin.rotation) * vrPlayer.rotation;

        Debug.Log(
            $"AR Pos: {arLocalPosition} Rot: {arLocalRotation.eulerAngles} | " +
            $"VR Pos: {vrLocalPosition} Rot: {vrLocalRotation.eulerAngles}"
        );
    }
}