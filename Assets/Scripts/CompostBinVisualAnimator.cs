using UnityEngine;

/// <summary>
/// Animasi visual Compost Bin.
/// Pasang pada GameObject yang sama dengan CompostBin.
/// Mengatur buka-tutup LidPivot, putaran otomatis StirrerPivot, getaran bin, dan partikel.
/// Jika CompostStirMinigame3D terpasang, kontrol tangan & mouse melingkar diserahkan ke minigame 3D.
/// </summary>
[DisallowMultipleComponent]
public class CompostBinVisualAnimator : MonoBehaviour
{
    [Header("Referensi")]
    [SerializeField] private CompostBin compostBin;
    [SerializeField] private Transform lidPivot;
    [SerializeField] private Transform stirrerPivot;
    [SerializeField] private Transform stirringHands;
    [SerializeField] private Transform bodyVisual;
    [SerializeField] private ParticleSystem processParticles;
    [SerializeField] private PlayerInteractionDetector interactionDetector;

    [Header("Posisi tutup")]
    [Tooltip("Gunakan identitas/0,0,0 sebagai pose tertutup setelah pivot dirapikan di Scene.")]
    [SerializeField] private bool resetLidToClosedPoseOnAwake = true;
    [SerializeField] private Vector3 lidClosedEuler = Vector3.zero;
    [Tooltip("Sudut tambahan relatif dari posisi tertutup saat bin sedang memproses.")]
    [SerializeField] private Vector3 lidOpenEuler = new Vector3(-75f, 0f, 0f);
    [SerializeField, Min(0.1f)] private float lidOpenSpeed = 4f;

    [Header("Pengaduk otomatis")]
    [SerializeField, Min(0f)] private float automaticStirSpeed = 24f;
    [SerializeField] private Vector3 stirAxisLocal = Vector3.up;

    [Header("Gerakan tangan pengaduk (mode non-3D)")]
    [SerializeField, Min(0f)] private float handsStirRadius = 0.06f;
    [SerializeField, Min(0f)] private float handsStirBobAmount = 0.015f;
    [SerializeField, Min(0f)] private float handsStirTiltAngle = 8f;

    [Header("Kontrol mouse melingkar (mode non-3D)")]
    [SerializeField] private bool enableMouseStir = true;
    [SerializeField, Min(90f)] private float degreesPerMouseStir = 360f;
    [SerializeField, Min(10f)] private float minimumCircleRadiusPixels = 35f;
    [SerializeField] private float mouseCircleCenterHeight = 0.75f;
    [SerializeField] private bool requirePlayerTargetingBin = true;

    [Header("Kontrol E alternatif")]
    [SerializeField, Min(0.05f)] private float keyStirDuration = 0.55f;
    [SerializeField, Min(0f)] private float keyStirSpeed = 420f;

    [Header("Gerakan badan bin (opsional)")]
    [SerializeField, Min(0f)] private float bodyShakeAmount = 0.008f;
    [SerializeField, Min(0f)] private float bodyShakeFrequency = 7f;

    private Quaternion lidClosedRotation;
    private Vector3 bodyInitialLocalPosition;
    private Vector3 handsInitialLocalPosition;
    private Quaternion handsInitialLocalRotation;
    private float handsStirAngle;
    private float keyStirTimer;

    private bool mouseDragging;
    private bool hasPreviousMouseAngle;
    private float previousMouseAngle;
    private float accumulatedMouseDegrees;
    private Camera mainCamera;
    private CompostStirMinigame3D stirMinigame3D;

