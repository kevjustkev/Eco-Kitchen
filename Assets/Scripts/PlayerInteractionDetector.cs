using UnityEngine;

/// <summary>
/// Mendeteksi objek Interactable terdekat di sekitar pemain.
/// E = aksi utama, Q = aksi kedua, Tab / scroll mouse = pilih item di inventory.
/// Pasang di objek Player.
/// </summary>
public class PlayerInteractionDetector : MonoBehaviour
{
    public float interactionRadius = 2.5f;
    public KeyCode interactKey = KeyCode.E;
    public KeyCode secondaryKey = KeyCode.Q;
    public KeyCode selectNextKey = KeyCode.Tab;

    [Tooltip("Layer yang diperiksa. Default: semua layer.")]
    public LayerMask interactableLayers = ~0;

    private Interactable currentTarget;
    private readonly Collider[] buffer = new Collider[32];

    public Interactable CurrentTarget => currentTarget;

    void Update()
    {
        // Saat panel UI terbuka, interaksi dengan dunia dimatikan.
        if (UIState.IsUIOpen) return;

        DetectInteractable();
        HandleSelectionInput();

        if (currentTarget == null) return;

        if (Input.GetKeyDown(interactKey))
            currentTarget.Interact();
        else if (Input.GetKeyDown(secondaryKey))
            currentTarget.InteractSecondary();
    }

    void HandleSelectionInput()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        if (Input.GetKeyDown(selectNextKey)) inv.SelectNext(1);

        // Saat klik kanan ditahan, scroll dipakai untuk zoom kamera, bukan memilih item
        if (Input.GetMouseButton(1)) return;

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.1f) inv.SelectNext(-1);
        else if (scroll < -0.1f) inv.SelectNext(1);
    }

    void DetectInteractable()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, interactionRadius, buffer,
            interactableLayers, QueryTriggerInteraction.Collide);

        Interactable nearest = null;
        float nearestDistance = Mathf.Infinity;

        for (int i = 0; i < count; i++)
        {
            Interactable it = buffer[i].GetComponentInParent<Interactable>();
            if (it == null || !it.isActiveAndEnabled) continue;

            // Jarak diukur ke titik terdekat collider, bukan ke pusat objek,
            // supaya meja panjang tetap terdeteksi dari ujungnya.
            Vector3 closest = buffer[i].ClosestPoint(transform.position);
            float distance = Vector3.Distance(transform.position, closest);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = it;
            }
        }

        currentTarget = nearest;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
