using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo-video feedback for the moment of alignment:
/// - before alignment: shared-space content is hidden and a hint follows the user's view
/// - on calibration: shared-space content appears, the marker flashes and pulses,
///   and a large "ALIGNED" banner is shown, then a small label stays above the marker.
/// Colors are applied via MaterialPropertyBlock so shared materials are not modified.
/// UI uses world-space uGUI so it renders in both eyes with single-pass instanced rendering.
/// </summary>
public class AlignmentFeedback : MonoBehaviour
{
    [Header("References")]
    public QuestMarkerCalibrator calibrator;
    public AlignmentManager alignmentManager;
    public Transform head;
    public Transform sharedSpace;
    public Transform markerVisual;
    public Renderer[] highlightRenderers;

    [Header("Behaviour")]
    public bool hideSharedSpaceUntilAligned = true;
    public Color flashColor = new Color(0.3f, 1f, 0.4f);
    public Color alignedColor = new Color(0.1f, 0.8f, 1f);
    public float flashDuration = 1.0f;
    public float bannerDuration = 2.5f;
    public float pulseScale = 1.6f;
    public float bannerDistance = 1.2f;
    public string hintText = "Place the RIGHT controller on the marker\nand press A to align";

    [Header("Miniature view (optional)")]
    [Tooltip("Root of the miniature shared-space view (e.g. VRSharedSpace). Moved beside the marker on each alignment so it never overlaps the real-scale content.")]
    public Transform miniViewRoot;
    [Tooltip("Sideways distance from the marker, measured to the user's right (negative = left).")]
    public float miniViewSideOffset = -0.6f;
    public float miniViewHeightOffset = 0.1f;
    [Tooltip("Label shown above the miniature once aligned. Empty = no label.")]
    public string miniViewLabel = "Remote view";
    public float miniViewLabelHeight = 0.35f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    MaterialPropertyBlock propertyBlock;
    Vector3 markerBaseScale = Vector3.one;

    Transform bannerRoot;
    Text bannerText;
    Transform markerLabelRoot;
    Text markerLabelText;
    Transform miniLabelRoot;
    Text miniLabelText;

    float bannerHideTime = -1f;
    Coroutine flashRoutine;

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (markerVisual != null)
            markerBaseScale = markerVisual.localScale;

