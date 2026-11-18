using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Compost Bin sekaligus tempat sampah organik.
/// Tekan E:
///   1. Jika kompos sudah jadi  -> ambil kompos ke inventory.
///   2. Jika membawa salad      -> salad dibuang, isinya jadi sisa organik di dalam bin.
///   3. Jika punya Sisa Organik -> semua dimasukkan ke bin.
/// Setiap isi bin mencapai "Waste Per Batch", proses pengomposan berjalan otomatis
/// selama "Process Seconds", lalu menghasilkan kompos.
/// </summary>
public class CompostBin : Interactable
{
    [Header("Item")]
    [Tooltip("Asset Sisa Organik")]
    public IngredientData wasteItem;
    [Tooltip("Asset Kompos (hasil proses)")]
    public IngredientData compostItem;

    [Header("Proses")]
    [Tooltip("Jumlah sisa organik untuk satu kali proses")]
    [Min(1)] public int wastePerBatch = 3;
    [Tooltip("Jumlah kompos yang dihasilkan per proses")]
    [Min(1)] public int compostPerBatch = 1;
    [Tooltip("Lama satu kali proses (detik)")]
    [Min(1f)] public float processSeconds = 45f;
    [Tooltip("Maksimal sisa organik yang bisa ditampung bin (di luar yang sedang diproses)")]
    [Min(1)] public int capacity = 12;

    [Header("Tampilan")]
    [Tooltip("Tinggi papan status di atas bin")]
    public float statusHeight = 1.8f;

    private int storedWaste;      // sisa organik yang menunggu diproses
    private bool processing;
    private float timer;
    private int readyCompost;     // kompos yang siap diambil

    private GameObject statusBoard;
    private TextMeshProUGUI statusText;
    private RectTransform barFill;
    private GameObject barRoot;

    public int StoredWaste => storedWaste;
    public int ReadyCompost => readyCompost;
    public bool IsProcessing => processing;

    void Start()
    {
        if (wasteItem == null) Debug.LogError($"{name}: Waste Item (Sisa Organik) belum diisi.");
        if (compostItem == null) Debug.LogError($"{name}: Compost Item (Kompos) belum diisi.");
        BuildStatusBoard();
        UpdateStatusBoard();
    }

    void Update()
    {
        if (wasteItem == null || compostItem == null) return;

        if (!processing && storedWaste >= wastePerBatch)
        {
            storedWaste -= wastePerBatch;
            processing = true;
            timer = 0f;
        }

        if (processing)
        {
            timer += Time.deltaTime;
            if (timer >= processSeconds)
            {
                processing = false;
                readyCompost += compostPerBatch;
                ShowMessage($"{compostPerBatch} {compostItem.displayName} sudah jadi!");
            }
        }

        UpdateStatusBoard();
    }

    void LateUpdate()
    {
        if (statusBoard != null && Camera.main != null)
            statusBoard.transform.rotation = Camera.main.transform.rotation;
    }

    // ------------------------------------------------------------------ interaksi

    public override string GetPromptText()
    {
        if (readyCompost > 0)
            return $"Tekan E untuk mengambil {readyCompost} {Name(compostItem)}";

        var carry = PlayerCarry.Instance;
        if (carry != null && carry.IsCarrying)
            return "Tekan E untuk membuang salad ke compost bin";

        int have = InventoryManager.Instance != null ? InventoryManager.Instance.GetCount(wasteItem) : 0;
        if (have > 0)
            return $"Tekan E untuk memasukkan {have} {Name(wasteItem)}";

        return $"{displayName}: butuh {Name(wasteItem)} untuk membuat kompos";
    }

