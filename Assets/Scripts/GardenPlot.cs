using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Satu petak kebun. Alur sesuai GDD (semua dengan tombol E):
///   Kosong -> tanam benih -> beri kompos -> siram -> tumbuh (detik) -> panen.
/// Tombol Q: cabut tanaman (benih hilang, menjadi sisa organik jika Waste Item diisi).
/// Pasang di setiap objek petak kebun (wajib punya Collider).
/// </summary>
public class GardenPlot : Interactable
{
    private enum State { Kosong, ButuhKompos, ButuhAir, Tumbuh, SiapPanen }

    [Header("Item")]
    [Tooltip("Asset Kompos")]
    public IngredientData compostItem;
    [Tooltip("Opsional: asset Sisa Organik, didapat saat tanaman dicabut")]
    public IngredientData wasteItem;

    [Header("Aturan")]
    [Tooltip("Tanaman wajib diberi kompos sebelum disiram")]
    public bool requireCompost = true;
    [Tooltip("Tanaman wajib disiram sebelum mulai tumbuh")]
    public bool requireWater = true;

    [Header("Tampilan")]
    public float statusHeight = 1.6f;
    [Tooltip("Tinggi maksimal tanaman saat siap panen")]
    public float plantMaxSize = 0.8f;

    [Header("Kunci (upgrade kebun)")]
    [Tooltip("Warna petak saat masih terkunci")]
    public Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    public bool IsLocked { get; private set; }
    public bool IsEmpty => state == State.Kosong;

    private Renderer plotRenderer;
    private Color originalColor;
    private bool colorCached;

    private State state = State.Kosong;
    private IngredientData seed;
    private float growTimer;

    private GameObject plantVisual;
    private Renderer plantRenderer;
    private GameObject statusBoard;
    private TextMeshProUGUI statusText;
    private RectTransform barFill;
    private GameObject barRoot;

    void Start()
    {
        if (requireCompost && compostItem == null)
            Debug.LogError($"{name}: Compost Item belum diisi (atau matikan Require Compost).");

        CacheRenderer();
        BuildPlantVisual();
        BuildStatusBoard();
        ApplyLockVisual();
        Refresh();
    }

