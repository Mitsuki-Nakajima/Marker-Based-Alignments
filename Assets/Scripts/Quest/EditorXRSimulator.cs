using UnityEngine;
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Editor-only stand-in for the Quest HMD and controllers, so Quest_Demo can be
/// driven and recorded from the Game View without a headset.
///
///   WASD          move head (Shift = faster)
///   R / F         head up / down
///   Right drag    look around
///   Q / E (hold)  reach left / right hand forward
///   Z / C (hold)  wave left / right hand
///   Space         align (handled by QuestMarkerCalibrator, unchanged)
///   H             toggle help overlay
///
/// Also swaps the transparent passthrough background for a grey background and a
/// floor grid while simulating. Does nothing in player builds or when a real XR
/// device is active (e.g. Quest Link on Windows).
/// </summary>
[DefaultExecutionOrder(-50)]
public class EditorXRSimulator : MonoBehaviour
{
    [Header("Rig")]
    public OVRCameraRig cameraRig;
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("Head")]
    public Vector3 startPosition = new Vector3(0f, 1.6f, 0f);
    public float startYaw;
    public float moveSpeed = 1.2f;
    public float fastMultiplier = 2.5f;
    public float lookSensitivity = 0.15f;

    [Header("Hands (head-yaw frame: x right, y up, z forward)")]
    public Vector3 leftRestOffset = new Vector3(-0.2f, -0.4f, 0.3f);
    public Vector3 rightRestOffset = new Vector3(0.2f, -0.4f, 0.3f);
    public Vector3 reachOffset = new Vector3(0f, 0.2f, 0.35f);
    public float waveAmplitude = 0.12f;
    public float waveFrequency = 9f;
    public float handSmoothing = 12f;

    [Header("Editor background")]
    public Color backgroundColor = new Color(0.32f, 0.33f, 0.35f, 1f);
    public bool showFloorGrid = true;
    public float gridSize = 10f;

    public bool showHelp = true;

#if UNITY_EDITOR
    float yaw;
    float pitch;
    Vector3 headPosition;
    Vector3 leftOffset;
    Vector3 rightOffset;
    float leftWave;
    float rightWave;
    bool simulating;

    void Awake()
    {
        if (UnityEngine.XR.XRSettings.isDeviceActive)
        {
            enabled = false;
            return;
        }

        simulating = true;
        headPosition = startPosition;
        yaw = startYaw;
        leftOffset = leftRestOffset;
        rightOffset = rightRestOffset;

        // The SDK's own editor emulator also listens to mouse/Ctrl; keep only one driver.
        if (cameraRig != null)
        {
            var emulator = cameraRig.GetComponent<OVRHeadsetEmulator>();
            if (emulator != null)
                emulator.enabled = false;
        }

        SetUpBackground();
    }

    void OnEnable()
    {
        if (cameraRig != null)
            cameraRig.UpdatedAnchors += OnRigUpdatedAnchors;
    }

    void OnDisable()
    {
        if (cameraRig != null)
            cameraRig.UpdatedAnchors -= OnRigUpdatedAnchors;
    }

    void Update()
    {
        if (!simulating)
            return;

        ReadInput(Time.deltaTime);
        ApplyPoses();
    }

    // OVRCameraRig rewrites its anchors every frame; re-apply ours afterwards.
    void OnRigUpdatedAnchors(OVRCameraRig rig)
    {
        if (simulating)
            ApplyPoses();
    }

    void ReadInput(float dt)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -80f, 80f);
        }

        if (kb == null)
            return;

        if (kb.hKey.wasPressedThisFrame)
            showHelp = !showHelp;

        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move += Vector3.forward;
        if (kb.sKey.isPressed) move += Vector3.back;
        if (kb.dKey.isPressed) move += Vector3.right;
        if (kb.aKey.isPressed) move += Vector3.left;
        if (kb.rKey.isPressed) move += Vector3.up;
        if (kb.fKey.isPressed) move += Vector3.down;

        float speed = moveSpeed * (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? fastMultiplier : 1f);
        if (move.sqrMagnitude > 0f)
            headPosition += yawRot * move.normalized * speed * dt;
        headPosition.y = Mathf.Max(0.2f, headPosition.y);

        UpdateHand(ref leftOffset, ref leftWave, leftRestOffset, kb.qKey.isPressed, kb.zKey.isPressed, -1f, dt);
        UpdateHand(ref rightOffset, ref rightWave, rightRestOffset, kb.eKey.isPressed, kb.cKey.isPressed, 1f, dt);
