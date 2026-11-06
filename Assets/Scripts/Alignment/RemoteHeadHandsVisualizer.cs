
using UnityEngine;

public class RemoteHeadHandsVisualizer : MonoBehaviour
{
    public SharedPlayerPoseProvider poseProvider;
    public Transform vrSharedSpace;

    public Transform remoteHead;
    public Transform remoteLeftHand;
    public Transform remoteRightHand;

    void LateUpdate()
    {
        if (poseProvider == null || vrSharedSpace == null)
            return;

        if (!poseProvider.TryGetSharedPlayerPose(
                out SharedPlayerPose pose))
            return;

        // Shared poses -> VR world poses
        ApplyPose(remoteHead, pose.head);
        ApplyPose(remoteLeftHand, pose.leftHand);
        ApplyPose(remoteRightHand, pose.rightHand);
    }

    void ApplyPose(Transform target, Pose sharedPose)
    {
        if (target == null)
            return;

        target.SetPositionAndRotation(
            vrSharedSpace.TransformPoint(sharedPose.position),
            vrSharedSpace.rotation * sharedPose.rotation
        );
    }
}
