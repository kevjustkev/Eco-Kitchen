using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Satu jenis pesanan pelanggan. Buat lewat: klik kanan di Project > Create > Eco Kitchen > Order.
/// Contoh variasi sesuai GDD: bahan tertentu, jumlah tertentu, dressing tertentu, tanpa bahan tertentu.
/// </summary>
[CreateAssetMenu(fileName = "NewOrder", menuName = "Eco Kitchen/Order")]
public class OrderData : ScriptableObject
{
    public enum DressingRule
    {
        Bebas,      // dressing apa saja atau tanpa dressing, tidak dinilai
        Tanpa,      // pelanggan TIDAK mau dressing
        Tertentu    // harus memakai dressing yang ditentukan
    }

    [System.Serializable]
    public class IngredientAmount
    {
        public IngredientData ingredient;
        [Min(1)] public int amount = 1;
    }

    [Header("Info")]
    public string orderName = "Salad Biasa";
    [TextArea] public string customerLine = "Saya pesan salad, ya!";

    [Header("Isi Pesanan")]
    [Tooltip("Bahan (yang sudah dipotong) dan jumlahnya")]
    public List<IngredientAmount> ingredients = new List<IngredientAmount>();

    public DressingRule dressingRule = DressingRule.Bebas;
    [Tooltip("Diisi jika Dressing Rule = Tertentu")]
    public IngredientData dressing;

    [Tooltip("Bahan yang TIDAK boleh ada (misalnya: tanpa Tomat Potong)")]
    public List<IngredientData> forbidden = new List<IngredientData>();

    [Header("Harga & Waktu")]
    public int basePrice = 20000;
    [Tooltip("Bonus jika Perfect dan disajikan sebelum kesabaran pelanggan tinggal setengah")]
    public int perfectBonus = 5000;
    [Tooltip("Lama pelanggan mau menunggu (detik)")]
    public float patienceSeconds = 60f;

    [Header("Progres")]
    [Tooltip("Pesanan ini mulai muncul pada hari ke-")]
    [Min(1)] public int unlockDay = 1;

    public bool Requires(IngredientData item)
    {
        foreach (var req in ingredients)
            if (req != null && req.ingredient == item) return true;
        return false;
    }

    /// <summary>Teks pesanan untuk gelembung di atas kepala pelanggan.</summary>
    public string Describe()
    {
        var sb = new StringBuilder();
        foreach (var req in ingredients)
        {
            if (req == null || req.ingredient == null) continue;
            sb.Append("• ").Append(req.ingredient.displayName);
            if (req.amount > 1) sb.Append(" x").Append(req.amount);
            sb.Append('\n');
        }

        if (dressingRule == DressingRule.Tertentu && dressing != null)
            sb.Append("• ").Append(dressing.displayName).Append('\n');
        else if (dressingRule == DressingRule.Tanpa)
            sb.Append("• Tanpa dressing\n");

        foreach (var f in forbidden)
            if (f != null) sb.Append("• <color=#C0392B>Tanpa ").Append(f.displayName).Append("</color>\n");

        return sb.ToString().TrimEnd('\n');
    }

    void OnValidate()
    {
        if (orderName != null) orderName = orderName.Trim();
        if (patienceSeconds < 5f) patienceSeconds = 5f;
        if (basePrice < 0) basePrice = 0;
    }
}
