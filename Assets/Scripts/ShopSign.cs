using UnityEngine;

/// <summary>
/// REVISI 1: Papan "Buka Restoran".
/// Pelanggan TIDAK langsung datang saat hari dimulai. Pemain harus menyiapkan bahan dulu
/// (panen, kompos, potong, racik), lalu menekan E di papan ini untuk membuka restoran.
/// Pasang di objek papan/pintu (wajib punya Collider). Cukup satu di scene.
/// </summary>
public class ShopSign : Interactable
{
    public override string GetPromptText()
    {
        var day = DayManager.Instance;
        if (day == null) return $"{displayName}";
        if (day.IsShopOpen) return "Restoran sedang buka";
        return "Tekan E untuk membuka restoran (pelanggan mulai datang)";
    }

    public override void Interact()
    {
        var day = DayManager.Instance;
        if (day == null) return;

        if (day.IsShopOpen)
        {
            ShowMessage("Restoran sudah buka");
            return;
        }
        day.OpenShop();
    }
}
