using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Market: tekan E untuk membuka panel belanja (MarketPanelUI).
/// Barang yang dibeli MENGISI STOK PETI yang menyimpan bahan tersebut (IngredientSource di scene).
/// </summary>
public class MarketStation : Interactable
{
    [System.Serializable]
    public class MarketItem
    {
        public IngredientData item;
        [Tooltip("Kosongkan untuk memakai Buy Price di asset bahan")]
        public int priceOverride = 0;
    }

    [Tooltip("Barang yang dijual di market")]
    public List<MarketItem> itemsForSale = new List<MarketItem>();

    public override string GetPromptText() => $"Tekan E untuk belanja di {displayName}";

    public override void Interact()
    {
        if (MarketPanelUI.Instance == null)
        {
            Debug.LogError("MarketPanelUI belum ada di scene. Pasang komponen MarketPanelUI di GameManager.");
            return;
        }
        MarketPanelUI.Instance.Open(this);
    }

    public int GetPrice(MarketItem m)
    {
        if (m == null || m.item == null) return 0;
        return m.priceOverride > 0 ? m.priceOverride : m.item.buyPrice;
    }

    /// <summary>Peti di scene yang menyimpan bahan ini (pertama yang ditemukan).</summary>
    public static IngredientSource FindSourceFor(IngredientData item)
    {
        foreach (var src in FindObjectsByType<IngredientSource>(FindObjectsSortMode.None))
            if (src.ingredient == item) return src;
        return null;
    }

    /// <summary>Membeli sejumlah barang. Hasilnya mengisi stok peti terkait.</summary>
    public bool TryBuy(MarketItem m, int amount, out string message)
    {
        if (m == null || m.item == null || amount <= 0) { message = "Barang tidak valid"; return false; }

        IngredientSource src = FindSourceFor(m.item);
        if (src == null)
        {
            message = $"Tidak ada peti untuk {m.item.displayName} di restoran";
            return false;
        }
        if (src.IsInfinite)
        {
            message = $"Stok {m.item.displayName} tidak terbatas, tidak perlu dibeli";
            return false;
        }

        int total = GetPrice(m) * amount;
        var eco = EconomyManager.Instance;
        if (eco == null || !eco.TrySpend(total))
        {
            message = $"Uang tidak cukup ({EconomyManager.Rupiah(total)})";
            return false;
        }

        src.AddStock(amount);
        message = $"Membeli {amount} {m.item.displayName} ({EconomyManager.Rupiah(total)}). Ambil di {src.displayName}";
        return true;
    }
}
