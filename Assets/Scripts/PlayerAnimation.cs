using UnityEngine;

/// <summary>
/// Menghubungkan gerak pemain dengan Animator model karakter.
/// Mengirim parameter:
///   "Speed"     (float) = kecepatan jalan saat ini (0 = diam, ~4 = jalan, ~7 = lari)
///   "IsRunning" (bool)  = sedang menahan Shift sambil bergerak
///   "Chop"      (trigger) = animasi memotong (dipakai nanti oleh Meja Potong)
/// Pasang di objek Player (yang sama dengan PlayerMovement).
/// Animator dicari otomatis di model anak (child) Player.
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [Tooltip("Kosongkan untuk mencari otomatis di child")]
    public Animator animator;

    [Tooltip("Kehalusan perubahan nilai Speed (semakin kecil semakin halus)")]
    public float speedDampTime = 0.1f;

    private PlayerMovement movement;
    private bool hasSpeed, hasRunning, hasChop;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int ChopHash = Animator.StringToHash("Chop");

    void Start()
    {
        movement = GetComponent<PlayerMovement>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning("PlayerAnimation: Animator tidak ditemukan di model karakter.");
            return;
        }
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PlayerAnimation: Animator belum punya Controller.");
            return;
        }

        animator.applyRootMotion = false; // gerak diatur PlayerMovement, bukan animasi

        foreach (var p in animator.parameters)
        {
            if (p.nameHash == SpeedHash && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
            if (p.nameHash == IsRunningHash && p.type == AnimatorControllerParameterType.Bool) hasRunning = true;
            if (p.nameHash == ChopHash && p.type == AnimatorControllerParameterType.Trigger) hasChop = true;
        }
        if (!hasSpeed) Debug.LogWarning("PlayerAnimation: parameter Float \"Speed\" belum dibuat di Animator Controller.");
    }

    void Update()
    {
        if (animator == null || movement == null) return;

        if (hasSpeed) animator.SetFloat(SpeedHash, movement.CurrentSpeed, speedDampTime, Time.deltaTime);
        if (hasRunning) animator.SetBool(IsRunningHash, movement.IsRunning);
    }

    /// <summary>Memutar animasi memotong (dipanggil nanti dari Meja Potong).</summary>
    public void PlayChop()
    {
        if (animator != null && hasChop) animator.SetTrigger(ChopHash);
    }
}
