using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Minigame MENGADUK KOMPOS 3D (kontrol layar 1:1, mulus di semua putaran).
/// Pasang di objek Compost area (bersama CompostBin).
/// </summary>
public class CompostStirMinigame3D : MonoBehaviour
{
    [Header("Referensi Objek")]
    [Tooltip("Masukkan objek hands-stirring-rough-unity dari Hierarchy")]
    public Transform stirringHands;
    [Tooltip("Masukkan objek StirrerPivot agar pengaduk di dalam bin ikut berputar")]
    public Transform stirrerPivot;
    [Tooltip("Opsional: titik tengah bukaan kompos (kosong = otomatis dari CompostContents / BinBody)")]
    public Transform workPoint;

    [Header("Penyesuaian Tangan 3D")]
    [Tooltip("Skala tangan saat minigame 3D aktif (mengimbangi scale Compost area 2,1,2)")]
    public Vector3 activeHandsScale = new Vector3(0.24f, 0.48f, 0.24f);
    [Tooltip("Geseran posisi tambahan tangan relatif terhadap titik kursor di lingkaran")]
    public Vector3 handPositionOffset = new Vector3(0f, 0.04f, 0f);
    [Tooltip("Putaran tambahan tangan agar lengan menghadap dari sisi pemain")]
    public Vector3 handRotationOffset = new Vector3(-65f, 180f, 0f);

    [Header("Kemudahan Kontrol")]
    [Tooltip("Sudut putaran mouse untuk 1x hitungan aduk (300 derajat = nyaman untuk mouse maupun touchpad)")]
    [Range(180f, 360f)] public float degreesPerLap = 300f;
    [Tooltip("Jarak minimum kursor dari pusat lingkaran layar dalam pixel")]
    [Range(8f, 50f)] public float deadzonePixels = 16f;

    [Header("Lingkaran Panduan 3D")]
    [Range(0.15f, 0.48f)] public float circleRadiusRatio = 0.34f;
    [Range(36, 96)] public int circleSegments = 64;

    [Header("Kamera Close-Up")]
    public bool autoCamera = true;
    public float cameraHeight = 1.45f;
    public float cameraBack = 0.95f;
    [Range(35f, 89f)] public float cameraPitch = 62f;
    [Range(0.6f, 2f)] public float cameraPadding = 1.05f;

    public static bool IsActive { get; private set; }

    private CompostBin compostBin;
    private Action onAllDone;
    private Action onCancel;
    private Camera cam;

    private GameObject root;
    private LineRenderer circleGuide;
    private LineRenderer progressArc;
    private GameObject startMarker;

    private Vector3 circleCenter;
    private float circleRadius;
    private float planeY;
    private Vector3 playerToward;
    private Vector3 screenRightOnPlane;
    private Vector3 screenUpOnPlane;

    // Status putaran kontinu (tidak pernah macet antar putaran)
    private bool dragging;
    private bool hasPrevScreenAngle;
    private float prevScreenAngle;
    private float lapStartAngle;
    private float currentVisualAngle;
    private float lapAccumulatedDegrees;
    private int lastSpinSign = 1;
    private float closeTimer = -1f;
    private float popupTimer;

    // Simpan transform awal tangan
    private Vector3 savedHandsLocalPos;
    private Quaternion savedHandsLocalRot;
    private Vector3 savedHandsLocalScale;
    private bool savedHandsActive;
    private Vector3 utensilLocalOffset;

    // UI
    private GameObject ui;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI progressText;
    private TextMeshProUGUI popupText;
    private TextMeshProUGUI hintText;

