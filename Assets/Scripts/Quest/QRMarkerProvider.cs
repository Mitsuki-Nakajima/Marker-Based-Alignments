using System;
using Meta.XR.MRUtilityKit;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Prototype 7A: drives the existing alignment pipeline from MRUK QR code tracking.
///
/// Same flow as QuestMarkerCalibrator: the detected pose is written to the marker
/// transform that FakeMarkerProvider reads, so AlignmentManager aligns SharedSpace to it.
/// The A-button calibrator stays as a manual fallback.
///
/// QR coordinate frame (MRUKTrackable transform, MRUK v209):
///   - origin = centre of the QR code
///   - local X / Y span the code's surface; +Y is the code's "up" (top edge direction)
///   - local +Z is the surface NORMAL, pointing OUT of the printed face toward the viewer.
///     (MRUK treats the code as a plane anchor; MRUKAnchor.Raycast only accepts rays
///     travelling toward local -Z, i.e. the visible face is the +Z side.)
///
/// SharedSpace content assumes Y-up, so with <see cref="levelToGravity"/> the marker frame
/// is converted to: up = world up, forward = the code's heading projected onto the floor.
///   - QR lying flat on a table (+Z ≈ up): forward = the code's +Y (its top edge).
///   - QR on a wall (+Z horizontal):       forward = the code's +Z (out of the wall).
/// </summary>
public class QRMarkerProvider : MonoBehaviour
{
    public enum DetectionState
    {
        WaitingForPermission,
        Searching,
        Tracking,
        Lost,
        ManualOverride
    }

    [Header("Alignment pipeline")]
    public FakeMarkerProvider markerProvider;
    [Tooltip("Transform FakeMarkerProvider reads as the detected marker (FakeDetectedMarker).")]
    public Transform markerTarget;
    [Tooltip("Optional A-button fallback. A manual press pauses QR following until a QR is (re)acquired.")]
    public QuestMarkerCalibrator manualCalibrator;

    [Header("QR")]
    [Tooltip("Only accept codes whose text payload equals this. Empty = accept any QR code.")]
    public string payloadFilter = "";
    [Tooltip("Convert the QR frame to a gravity-aligned (yaw-only) frame. See class comment.")]
    public bool levelToGravity = true;
    [Tooltip("Keep following the code while it is tracked (otherwise align once per acquisition).")]
    public bool followWhileTracked = true;

    [Tooltip("Request com.oculus.permission.USE_SCENE at startup if not yet granted. MRUK will not start trackers without it.")]
    public bool requestScenePermission = true;

    /// <summary>Raised when a QR is acquired or re-acquired and alignment snaps to it. Args: marker pose, payload.</summary>
    public event Action<Pose, string> QRAligned;

    public DetectionState State { get; private set; } = DetectionState.Searching;
    public string LastPayload { get; private set; }
    public int AcquisitionCount { get; private set; }

    MRUKTrackable current;
    bool wasTracked;
    bool subscribed;

    void Start()
    {
        if (manualCalibrator != null)
            manualCalibrator.Calibrated += OnManualCalibrated;

        RequestPermissionIfNeeded();
        TrySubscribe();
    }

    void OnDestroy()
    {
        if (manualCalibrator != null)
            manualCalibrator.Calibrated -= OnManualCalibrated;

        if (subscribed && MRUK.Instance != null)
        {
            MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
            MRUK.Instance.SceneSettings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
        }
    }

    void RequestPermissionIfNeeded()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
        {
            State = DetectionState.WaitingForPermission;
            if (requestScenePermission)
                Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission);
        }
#endif
    }

    void TrySubscribe()
    {
        if (subscribed || MRUK.Instance == null)
            return;

        var settings = MRUK.Instance.SceneSettings;

        // Make sure QR tracking is requested even if the scene asset was not configured.
        var config = settings.TrackerConfiguration;
        if (!config.QRCodeTrackingEnabled)
        {
            config.QRCodeTrackingEnabled = true;
            settings.TrackerConfiguration = config;
        }

        settings.TrackableAdded.AddListener(OnTrackableAdded);
        settings.TrackableRemoved.AddListener(OnTrackableRemoved);
        subscribed = true;
    }

    bool Accepts(MRUKTrackable trackable)
    {
        if (trackable == null || trackable.TrackableType != OVRAnchor.TrackableType.QRCode)
            return false;

        return string.IsNullOrEmpty(payloadFilter) || trackable.MarkerPayloadString == payloadFilter;
    }

    void OnTrackableAdded(MRUKTrackable trackable)
    {
        if (!Accepts(trackable))
            return;

        current = trackable;
        wasTracked = false; // treat as a fresh acquisition in Update
    }

    void OnTrackableRemoved(MRUKTrackable trackable)
    {
        if (trackable != current)
            return;

        current = null;
        wasTracked = false;
        if (State != DetectionState.ManualOverride)
            State = DetectionState.Lost;
    }

    void OnManualCalibrated(Pose pose)
    {
        State = DetectionState.ManualOverride;
    }

    void Update()
    {
        if (!subscribed)
            TrySubscribe();

#if UNITY_ANDROID && !UNITY_EDITOR
        if (State == DetectionState.WaitingForPermission)
        {
            if (!Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
                return;
            State = DetectionState.Searching;
        }
#endif

        if (current == null)
            return;

        bool tracked = current.IsTracked;
        bool acquired = tracked && !wasTracked;
        wasTracked = tracked;

        if (!tracked)
        {
            if (State == DetectionState.Tracking)
                State = DetectionState.Lost;
            return;
        }

        // Manual A-press wins until the code is re-acquired.
        if (State == DetectionState.ManualOverride && !acquired)
            return;

        if (!acquired && !followWhileTracked)
            return;

        Pose pose = ToMarkerPose(current.transform);
        markerTarget.SetPositionAndRotation(pose.position, pose.rotation);
        if (markerProvider != null)
            markerProvider.fakeMarker = markerTarget;

        State = DetectionState.Tracking;
        LastPayload = current.MarkerPayloadString;

        if (acquired)
        {
            AcquisitionCount++;
            QRAligned?.Invoke(pose, LastPayload);
        }
    }

    Pose ToMarkerPose(Transform qr)
    {
        if (!levelToGravity)
            return new Pose(qr.position, qr.rotation);

        Vector3 normal = qr.forward; // +Z: out of the printed face
        Vector3 heading = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.7f
            ? qr.up       // lying flat: use the code's top edge
            : normal;     // on a wall: face out of the wall

        heading = Vector3.ProjectOnPlane(heading, Vector3.up);
        Quaternion rotation = heading.sqrMagnitude > 1e-4f
            ? Quaternion.LookRotation(heading.normalized, Vector3.up)
            : Quaternion.identity;

        return new Pose(qr.position, rotation);
    }

    public string StatusText()
    {
        switch (State)
        {
            case DetectionState.WaitingForPermission: return "QR: waiting for Spatial Data permission";
            case DetectionState.Searching: return "QR: searching...";
            case DetectionState.Tracking: return $"QR: tracking  \"{LastPayload}\"";
            case DetectionState.Lost: return $"QR: lost  \"{LastPayload}\"";
            case DetectionState.ManualOverride: return "Manual (A button)";
            default: return "";
        }
    }
}