    /// <summary>Dipanggil GardenManager untuk mengunci / membuka petak.</summary>
    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        CacheRenderer();
        ApplyLockVisual();
        Refresh();
    }

    private void CacheRenderer()
    {
        if (colorCached) return;
        plotRenderer = GetComponent<Renderer>();
        if (plotRenderer != null)
        {
            originalColor = plotRenderer.material.color;
            colorCached = true;
        }
    }

    private void ApplyLockVisual()
    {
        if (plotRenderer != null && colorCached)
            plotRenderer.material.color = IsLocked ? lockedColor : originalColor;
    }

    void Update()
    {
        if (state == State.Tumbuh)
        {
            growTimer += Time.deltaTime;
            if (growTimer >= seed.growSeconds)
            {
                state = State.SiapPanen;
                ShowMessage($"{seed.harvestResult.displayName} siap dipanen!");
            }
        }
        Refresh();
    }

    void LateUpdate()
    {
        if (statusBoard != null && Camera.main != null)
            statusBoard.transform.rotation = Camera.main.transform.rotation;
    }

    // ------------------------------------------------------------------ interaksi

    public override string GetPromptText()
    {
        if (IsLocked) return "Petak terkunci. Upgrade kebun di Market";

        switch (state)
        {
            case State.Kosong:
                IngredientData next = FindSeed();
                return next != null
                    ? $"Tekan E untuk menanam {next.displayName}"
                    : $"{displayName}: butuh benih untuk ditanam";
            case State.ButuhKompos:
                return HasCompost()
                    ? $"Tekan E untuk memberi {compostItem.displayName}"
                    : $"Tanaman butuh {Name(compostItem)}. Buat di Compost Bin";
            case State.ButuhAir:
                return "Tekan E untuk menyiram tanaman";
            case State.Tumbuh:
                return $"{seed.harvestResult.displayName} sedang tumbuh... {Percent()}%";
            default:
                return $"Tekan E untuk memanen {seed.harvestAmount} {seed.harvestResult.displayName}";
        }
    }

    public override string GetSecondaryPromptText()
    {
        if (IsLocked) return null;
        return state != State.Kosong && state != State.SiapPanen ? "Tekan Q untuk mencabut tanaman" : null;
    }

    public override void Interact()
    {
        if (IsLocked)
        {
            ShowMessage("Petak ini masih terkunci. Upgrade kebun di Market");
            return;
        }

        var inv = InventoryManager.Instance;
        if (inv == null) return;

        switch (state)
        {
            case State.Kosong:
                IngredientData s = FindSeed();
                if (s == null) { ShowMessage("Kamu tidak punya benih"); return; }
                inv.RemoveItem(s);
                seed = s;
                growTimer = 0f;
                state = requireCompost ? State.ButuhKompos : (requireWater ? State.ButuhAir : State.Tumbuh);
                ShowMessage($"Menanam {s.displayName}");
                break;

            case State.ButuhKompos:
                if (!HasCompost()) { ShowMessage($"Kamu tidak punya {Name(compostItem)}"); return; }
                inv.RemoveItem(compostItem);
                state = requireWater ? State.ButuhAir : State.Tumbuh;
                ShowMessage("Tanaman diberi kompos");
                break;

            case State.ButuhAir:
                state = State.Tumbuh;
                growTimer = 0f;
                ShowMessage("Tanaman disiram, mulai tumbuh");
                break;

            case State.Tumbuh:
                ShowMessage($"Tunggu sebentar, tanaman baru {Percent()}%");
                break;

            case State.SiapPanen:
                inv.AddItem(seed.harvestResult, seed.harvestAmount);
                ShowMessage($"Memanen {seed.harvestAmount} {seed.harvestResult.displayName}");
                seed = null;
                state = State.Kosong;
                break;
        }
        Refresh();
    }

    public override void InteractSecondary()
    {
        if (IsLocked) return;
        if (state == State.Kosong || state == State.SiapPanen) return;

        if (wasteItem != null && InventoryManager.Instance != null)
            InventoryManager.Instance.AddItem(wasteItem);

        ShowMessage("Tanaman dicabut");
        seed = null;
        state = State.Kosong;
        Refresh();
    }

    // ------------------------------------------------------------------ bantuan

    private IngredientData FindSeed()
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return null;

        IngredientData selected = inv.SelectedItem;
        if (selected != null && selected.IsSeed) return selected;

        foreach (var item in inv.GetAllItems())
            if (item != null && item.IsSeed) return item;
        return null;
    }

    private bool HasCompost() =>
        compostItem != null && InventoryManager.Instance != null && InventoryManager.Instance.HasItem(compostItem);

    private int Percent() =>
        seed == null ? 0 : Mathf.Clamp(Mathf.FloorToInt(growTimer / seed.growSeconds * 100f), 0, 100);

    private static string Name(IngredientData item) => item != null ? item.displayName : "?";

    // ------------------------------------------------------------------ tampilan

    private void BuildPlantVisual()
    {
        plantVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        plantVisual.name = "PlantVisual";
        Destroy(plantVisual.GetComponent<Collider>()); // deteksi tetap memakai collider petak
        plantVisual.transform.SetParent(transform, true);
        plantRenderer = plantVisual.GetComponent<Renderer>();
        plantVisual.SetActive(false);
    }

    private void UpdatePlantVisual()
    {
        bool show = state != State.Kosong && seed != null;
        plantVisual.SetActive(show);
        if (!show) return;

        // Ukuran tanaman mengikuti progres tumbuh
        float t;
        switch (state)
        {
            case State.Tumbuh: t = Mathf.Lerp(0.2f, 1f, growTimer / seed.growSeconds); break;
            case State.SiapPanen: t = 1f; break;
            default: t = 0.2f; break; // baru ditanam
        }
        float size = plantMaxSize * t;

        // Posisi di atas permukaan petak, ukuran dalam satuan dunia (tidak ikut skala petak)
        Collider col = GetComponent<Collider>();
        float topY = col != null ? col.bounds.max.y : transform.position.y;
        Vector3 center = col != null ? col.bounds.center : transform.position;
        plantVisual.transform.position = new Vector3(center.x, topY + size * 0.5f, center.z);

        Vector3 ls = transform.lossyScale;
        plantVisual.transform.localScale = new Vector3(size / Mathf.Max(0.001f, ls.x),
                                                       size / Mathf.Max(0.001f, ls.y),
                                                       size / Mathf.Max(0.001f, ls.z));

        Color target = seed.harvestResult != null ? seed.harvestResult.color : seed.color;
        Color c = state == State.SiapPanen ? target : Color.Lerp(new Color(0.3f, 0.6f, 0.25f), target, t * 0.6f);
        if (plantRenderer != null) plantRenderer.material.color = c;
    }

    private void BuildStatusBoard()
    {
        statusBoard = new GameObject("PlotStatus", typeof(RectTransform), typeof(Canvas));
        statusBoard.transform.SetParent(transform, false);

        Vector3 s = transform.lossyScale;
        statusBoard.transform.localPosition = new Vector3(0f, statusHeight / Mathf.Max(0.001f, s.y), 0f);
        statusBoard.transform.localScale = new Vector3(0.01f / Mathf.Max(0.001f, s.x),
                                                       0.01f / Mathf.Max(0.001f, s.y),
                                                       0.01f / Mathf.Max(0.001f, s.z));

        var canvas = statusBoard.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)statusBoard.transform).sizeDelta = new Vector2(300, 110);

        Image bg = UIBuilder.CreateImage("Bg", statusBoard.transform, new Color(0.12f, 0.22f, 0.10f, 0.88f));
        UIBuilder.Stretch(bg.rectTransform);

        statusText = UIBuilder.CreateText("Text", statusBoard.transform, "", 28, TextAlignmentOptions.Center);
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 14;
        statusText.fontSizeMax = 28;
        UIBuilder.Stretch(statusText.rectTransform);
        statusText.rectTransform.offsetMin = new Vector2(10, 32);
        statusText.rectTransform.offsetMax = new Vector2(-10, -6);

        Image barBg = UIBuilder.CreateImage("Bar", statusBoard.transform, new Color(0.1f, 0.1f, 0.1f, 1f));
        RectTransform brt = barBg.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.offsetMin = new Vector2(14, 10);
        brt.offsetMax = new Vector2(-14, 28);
        barRoot = barBg.gameObject;

        Image fill = UIBuilder.CreateImage("Fill", barBg.transform, new Color(0.35f, 0.75f, 0.3f, 1f));
        barFill = fill.rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;
    }

    private void Refresh()
    {
        if (statusText == null) return;

        // Petak terkunci: papan status disembunyikan (cukup warna abu-abu)
        if (statusBoard != null) statusBoard.SetActive(!IsLocked);
        if (IsLocked)
        {
            UpdatePlantVisual();
            return;
        }

        switch (state)
        {
            case State.Kosong: statusText.text = "Petak kosong"; break;
            case State.ButuhKompos: statusText.text = $"{seed.displayName}\n<size=75%>Butuh kompos</size>"; break;
            case State.ButuhAir: statusText.text = $"{seed.displayName}\n<size=75%>Butuh disiram</size>"; break;
            case State.Tumbuh: statusText.text = $"{seed.harvestResult.displayName}\n<size=75%>Tumbuh {Percent()}%</size>"; break;
            case State.SiapPanen: statusText.text = $"<color=#A9DFBF><b>{seed.harvestResult.displayName}\nSiap panen!</b></color>"; break;
        }

        barRoot.SetActive(state == State.Tumbuh);
        if (state == State.Tumbuh)
            barFill.anchorMax = new Vector2(Mathf.Clamp01(growTimer / seed.growSeconds), 1f);

        UpdatePlantVisual();
    }
}
