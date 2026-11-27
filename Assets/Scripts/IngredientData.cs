using UnityEngine;

/// <summary>
/// Data satu jenis bahan/item (Selada, Tomat, Selada Potong, Sisa Organik, dst).
/// Buat lewat: klik kanan di Project > Create > Eco Kitchen > Ingredient.
/// Menambah bahan baru cukup dengan membuat asset baru, tanpa mengubah kode.
/// </summary>
[CreateAssetMenu(fileName = "NewIngredient", menuName = "Eco Kitchen/Ingredient")]
public class IngredientData : ScriptableObject
{
    [Header("Identitas")]
    [Tooltip("Nama yang tampil di layar, misalnya: Selada")]
    public string displayName = "Bahan Baru";

    [Tooltip("Ikon untuk UI (opsional, dipakai di tahap berikutnya)")]
    public Sprite icon;

    [Tooltip("Warna penanda (opsional)")]
    public Color color = Color.white;

    [Header("Pemotongan")]
    [Tooltip("Hasil jika bahan ini dipotong. Kosongkan jika bahan tidak bisa dipotong.")]
    public IngredientData cutResult;

    [Tooltip("Sisa organik yang dihasilkan saat dipotong (untuk sistem kompos). Boleh kosong.")]
    public IngredientData wasteResult;

    [Tooltip("Berapa kali tekan E sampai bahan selesai dipotong")]
    [Min(1)] public int chopsRequired = 3;

    [Header("Racik Salad")]
    [Tooltip("Centang untuk bahan yang boleh dimasukkan ke mangkuk (misalnya Selada Potong, Tomat Potong)")]
    public bool canGoInBowl = false;

    [Tooltip("Centang jika item ini adalah dressing. Satu mangkuk hanya boleh satu dressing.")]
    public bool isDressing = false;

    [Header("Model 3D (minigame 3D)")]
    [Tooltip("Model 3D bahan ini. Bahan mentah: model utuh (tomato, lettuce). " +
             "Bahan potong: model potongan (TomatIrisan, SeladaPotongan). Dressing: model botol.")]
    public GameObject model3D;

    [Tooltip("Opsional, khusus bahan mentah: model setengah bagian (TomatBelah) untuk efek terbelah saat dipotong")]
    public GameObject halfModel3D;

    [Tooltip("Pengali ukuran model 3D bahan ini")]
    public float modelScale = 1f;

    [Header("Benih (kebun)")]
    [Tooltip("Isi jika item ini BENIH: hasil panennya (misalnya Benih Selada -> Selada). Kosongkan untuk item biasa.")]
    public IngredientData harvestResult;

    [Tooltip("Jumlah hasil panen per tanaman")]
    [Min(1)] public int harvestAmount = 2;

    [Tooltip("Lama tumbuh setelah disiram (detik)")]
    [Min(1f)] public float growSeconds = 60f;

    [Header("Ekonomi (dipakai di tahap berikutnya)")]
    [Tooltip("Harga beli di market")]
    public int buyPrice = 0;

    public bool CanBeCut => cutResult != null;
    public bool IsSeed => harvestResult != null;

    void OnValidate()
    {
        if (displayName != null) displayName = displayName.Trim();
        if (cutResult == this)
        {
            Debug.LogWarning($"[{name}] Cut Result tidak boleh dirinya sendiri.");
            cutResult = null;
        }
        if (harvestResult == this)
        {
            Debug.LogWarning($"[{name}] Harvest Result tidak boleh dirinya sendiri.");
            harvestResult = null;
        }
    }
}