    public void Begin(CompostBin bin, Action done = null, Action cancel = null)
    {
        if (IsActive || bin == null) return;

        compostBin = bin;
        onAllDone = done;
        onCancel = cancel;
        cam = Minigame3DUtil.GetCamera();

        IsActive = true;
        UIState.SetUIOpen(true);

        root = new GameObject("CompostStirMinigame3D");

        Bounds binBounds = FindCompostOpeningBounds(out float surfaceY);
        Vector3 centerXZ = workPoint != null ? workPoint.position : binBounds.center;
        planeY = workPoint != null ? workPoint.position.y : surfaceY + 0.08f;
        circleCenter = new Vector3(centerXZ.x, planeY, centerXZ.z);

        float shortSide = Mathf.Max(0.25f, Mathf.Min(binBounds.size.x, binBounds.size.z));
        circleRadius = shortSide * circleRadiusRatio;

        playerToward = PlayerDirection(circleCenter);
        if (playerToward.sqrMagnitude < 0.001f)
            playerToward = -Flat(transform.forward).normalized;

        Vector3 cp;
        Quaternion cr;
        if (autoCamera)
        {
            float viewRadius = Mathf.Max(binBounds.extents.x, binBounds.extents.z, circleRadius * 1.4f);
            Minigame3DUtil.FramePose(cam, circleCenter, playerToward, viewRadius, cameraPitch, cameraPadding, out cp, out cr);
        }
        else
        {
            Minigame3DUtil.CloseUpPose(circleCenter, playerToward, cameraHeight, cameraBack, out cp, out cr);
        }

        var focus = CameraFocus.Get();
        if (focus != null) focus.FocusOn(cp, cr);

        // Sumbu mendatar kamera agar sudut mouse 2D di layar selaras 1:1 dengan lingkaran 3D
        screenUpOnPlane = -playerToward;
        screenRightOnPlane = Vector3.Cross(Vector3.up, screenUpOnPlane).normalized;

        BuildCircleGuide();
        progressArc = Minigame3DUtil.CreateLine("StirProgressArc", root.transform, new Color(0.30f, 0.88f, 0.42f), circleRadius * 0.14f);

        lapStartAngle = -90f; // bagian bawah lingkaran (terdekat dengan pemain)
        currentVisualAngle = lapStartAngle;
        lapAccumulatedDegrees = 0f;
        dragging = false;
        hasPrevScreenAngle = false;
        closeTimer = -1f;

        SetupHands();

        BuildUI();
        titleText.text = "Aduk Kompos";
        hintText.text = "Tahan klik kiri di mana saja dan putar mouse melingkar. Esc = batal";
        UpdateProgressLabel();
    }

    private void End()
    {
        IsActive = false;

        if (stirringHands != null)
        {
            stirringHands.localPosition = savedHandsLocalPos;
            stirringHands.localRotation = savedHandsLocalRot;
            stirringHands.localScale = savedHandsLocalScale;
            stirringHands.gameObject.SetActive(savedHandsActive);
        }

        if (root != null) Destroy(root);
        if (ui != null) Destroy(ui);

        Cursor.visible = true;
        var focus = CameraFocus.Get();
        if (focus != null) focus.Release();
        UIState.SetUIOpen(false);
    }

    private void Cancel()
    {
        End();
        onCancel?.Invoke();
    }

    private void Finish()
    {
        End();
        onAllDone?.Invoke();
    }

    private void Update()
    {
        if (!IsActive || root == null) return;

        if (popupTimer > 0f)
        {
            popupTimer -= Time.unscaledDeltaTime;
            if (popupTimer <= 0f && closeTimer < 0f && popupText != null)
                popupText.text = "";
        }

        if (closeTimer >= 0f)
        {
            closeTimer -= Time.unscaledDeltaTime;
            if (closeTimer <= 0f) Finish();
            return;
        }

        if (compostBin == null || !compostBin.NeedsStir)
        {
            Finish();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        if (cam == null) cam = Minigame3DUtil.GetCamera();
        if (cam == null) return;

        // Hitung posisi kursor relatif terhadap titik tengah lingkaran di layar (2D)
        Vector3 screenCenter3D = cam.WorldToScreenPoint(circleCenter);
        Vector2 screenCenter = new Vector2(screenCenter3D.x, screenCenter3D.y);
        Vector2 screenRadial = (Vector2)Input.mousePosition - screenCenter;
        float distPixels = screenRadial.magnitude;
        float screenAngle = Mathf.Atan2(screenRadial.y, screenRadial.x) * Mathf.Rad2Deg;

        if (distPixels >= deadzonePixels)
            currentVisualAngle = screenAngle;

        PlaceHandsAtTarget(PointOnScreenAlignedCircle(currentVisualAngle), false);

        if (!dragging && startMarker != null && startMarker.activeSelf)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.25f;
            startMarker.transform.localScale = Vector3.one * (circleRadius * 0.24f * pulse);
        }

        // Mulai mengaduk dengan menahan klik kiri di mana saja
        if (Input.GetMouseButtonDown(0))
        {
            dragging = true;
            hasPrevScreenAngle = distPixels >= deadzonePixels;
            prevScreenAngle = screenAngle;
            if (lapAccumulatedDegrees <= 1f)
                lapStartAngle = currentVisualAngle;
            hintText.text = "Putar terus melingkar sampai bar lingkaran penuh...";
        }

        if (Input.GetMouseButtonUp(0))
        {
            dragging = false;
            hasPrevScreenAngle = false;
            hintText.text = "Tahan klik kiri lagi untuk melanjutkan putaran";
            return;
        }

        if (!dragging) return;

        if (distPixels < deadzonePixels)
        {
            hasPrevScreenAngle = false;
            return;
        }

        if (!hasPrevScreenAngle)
        {
            prevScreenAngle = screenAngle;
            hasPrevScreenAngle = true;
            return;
        }

        float delta = Mathf.DeltaAngle(prevScreenAngle, screenAngle);
        prevScreenAngle = screenAngle;

        // Terima ayunan cepat hingga 165 derajat per frame agar putaran ke-2 & ke-3 tidak pernah macet
        if (Mathf.Abs(delta) < 0.05f || Mathf.Abs(delta) > 165f)
            return;

        lastSpinSign = delta >= 0f ? 1 : -1;
        lapAccumulatedDegrees += Mathf.Abs(delta);

        if (stirrerPivot != null)
            stirrerPivot.Rotate(Vector3.up, -delta, Space.Self);

        // Selesaikan putaran secara kontinu (sisa sudut berlebih langsung diteruskan ke putaran berikutnya)
        while (lapAccumulatedDegrees >= degreesPerLap && compostBin.NeedsStir)
        {
            lapAccumulatedDegrees -= degreesPerLap;
            lapStartAngle = currentVisualAngle;
            CompleteOneLap();
        }

        UpdateProgressArc();
    }

