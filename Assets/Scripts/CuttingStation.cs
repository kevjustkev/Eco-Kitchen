using UnityEngine;

/// <summary>
/// Meja potong ala Overcooked: tekan E berkali-kali sampai bahan selesai dipotong.
/// - Tekan E pertama: bahan dari inventory ditaruh di talenan dan mulai dipotong.
/// - Progres tersimpan di meja; pemain boleh pergi lalu kembali melanjutkan.
/// - Tekan Q: ambil kembali bahan yang belum selesai dipotong.
/// Bahan yang dipilih: item yang sedang dipilih di inventory (Tab/scroll) jika bisa dipotong,
/// kalau tidak, bahan pertama yang bisa dipotong.
/// </summary>
public class CuttingStation : Interactable
{
    [Tooltip("Jika diisi, meja ini HANYA menerima bahan di daftar ini. Kosongkan = semua bahan yang bisa dipotong.")]
    public IngredientData[] allowedIngredients;

    [Tooltip("Opsional: objek visual yang muncul saat ada bahan di talenan")]
    public GameObject choppingVisual;

    [Tooltip("Opsional: Image (Image Type = Filled) di Canvas world space sebagai bar progres")]
    public UnityEngine.UI.Image progressFill;

    private IngredientData onBoard;   // bahan yang sedang dipotong
    private int chops;                // jumlah potongan saat ini

    void Start() => UpdateVisual();

    public override string GetPromptText()
    {
        if (onBoard != null)
            return $"Tekan E untuk memotong {onBoard.displayName} {ProgressBar()}";

        IngredientData next = FindCuttable();
        if (next == null) return $"{displayName}: tidak ada bahan untuk dipotong";
        return $"Tekan E untuk mulai memotong {next.displayName}";
    }

    public override string GetSecondaryPromptText()
    {
        return onBoard != null ? $"Tekan Q untuk mengambil kembali {onBoard.displayName}" : null;
    }

    public override void Interact()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        // Belum ada bahan di talenan: ambil dari inventory
        if (onBoard == null)
        {
            IngredientData raw = FindCuttable();
            if (raw == null)
            {
                ShowMessage("Tidak ada bahan yang bisa dipotong!");
                return;
            }
            inv.RemoveItem(raw);
            onBoard = raw;
            chops = 0;
        }

        chops++;

        if (chops >= Mathf.Max(1, onBoard.chopsRequired))
        {
            inv.AddItem(onBoard.cutResult);
            if (onBoard.wasteResult != null) inv.AddItem(onBoard.wasteResult);
            ShowMessage($"{onBoard.displayName} selesai dipotong menjadi {onBoard.cutResult.displayName}");
            onBoard = null;
            chops = 0;
        }

        UpdateVisual();
    }

    public override void InteractSecondary()
    {
        if (onBoard == null || InventoryManager.Instance == null) return;

        InventoryManager.Instance.AddItem(onBoard);
        ShowMessage($"{onBoard.displayName} diambil kembali");
        onBoard = null;
        chops = 0;
        UpdateVisual();
    }

    private string ProgressBar()
    {
        int total = Mathf.Max(1, onBoard.chopsRequired);
        return $"[{new string('#', chops)}{new string('-', total - chops)}] {chops}/{total}";
    }

    private void UpdateVisual()
    {
        if (choppingVisual != null) choppingVisual.SetActive(onBoard != null);

        if (progressFill != null)
        {
            progressFill.gameObject.SetActive(onBoard != null);
            if (onBoard != null)
                progressFill.fillAmount = (float)chops / Mathf.Max(1, onBoard.chopsRequired);
        }
    }

    private IngredientData FindCuttable()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return null;

        IngredientData selected = inv.SelectedItem;
        if (selected != null && selected.CanBeCut && IsAllowed(selected)) return selected;

        foreach (IngredientData item in inv.GetAllItems())
            if (item != null && item.CanBeCut && IsAllowed(item)) return item;

        return null;
    }

    private bool IsAllowed(IngredientData item)
    {
        if (allowedIngredients == null || allowedIngredients.Length == 0) return true;
        foreach (var a in allowedIngredients)
            if (a == item) return true;
        return false;
    }
}
