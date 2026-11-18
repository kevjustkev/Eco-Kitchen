using UnityEngine;

/// <summary>
/// Kelas induk untuk semua objek yang bisa diajak berinteraksi dengan tombol E.
/// Jangan dipasang langsung; pasang turunannya (IngredientSource, CuttingStation, MixingStation, dst).
/// Objek wajib punya Collider agar bisa dideteksi.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Nama objek yang tampil di prompt, misalnya: Meja Potong")]
    public string displayName = "Objek";

    /// <summary>Teks yang tampil saat pemain mendekat.</summary>
    public virtual string GetPromptText()
    {
        return $"Tekan E untuk berinteraksi dengan {displayName}";
    }

    /// <summary>Dipanggil saat pemain menekan E.</summary>
    public abstract void Interact();

    /// <summary>Teks aksi kedua (tombol Q). Kembalikan null jika objek tidak punya aksi kedua.</summary>
    public virtual string GetSecondaryPromptText() => null;

    /// <summary>Dipanggil saat pemain menekan Q.</summary>
    public virtual void InteractSecondary() { }

    /// <summary>Menampilkan pesan singkat ke pemain (sementara lewat Console + UI notifikasi).</summary>
    protected void ShowMessage(string message)
    {
        Debug.Log(message);
        if (NotificationUI.Instance != null)
            NotificationUI.Instance.Show(message);
    }

    protected virtual void OnValidate()
    {
        if (displayName != null) displayName = displayName.Trim();
    }
}
