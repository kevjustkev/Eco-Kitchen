using System.Text;
using UnityEngine;

/// <summary>
/// Meja campur SEMENTARA: menggabungkan bahan-bahan tertentu menjadi satu hasil.
/// Di tahap 2 ini akan diganti sistem mangkuk (Bowl) yang lebih fleksibel.
/// </summary>
public class MixingStation : Interactable
{
    [System.Serializable]
    public class Requirement
    {
        public IngredientData ingredient;
        [Min(1)] public int amount = 1;
    }

    [Header("Resep Campuran")]
    public Requirement[] inputs;
    public IngredientData output;

    public override string GetPromptText()
    {
        if (output == null) return $"{displayName} (resep belum diatur)";
        string missing = GetMissingText();
        return string.IsNullOrEmpty(missing)
            ? $"Tekan E untuk membuat {output.displayName}"
            : $"{displayName}: kurang {missing}";
    }

    public override void Interact()
    {
        var inv = InventoryManager.Instance;
        if (inv == null || output == null || inputs == null || inputs.Length == 0) return;

        string missing = GetMissingText();
        if (!string.IsNullOrEmpty(missing))
        {
            ShowMessage("Bahan belum lengkap: " + missing);
            return;
        }

        foreach (var req in inputs)
            inv.RemoveItem(req.ingredient, req.amount);

        inv.AddItem(output);
        ShowMessage($"{output.displayName} berhasil dibuat!");
    }

    private string GetMissingText()
    {
        var inv = InventoryManager.Instance;
        if (inv == null || inputs == null) return "";

        var sb = new StringBuilder();
        foreach (var req in inputs)
        {
            if (req == null || req.ingredient == null) continue;
            int lack = req.amount - inv.GetCount(req.ingredient);
            if (lack > 0)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{lack} {req.ingredient.displayName}");
            }
        }
        return sb.ToString();
    }
}
