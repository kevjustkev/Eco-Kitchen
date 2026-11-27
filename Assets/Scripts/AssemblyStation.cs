using UnityEngine;

/// <summary>
/// Meja Racik: tekan E untuk membuka panel racik salad (AssemblyPanelUI).
/// Isi mangkuk tersimpan di meja, jadi panel boleh ditutup lalu dibuka lagi.
/// </summary>
public class AssemblyStation : Interactable
{
    [Tooltip("Opsional: objek mangkuk di atas meja yang muncul saat mangkuk berisi")]
    public GameObject bowlVisual;
    [Tooltip("Centang untuk memakai minigame 3D (butuh komponen AssemblyMinigame3D di objek ini)")]
    public bool useMinigame3D = true;

    public SaladBowl Bowl { get; private set; } = new SaladBowl();

    void Start() => UpdateVisual();

    public override string GetPromptText()
    {
        var carry = PlayerCarry.Instance;
        if (carry != null && carry.IsCarrying && Bowl.IsEmpty)
            return $"Tekan E untuk menaruh salad kembali ke {displayName}";

        return Bowl.IsEmpty
            ? $"Tekan E untuk meracik salad di {displayName}"
            : $"Tekan E untuk melanjutkan racikan: {Bowl.Describe()}";
    }

    public override void Interact()
    {
        var carry = PlayerCarry.Instance;

        // Salad yang dibawa boleh ditaruh kembali untuk diubah isinya
        if (carry != null && carry.IsCarrying && Bowl.IsEmpty)
        {
            Bowl = carry.Drop();
            UpdateVisual();
            ShowMessage("Salad ditaruh kembali di meja");
        }

        // Mode 3D: kalau komponen AssemblyMinigame3D terpasang dan dicentang
        var minigame3D = GetComponent<AssemblyMinigame3D>();
        if (minigame3D == null) minigame3D = GetComponentInChildren<AssemblyMinigame3D>();
        if (minigame3D == null) minigame3D = GetComponentInParent<AssemblyMinigame3D>();
        Debug.Log($"[AssemblyStation] {name}: useMinigame3D={useMinigame3D}, komponen 3D {(minigame3D != null ? "ada" : "TIDAK ADA di objek ini")}");
        if (useMinigame3D && minigame3D != null && minigame3D.enabled)
        {
            minigame3D.Begin(this);
            return;
        }

        if (AssemblyPanelUI.Instance == null)
        {
            Debug.LogError("AssemblyPanelUI belum ada di scene. Buat GameObject kosong lalu pasang komponen AssemblyPanelUI.");
            return;
        }

        AssemblyPanelUI.Instance.Open(this);
    }

    /// <summary>Memasukkan satu item dari inventory ke mangkuk.</summary>
    public bool TryAdd(IngredientData item, out string message)
    {
        var inv = InventoryManager.Instance;

        if (item == null || inv == null) { message = "Item tidak valid"; return false; }

        if (!item.canGoInBowl && !item.isDressing)
        { message = $"{item.displayName} tidak bisa dimasukkan ke mangkuk"; return false; }

        if (item.isDressing && Bowl.dressing != null)
        { message = $"Mangkuk sudah berisi {Bowl.dressing.displayName}"; return false; }

        if (!item.isDressing && Bowl.IsFull)
        { message = $"Mangkuk penuh (maksimal {SaladBowl.MaxIngredients} bahan)"; return false; }

        if (!inv.RemoveItem(item))
        { message = $"{item.displayName} habis"; return false; }

        if (item.isDressing) Bowl.dressing = item;
        else Bowl.ingredients.Add(item);

        UpdateVisual();
        message = $"{item.displayName} dimasukkan";
        return true;
    }

    /// <summary>Mengosongkan mangkuk; semua isinya dikembalikan ke inventory.</summary>
    public void ClearBowl()
    {
        var inv = InventoryManager.Instance;
        if (inv != null)
        {
            foreach (var item in Bowl.ingredients) inv.AddItem(item);
            if (Bowl.dressing != null) inv.AddItem(Bowl.dressing);
        }
        Bowl = new SaladBowl();
        UpdateVisual();
    }

    /// <summary>Pemain membawa salad yang sudah jadi.</summary>
    public bool TryTakeBowl(out string message)
    {
        var carry = PlayerCarry.Instance;

        if (!Bowl.HasIngredients) { message = "Mangkuk masih kosong, masukkan bahan dulu"; return false; }
        if (carry == null) { message = "Komponen PlayerCarry belum dipasang di Player"; return false; }
        if (carry.IsCarrying) { message = "Kamu sudah membawa salad lain"; return false; }

        carry.Pick(Bowl);
        message = $"Membawa salad: {Bowl.Describe()}";
        Bowl = new SaladBowl();
        UpdateVisual();
        return true;
    }

    public void UpdateVisual()
    {
        if (bowlVisual != null) bowlVisual.SetActive(!Bowl.IsEmpty);
    }
}
