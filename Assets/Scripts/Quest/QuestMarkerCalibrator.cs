using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Quest stand-in for real marker detection: pressing the A button snaps the
/// FakeMarkerProvider's marker transform to the right controller's pose, which
/// AlignmentManager then picks up as the "detected" marker.
/// Until the first press the provider has no marker, so AlignmentManager reports
/// IsAligned == false. In the Editor, Space triggers the same calibration.
/// </summary>
public class QuestMarkerCalibrator : MonoBehaviour
{
    public FakeMarkerProvider markerProvider;

    [Tooltip("Transform that FakeMarkerProvider reads as the detected marker.")]
    public Transform markerTarget;

    [Tooltip("Controller anchor whose pose becomes the marker pose (e.g. RightControllerAnchor).")]
    public Transform poseSource;

    public OVRInput.Button calibrateButton = OVRInput.Button.One;
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Tooltip("Keep the marker frame level (yaw only) so the shared space is not tilted by how the controller is held.")]
    public bool keepUpright = true;

    [Tooltip("Start unaligned and wait for the first button press.")]
    public bool requireCalibration = true;

    public float hapticDuration = 0.15f;

    public event Action<Pose> Calibrated;

    public bool IsCalibrated { get; private set; }
    public int CalibrationCount { get; private set; }

    void Awake()
    {
        if (requireCalibration && markerProvider != null)
            markerProvider.fakeMarker = null;
    }

    void Update()
    {
        bool pressed = OVRInput.GetDown(calibrateButton, controller);

#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            pressed = true;
#endif

        if (pressed)
            Calibrate();
    }

    public void Calibrate()
    {
        if (markerTarget == null || poseSource == null)
            return;

        Vector3 position = poseSource.position;
        Quaternion rotation = poseSource.rotation;

        if (keepUpright)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);

            // Controller pointing straight down: use its up axis for heading instead.
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);

            rotation = forward.sqrMagnitude < 1e-4f
                ? Quaternion.identity
                : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        markerTarget.SetPositionAndRotation(position, rotation);

        if (markerProvider != null)
            markerProvider.fakeMarker = markerTarget;

        IsCalibrated = true;
        CalibrationCount++;

        if (hapticDuration > 0f)
        {
            StopAllCoroutines();
            StartCoroutine(HapticPulse());
        }

        Calibrated?.Invoke(new Pose(position, rotation));
    }

    IEnumerator HapticPulse()
    {
        OVRInput.SetControllerVibration(1f, 0.6f, controller);
        yield return new WaitForSeconds(hapticDuration);
        OVRInput.SetControllerVibration(0f, 0f, controller);
    }
}
