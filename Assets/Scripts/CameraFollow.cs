using UnityEngine;

/// <summary>
/// Kamera atas-miring yang mengikuti pemain (gaya Overcooked), bisa diputar dan di-zoom:
/// - Tahan KLIK KANAN + geser mouse kiri/kanan = memutar kamera mengelilingi pemain
/// - Tahan KLIK KANAN + scroll = zoom
/// - (opsional) geser mouse atas/bawah = mengubah kemiringan, jika Allow Pitch dicentang
/// Pasang di Main Camera.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    [Tooltip("Titik yang dilihat kamera, relatif terhadap posisi pemain")]
    public Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Posisi Awal")]
    [Tooltip("Kemiringan kamera (derajat). 90 = tegak lurus dari atas")]
    [Range(10f, 89f)] public float pitch = 50f;
    [Tooltip("Arah kamera (derajat). 0 = melihat ke arah +Z")]
    public float yaw = 0f;
    public float distance = 12f;

    [Header("Rotasi Mouse")]
    public bool enableRotation = true;
    [Tooltip("0 = klik kiri, 1 = klik kanan, 2 = klik tengah")]
    public int rotateMouseButton = 1;
    public float rotateSpeed = 4f;
    [Tooltip("Izinkan mengubah kemiringan dengan geser mouse atas/bawah")]
    public bool allowPitch = false;
    public float minPitch = 30f;
    public float maxPitch = 75f;

    [Header("Zoom")]
    public bool enableZoom = true;
    public float minDistance = 7f;
    public float maxDistance = 18f;
    public float zoomSpeed = 1.5f;

    [Header("Kehalusan")]
    [Tooltip("Semakin besar, kamera semakin cepat menyusul pemain")]
    public float followSmoothness = 10f;

    private Vector3 currentPivot;

    void Start()
    {
        if (target == null)
        {
            var player = FindFirstObjectByType<PlayerMovement>();
            if (player != null) target = player.transform;
        }
        if (target != null) currentPivot = target.position + targetOffset;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Input putar & zoom (tidak aktif saat panel UI terbuka)
        if (!UIState.IsUIOpen && Input.GetMouseButton(rotateMouseButton))
        {
            if (enableRotation)
            {
                yaw += Input.GetAxis("Mouse X") * rotateSpeed;
                if (allowPitch)
                    pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * rotateSpeed, minPitch, maxPitch);
            }

            if (enableZoom)
            {
                float scroll = Input.mouseScrollDelta.y;
                distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
            }
        }

        // Ikuti pemain dengan halus
        Vector3 desiredPivot = target.position + targetOffset;
        float t = 1f - Mathf.Exp(-followSmoothness * Time.deltaTime);
        currentPivot = Vector3.Lerp(currentPivot, desiredPivot, t);

        // Posisikan kamera di orbit sekitar pemain
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = currentPivot - rotation * Vector3.forward * distance;
        transform.rotation = rotation;
    }

    void OnValidate()
    {
        if (minDistance > maxDistance) minDistance = maxDistance;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        if (minPitch > maxPitch) minPitch = maxPitch;
    }
}
