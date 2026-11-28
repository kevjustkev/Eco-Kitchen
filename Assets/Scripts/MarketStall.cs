using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// REVISI 3: Lapak pasar fisik (pengganti panel belanja berbasis UI).
/// Pemain berjalan ke lapak di area pasar, lalu:
///   E = beli (barang langsung masuk inventory pemain, bayar sesuai harga hari ini)
///   Q = tawar harga (sekali per hari per lapak, bisa untung bisa rugi)
/// Harga berubah tiap hari dan stok lapak terbatas, jadi pemain harus memilih belanja apa.
/// Satu lapak menjual satu jenis bahan. Pasang di objek lapak (wajib punya Collider).
/// </summary>
public class MarketStall : Interactable
{
    [Header("Dagangan")]
    public IngredientData item;
    [Tooltip("Isi untuk mengganti Buy Price di asset bahan. 0 = pakai Buy Price asset")]
    public int basePriceOverride = 0;
    [Tooltip("Jumlah yang didapat setiap kali membeli")]
    [Min(1)] public int amountPerPurchase = 1;
    [Tooltip("Stok lapak per hari. -1 = tidak terbatas")]
    public int dailyStock = 6;

    [Header("Harga harian")]
    [Tooltip("Harga hari ini = harga dasar x angka acak di rentang ini")]
    public Vector2 priceMultiplierRange = new Vector2(0.8f, 1.3f);

    [Header("Tawar-menawar (tombol Q)")]
    [Range(0f, 1f)] public float haggleSuccessChance = 0.5f;
    [Tooltip("Diskon jika tawaran berhasil (0.2 = 20%)")]
    [Range(0f, 0.9f)] public float haggleDiscount = 0.2f;
    [Tooltip("Kenaikan harga jika penjual menolak (0.1 = 10%)")]
    [Range(0f, 0.9f)] public float haggleSurcharge = 0.1f;

    [Header("Tampilan")]
    public float labelHeight = 1.6f;

    private int stock;
    private float multiplier = 1f;
    private float haggleMod;      // -0.2 = diskon 20%, +0.1 = naik 10%
    private bool haggledToday;

    private GameObject labelRoot;
    private TextMeshProUGUI labelText;

    public bool IsInfinite => dailyStock < 0;

    void Start()
    {
        NewDay();
        BuildLabel();
        UpdateLabel();

        if (DayManager.Instance != null)
            DayManager.Instance.OnDayStarted += OnDayStarted;
    }

    void OnDestroy()
    {
        if (DayManager.Instance != null)
            DayManager.Instance.OnDayStarted -= OnDayStarted;
    }

    private void OnDayStarted(int day)
    {
        NewDay();
        UpdateLabel();
    }

    private void NewDay()
    {
        stock = dailyStock;
        multiplier = Random.Range(priceMultiplierRange.x, priceMultiplierRange.y);
        haggleMod = 0f;
        haggledToday = false;
    }

    void LateUpdate()
    {
        if (labelRoot != null && Camera.main != null)
            labelRoot.transform.rotation = Camera.main.transform.rotation;
    }

    // ------------------------------------------------------------------ harga

    public int CurrentPrice
    {
        get
        {
            int basePrice = basePriceOverride > 0 ? basePriceOverride : (item != null ? item.buyPrice : 0);
            if (basePrice <= 0) basePrice = 1000;
            float p = basePrice * multiplier * (1f + haggleMod);
            return Mathf.Max(100, Mathf.RoundToInt(p / 100f) * 100);
        }
    }

    // ------------------------------------------------------------------ interaksi

    public override string GetPromptText()
    {
        if (item == null) return $"{displayName} (dagangan belum diatur)";
        if (!IsInfinite && stock < amountPerPurchase) return $"{displayName}: {item.displayName} habis hari ini";

        return $"Tekan E untuk membeli {amountPerPurchase} {item.displayName} " +
               $"({EconomyManager.Rupiah(CurrentPrice * amountPerPurchase)})";
    }