    public override void Interact()
    {
        var inv = InventoryManager.Instance;
        if (inv == null || wasteItem == null || compostItem == null) return;

        // 1. Ambil kompos yang sudah jadi
        if (readyCompost > 0)
        {
            inv.AddItem(compostItem, readyCompost);
            ShowMessage($"Mengambil {readyCompost} {compostItem.displayName}");
            readyCompost = 0;
            return;
        }

        // 2. Buang salad yang dibawa
        var carry = PlayerCarry.Instance;
        if (carry != null && carry.IsCarrying)
        {
            SaladBowl salad = carry.Drop();
            int amount = salad.ingredients.Count + (salad.dressing != null ? 1 : 0);
            int stored = StoreWaste(amount);
            int overflow = amount - stored;
            if (overflow > 0) inv.AddItem(wasteItem, overflow); // bin penuh: sisanya ke inventory

            ShowMessage(overflow > 0
                ? $"Salad dibuang. Bin penuh, {overflow} {wasteItem.displayName} masuk inventory"
                : $"Salad dibuang menjadi {amount} {wasteItem.displayName}");
            return;
        }

        // 3. Masukkan sisa organik dari inventory
        int have = inv.GetCount(wasteItem);
        if (have <= 0)
        {
            ShowMessage($"Kamu tidak punya {wasteItem.displayName}");
            return;
        }

        int space = capacity - storedWaste;
        if (space <= 0)
        {
            ShowMessage("Compost bin penuh, tunggu proses selesai");
            return;
        }

        int put = Mathf.Min(have, space);
        inv.RemoveItem(wasteItem, put);
        StoreWaste(put);
        ShowMessage($"Memasukkan {put} {wasteItem.displayName} ke compost bin");
    }

    /// <summary>Menambah sisa organik ke bin. Mengembalikan jumlah yang benar-benar masuk.</summary>
    private int StoreWaste(int amount)
    {
        int put = Mathf.Clamp(amount, 0, capacity - storedWaste);
        storedWaste += put;
        return put;
    }

    private static string Name(IngredientData item) => item != null ? item.displayName : "?";

    // ------------------------------------------------------------------ papan status (dibuat lewat kode)

    private void BuildStatusBoard()
    {
        statusBoard = new GameObject("CompostStatus", typeof(RectTransform), typeof(Canvas));
        statusBoard.transform.SetParent(transform, false);

        Vector3 s = transform.lossyScale;
        statusBoard.transform.localPosition = new Vector3(0f, statusHeight / Mathf.Max(0.001f, s.y), 0f);
        statusBoard.transform.localScale = new Vector3(0.01f / Mathf.Max(0.001f, s.x),
                                                       0.01f / Mathf.Max(0.001f, s.y),
                                                       0.01f / Mathf.Max(0.001f, s.z));

        var canvas = statusBoard.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)statusBoard.transform).sizeDelta = new Vector2(320, 130);

        Image bg = UIBuilder.CreateImage("Bg", statusBoard.transform, new Color(0.15f, 0.25f, 0.12f, 0.9f));
        UIBuilder.Stretch(bg.rectTransform);

        statusText = UIBuilder.CreateText("Text", statusBoard.transform, "", 30, TextAlignmentOptions.Center);
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 14;
        statusText.fontSizeMax = 30;
        UIBuilder.Stretch(statusText.rectTransform);
        statusText.rectTransform.offsetMin = new Vector2(10, 36);
        statusText.rectTransform.offsetMax = new Vector2(-10, -6);

        Image barBg = UIBuilder.CreateImage("Bar", statusBoard.transform, new Color(0.1f, 0.1f, 0.1f, 1f));
        RectTransform brt = barBg.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.offsetMin = new Vector2(14, 10);
        brt.offsetMax = new Vector2(-14, 30);
        barRoot = barBg.gameObject;

        Image fill = UIBuilder.CreateImage("Fill", barBg.transform, new Color(0.55f, 0.4f, 0.2f, 1f));
        barFill = fill.rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;
    }

    private void UpdateStatusBoard()
    {
        if (statusText == null) return;

        string line1;
        if (readyCompost > 0)
            line1 = $"<color=#A9DFBF><b>Kompos siap: {readyCompost}</b></color>";
        else if (processing)
            line1 = $"Memproses... {Mathf.FloorToInt(timer / processSeconds * 100f)}%";
        else
            line1 = "Compost Bin";

        statusText.text = $"{line1}\n<size=75%>Sisa organik: {storedWaste}  (butuh {wastePerBatch} per kompos)</size>";

        barRoot.SetActive(processing);
        if (processing)
            barFill.anchorMax = new Vector2(Mathf.Clamp01(timer / processSeconds), 1f);
    }
}