    private void Awake()
    {
        if (compostBin == null)
            compostBin = GetComponent<CompostBin>();

        stirMinigame3D = GetComponent<CompostStirMinigame3D>();

        if (interactionDetector == null)
            interactionDetector = FindFirstObjectByType<PlayerInteractionDetector>();

        mainCamera = Camera.main;

        if (lidPivot != null)
        {
            lidClosedRotation = Quaternion.Euler(lidClosedEuler);
            if (resetLidToClosedPoseOnAwake)
                lidPivot.localRotation = lidClosedRotation;
        }

        if (bodyVisual != null)
            bodyInitialLocalPosition = bodyVisual.localPosition;

        if (stirringHands != null)
        {
            handsInitialLocalPosition = stirringHands.localPosition;
            handsInitialLocalRotation = stirringHands.localRotation;
            stirringHands.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (compostBin == null)
            return;

        if (stirMinigame3D == null)
            stirMinigame3D = GetComponent<CompostStirMinigame3D>();

        bool processing = compostBin.IsProcessing;
        bool waitingForStir = compostBin.NeedsStir;

        // Tutup terbuka saat bin sedang memproses / sedang diaduk.
        if (lidPivot != null)
        {
            Quaternion target = processing
                ? lidClosedRotation * Quaternion.Euler(lidOpenEuler)
                : lidClosedRotation;

            lidPivot.localRotation = Quaternion.Slerp(
                lidPivot.localRotation, target, Time.deltaTime * lidOpenSpeed);
        }

        // Pengaduk berputar perlahan selama proses aktif, lalu berhenti ketika diminta diaduk.
        if (stirrerPivot != null)
        {
            Vector3 axis = stirAxisLocal.sqrMagnitude > 0.001f ? stirAxisLocal.normalized : Vector3.up;
            if (processing && !waitingForStir)
                stirrerPivot.Rotate(axis, automaticStirSpeed * Time.deltaTime, Space.Self);

            if (keyStirTimer > 0f)
            {
                stirrerPivot.Rotate(axis, keyStirSpeed * Time.deltaTime, Space.Self);
                keyStirTimer -= Time.deltaTime;
            }
        }

        HandleMouseStir();
        UpdateStirringHands(waitingForStir);

        if (bodyVisual != null)
        {
            if (processing && !waitingForStir)
            {
                float wave = Mathf.Sin(Time.time * bodyShakeFrequency) * bodyShakeAmount;
                bodyVisual.localPosition = bodyInitialLocalPosition + new Vector3(wave, 0f, 0f);
            }
            else
            {
                bodyVisual.localPosition = Vector3.Lerp(
                    bodyVisual.localPosition, bodyInitialLocalPosition, Time.deltaTime * 8f);
            }
        }

        if (processParticles != null)
        {
            if (processing && !processParticles.isPlaying)
                processParticles.Play();
            else if (!processing && processParticles.isPlaying)
                processParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateStirringHands(bool waitingForStir)
    {
        if (stirringHands == null)
            return;

        // Jika minigame 3D sedang berjalan, biarkan CompostStirMinigame3D yang mengatur posisi tangan.
        if (CompostStirMinigame3D.IsActive)
            return;

        // Jika komponen minigame 3D terpasang, sembunyikan tangan selama belum masuk minigame (sebelum tekan E).
        if (stirMinigame3D != null)
        {
            if (stirringHands.gameObject.activeSelf)
                stirringHands.gameObject.SetActive(false);
            return;
        }

        bool showHands = waitingForStir || keyStirTimer > 0f;
        if (stirringHands.gameObject.activeSelf != showHands)
        {
            stirringHands.gameObject.SetActive(showHands);
            if (showHands)
            {
                stirringHands.localPosition = handsInitialLocalPosition;
                stirringHands.localRotation = handsInitialLocalRotation;
            }
        }

        if (!showHands)
            return;

        bool isActivelyStirring = (mouseDragging && Input.GetMouseButton(0)) || keyStirTimer > 0f;
        if (keyStirTimer > 0f)
            handsStirAngle += keyStirSpeed * Mathf.Deg2Rad * Time.deltaTime;

        Vector3 targetLocalPos = handsInitialLocalPosition;
        Quaternion targetLocalRot = handsInitialLocalRotation;

        if (isActivelyStirring)
        {
            Vector3 circleOffset = new Vector3(
                Mathf.Cos(handsStirAngle) * handsStirRadius,
                Mathf.Sin(handsStirAngle * 2f) * handsStirBobAmount,
                Mathf.Sin(handsStirAngle) * handsStirRadius);

            targetLocalPos = handsInitialLocalPosition + circleOffset;
            Quaternion tilt = Quaternion.Euler(
                Mathf.Sin(handsStirAngle) * handsStirTiltAngle,
                0f,
                Mathf.Cos(handsStirAngle) * handsStirTiltAngle);
            targetLocalRot = tilt * handsInitialLocalRotation;
        }

        stirringHands.localPosition = Vector3.Lerp(
            stirringHands.localPosition, targetLocalPos, Time.deltaTime * 14f);
        stirringHands.localRotation = Quaternion.Slerp(
            stirringHands.localRotation, targetLocalRot, Time.deltaTime * 14f);
    }

    private void HandleMouseStir()
    {
        // Serahkan input mouse ke CompostStirMinigame3D jika terpasang.
        if (CompostStirMinigame3D.IsActive || stirMinigame3D != null)
        {
            ResetMouseGesture();
            return;
        }

        bool canStir = enableMouseStir
            && compostBin != null
            && compostBin.NeedsStir
            && !UIState.IsUIOpen
            && IsPlayerTargetingThisBin();

        if (!canStir)
        {
            ResetMouseGesture();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            mouseDragging = true;
            hasPreviousMouseAngle = false;
            accumulatedMouseDegrees = 0f;
        }

        if (Input.GetMouseButtonUp(0))
        {
            ResetMouseGesture();
            return;
        }

        if (!mouseDragging || !Input.GetMouseButton(0))
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Vector3 worldCenter = compostBin.transform.position + Vector3.up * mouseCircleCenterHeight;
        Vector3 screenCenter3D = mainCamera.WorldToScreenPoint(worldCenter);
        if (screenCenter3D.z <= 0f)
            return;

        Vector2 screenCenter = new Vector2(screenCenter3D.x, screenCenter3D.y);
        Vector2 radial = (Vector2)Input.mousePosition - screenCenter;
        if (radial.magnitude < minimumCircleRadiusPixels)
        {
            hasPreviousMouseAngle = false;
            return;
        }

        float currentAngle = Mathf.Atan2(radial.y, radial.x) * Mathf.Rad2Deg;
        if (!hasPreviousMouseAngle)
        {
            previousMouseAngle = currentAngle;
            hasPreviousMouseAngle = true;
            return;
        }

        float deltaAngle = Mathf.DeltaAngle(previousMouseAngle, currentAngle);
        previousMouseAngle = currentAngle;

        if (Mathf.Abs(deltaAngle) > 120f || Mathf.Abs(deltaAngle) < 0.05f)
            return;

        if (stirrerPivot != null)
        {
            Vector3 axis = stirAxisLocal.sqrMagnitude > 0.001f ? stirAxisLocal.normalized : Vector3.up;
            stirrerPivot.Rotate(axis, deltaAngle, Space.Self);
        }

        handsStirAngle += deltaAngle * Mathf.Deg2Rad;

        accumulatedMouseDegrees += Mathf.Abs(deltaAngle);
        while (accumulatedMouseDegrees >= degreesPerMouseStir && compostBin.NeedsStir)
        {
            accumulatedMouseDegrees -= degreesPerMouseStir;
            compostBin.RegisterMouseStir();
        }

        if (!compostBin.NeedsStir)
            ResetMouseGesture();
    }

    private bool IsPlayerTargetingThisBin()
    {
        if (!requirePlayerTargetingBin)
            return true;

        if (interactionDetector == null)
            interactionDetector = FindFirstObjectByType<PlayerInteractionDetector>();

        return interactionDetector == null || interactionDetector.CurrentTarget == compostBin;
    }

    private void ResetMouseGesture()
    {
        mouseDragging = false;
        hasPreviousMouseAngle = false;
        accumulatedMouseDegrees = 0f;
    }

    public void PlayStir()
    {
        keyStirTimer = keyStirDuration;
    }
}