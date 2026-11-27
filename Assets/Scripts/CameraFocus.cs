using UnityEngine;

/// <summary>
/// Memindahkan Main Camera dengan halus ke posisi close-up (untuk minigame 3D),
/// lalu mengembalikannya ke kamera biasa. CameraFollow dimatikan selama fokus.
/// Tidak perlu dipasang manual: ditambahkan otomatis ke Main Camera saat dipakai.
/// </summary>
public class CameraFocus : MonoBehaviour
{
    public float moveDuration = 0.45f;

    private CameraFollow follow;
    private Vector3 fromPos, toPos;
    private Quaternion fromRot, toRot;
    private float t = 1f;
    private bool focused, returning;

    public static CameraFocus Get()
    {
        Camera cam = Minigame3DUtil.GetCamera();
        if (cam == null) return null;
        var f = cam.GetComponent<CameraFocus>();
        if (f == null) f = cam.gameObject.AddComponent<CameraFocus>();
        return f;
    }

    /// <summary>Kamera pindah ke posisi dan arah tertentu.</summary>
    public void FocusOn(Vector3 position, Quaternion rotation)
    {
        if (follow == null) follow = GetComponent<CameraFollow>();
        if (follow != null) follow.enabled = false;

        fromPos = transform.position; fromRot = transform.rotation;
        toPos = position; toRot = rotation;
        t = 0f; focused = true; returning = false;
    }

    /// <summary>Kamera kembali mengikuti pemain.</summary>
    public void Release()
    {
        if (!focused) return;
        focused = false;
        returning = true;
        fromPos = transform.position; fromRot = transform.rotation;
        t = 0f;
    }

    void LateUpdate()
    {
        if (!focused && !returning) return;

        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, moveDuration));
        float k = Mathf.SmoothStep(0f, 1f, t);

        if (focused)
        {
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
        }
        else if (returning)
        {
            // kembali ke posisi yang dipakai CameraFollow
            if (follow != null)
            {
                follow.GetDesiredPose(out Vector3 goalPos, out Quaternion goalRot);
                transform.SetPositionAndRotation(Vector3.Lerp(fromPos, goalPos, k), Quaternion.Slerp(fromRot, goalRot, k));
            }
            if (t >= 1f)
            {
                returning = false;
                if (follow != null) { follow.SnapToTarget(); follow.enabled = true; }
            }
        }
    }
}