    private void CompleteOneLap()
    {
        ShowPopup("Bagus!", new Color(0.30f, 0.85f, 0.40f));
        compostBin.RegisterMouseStir();
        UpdateProgressLabel();

        if (!compostBin.NeedsStir)
        {
            dragging = false;
            hasPrevScreenAngle = false;
            lapAccumulatedDegrees = 0f;
            if (progressArc != null) progressArc.positionCount = 0;
            if (startMarker != null) startMarker.SetActive(false);
            if (circleGuide != null) circleGuide.gameObject.SetActive(false);

            ShowPopup("SELESAI DIADUK!", new Color(0.30f, 0.85f, 0.40f), 60f);
            hintText.text = "";
            progressText.text = "";
            closeTimer = 0.85f;
        }
    }

    // ------------------------------------------------------------------ visual lingkaran & tangan

    private void BuildCircleGuide()
    {
        circleGuide = Minigame3DUtil.CreateLine(
            "StirCircleGuide",
            root.transform,
            new Color(1f, 1f, 1f, 0.45f),
            circleRadius * 0.12f,
            true);

        circleGuide.loop = true;
        circleGuide.positionCount = circleSegments;
        for (int i = 0; i < circleSegments; i++)
        {
            float ang = (i / (float)circleSegments) * 360f;
            circleGuide.SetPosition(i, PointOnScreenAlignedCircle(ang));
        }

        float circumference = 2f * Mathf.PI * circleRadius;
        circleGuide.material.mainTextureScale = new Vector2(Mathf.Max(6f, circumference / (circleRadius * 0.25f)), 1f);

        Vector3 startPos = PointOnScreenAlignedCircle(-90f) + Vector3.up * 0.006f;
        startMarker = Minigame3DUtil.Marker("StartMarker", root.transform, startPos, circleRadius * 0.22f, new Color(0.3f, 0.85f, 0.4f));
    }

