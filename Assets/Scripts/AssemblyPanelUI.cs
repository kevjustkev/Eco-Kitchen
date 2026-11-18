using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel racik salad (gaya Good Pizza Great Pizza). Seluruh tampilan DIBUAT OTOMATIS lewat kode,
/// jadi cukup buat satu GameObject kosong di scene lalu pasang komponen ini.
/// Syarat: scene punya EventSystem (sudah ada di scene Eco Kitchen).
/// </summary>
public class AssemblyPanelUI : MonoBehaviour
{
    public static AssemblyPanelUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.station != null;

    [Header("Warna")]
    public Color panelColor = new Color(0.10f, 0.12f, 0.10f, 0.96f);
    public Color ingredientColor = new Color(0.27f, 0.58f, 0.32f, 1f);
    public Color dressingColor = new Color(0.80f, 0.58f, 0.20f, 1f);
    public Color takeColor = new Color(0.20f, 0.45f, 0.75f, 1f);
    public Color clearColor = new Color(0.70f, 0.25f, 0.22f, 1f);
    public Color closeColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    private AssemblyStation station;

    private GameObject root;
    private TMP_Text titleText, bowlText, statusText, emptyIngredientText, emptyDressingText;
    private RectTransform ingredientGrid, dressingGrid;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private bool subscribed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildUI();
        root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (subscribed && InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // ------------------------------------------------------------------ buka / tutup

    public void Open(AssemblyStation s)
    {
        station = s;
        root.SetActive(true);
        UIState.SetUIOpen(true);
        statusText.text = "";

        if (!subscribed && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += Refresh;
            subscribed = true;
        }
        Refresh();
    }

    public void Close()
    {
        station = null;
        root.SetActive(false);
        UIState.SetUIOpen(false);
    }

    // ------------------------------------------------------------------ isi panel

    public void Refresh()
    {
        if (!IsOpen) return;

        foreach (var go in spawnedButtons) Destroy(go);
        spawnedButtons.Clear();

        titleText.text = station.displayName.ToUpper();
        bowlText.text = "Isi mangkuk:\n<b>" + station.Bowl.Describe() + "</b>\n" +
                        $"<size=80%>({station.Bowl.ingredients.Count}/{SaladBowl.MaxIngredients} bahan)</size>";

        bool anyIngredient = false, anyDressing = false;
        var inv = InventoryManager.Instance;

        if (inv != null)
        {
            foreach (IngredientData item in inv.GetAllItems())
            {
                if (item == null) continue;
                IngredientData captured = item;
                string label = $"{item.displayName}\n<size=80%>punya: {inv.GetCount(item)}</size>";

                if (item.isDressing)
                {
                    spawnedButtons.Add(CreateButton(dressingGrid, label, dressingColor, () => OnAdd(captured)));
                    anyDressing = true;
                }
                else if (item.canGoInBowl)
                {
                    spawnedButtons.Add(CreateButton(ingredientGrid, label, ingredientColor, () => OnAdd(captured)));
                    anyIngredient = true;
                }
            }
        }

        emptyIngredientText.gameObject.SetActive(!anyIngredient);
        emptyDressingText.gameObject.SetActive(!anyDressing);
    }

    private void OnAdd(IngredientData item)
    {
        if (!IsOpen) return;
        station.TryAdd(item, out string msg);
        statusText.text = msg;
        Refresh();
    }

    private void OnClear()
    {
        if (!IsOpen) return;
        station.ClearBowl();
        statusText.text = "Mangkuk dikosongkan, bahan kembali ke inventory";
        Refresh();
    }

    private void OnTake()
    {
        if (!IsOpen) return;
        if (station.TryTakeBowl(out string msg))
        {
            if (NotificationUI.Instance != null) NotificationUI.Instance.Show(msg);
            Close();
        }
        else
        {
            statusText.text = msg;
        }
    }

    // ------------------------------------------------------------------ pembuatan UI otomatis

    private void BuildUI()
    {
        // Canvas sendiri, di atas UI lain
        root = new GameObject("AssemblyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Latar gelap
        Image dim = CreateImage("Dim", root.transform, new Color(0, 0, 0, 0.55f));
        Stretch(dim.rectTransform);

        // Panel utama
        Image panel = CreateImage("Panel", root.transform, panelColor);
        RectTransform prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1000, 760);

        var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(40, 40, 30, 30);
        vlg.spacing = 12;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        titleText = CreateText("Title", panel.transform, "MEJA RACIK", 44, FontStyles.Bold, TextAlignmentOptions.Center, 56);
        bowlText = CreateText("Bowl", panel.transform, "", 28, FontStyles.Normal, TextAlignmentOptions.Center, 120);

        CreateText("IngredientLabel", panel.transform, "BAHAN  (klik untuk memasukkan ke mangkuk)", 24, FontStyles.Bold, TextAlignmentOptions.Left, 34);
        emptyIngredientText = CreateText("IngredientEmpty", panel.transform,
            "Belum ada bahan siap pakai. Potong bahan dulu di Meja Potong.", 22, FontStyles.Italic, TextAlignmentOptions.Left, 30);
        ingredientGrid = CreateGrid("IngredientGrid", panel.transform);

        CreateText("DressingLabel", panel.transform, "DRESSING  (maksimal satu)", 24, FontStyles.Bold, TextAlignmentOptions.Left, 34);
        emptyDressingText = CreateText("DressingEmpty", panel.transform,
            "Belum membawa dressing. Ambil dari rak dressing.", 22, FontStyles.Italic, TextAlignmentOptions.Left, 30);
        dressingGrid = CreateGrid("DressingGrid", panel.transform);

        // Pengisi ruang kosong agar tombol aksi menempel di bawah
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(panel.transform, false);
        spacer.GetComponent<LayoutElement>().flexibleHeight = 1;

        statusText = CreateText("Status", panel.transform, "", 24, FontStyles.Italic, TextAlignmentOptions.Center, 34);
        statusText.color = new Color(1f, 0.9f, 0.5f);

        // Baris tombol aksi
        var actions = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        actions.transform.SetParent(panel.transform, false);
        var hlg = actions.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        actions.GetComponent<LayoutElement>().preferredHeight = 70;

        CreateButton(actions.transform, "Kosongkan", clearColor, OnClear);
        CreateButton(actions.transform, "Selesai & Bawa", takeColor, OnTake);
        CreateButton(actions.transform, "Tutup (Esc)", closeColor, Close);
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private TMP_Text CreateText(string name, Transform parent, string text, float size,
                                FontStyles style, TextAlignmentOptions align, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        return t;
    }

    private RectTransform CreateGrid(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
        go.transform.SetParent(parent, false);
        var grid = go.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(210, 70);
        grid.spacing = new Vector2(14, 14);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        return (RectTransform)go.transform;
    }

    private GameObject CreateButton(Transform parent, string label, Color color, UnityAction onClick)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = color;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 60;
        le.preferredHeight = 70;

        var textGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        var t = textGO.GetComponent<TextMeshProUGUI>();
        t.text = label;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.enableAutoSizing = true;
        t.fontSizeMin = 14;
        t.fontSizeMax = 26;
        t.raycastTarget = false;
        RectTransform trt = t.rectTransform;
        Stretch(trt);
        trt.offsetMin = new Vector2(8, 4);
        trt.offsetMax = new Vector2(-8, -4);

        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