#endif
    }

    void UpdateHand(ref Vector3 offset, ref float wavePhase, Vector3 rest, bool reach, bool wave, float side, float dt)
    {
        Vector3 target = rest;
        if (reach)
            target += reachOffset;

        if (wave)
        {
            wavePhase += dt * waveFrequency;
            target += new Vector3(Mathf.Sin(wavePhase) * waveAmplitude * side, 0.3f, 0.1f);
        }
        else
        {
            wavePhase = 0f;
        }

        offset = Vector3.Lerp(offset, target, 1f - Mathf.Exp(-handSmoothing * dt));
    }

    void ApplyPoses()
    {
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Quaternion headRot = Quaternion.Euler(pitch, yaw, 0f);

        if (head != null)
            head.SetPositionAndRotation(headPosition, headRot);

        // Hands follow the head's position and heading, not its pitch, like a body would.
        Quaternion handRot = yawRot * Quaternion.Euler(-20f, 0f, 0f);
        if (leftHand != null)
            leftHand.SetPositionAndRotation(headPosition + yawRot * leftOffset,
                handRot * Quaternion.Euler(0f, 0f, Mathf.Sin(leftWave) * 25f));
        if (rightHand != null)
            rightHand.SetPositionAndRotation(headPosition + yawRot * rightOffset,
                handRot * Quaternion.Euler(0f, 0f, -Mathf.Sin(rightWave) * 25f));
    }

    void SetUpBackground()
    {
        if (head != null)
        {
            var cam = head.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = backgroundColor;
            }
        }

        if (showFloorGrid)
            CreateFloorGrid();
    }

    void CreateFloorGrid()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            return;

        const int px = 128;
        var tex = new Texture2D(px, px, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "EditorGridTex" };
        Color floor = new Color(0.24f, 0.25f, 0.27f, 1f);
        Color line = new Color(0.55f, 0.57f, 0.6f, 1f);
        var pixels = new Color[px * px];
        for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
                pixels[y * px + x] = (x < 2 || y < 2) ? line : floor;
        tex.SetPixels(pixels);
        tex.Apply(true);

        var mat = new Material(shader) { name = "EditorGridMat" };
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", new Vector2(gridSize, gridSize)); // 1 m cells

        var grid = GameObject.CreatePrimitive(PrimitiveType.Quad);
        grid.name = "EditorFloorGrid";
        Destroy(grid.GetComponent<Collider>());
        grid.transform.SetParent(transform, false);
        grid.transform.SetPositionAndRotation(new Vector3(0f, -0.001f, 0f), Quaternion.Euler(90f, 0f, 0f));
        grid.transform.localScale = new Vector3(gridSize, gridSize, 1f);
        grid.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void OnGUI()
    {
        if (!simulating || !showHelp)
            return;

        const string help =
            "<b>Editor XR Simulator</b>\n" +
            "WASD move   R/F up/down   Shift fast\n" +
            "Right-drag  look\n" +
            "Q / E  reach left / right hand\n" +
            "Z / C  wave left / right hand\n" +
            "Space  align (right hand = marker)\n" +
            "H  hide this help";

        var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = 13, padding = new RectOffset(10, 10, 8, 8) };
        GUI.Box(new Rect(10, 10, 300, 132), help, style);
    }
#else
    void Awake()
    {
        enabled = false;
    }
#endif
}
