using System.Collections.Generic;
using System.Text;

/// <summary>
/// Isi satu mangkuk salad: daftar bahan + satu dressing (opsional).
/// Bukan komponen; dipakai oleh AssemblyStation, PlayerCarry, dan (tahap 3) penilaian pesanan.
/// </summary>
[System.Serializable]
public class SaladBowl
{
    public const int MaxIngredients = 6;

    public List<IngredientData> ingredients = new List<IngredientData>();
    public IngredientData dressing;

    public bool IsEmpty => ingredients.Count == 0 && dressing == null;
    public bool IsFull => ingredients.Count >= MaxIngredients;
    public bool HasIngredients => ingredients.Count > 0;

    public int CountOf(IngredientData item)
    {
        int n = 0;
        foreach (var i in ingredients) if (i == item) n++;
        return n;
    }

    /// <summary>Contoh hasil: "Selada Potong x2, Tomat Potong + Dressing Wijen"</summary>
    public string Describe()
    {
        if (IsEmpty) return "(kosong)";

        var sb = new StringBuilder();
        var counted = new List<IngredientData>();
        foreach (var item in ingredients)
        {
            if (item == null || counted.Contains(item)) continue;
            counted.Add(item);
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(item.displayName);
            int n = CountOf(item);
            if (n > 1) sb.Append(" x").Append(n);
        }

        if (sb.Length == 0) sb.Append("(tanpa bahan)");
        sb.Append(dressing != null ? " + " + dressing.displayName : " (tanpa dressing)");
        return sb.ToString();
    }
}