    public override string GetSecondaryPromptText()
    {
        if (item == null || haggledToday) return null;
        return "Tekan Q untuk menawar harga";
    }

    public override void Interact()
    {
        if (item == null) return;
        var inv = InventoryManager.Instance;
        var eco = EconomyManager.Instance;
        if (inv == null || eco == null) return;

        if (!IsInfinite && stock < amountPerPurchase)
        {
            ShowMessage($"{item.displayName} sudah habis, kembali besok");
            return;
        }

        int total = CurrentPrice * amountPerPurchase;
        if (!eco.TrySpend(total))
        {
            ShowMessage($"Uang tidak cukup ({EconomyManager.Rupiah(total)})");
            return;
        }

        inv.AddItem(item, amountPerPurchase);
        if (!IsInfinite) stock -= amountPerPurchase;
        UpdateLabel();
        ShowMessage($"Membeli {amountPerPurchase} {item.displayName} ({EconomyManager.Rupiah(total)})");
    }

    public override void InteractSecondary()
    {
        if (item == null || haggledToday) return;
        haggledToday = true;

        if (Random.value <= haggleSuccessChance)
        {
            haggleMod = -haggleDiscount;
            ShowMessage($"Tawaran berhasil! Harga {item.displayName} turun {Mathf.RoundToInt(haggleDiscount * 100)}% hari ini");
        }
        else
        {
            haggleMod = haggleSurcharge;
            ShowMessage($"Penjual tersinggung, harga {item.displayName} naik {Mathf.RoundToInt(haggleSurcharge * 100)}%");
        }
        UpdateLabel();
    }

    // ------------------------------------------------------------------ label (dibuat lewat kode)

    private void BuildLabel()
    {
        labelRoot = new GameObject("StallLabel", typeof(RectTransform), typeof(Canvas));
        labelRoot.transform.SetParent(transform, false);

        Vector3 s = transform.lossyScale;
        labelRoot.transform.localPosition = new Vector3(0f, labelHeight / Mathf.Max(0.001f, s.y), 0f);
        labelRoot.transform.localScale = new Vector3(0.01f / Mathf.Max(0.001f, s.x),
                                                     0.01f / Mathf.Max(0.001f, s.y),
                                                     0.01f / Mathf.Max(0.001f, s.z));
        labelRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)labelRoot.transform).sizeDelta = new Vector2(300, 100);

        var bg = UIBuilder.CreateImage("Bg", labelRoot.transform, new Color(0.25f, 0.17f, 0.08f, 0.9f));
        UIBuilder.Stretch(bg.rectTransform);

        labelText = UIBuilder.CreateText("Text", labelRoot.transform, "", 30, TextAlignmentOptions.Center);
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 12;
        labelText.fontSizeMax = 30;
        UIBuilder.Stretch(labelText.rectTransform, 6f);
    }

    private void UpdateLabel()
    {
        if (labelText == null || item == null) return;

        string stockText = IsInfinite ? "stok bebas" : (stock < amountPerPurchase ? "<color=#EC7063>HABIS</color>" : $"stok {stock}");
        string trend = haggleMod < 0f ? " <color=#58D68D>(diskon)</color>"
                     : haggleMod > 0f ? " <color=#EC7063>(naik)</color>" : "";
        labelText.text = $"<b>{item.displayName}</b>\n<size=75%>{EconomyManager.Rupiah(CurrentPrice)}{trend}  |  {stockText}</size>";
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (amountPerPurchase < 1) amountPerPurchase = 1;
        if (priceMultiplierRange.y < priceMultiplierRange.x) priceMultiplierRange.y = priceMultiplierRange.x;
        if (item != null && (string.IsNullOrEmpty(displayName) || displayName == "Objek"))
            displayName = "Lapak " + item.displayName;
    }
}
