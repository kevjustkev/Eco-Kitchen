using UnityEngine;

/// <summary>
/// Gerak pemain sesuai GDD:
/// - WASD bergerak MENGIKUTI ARAH KAMERA (W = menjauh dari kamera)
/// - Karakter berputar menghadap arah jalan
/// - Shift untuk berlari
/// Pasang di objek Player (wajib ada CharacterController).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Kecepatan")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public KeyCode runKey = KeyCode.LeftShift;

    [Tooltip("Seberapa cepat mencapai kecepatan penuh / berhenti (semakin besar semakin responsif)")]
    public float acceleration = 25f;

    [Header("Rotasi")]
    [Tooltip("Kecepatan berputar menghadap arah jalan (derajat per detik)")]
    public float rotationSpeed = 720f;

    [Header("Kamera")]
    [Tooltip("Kamera acuan arah gerak. Kosong = Main Camera")]
    public Transform cameraTransform;

    [Header("Gravitasi")]
    public float gravity = -9.81f;

    public bool IsRunning { get; private set; }
    public bool IsMoving { get; private set; }
    /// <summary>Kecepatan mendatar saat ini (untuk animasi nanti).</summary>
    public float CurrentSpeed => planarVelocity.magnitude;

    private CharacterController controller;
    private Vector3 planarVelocity;
    private float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        // 1. Baca input (dimatikan saat panel UI terbuka)
        Vector3 input = Vector3.zero;
        if (!UIState.IsUIOpen)
        {
            input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            input = Vector3.ClampMagnitude(input, 1f);
        }

        // 2. Ubah input menjadi arah dunia berdasarkan arah kamera
        Vector3 move = input;
        if (cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward; forward.y = 0f; forward.Normalize();
            Vector3 right = cameraTransform.right;     right.y = 0f;   right.Normalize();
            move = forward * input.z + right * input.x;
            move = Vector3.ClampMagnitude(move, 1f);
        }

        IsMoving = move.sqrMagnitude > 0.01f;
        IsRunning = IsMoving && (Input.GetKey(runKey) || Input.GetKey(KeyCode.RightShift));

        // 3. Percepatan halus menuju kecepatan target
        float targetSpeed = IsRunning ? runSpeed : walkSpeed;
        planarVelocity = Vector3.MoveTowards(planarVelocity, move * targetSpeed, acceleration * Time.deltaTime);

        // 4. Putar karakter menghadap arah jalan
        if (IsMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 5. Gravitasi
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = planarVelocity;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}