        bannerText = CreateLabel("AlignmentBanner", 72, out bannerRoot);
        markerLabelText = CreateLabel("MarkerLabel", 48, out markerLabelRoot);
        markerLabelRoot.gameObject.SetActive(false);
        miniLabelText = CreateLabel("MiniViewLabel", 40, out miniLabelRoot);
        miniLabelRoot.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (calibrator != null)
            calibrator.Calibrated += OnCalibrated;
    }

    void OnDisable()
    {
        if (calibrator != null)
            calibrator.Calibrated -= OnCalibrated;
    }

    void Start()
    {
        if (hideSharedSpaceUntilAligned && !IsAligned())
        {
            HideRenderersUnder(sharedSpace);
            HideRenderersUnder(miniViewRoot);
        }

        ShowBanner(hintText, Color.white, -1f);
    }

    void HideRenderersUnder(Transform root)
    {
        if (root == null)
            return;

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (r.enabled)
            {
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
        }
    }

    bool IsAligned()
    {
        return alignmentManager != null && alignmentManager.IsAligned;
    }

    void OnCalibrated(Pose pose)
    {
        foreach (Renderer r in hiddenRenderers)
        {
            if (r != null)
                r.enabled = true;
        }
        hiddenRenderers.Clear();

        PlaceMiniView(pose);

        int count = calibrator != null ? calibrator.CalibrationCount : 1;
        ShowBanner(count > 1 ? $"RE-ALIGNED  #{count}" : "ALIGNED", flashColor, bannerDuration);

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashAndPulse());
    }

    void PlaceMiniView(Pose markerPose)
    {
        if (miniViewRoot == null)
            return;

        Vector3 right = head != null
            ? Vector3.ProjectOnPlane(head.right, Vector3.up)
            : markerPose.rotation * Vector3.right;
        if (right.sqrMagnitude < 1e-4f)
            right = markerPose.rotation * Vector3.right;

        // Same orientation as the real marker frame, so the miniature mirrors the room 1:1 in direction.
        miniViewRoot.SetPositionAndRotation(
            markerPose.position + right.normalized * miniViewSideOffset + Vector3.up * miniViewHeightOffset,
            markerPose.rotation);
    }

    IEnumerator FlashAndPulse()
    {
        float t = 0f;
        while (t < flashDuration)
        {
            float k = t / flashDuration;
            // quick blink (3 pulses) fading into the steady aligned color
            float blink = 0.5f + 0.5f * Mathf.Cos(k * Mathf.PI * 6f);
            SetHighlightColor(Color.Lerp(alignedColor, flashColor, blink * (1f - k)));

            if (markerVisual != null)
            {
                float s = Mathf.Lerp(pulseScale, 1f, Mathf.SmoothStep(0f, 1f, k));
                markerVisual.localScale = markerBaseScale * s;
            }

            t += Time.deltaTime;
            yield return null;
        }

        SetHighlightColor(alignedColor);
        if (markerVisual != null)
            markerVisual.localScale = markerBaseScale;

        flashRoutine = null;
    }

    void SetHighlightColor(Color color)
    {
        if (highlightRenderers == null)
            return;

        foreach (Renderer r in highlightRenderers)
        {
            if (r == null)
                continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            r.SetPropertyBlock(propertyBlock);
        }
    }

    void ShowBanner(string text, Color color, float duration)
    {
        bannerText.text = text;
        bannerText.color = color;
        bannerRoot.gameObject.SetActive(true);
        bannerHideTime = duration > 0f ? Time.time + duration : -1f;
        PlaceBanner(true);
    }

    void LateUpdate()
    {
        if (bannerRoot.gameObject.activeSelf)
        {
            PlaceBanner(false);

            if (bannerHideTime > 0f && Time.time >= bannerHideTime)
                bannerRoot.gameObject.SetActive(false);
        }

        bool aligned = IsAligned();
        markerLabelRoot.gameObject.SetActive(aligned && sharedSpace != null);

        if (aligned && sharedSpace != null)
        {
            markerLabelRoot.position = sharedSpace.position + Vector3.up * 0.15f;
            FaceHead(markerLabelRoot);
            markerLabelText.text = "Shared Origin\n<size=36>Aligned</size>";
            markerLabelText.color = alignedColor;
        }

        bool showMiniLabel = aligned && miniViewRoot != null && !string.IsNullOrEmpty(miniViewLabel);
        miniLabelRoot.gameObject.SetActive(showMiniLabel);

        if (showMiniLabel)
        {
            miniLabelRoot.position = miniViewRoot.position + Vector3.up * miniViewLabelHeight;
            FaceHead(miniLabelRoot);
            miniLabelText.text = miniViewLabel;
            miniLabelText.color = Color.white;
        }
    }

    void PlaceBanner(bool snap)
    {
        if (head == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.forward;

        Vector3 target = head.position + forward.normalized * bannerDistance + Vector3.down * 0.1f;
        bannerRoot.position = snap
            ? target
            : Vector3.Lerp(bannerRoot.position, target, 1f - Mathf.Exp(-4f * Time.deltaTime));
        FaceHead(bannerRoot);
    }

    void FaceHead(Transform t)
    {
        if (head == null)
            return;

        Vector3 away = t.position - head.position;
        if (away.sqrMagnitude > 1e-6f)
            t.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    Text CreateLabel(string name, int fontSize, out Transform root)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        root = go.transform;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(1200f, 300f);
        rect.localScale = Vector3.one * 0.001f;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);

        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
        text.raycastTarget = false;

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        return text;
    }
}
