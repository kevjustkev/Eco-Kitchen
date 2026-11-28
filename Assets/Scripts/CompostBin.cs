using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [Tooltip("Jumlah kompos dasar yang dihasilkan per proses")]
    [Min(1)] public int compostPerBatch = 1;
    [Tooltip("Lama proses di luar waktu menunggu diaduk (detik)")]
    [Min(1f)] public float processSeconds = 45f;
    [Tooltip("Maksimal sisa organik yang bisa ditampung bin (di luar yang sedang diproses)")]
    [Min(1)] public int capacity = 12;

    [Header("Aduk (interaktif)")]
    [Tooltip("Setiap berapa detik proses, kompos perlu diaduk")]
    [Min(3f)] public float stirIntervalSeconds = 12f;
    [Tooltip("Berapa putaran lingkaran untuk satu kali mengaduk")]
    [Min(1)] public int stirPressesRequired = 3;
    [Tooltip("Batas waktu mengaduk. Lewat dari ini dihitung terlambat dan kualitas turun")]
    [Min(1f)] public float stirGraceSeconds = 8f;
    [Tooltip("Tambahan kompos jika tidak pernah terlambat mengaduk")]
    [Min(0)] public int perfectBonus = 1;

    [Header("Tampilan")]
    [Tooltip("Tinggi papan status di atas bin")]
    public float statusHeight = 1.8f;

    private int storedWaste;
    private bool processing;
    private float timer;
    private int readyCompost;

    private float sinceStir;
    private bool needsStir;
    private float stirWait;
    private int stirPresses;
    private bool lateCounted;
    private int missedStirs;

    private CompostBinVisualAnimator visualAnimator;
    private CompostStirMinigame3D stirMinigame3D;

    private GameObject statusBoard;
    private TextMeshProUGUI statusText;
    private RectTransform barFill;
    private GameObject barRoot;

    public int StoredWaste => storedWaste;
    public int ReadyCompost => readyCompost;
    public bool IsProcessing => processing;
    public bool NeedsStir => processing && needsStir;
    public int StirPresses => stirPresses;
    public int StirPressesRequired => stirPressesRequired;

    void Start()
    {
        visualAnimator = GetComponent<CompostBinVisualAnimator>();
        stirMinigame3D = GetComponent<CompostStirMinigame3D>();
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
            sinceStir = 0f;
            needsStir = true;
            stirWait = 0f;
            stirPresses = 0;
            lateCounted = false;
            missedStirs = 0;
            ShowMessage("Bahan kompos masuk. Tutup terbuka! Tekan E untuk mengaduk kompos.");
        }

        if (processing)
        {
            if (needsStir)
            {
                // Jangan kurangi waktu grace selama pemain sedang berada di dalam minigame 3D
                if (!CompostStirMinigame3D.IsActive)
                {
                    stirWait += Time.deltaTime;
                    if (!lateCounted && stirWait > stirGraceSeconds)
                    {
                        lateCounted = true;
                        missedStirs++;
                        ShowMessage("Kompos terlambat diaduk, kualitasnya turun!");
                    }
                }
            }
            else
            {
                timer += Time.deltaTime;
                sinceStir += Time.deltaTime;

                if (timer >= processSeconds)
                {
                    FinishBatch();
                }
                else if (sinceStir >= stirIntervalSeconds)
                {
                    needsStir = true;
                    stirWait = 0f;
                    stirPresses = 0;
                    lateCounted = false;
                    ShowMessage("Kompos perlu diaduk! Dekati bin dan tekan E.");
                }
            }
        }

        UpdateStatusBoard();
    }

    void LateUpdate()
    {
        if (statusBoard != null && Camera.main != null)
            statusBoard.transform.rotation = Camera.main.transform.rotation;
    }

    private void FinishBatch()
    {
        processing = false;
        needsStir = false;

        int yield = compostPerBatch
                    + (missedStirs == 0 ? perfectBonus : 0)
                    - Mathf.Max(0, missedStirs - 1);
        yield = Mathf.Max(1, yield);
        readyCompost += yield;

        string quality = missedStirs == 0 ? "kualitas bagus" : (missedStirs == 1 ? "kualitas cukup" : "kualitas rendah");
        ShowMessage($"{yield} {compostItem.displayName} sudah jadi ({quality})!");
    }

    public override string GetPromptText()
    {
        if (NeedsStir)
        {
            if (stirMinigame3D != null)
                return $"Tekan E untuk mengaduk kompos ({stirPresses}/{stirPressesRequired} putaran)";
            return $"Tahan klik kiri dan putar mouse melingkar ({stirPresses}/{stirPressesRequired} putaran)";
        }

        if (readyCompost > 0)
            return $"Tekan E untuk mengambil {readyCompost} {Name(compostItem)}";

        var carry = PlayerCarry.Instance;
        if (carry != null && carry.IsCarrying)
            return "Tekan E untuk membuang salad ke compost bin";

        int have = InventoryManager.Instance != null ? InventoryManager.Instance.GetCount(wasteItem) : 0;
        if (have > 0)
            return $"Tekan E untuk memasukkan {have} {Name(wasteItem)}";

        if (processing)
            return $"Kompos sedang diproses... {Mathf.FloorToInt(timer / processSeconds * 100f)}%";

        return $"{displayName}: butuh {Name(wasteItem)} untuk membuat kompos";
    }

    public override void Interact()
    {
        if (NeedsStir)
        {
            if (stirMinigame3D == null)
                stirMinigame3D = GetComponent<CompostStirMinigame3D>();

            if (stirMinigame3D != null)
            {
                if (!CompostStirMinigame3D.IsActive)
                    stirMinigame3D.Begin(this);
                return;
            }

            visualAnimator?.PlayStir();
            RegisterStirProgress();
            return;
        }

        var inv = InventoryManager.Instance;
        if (inv == null || wasteItem == null || compostItem == null) return;

        if (readyCompost > 0)
        {
            inv.AddItem(compostItem, readyCompost);
            ShowMessage($"Mengambil {readyCompost} {compostItem.displayName}");
            readyCompost = 0;
            return;
        }

        var carry = PlayerCarry.Instance;
        if (carry != null && carry.IsCarrying)
        {
            SaladBowl salad = carry.Drop();
            int amount = salad.ingredients.Count + (salad.dressing != null ? 1 : 0);
            int stored = StoreWaste(amount);
            int overflow = amount - stored;
            if (overflow > 0) inv.AddItem(wasteItem, overflow);

            ShowMessage(overflow > 0
                ? $"Salad dibuang. Bin penuh, {overflow} {wasteItem.displayName} masuk inventory"
                : $"Salad dibuang menjadi {amount} {wasteItem.displayName}");
            return;
        }

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

    public void RegisterMouseStir()
    {
        if (!NeedsStir) return;
        RegisterStirProgress();
    }

    private void RegisterStirProgress()
    {
        if (!NeedsStir) return;

        stirPresses++;
        if (stirPresses >= stirPressesRequired)
        {
            needsStir = false;
            sinceStir = 0f;
            stirWait = 0f;
            lateCounted = false;
            ShowMessage("Kompos selesai diaduk, proses lanjut");
        }
        else
        {
            ShowMessage($"Mengaduk... {stirPresses}/{stirPressesRequired}. Lanjutkan putaran!");
        }
    }

    private int StoreWaste(int amount)
    {
        int put = Mathf.Clamp(amount, 0, capacity - storedWaste);
        storedWaste += put;
        return put;
    }

    private static string Name(IngredientData item) => item != null ? item.displayName : "?";

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
        if (statusBoard != null)
            statusBoard.SetActive(!CompostStirMinigame3D.IsActive);

        if (statusText == null) return;

        string line1;
        if (NeedsStir)
        {
            int sisa = Mathf.CeilToInt(stirGraceSeconds - stirWait);
            line1 = sisa > 0
                ? $"<color=#F5B041><b>TEKAN E ADUK! {stirPresses}/{stirPressesRequired}</b></color> ({sisa}s)"
                : $"<color=#EC7063><b>TERLAMBAT DIADUK!</b></color> ({stirPresses}/{stirPressesRequired})";
        }
        else if (readyCompost > 0)
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