using UnityEngine;

/// <summary>
/// Tempat mengambil bahan (peti/rak bahan). Objeknya TIDAK hilang setelah diambil.
/// Stock = -1 berarti tidak terbatas. Stok diisi ulang lewat pembelian di Market.
/// </summary>
public class IngredientSource : Interactable
{
    [Header("Bahan")]
    public IngredientData ingredient;

    [Tooltip("Stok awal. -1 = tidak terbatas")]
    public int stock = 5;

    [Tooltip("Jumlah yang diambil setiap tekan E")]
    public int amountPerTake = 1;

    [Tooltip("Tampilkan jumlah stok di atas peti")]
    public bool showStockLabel = true;
    public float labelHeight = 1.4f;

    public bool IsInfinite => stock < 0;

    private TMPro.TextMeshProUGUI stockLabel;
    private GameObject labelRoot;

    void Start()
    {
        if (showStockLabel) BuildLabel();
        UpdateLabel();
    }

    void LateUpdate()
    {
        if (labelRoot != null && Camera.main != null)
            labelRoot.transform.rotation = Camera.main.transform.rotation;
    }

    public override string GetPromptText()
    {
        if (ingredient == null) return $"{displayName} (bahan belum diatur)";

        if (!IsInfinite && stock <= 0)
            return $"Stok {ingredient.displayName} habis. Beli di Market atau panen sendiri";

        string stockText = IsInfinite ? "" : $" (stok: {stock})";
        return $"Tekan E untuk mengambil {ingredient.displayName}{stockText}";
    }

    public override void Interact()
    {
        if (ingredient == null)
        {
            Debug.LogWarning($"{name}: field Ingredient belum diisi di Inspector.");
            return;
        }
        if (InventoryManager.Instance == null) return;

        if (!IsInfinite && stock < amountPerTake)
        {
            ShowMessage($"Stok {ingredient.displayName} habis!");
            return;
        }

        InventoryManager.Instance.AddItem(ingredient, amountPerTake);
        if (!IsInfinite) stock -= amountPerTake;
        UpdateLabel();

        ShowMessage($"Mengambil {ingredient.displayName}");
    }

    /// <summary>Menambah stok (dipanggil Market saat membeli).</summary>
    public void AddStock(int amount)
    {
        if (IsInfinite || amount <= 0) return;
        stock += amount;
        UpdateLabel();
    }

    // ------------------------------------------------------------------ label stok

    private void BuildLabel()
    {
        labelRoot = new GameObject("StockLabel", typeof(RectTransform), typeof(Canvas));
        labelRoot.transform.SetParent(transform, false);

        Vector3 s = transform.lossyScale;
        labelRoot.transform.localPosition = new Vector3(0f, labelHeight / Mathf.Max(0.001f, s.y), 0f);
        labelRoot.transform.localScale = new Vector3(0.01f / Mathf.Max(0.001f, s.x),
                                                     0.01f / Mathf.Max(0.001f, s.y),
                                                     0.01f / Mathf.Max(0.001f, s.z));
        labelRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)labelRoot.transform).sizeDelta = new Vector2(260, 70);

        var bg = UIBuilder.CreateImage("Bg", labelRoot.transform, new Color(0f, 0f, 0f, 0.6f));
        UIBuilder.Stretch(bg.rectTransform);

        stockLabel = UIBuilder.CreateText("Text", labelRoot.transform, "", 30, TMPro.TextAlignmentOptions.Center);
        stockLabel.enableAutoSizing = true;
        stockLabel.fontSizeMin = 12;
        stockLabel.fontSizeMax = 30;
        UIBuilder.Stretch(stockLabel.rectTransform, 6f);
    }

    private void UpdateLabel()
    {
        if (stockLabel == null || ingredient == null) return;
        string count = IsInfinite ? "tak terbatas" : (stock <= 0 ? "<color=#EC7063>HABIS</color>" : stock.ToString());
        stockLabel.text = $"{ingredient.displayName}: {count}";
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (amountPerTake < 1) amountPerTake = 1;
        if (ingredient != null && (string.IsNullOrEmpty(displayName) || displayName == "Objek"))
            displayName = "Peti " + ingredient.displayName;
    }
}
