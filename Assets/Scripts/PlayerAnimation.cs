using UnityEngine;

/// <summary>
/// Menghubungkan gerak pemain dengan Animator model karakter. Pasang di objek Player (yang sama dengan PlayerMovement).
/// Otomatis mendukung dua jenis controller:
///   - ithappy (Character_Movement): parameter "Vert", "Hor", "State" (0 = jalan, 1 = lari)
///   - controller lama: "Speed" (float), "IsRunning" (bool)
/// Parameter trigger "Chop" (opsional) diputar saat memotong.
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [Tooltip("Animator di model karakter (child). Kosongkan untuk mencari otomatis.")]
    public Animator animator;
    [Tooltip("Dipakai kalau Animator model belum punya Controller (isi Character_Movement dari ithappy)")]
    public RuntimeAnimatorController fallbackController;

    [Tooltip("Kehalusan perubahan nilai (semakin kecil semakin cepat berubah)")]
    public float speedDampTime = 0.1f;

    [Header("Info (hanya untuk dicek saat Play)")]
    [SerializeField] private float speedDikirim;
    [SerializeField] private string status = "-";

    private PlayerMovement movement;
    private bool hasSpeed, hasRunning, hasChop, hasVert, hasHor, hasState;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int ChopHash = Animator.StringToHash("Chop");
    private static readonly int VertHash = Animator.StringToHash("Vert");
    private static readonly int HorHash = Animator.StringToHash("Hor");
    private static readonly int StateHash = Animator.StringToHash("State");

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Fail("PlayerMovement tidak ada di objek ini. Pasang Player Animation di objek Player.");
            return;
        }

        DisablePackMovement();

        animator = CharacterAnimUtil.FindAnimator(this, animator, fallbackController);
        if (animator == null)
        {
            Fail("Tidak ada Animator dengan Controller di model karakter. Isi Fallback Controller = Character_Movement (ithappy).");
            return;
        }

        foreach (var p in animator.parameters)
        {
            if (p.nameHash == SpeedHash && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
            if (p.nameHash == IsRunningHash && p.type == AnimatorControllerParameterType.Bool) hasRunning = true;
            if (p.nameHash == ChopHash && p.type == AnimatorControllerParameterType.Trigger) hasChop = true;
            if (p.nameHash == VertHash) hasVert = true;
            if (p.nameHash == HorHash) hasHor = true;
            if (p.nameHash == StateHash) hasState = true;
        }

        if (!hasSpeed && !hasVert)
        {
            Fail($"Controller '{animator.runtimeAnimatorController.name}' tidak punya parameter \"Vert\" (ithappy) atau \"Speed\".");
            return;
        }

        status = $"OK: Animator di '{animator.gameObject.name}', mode {(hasVert ? "ithappy" : "Speed")}";
        Debug.Log($"PlayerAnimation {status}");
    }

    void Update()
    {
        if (animator == null || movement == null || (!hasSpeed && !hasVert)) return;

        speedDikirim = movement.CurrentSpeed;
        bool running = movement.IsRunning;

        if (hasVert)
        {
            float vert = running ? 1f : Mathf.Clamp01(speedDikirim / Mathf.Max(0.01f, movement.walkSpeed));
            if (speedDikirim < 0.05f) vert = 0f;
            animator.SetFloat(VertHash, vert, speedDampTime, Time.deltaTime);
            if (hasHor) animator.SetFloat(HorHash, 0f);
            if (hasState) animator.SetFloat(StateHash, running ? 1f : 0f, speedDampTime * 2f, Time.deltaTime);
        }
        if (hasSpeed) animator.SetFloat(SpeedHash, speedDikirim, speedDampTime, Time.deltaTime);
        if (hasRunning) animator.SetBool(IsRunningHash, running);
    }

    /// <summary>Memutar animasi memotong (kalau controller punya trigger "Chop").</summary>
    public void PlayChop()
    {
        if (animator != null && hasChop) animator.SetTrigger(ChopHash);
    }

    /// <summary>
    /// Karakter hasil simpan Customization Tool ithappy membawa script geraknya sendiri
    /// (CharacterMover, MovePlayerInput, CharacterController). Dimatikan supaya tidak bentrok dengan PlayerMovement.
    /// </summary>
    private void DisablePackMovement()
    {
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb.gameObject == gameObject) continue;
            string t = mb.GetType().Name;
            if (t == "CharacterMover" || t == "MovePlayerInput" || t == "PlayerCamera" || t == "ThirdPersonCamera")
            {
                mb.enabled = false;
                Debug.Log($"PlayerAnimation: mematikan {t} bawaan ithappy di '{mb.gameObject.name}'");
            }
        }
        foreach (var cc in GetComponentsInChildren<CharacterController>(true))
            if (cc.gameObject != gameObject) cc.enabled = false;
    }

    private void Fail(string message)
    {
        status = "MASALAH: " + message;
        Debug.LogWarning("PlayerAnimation " + status);
    }
}
