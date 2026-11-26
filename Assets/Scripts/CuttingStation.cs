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

    [Header("Mode potong")]
    [Tooltip("Centang = memotong lewat panel ala Cooking Mama (ikuti garis dengan mouse). " +
             "Matikan = mode lama, tekan E berkali-kali.")]
    public bool useCuttingPanel = true;

    [Header("Hasil potong mode panel (potongan bahan / sisa organik)")]
    public int perfectYield = 2;
    public int perfectWaste = 1;
    public int goodYield = 1;
    public int goodWaste = 1;
    public int badYield = 1;
    public int badWaste = 2;

    private bool PanelMode => useCuttingPanel && CuttingPanelUI.Instance != null;

    private IngredientData onBoard;   // bahan yang sedang dipotong (mode lama)
    private int chops;                // jumlah potongan saat ini

    void Start() => UpdateVisual();

    public override string GetPromptText()
    {
        if (PanelMode)
        {
            IngredientData item = FindCuttable();
            return item == null ? $"{displayName}: tidak ada bahan untuk dipotong"
                                : $"Tekan E untuk memotong {item.displayName}";
        }

        if (onBoard != null)
            return $"Tekan E untuk memotong {onBoard.displayName} {ProgressBar()}";

        IngredientData next = FindCuttable();
        if (next == null) return $"{displayName}: tidak ada bahan untuk dipotong";
        return $"Tekan E untuk mulai memotong {next.displayName}";
    }

    public override string GetSecondaryPromptText()
    {
        if (PanelMode) return null;
        return onBoard != null ? $"Tekan Q untuk mengambil kembali {onBoard.displayName}" : null;
    }

    public override void Interact()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        // ---- Mode panel: ikuti garis dengan pisau ----
        if (PanelMode)
        {
            IngredientData raw = FindCuttable();
            if (raw == null)
            {
                ShowMessage("Tidak ada bahan yang bisa dipotong!");
                return;
            }

            inv.RemoveItem(raw); // bahan ditaruh di talenan
            CuttingPanelUI.Instance.Open(raw,
                grade => GiveResult(raw, grade),
                () =>
                {
                    inv.AddItem(raw); // batal: bahan dikembalikan
                    ShowMessage($"Batal memotong {raw.displayName}");
                });
            return;
        }

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

    /// <summary>Memberikan hasil potong sesuai nilai dari panel.</summary>
    private void GiveResult(IngredientData raw, CutGrade grade)
    {
        var inv = InventoryManager.Instance;
        if (inv == null || raw.cutResult == null) return;

        int yield, waste;
        string label;
        switch (grade)
        {
            case CutGrade.Perfect: yield = perfectYield; waste = perfectWaste; label = "PERFECT!"; break;
            case CutGrade.Good: yield = goodYield; waste = goodWaste; label = "GOOD"; break;
            default: yield = badYield; waste = badWaste; label = "BAD"; break;
        }

        if (yield > 0) inv.AddItem(raw.cutResult, yield);
        if (raw.wasteResult != null && waste > 0) inv.AddItem(raw.wasteResult, waste);

        string wasteText = raw.wasteResult != null && waste > 0 ? $" + {waste} {raw.wasteResult.displayName}" : "";
        ShowMessage($"{label} {yield} {raw.cutResult.displayName}{wasteText}");
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
