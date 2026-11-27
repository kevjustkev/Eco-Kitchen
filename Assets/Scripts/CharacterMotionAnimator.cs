using UnityEngine;

/// <summary>
/// Menggerakkan animasi karakter dari perpindahan objek (tidak perlu input).
/// Dipakai untuk PELANGGAN: pasang di objek induk prefab Customer (yang punya script Customer).
/// Mendukung controller ithappy (Vert / Hor / State) maupun controller dengan parameter "Speed".
/// </summary>
public class CharacterMotionAnimator : MonoBehaviour
{
    [Tooltip("Animator di model karakter (child). Kosong = dicari otomatis.")]
    public Animator animator;
    [Tooltip("Dipakai kalau Animator model belum punya Controller (isi Character_Movement dari ithappy)")]
    public RuntimeAnimatorController fallbackController;
    [Tooltip("Kecepatan yang dianggap jalan normal (samakan dengan Walk Speed di Customer)")]
    public float walkSpeed = 3f;
    [Tooltip("Di atas kecepatan ini dianggap berlari")]
    public float runSpeed = 5.5f;
    public float dampTime = 0.12f;

    private Vector3 lastPos;
    private float speed;
    private bool hasVert, hasState, hasHor, hasSpeed;

    private static readonly int VertHash = Animator.StringToHash("Vert");
    private static readonly int HorHash = Animator.StringToHash("Hor");
    private static readonly int StateHash = Animator.StringToHash("State");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Start()
    {
        animator = CharacterAnimUtil.FindAnimator(this, animator, fallbackController);
        if (animator == null)
        {
            Debug.LogWarning($"[CharacterMotionAnimator] {name}: tidak ada Animator di model. Model ithappy sudah dipasang sebagai child?");
            enabled = false;
            return;
        }
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == VertHash) hasVert = true;
            if (p.nameHash == StateHash) hasState = true;
            if (p.nameHash == HorHash) hasHor = true;
            if (p.nameHash == SpeedHash) hasSpeed = true;
        }
        lastPos = transform.position;
    }

    void Update()
    {
        if (animator == null) return;
        Vector3 d = transform.position - lastPos; d.y = 0f;
        lastPos = transform.position;
        float now = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
        speed = Mathf.Lerp(speed, now, 1f - Mathf.Exp(-12f * Time.deltaTime));
        if (speed < 0.05f) speed = 0f;

        bool running = speed > runSpeed;
        if (hasVert) animator.SetFloat(VertHash, running ? 1f : Mathf.Clamp01(speed / Mathf.Max(0.01f, walkSpeed)), dampTime, Time.deltaTime);
        if (hasHor) animator.SetFloat(HorHash, 0f);
        if (hasState) animator.SetFloat(StateHash, running ? 1f : 0f, dampTime * 2f, Time.deltaTime);
        if (hasSpeed) animator.SetFloat(SpeedHash, speed, dampTime, Time.deltaTime);
    }
}

public static class CharacterAnimUtil
{
    /// <summary>Mencari Animator di child; memberi controller cadangan bila kosong; mematikan root motion.</summary>
    public static Animator FindAnimator(Component owner, Animator current, RuntimeAnimatorController fallback)
    {
        Animator found = current;
        if (found == null || found.runtimeAnimatorController == null)
        {
            found = null;
            foreach (var a in owner.GetComponentsInChildren<Animator>(true))
                if (a.runtimeAnimatorController != null) { found = a; break; }
            if (found == null)
            {
                found = owner.GetComponentInChildren<Animator>(true);
                if (found != null && fallback != null) found.runtimeAnimatorController = fallback;
                if (found != null && found.runtimeAnimatorController == null) found = null;
            }
        }
        if (found != null) found.applyRootMotion = false;
        return found;
    }
}
