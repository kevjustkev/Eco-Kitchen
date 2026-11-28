using System;
using UnityEngine;

/// <summary>Statistik satu hari.</summary>
[Serializable]
public class DayStats
{
    public int served, perfect, good, bad, failed, income;
}

/// <summary>
/// Mengatur hari: lama hari, kedatangan pelanggan (satu per satu), dan akhir hari.
/// Pasang di objek GameManager.
/// </summary>
public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    [Header("Waktu")]
    [Tooltip("Lama satu hari (detik)")]
    public float dayLengthSeconds = 180f;
    [Tooltip("Jeda sebelum pelanggan berikutnya datang (detik, acak antara X dan Y)")]
    public Vector2 spawnDelayRange = new Vector2(3f, 6f);
    [Tooltip("Pelanggan baru tidak datang jika sisa waktu kurang dari ini (detik)")]
    public float lastCallSeconds = 15f;

    [Header("Fase Persiapan (Revisi 1)")]
    [Tooltip("Jika dicentang, pelanggan baru datang setelah pemain membuka restoran lewat ShopSign. " +
             "Waktu hari berhenti selama persiapan, jadi pemain sempat panen, kompos, potong, dan racik dulu.")]
    public bool requirePrepPhase = true;

    [Header("Pelanggan")]
    public Customer customerPrefab;
    public Transform spawnPoint;
    public Transform counterPoint;
    public Transform exitPoint;

    [Header("Pesanan")]
    public OrderData[] orders;

    [Header("Mega Order (kondisi menang)")]
    [Tooltip("Salad-salad yang dipesan pelanggan VIP, disajikan satu per satu")]
    public OrderData[] megaOrderSalads;
    [Tooltip("Opsional: prefab khusus pelanggan VIP. Kosong = pakai Customer Prefab biasa")]
    public Customer vipPrefab;
    [Tooltip("Total waktu tunggu VIP untuk semua salad (detik)")]
    public float megaPatienceSeconds = 240f;
    [Tooltip("Bonus uang jika Mega Order berhasil")]
    public int megaBonus = 100000;

    [Header("Syarat munculnya Mega Order")]
    [Min(1)] public int megaMinDay = 3;
    [Range(0, 100)] public int megaMinReputation = 70;
    [Min(1)] public int megaMinGardenLevel = 3;

    public int CurrentDay { get; private set; }
    public float TimeLeft { get; private set; }
    public bool IsDayRunning { get; private set; }
    public DayStats Today { get; private set; } = new DayStats();
    public DayStats Total { get; private set; } = new DayStats();
    public bool IsGameOver { get; private set; }
    public bool MegaOrderToday { get; private set; }
    /// <summary>True jika restoran sudah dibuka hari ini (pelanggan boleh datang).</summary>
    public bool IsShopOpen { get; private set; }

    public event Action OnShopOpened;

    public event Action<int> OnDayStarted;
    public event Action<int, DayStats> OnDayEnded;
    /// <summary>true = menang (Mega Order sukses), false = kalah (bangkrut).</summary>
    public event Action<bool, DayStats> OnGameEnded;

    private bool megaSpawned;

    private Customer currentCustomer;
    private float nextSpawnTime;
    private bool setupValid;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        setupValid = ValidateSetup();

        // Pengaman: tanpa ShopSign di scene, fase persiapan tidak bisa diakhiri
        if (requirePrepPhase && FindFirstObjectByType<ShopSign>() == null)
        {
            Debug.LogWarning("DayManager: tidak ada ShopSign di scene, Require Prep Phase dimatikan.");
            requirePrepPhase = false;
        }

        StartDay(1);
    }

    void Update()
    {
        if (IsGameOver || !IsDayRunning || !setupValid) return;
        if (!IsShopOpen) return; // masih fase persiapan, waktu dan pelanggan belum jalan

        if (TimeLeft > 0f)
        {
            TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);

            if (currentCustomer == null && Time.time >= nextSpawnTime && TimeLeft > lastCallSeconds)
                SpawnCustomer();
        }
        else if (currentCustomer == null)
        {
            EndDay();
        }
    }

    // ------------------------------------------------------------------ hari

    private void StartDay(int day)
    {
        CurrentDay = day;
        TimeLeft = dayLengthSeconds;
        Today = new DayStats();
        IsDayRunning = true;
        nextSpawnTime = Time.time + 2f;

        MegaOrderToday = MegaConditionsMet();
        megaSpawned = false;
        IsShopOpen = !requirePrepPhase;

        if (NotificationUI.Instance != null)
        {
            string prep = requirePrepPhase ? " Siapkan bahan dulu, lalu buka restoran di papan." : "";
            NotificationUI.Instance.Show(MegaOrderToday
                ? $"Hari {day} dimulai! Pelanggan VIP akan datang dengan MEGA ORDER!{prep}"
                : $"Hari {day} dimulai!{prep}");
        }
        OnDayStarted?.Invoke(day);
    }

    /// <summary>Dipanggil ShopSign saat pemain membuka restoran. Waktu hari dan pelanggan mulai berjalan.</summary>
    public void OpenShop()
    {
        if (IsShopOpen || IsGameOver || !IsDayRunning) return;

        IsShopOpen = true;
        nextSpawnTime = Time.time + 2f;
        if (NotificationUI.Instance != null) NotificationUI.Instance.Show("Restoran dibuka! Pelanggan mulai berdatangan.");
        OnShopOpened?.Invoke();
    }

    private void EndDay()
    {
        IsDayRunning = false;

        // Kondisi kalah: bangkrut (uang <= 0 di akhir hari)
        if (EconomyManager.Instance != null && EconomyManager.Instance.Money <= 0)
        {
            IsGameOver = true;
            Debug.Log("BANGKRUT: uang habis di akhir hari.");
            OnGameEnded?.Invoke(false, Total);
            return;
        }

        Debug.Log($"Hari {CurrentDay} selesai. Pendapatan: {EconomyManager.Rupiah(Today.income)}");
        OnDayEnded?.Invoke(CurrentDay, Today);
    }

    /// <summary>Dipanggil tombol "Mulai Hari Berikutnya" di layar ringkasan.</summary>
    public void StartNextDay()
    {
        StartDay(CurrentDay + 1);
    }

    // ------------------------------------------------------------------ pelanggan

    private void SpawnCustomer()
    {
        // Pelanggan pertama di hari Mega Order adalah VIP
        if (MegaOrderToday && !megaSpawned)
        {
            var list = new System.Collections.Generic.List<OrderData>();
            foreach (var o in megaOrderSalads) if (o != null) list.Add(o);
            if (list.Count > 0)
            {
                Customer prefab = vipPrefab != null ? vipPrefab : customerPrefab;
                currentCustomer = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
                currentCustomer.SetupVip(list, megaPatienceSeconds, counterPoint.position, exitPoint.position);
                megaSpawned = true;
                return;
            }
        }

        OrderData order = PickOrder();
        if (order == null) return;

        currentCustomer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
        currentCustomer.Setup(order, counterPoint.position, exitPoint.position);
    }

    private OrderData PickOrder()
    {
        var available = new System.Collections.Generic.List<OrderData>();
        foreach (var o in orders)
            if (o != null && o.unlockDay <= CurrentDay) available.Add(o);

        if (available.Count == 0)
            foreach (var o in orders) if (o != null) available.Add(o);

        return available.Count == 0 ? null : available[UnityEngine.Random.Range(0, available.Count)];
    }

    /// <summary>Dipanggil Customer saat pesanan selesai dinilai atau pelanggan pergi marah.</summary>
    public void ReportResult(OrderResult result)
    {
        switch (result.grade)
        {
            case OrderGrade.Perfect: Today.perfect++; Today.served++; break;
            case OrderGrade.Good: Today.good++; Today.served++; break;
            case OrderGrade.Bad: Today.bad++; Today.served++; break;
            case OrderGrade.Failed: Today.failed++; break;
        }
        Today.income += result.payment;

        switch (result.grade)
        {
            case OrderGrade.Perfect: Total.perfect++; Total.served++; break;
            case OrderGrade.Good: Total.good++; Total.served++; break;
            case OrderGrade.Bad: Total.bad++; Total.served++; break;
            case OrderGrade.Failed: Total.failed++; break;
        }
        Total.income += result.payment;
    }

    // ------------------------------------------------------------------ Mega Order

    /// <summary>Dipanggil pelanggan VIP saat Mega Order berhasil atau gagal.</summary>
    public void ReportMegaResult(bool success)
    {
        if (!success)
        {
            MegaOrderToday = false;
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.Show("Mega Order gagal. VIP akan kembali di hari berikutnya.");
            return;
        }

        if (EconomyManager.Instance != null) EconomyManager.Instance.AddMoney(megaBonus);
        Total.income += megaBonus;

        IsGameOver = true;
        IsDayRunning = false;
        Debug.Log("MENANG: Mega Order berhasil!");
        OnGameEnded?.Invoke(true, Total);
    }

    public bool MegaConditionsMet()
    {
        if (megaOrderSalads == null || megaOrderSalads.Length == 0) return false;
        int rep = EconomyManager.Instance != null ? EconomyManager.Instance.Reputation : 0;
        int garden = GardenManager.Instance != null ? GardenManager.Instance.Level : megaMinGardenLevel;
        return CurrentDay >= megaMinDay && rep >= megaMinReputation && garden >= megaMinGardenLevel;
    }

    /// <summary>Teks progres syarat Mega Order untuk HUD.</summary>
    public string GetMegaProgressText()
    {
        if (megaOrderSalads == null || megaOrderSalads.Length == 0) return "";
        if (MegaOrderToday) return "<color=#F4D03F>MEGA ORDER hari ini!</color>";

        int rep = EconomyManager.Instance != null ? EconomyManager.Instance.Reputation : 0;
        int garden = GardenManager.Instance != null ? GardenManager.Instance.Level : megaMinGardenLevel;

        string Mark(bool ok, string text) => ok ? $"<color=#58D68D>{text}</color>" : text;
        return "Target Mega Order: " +
               Mark(CurrentDay >= megaMinDay, $"Hari {CurrentDay}/{megaMinDay}") + "  " +
               Mark(rep >= megaMinReputation, $"Rep {rep}/{megaMinReputation}") + "  " +
               Mark(garden >= megaMinGardenLevel, $"Kebun Lv {garden}/{megaMinGardenLevel}");
    }

    /// <summary>Dipanggil Customer saat sudah keluar dari restoran.</summary>
    public void CustomerGone(Customer customer)
    {
        if (customer == currentCustomer) currentCustomer = null;
        nextSpawnTime = Time.time + UnityEngine.Random.Range(spawnDelayRange.x, spawnDelayRange.y);
    }

    private bool ValidateSetup()
    {
        bool ok = true;
        if (customerPrefab == null) { Debug.LogError("DayManager: Customer Prefab belum diisi."); ok = false; }
        if (spawnPoint == null || counterPoint == null || exitPoint == null)
        { Debug.LogError("DayManager: Spawn Point / Counter Point / Exit Point belum diisi."); ok = false; }
        if (orders == null || orders.Length == 0) { Debug.LogError("DayManager: daftar Orders masih kosong."); ok = false; }
        return ok;
    }
}