    private void UpdateProgressArc()
    {
        if (progressArc == null) return;

        float ratio = Mathf.Clamp01(lapAccumulatedDegrees / Mathf.Max(1f, degreesPerLap));
        if (ratio <= 0.01f)
        {
            progressArc.positionCount = 0;
            return;
        }

        int steps = Mathf.Max(2, Mathf.CeilToInt(circleSegments * ratio));
        progressArc.positionCount = steps + 1;
        float sweep = 360f * ratio * lastSpinSign;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float ang = lapStartAngle + sweep * t;
            progressArc.SetPosition(i, PointOnScreenAlignedCircle(ang) + Vector3.up * 0.005f);
        }
    }

    private Vector3 PointOnScreenAlignedCircle(float screenAngleDeg)
    {
        float rad = screenAngleDeg * Mathf.Deg2Rad;
        Vector3 dir = screenRightOnPlane * Mathf.Cos(rad) + screenUpOnPlane * Mathf.Sin(rad);
        return circleCenter + dir * circleRadius;
    }

    private void SetupHands()
    {
        if (stirringHands == null) return;

        savedHandsLocalPos = stirringHands.localPosition;
        savedHandsLocalRot = stirringHands.localRotation;
        savedHandsLocalScale = stirringHands.localScale;
        savedHandsActive = stirringHands.gameObject.activeSelf;

        stirringHands.gameObject.SetActive(true);
        stirringHands.localScale = activeHandsScale;

        Quaternion baseLook = Quaternion.LookRotation(-playerToward, Vector3.up) * Quaternion.Euler(handRotationOffset);
        stirringHands.rotation = baseLook;

        Transform utensil = stirringHands.Find("Utensil_Group");
        if (utensil != null)
        {
            Bounds ub = Minigame3DUtil.GetBounds(utensil.gameObject);
            Vector3 tipWorld = new Vector3(ub.center.x, ub.min.y, ub.center.z);
            utensilLocalOffset = stirringHands.InverseTransformPoint(tipWorld);
        }
        else
        {
            utensilLocalOffset = Vector3.zero;
        }

        PlaceHandsAtTarget(PointOnScreenAlignedCircle(currentVisualAngle), true);
    }

    private void PlaceHandsAtTarget(Vector3 targetPoint, bool instant)
    {
        if (stirringHands == null) return;

        float lift = dragging ? 0.0f : 0.04f;
        Vector3 rel = Flat(targetPoint - circleCenter);
        float sideAngle = Vector3.SignedAngle(-playerToward, (rel - playerToward * circleRadius * 1.8f).normalized, Vector3.up);
        Quaternion targetRot = Quaternion.LookRotation(-playerToward, Vector3.up)
                             * Quaternion.Euler(0f, Mathf.Clamp(sideAngle * 0.45f, -22f, 22f), 0f)
                             * Quaternion.Euler(handRotationOffset);

        if (instant)
        {
            stirringHands.rotation = targetRot;
            Vector3 tipWorld = stirringHands.TransformPoint(utensilLocalOffset);
            stirringHands.position += (targetPoint - tipWorld) + handPositionOffset + Vector3.up * lift;
        }
        else
        {
            float t = 1f - Mathf.Exp(-28f * Time.unscaledDeltaTime);
            stirringHands.rotation = Quaternion.Slerp(stirringHands.rotation, targetRot, t);
            Vector3 tipWorld = stirringHands.TransformPoint(utensilLocalOffset);
            Vector3 desiredPos = stirringHands.position + (targetPoint - tipWorld) + handPositionOffset + Vector3.up * lift;
            stirringHands.position = Vector3.Lerp(stirringHands.position, desiredPos, t);
        }
    }

    private Bounds FindCompostOpeningBounds(out float surfaceY)
    {
        Transform contents = transform.Find("Visual_Compostbin/compost-bin-rough-unity/Carcass_Group/CompostContents");
        if (contents != null)
        {
            Bounds cb = Minigame3DUtil.GetBounds(contents.gameObject);
            surfaceY = cb.max.y;
            return cb;
        }

        Transform body = transform.Find("Visual_Compostbin/compost-bin-rough-unity/Carcass_Group/BinBody");
        if (body != null)
        {
            Bounds bb = Minigame3DUtil.GetBounds(body.gameObject);
            surfaceY = bb.min.y + bb.size.y * 0.78f;
            return bb;
        }

        Bounds fallback = Minigame3DUtil.GetBounds(gameObject);
        surfaceY = fallback.max.y;
        return fallback;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private Vector3 PlayerDirection(Vector3 work)
    {
        var player = FindFirstObjectByType<PlayerMovement>();
        Vector3 d = player != null ? player.transform.position - work : -transform.forward;
        return Flat(d).normalized;
    }

    // ------------------------------------------------------------------ UI

    private void UpdateProgressLabel()
    {
        if (progressText == null || compostBin == null) return;
        int currentLap = Mathf.Min(compostBin.StirPresses + 1, compostBin.StirPressesRequired);
        progressText.text = $"Putaran {currentLap} / {compostBin.StirPressesRequired}";
    }

    private void ShowPopup(string text, Color c, float sizePx = 48f)
    {
        if (popupText == null) return;
        popupText.text = text;
        popupText.color = c;
        popupText.fontSize = sizePx;
        popupTimer = 0.75f;
    }

    private void BuildUI()
    {
        ui = UIBuilder.CreateOverlayCanvas("CompostStirUI3D", null, 120);
        titleText = Text("Title", "", 44, new Vector2(0f, 470f), FontStyles.Bold);
        progressText = Text("Progress", "", 28, new Vector2(0f, 420f), FontStyles.Normal);
        popupText = Text("Popup", "", 48, new Vector2(0f, 340f), FontStyles.Bold);
        popupText.outlineWidth = 0.2f;
        popupText.outlineColor = new Color32(0, 0, 0, 255);
        hintText = Text("Hint", "", 26, new Vector2(0f, -440f), FontStyles.Italic);
        hintText.color = new Color(1f, 0.92f, 0.6f);

        var cancel = UIBuilder.CreateButton("Cancel", ui.transform, "Batal (Esc)", new Color(0.35f, 0.35f, 0.35f), Cancel, 24);
        UIBuilder.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(240f, 60f));

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private TextMeshProUGUI Text(string name, string text, float size, Vector2 pos, FontStyles style)
    {
        var t = UIBuilder.CreateText(name, ui.transform, text, size, TextAlignmentOptions.Center, style);
        t.outlineWidth = 0.15f;
        t.outlineColor = new Color32(0, 0, 0, 200);
        UIBuilder.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(1400f, 60f));
        return t;
    }
}