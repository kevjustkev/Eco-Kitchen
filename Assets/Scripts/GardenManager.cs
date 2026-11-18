using System;
using UnityEngine;

/// <summary>
/// Mengatur level kebun dan petak yang terbuka, sesuai GDD:
/// Level 1 = 1 petak, Level 2 = 3 petak, Level 3 = 6 petak, Level 4 = 9 petak.
/// Pasang di objek GameManager, lalu isi daftar Plots sesuai urutan petak yang dibuka.
/// </summary>
public class GardenManager : MonoBehaviour
{
    public static GardenManager Instance { get; private set; }

    [Tooltip("Semua petak kebun, urut dari yang pertama terbuka")]
    public GardenPlot[] plots;

    [Tooltip("Jumlah petak terbuka di setiap level (index 0 = level 1)")]
    public int[] plotsPerLevel = { 1, 3, 6, 9 };

    [Tooltip("Biaya upgrade KE level tersebut (index 0 = level 1, selalu 0)")]
    public int[] upgradeCost = { 0, 50000, 120000, 250000 };

    public int Level { get; private set; } = 1;
    public int MaxLevel => plotsPerLevel.Length;
    public bool IsMaxLevel => Level >= MaxLevel;

    public int UnlockedPlots => Mathf.Min(plotsPerLevel[Mathf.Clamp(Level - 1, 0, MaxLevel - 1)], plots.Length);
    public int NextUpgradeCost => IsMaxLevel ? 0 : upgradeCost[Mathf.Clamp(Level, 0, upgradeCost.Length - 1)];
    public int NextLevelPlots => IsMaxLevel ? UnlockedPlots : plotsPerLevel[Level];

    public event Action<int> OnLevelChanged;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (plots == null || plots.Length == 0)
            Debug.LogWarning("GardenManager: daftar Plots masih kosong.");
        else if (plots.Length < plotsPerLevel[MaxLevel - 1])
            Debug.LogWarning($"GardenManager: level maksimal butuh {plotsPerLevel[MaxLevel - 1]} petak, " +
                             $"baru ada {plots.Length}. Petak tambahan tidak akan terbuka.");

        ApplyLocks();
    }

    /// <summary>Membeli upgrade ke level berikutnya. Mengembalikan pesan hasil.</summary>
    public bool TryUpgrade(out string message)
    {
        if (IsMaxLevel) { message = "Kebun sudah level maksimal"; return false; }

        int cost = NextUpgradeCost;
        var eco = EconomyManager.Instance;
        if (eco == null || !eco.TrySpend(cost))
        {
            message = $"Uang tidak cukup untuk upgrade ({EconomyManager.Rupiah(cost)})";
            return false;
        }

        Level++;
        ApplyLocks();
        OnLevelChanged?.Invoke(Level);
        message = $"Kebun naik ke level {Level}! Sekarang ada {UnlockedPlots} petak";
        return true;
    }

    private void ApplyLocks()
    {
        if (plots == null) return;
        int open = UnlockedPlots;
        for (int i = 0; i < plots.Length; i++)
            if (plots[i] != null) plots[i].SetLocked(i >= open);
    }
}
