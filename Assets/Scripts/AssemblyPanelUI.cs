using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class AssemblyPanelUI : MonoBehaviour
{
    public static AssemblyPanelUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.station != null;

    [Header("Ukuran")]
    public float bowlSize = 480f;
    [Tooltip("Ukuran tiap potongan bahan di dalam mangkuk")]
    public float pieceSize = 350f;
    [Tooltip("Jumlah potongan yang muncul setiap satu bahan dimasukkan")]
    public int piecesPerIngredient = 3;

    [Header("Dressing zigzag")]
    [Tooltip("Ketebalan garis dressing")]
    public float drizzleWidth = 16f;
    [Tooltip("Jarak maksimal mouse dari garis panduan agar dressing tertuang")]
    public float followDistance = 55f;
    [Tooltip("Jarak maksimal dari titik hijau untuk mulai / melanjutkan menuang")]
    public float startRadius = 55f;

    // ------------------------------------------------------------------ status
    private AssemblyStation station;
    private enum Drag { None, Ingredient, Bottle }
    private Drag drag = Drag.None;
    private IngredientData dragItem;

    private class Slot { public RectTransform rect; public IngredientData item; }
    private readonly List<Slot> bins = new List<Slot>();
    private readonly List<Slot> bottles = new List<Slot>();
    private readonly List<GameObject> dynamicUI = new List<GameObject>();
    private readonly List<GameObject> pieces = new List<GameObject>();

    // dressing zigzag
    private readonly List<Vector2> path = new List<Vector2>();
    private readonly List<GameObject> guideObjects = new List<GameObject>();
    private readonly List<GameObject> drizzleObjects = new List<GameObject>();
    private readonly List<Vector2> drawnPoints = new List<Vector2>();
    private float pathLength, progress;   // progress = jarak yang sudah dituang sepanjang garis
    private bool tracing;
    private Vector2 lastDrawn;
    private RectTransform progressMarker;

    // ------------------------------------------------------------------ tampilan
    private GameObject root;
    private RectTransform rootRect, bowl, binRow, bottleArea, ghost;
    private Image ghostImage, drizzleImage;
    private TextMeshProUGUI bowlText, statusText, ticketText;
    private static Sprite circleSprite;
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ buka / tutup

    public void Open(AssemblyStation s)
    {
        station = s;
        root.SetActive(true);
        UIState.SetUIOpen(true);
        statusText.text = "Seret bahan ke mangkuk. Klik botol dressing, lalu tuang mengikuti garis zigzag.";
        EndDrag();
        RebuildBowl();
        Refresh();
    }

    public void Close()
    {
        EndDrag();
        station = null;
        root.SetActive(false);
        UIState.SetUIOpen(false);
    }

    // ------------------------------------------------------------------ input

    void Update()
    {
        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (drag == Drag.Bottle) { CancelPour("Batal menuang dressing"); return; }
            Close();
            return;
        }

        Vector2 mouse = Input.mousePosition;

        if (drag == Drag.None)
        {
            if (Input.GetMouseButtonDown(0)) TryStartDrag(mouse);
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, mouse, null, out Vector2 lp))
            ghost.anchoredPosition = lp;

        bool overBowl = InsideBowl(mouse, out Vector2 bowlLocal);

        if (drag == Drag.Ingredient) UpdateIngredientDrag(overBowl, bowlLocal);
        else UpdatePour(bowlLocal);
    }

    private void UpdateIngredientDrag(bool overBowl, Vector2 bowlLocal)
    {
        ghost.localScale = Vector3.one * (overBowl ? 1.1f : 1f);
        if (!Input.GetMouseButtonUp(0)) return;

        if (overBowl)
        {
            if (station.TryAdd(dragItem, out string msg))
            {
                // potongan pertama di titik jatuh, sisanya menyebar mengisi mangkuk
                SpawnPiece(dragItem, ClampInBowl(bowlLocal), true);
                for (int i = 1; i < Mathf.Max(1, piecesPerIngredient); i++)
                    SpawnPiece(dragItem, SpiralPosition(pieces.Count), true);
            }
            statusText.text = msg;
            Refresh();
        }
        EndDrag();
    }

    private void TryStartDrag(Vector2 mouse)
    {
        var inv = InventoryManager.Instance;
        if (inv == null) return;

        foreach (var b in bins)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(b.rect, mouse, null)) continue;
            if (inv.GetCount(b.item) <= 0) { statusText.text = $"{b.item.displayName} habis"; return; }
            if (station.Bowl.IsFull) { statusText.text = $"Mangkuk penuh (maksimal {SaladBowl.MaxIngredients} bahan)"; return; }
            BeginDrag(Drag.Ingredient, b.item, PieceSprite(b.item), new Vector2(pieceSize * 0.8f, pieceSize * 0.8f), new Vector2(0.5f, 0.5f));
            return;
        }

        foreach (var b in bottles)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(b.rect, mouse, null)) continue;
            if (station.Bowl.dressing != null) { statusText.text = $"Mangkuk sudah berisi {station.Bowl.dressing.displayName}"; return; }
            if (inv.GetCount(b.item) <= 0) { statusText.text = $"{b.item.displayName} habis"; return; }
            BeginDrag(Drag.Bottle, b.item, BottleSprite(b.item), new Vector2(100f, 200f), new Vector2(0.5f, 0.97f));
            b.rect.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.3f);
            StartPour();
            return;
        }
    }

    private void BeginDrag(Drag kind, IngredientData item, Sprite sprite, Vector2 size, Vector2 pivot)
    {
        drag = kind;
        dragItem = item;
        ghostImage.sprite = sprite != null ? sprite : Circle();
        ghostImage.color = sprite != null ? Color.white : new Color(0.55f, 0.8f, 0.45f);
        ghost.pivot = pivot;
        ghost.sizeDelta = size;
        ghost.localEulerAngles = Vector3.zero;
        ghost.localScale = Vector3.one;
        ghost.gameObject.SetActive(true);
        ghost.SetAsLastSibling();
    }

    private void EndDrag()
    {
        drag = Drag.None;
        dragItem = null;
        tracing = false;
        if (ghost != null) ghost.gameObject.SetActive(false);
        foreach (var b in bottles) if (b.rect != null) b.rect.GetComponent<Image>().color = Color.white;
        ClearGuide();
    }

    // ------------------------------------------------------------------ dressing zigzag

    private void StartPour()
    {
        ClearDrizzle();
        drizzleImage.gameObject.SetActive(false);
        BuildPath();
        progress = 0f;
        tracing = false;
        DrawGuide();
        statusText.text = $"Tahan klik di titik hijau, lalu tuang {dragItem.displayName} mengikuti garis zigzag. Esc = batal";
    }

    private void BuildPath()
    {
        path.Clear();
        float x = bowlSize * 0.29f, top = bowlSize * 0.23f;
        int zig = 6;
        for (int i = 0; i < zig; i++)
        {
            float y = Mathf.Lerp(top, -top, i / (float)(zig - 1));
            path.Add(new Vector2(i % 2 == 0 ? -x : x, y));
        }
        pathLength = 0f;
        for (int i = 1; i < path.Count; i++) pathLength += Vector2.Distance(path[i - 1], path[i]);
    }

    private void UpdatePour(Vector2 bowlLocal)
    {
        bool holding = Input.GetMouseButton(0);
        float tilt = holding && tracing ? 150f : 25f;
        float z = Mathf.LerpAngle(ghost.localEulerAngles.z, tilt, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        ghost.localEulerAngles = new Vector3(0f, 0f, z);

        if (Input.GetMouseButtonDown(1)) { CancelPour("Batal menuang dressing"); return; }

        Vector2 current = PointAt(progress);
        if (progressMarker != null)
        {
            progressMarker.anchoredPosition = current;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.15f;
            progressMarker.localScale = Vector3.one * pulse;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if ((bowlLocal - current).magnitude <= startRadius)
            {
                tracing = true;
                lastDrawn = current;
            }
            else statusText.text = "Mulai menuang dari titik hijau";
        }

        if (tracing && Input.GetMouseButtonUp(0))
        {
            tracing = false;
            statusText.text = "Lanjutkan dari titik hijau sampai garis selesai";
        }

        if (!tracing || !holding) return;

        // posisi terdekat pada garis, hanya boleh maju sedikit demi sedikit (tidak bisa melompat)
        float s = ProjectOnPath(bowlLocal, progress, out float dist);
        if (dist > followDistance)
        {
            statusText.text = "Terlalu jauh dari garis, kembali ke titik hijau";
            tracing = false;
            return;
        }

        if (s > progress && s - progress < bowlSize * 0.35f) progress = s;

        // gambar siraman tepat di jalur mouse
        if ((bowlLocal - lastDrawn).magnitude >= 10f)
        {
            DrawSegment(lastDrawn, bowlLocal);
            lastDrawn = bowlLocal;
        }

        int pct = Mathf.FloorToInt(progress / pathLength * 100f);
        statusText.text = $"Menuang {dragItem.displayName}... {Mathf.Min(pct, 100)}%";

        if (progress >= pathLength * 0.97f) FinishPour();
    }

    private void FinishPour()
    {
        IngredientData dressing = dragItem;
        if (station.TryAdd(dressing, out string msg))
        {
            if (IsSesame(dressing)) SprinkleSeeds();
            statusText.text = msg;
            drag = Drag.None;          // simpan siraman yang sudah digambar
            dragItem = null;
            tracing = false;
            ghost.gameObject.SetActive(false);
            foreach (var b in bottles) if (b.rect != null) b.rect.GetComponent<Image>().color = Color.white;
            ClearGuide();
        }
        else
        {
            CancelPour(msg);
            return;
        }
        Refresh();
    }

    private void CancelPour(string message)
    {
        ClearDrizzle();
        EndDrag();
        if (station != null && station.Bowl.dressing != null) ShowStaticDrizzle(station.Bowl.dressing);
        statusText.text = message;
    }

    /// <summary>Titik pada garis zigzag di jarak s dari awal.</summary>
    private Vector2 PointAt(float s)
    {
        for (int i = 1; i < path.Count; i++)
        {
            float seg = Vector2.Distance(path[i - 1], path[i]);
            if (s <= seg) return Vector2.Lerp(path[i - 1], path[i], seg <= 0f ? 0f : s / seg);
            s -= seg;
        }
        return path.Count > 0 ? path[path.Count - 1] : Vector2.zero;
    }

    /// <summary>Proyeksi titik ke garis, dicari di sekitar progres sekarang. Mengembalikan jarak sepanjang garis.</summary>
    private float ProjectOnPath(Vector2 p, float around, out float distance)
    {
        float best = around, bestDist = float.MaxValue, acc = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 a = path[i - 1], b = path[i];
            float seg = Vector2.Distance(a, b);
            float t = seg <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (seg * seg));
            float sHere = acc + t * seg;
            float d = Vector2.Distance(p, Vector2.Lerp(a, b, t));
            // hanya segmen di sekitar progres sekarang yang dipertimbangkan
            if (sHere >= around - 40f && sHere <= around + bowlSize * 0.5f && d < bestDist)
            {
                bestDist = d;
                best = sHere;
            }
            acc += seg;
        }
        distance = bestDist;
        return best;
    }

    private void DrawGuide()
    {
        ClearGuide();
        Color dot = new Color(1f, 1f, 1f, 0.85f);
        float step = 16f;
        for (float s = 0f; s <= pathLength; s += step)
            guideObjects.Add(CircleAt(bowl, PointAt(s), 7f, dot).gameObject);

        Image marker = CircleAt(bowl, path[0], 34f, new Color(0.3f, 0.85f, 0.4f, 1f));
        progressMarker = marker.rectTransform;
        guideObjects.Add(marker.gameObject);
        guideObjects.Add(CircleAt(bowl, path[path.Count - 1], 20f, new Color(0.95f, 0.4f, 0.35f, 0.9f)).gameObject);
    }

    private void ClearGuide()
    {
        foreach (var o in guideObjects) if (o != null) Destroy(o);
        guideObjects.Clear();
        progressMarker = null;
    }

    private void DrawSegment(Vector2 a, Vector2 b)
    {
        Color main = DressingColor(dragItem), edge = main * 0.7f; edge.a = 1f;
        drizzleObjects.Add(Line(a, b, drizzleWidth + 6f, edge));
        drizzleObjects.Add(CircleAt(bowl, b, drizzleWidth + 6f, edge).gameObject);
        drizzleObjects.Add(Line(a, b, drizzleWidth, main));
        drizzleObjects.Add(CircleAt(bowl, b, drizzleWidth, main).gameObject);
        drawnPoints.Add(b);
    }

    private GameObject Line(Vector2 a, Vector2 b, float width, Color color)
    {
        Image img = UIBuilder.CreateImage("Drizzle", bowl, color);
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = a;
        rt.sizeDelta = new Vector2(Vector2.Distance(a, b), width);
        rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        return img.gameObject;
    }

    private void SprinkleSeeds()
    {
        var rnd = new System.Random(5);
        for (int i = 0; i < drawnPoints.Count; i += 2)
        {
            Vector2 p = drawnPoints[i] + new Vector2((float)rnd.NextDouble() * 12f - 6f, (float)rnd.NextDouble() * 12f - 6f);
            Color c = rnd.NextDouble() < 0.65 ? new Color(0.98f, 0.95f, 0.86f) : new Color(0.22f, 0.17f, 0.13f);
            Image seed = CircleAt(bowl, p, 6f, c);
            seed.rectTransform.sizeDelta = new Vector2(9f, 5f);
            seed.rectTransform.localEulerAngles = new Vector3(0f, 0f, (float)rnd.NextDouble() * 180f);
            drizzleObjects.Add(seed.gameObject);
        }
    }

    private void ClearDrizzle()
    {
        foreach (var o in drizzleObjects) if (o != null) Destroy(o);
        drizzleObjects.Clear();
        drawnPoints.Clear();
    }

    private static bool IsSesame(IngredientData d) =>
        d != null && (d.name.ToLower().Contains("wijen") || d.displayName.ToLower().Contains("wijen"));

    private static Color DressingColor(IngredientData d)
    {
        if (d == null) return new Color(0.95f, 0.88f, 0.7f);
        if (d.color != Color.white) return d.color;
        string n = (d.name + d.displayName).ToLower();
        if (n.Contains("wijen")) return new Color(0.77f, 0.53f, 0.26f);
        if (n.Contains("mayo")) return new Color(1f, 0.96f, 0.82f);
        return new Color(0.95f, 0.88f, 0.7f);
    }

    // ------------------------------------------------------------------ mangkuk

    private bool InsideBowl(Vector2 screen, out Vector2 local)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bowl, screen, null, out local)) return false;
        return local.magnitude <= bowlSize * 0.42f;
    }

    private Vector2 ClampInBowl(Vector2 p)
    {
        float max = bowlSize * 0.24f;
        return p.magnitude > max ? p.normalized * max : p;
    }

    /// <summary>Posisi menyebar rata di mangkuk (spiral sudut emas).</summary>
    private Vector2 SpiralPosition(int index)
    {
        float maxR = bowlSize * 0.24f;
        float angle = index * 2.39996f;
        float r = Mathf.Sqrt((index % 18 + 0.5f) / 18f) * maxR;
        return new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
    }

    private void SpawnPiece(IngredientData item, Vector2 pos, bool pop)
    {
        Sprite sp = PieceSprite(item);
        Image img = UIBuilder.CreateImage("Piece", bowl, sp != null ? Color.white : new Color(0.55f, 0.8f, 0.45f));
        img.sprite = sp != null ? sp : Circle();
        img.preserveAspect = true;
        img.raycastTarget = false;
        float size = pieceSize * Random.Range(0.85f, 1.1f);
        UIBuilder.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        img.rectTransform.localEulerAngles = new Vector3(0f, 0f, Random.Range(-60f, 60f));
        img.rectTransform.localScale = Vector3.one * (pop ? 1.25f : 1f);
        pieces.Add(img.gameObject);
        KeepDrizzleOnTop();
    }

    private void KeepDrizzleOnTop()
    {
        drizzleImage.transform.SetAsLastSibling();
        foreach (var o in drizzleObjects) if (o != null) o.transform.SetAsLastSibling();
        foreach (var o in guideObjects) if (o != null) o.transform.SetAsLastSibling();
    }

    /// <summary>Menyusun ulang isi mangkuk dari data (saat panel dibuka lagi).</summary>
    private void RebuildBowl()
    {
        foreach (var p in pieces) if (p != null) Destroy(p);
        pieces.Clear();
        ClearDrizzle();

        int k = 0;
        foreach (var item in station.Bowl.ingredients)
            for (int i = 0; i < Mathf.Max(1, piecesPerIngredient); i++)
                SpawnPiece(item, SpiralPosition(k++), false);

        if (station.Bowl.dressing != null) ShowStaticDrizzle(station.Bowl.dressing);
        else drizzleImage.gameObject.SetActive(false);
    }

    /// <summary>Siraman bergambar (dipakai saat panel dibuka lagi, karena garis yang digambar tidak disimpan).</summary>
    private void ShowStaticDrizzle(IngredientData dressing)
    {
        Sprite sp = Load("Assembly/Siraman_" + dressing.name);
        if (sp == null) { drizzleImage.gameObject.SetActive(false); return; }
        drizzleImage.sprite = sp;
        drizzleImage.color = Color.white;
        drizzleImage.gameObject.SetActive(true);
        drizzleImage.transform.SetAsLastSibling();
    }

    private Image CircleAt(RectTransform parent, Vector2 pos, float size, Color color)
    {
        Image img = UIBuilder.CreateImage("Dot", parent, color);
        img.sprite = Circle();
        img.raycastTarget = false;
        UIBuilder.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        return img;
    }

    // ------------------------------------------------------------------ isi panel

    private void Refresh()
    {
        if (!IsOpen) return;

        foreach (var o in dynamicUI) if (o != null) Destroy(o);
        dynamicUI.Clear();
        bins.Clear();
        bottles.Clear();

        var inv = InventoryManager.Instance;
        if (inv != null)
        {
            foreach (IngredientData item in inv.GetAllItems())
            {
                if (item == null) continue;
                if (item.isDressing) CreateBottle(item, inv.GetCount(item));
                else if (item.canGoInBowl) CreateBin(item, inv.GetCount(item));
            }
        }

        if (bins.Count == 0) dynamicUI.Add(Note(binRow, "Belum ada bahan potong. Potong bahan dulu di Meja Potong.").gameObject);
        if (bottles.Count == 0) dynamicUI.Add(Note(bottleArea, "Belum membawa dressing. Ambil dari rak dressing.").gameObject);

        bowlText.text = $"<b>{station.Bowl.Describe()}</b>\n<size=80%>{station.Bowl.ingredients.Count}/{SaladBowl.MaxIngredients} bahan</size>";
        ticketText.text = BuildTicket();
    }

    private void CreateBin(IngredientData item, int count)
    {
        Image card = UIBuilder.CreateImage("Bin", binRow, new Color(1f, 1f, 1f, 0.95f));
        card.gameObject.AddComponent<LayoutElement>().preferredWidth = 170;
        dynamicUI.Add(card.gameObject);

        Image pic = UIBuilder.CreateImage("Pic", card.transform, Color.white);
        Sprite sp = PieceSprite(item);
        pic.sprite = sp != null ? sp : Circle();
        if (sp == null) pic.color = new Color(0.55f, 0.8f, 0.45f);
        pic.preserveAspect = true;
        pic.raycastTarget = false;
        UIBuilder.Place(pic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(120f, 110f));

        var label = UIBuilder.CreateText("Label", card.transform, $"{item.displayName}\n<b>x{count}</b>", 20, TextAlignmentOptions.Center);
        label.color = new Color(0.15f, 0.15f, 0.15f);
        UIBuilder.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(160f, 54f));

        bins.Add(new Slot { rect = card.rectTransform, item = item });
    }

    private void CreateBottle(IngredientData item, int count)
    {
        Image img = UIBuilder.CreateImage("Bottle", bottleArea, Color.white);
        Sprite sp = BottleSprite(item);
        img.sprite = sp != null ? sp : Circle();
        if (sp == null) img.color = new Color(0.9f, 0.8f, 0.55f);
        img.preserveAspect = true;
        img.gameObject.AddComponent<LayoutElement>().preferredWidth = 130;
        dynamicUI.Add(img.gameObject);

        var label = UIBuilder.CreateText("Label", img.transform, $"{item.displayName}\nx{count}", 20, TextAlignmentOptions.Center);
        label.color = new Color(0.15f, 0.15f, 0.15f);
        UIBuilder.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(170f, 54f));

        bottles.Add(new Slot { rect = img.rectTransform, item = item });
    }

    private string BuildTicket()
    {
        foreach (var c in FindObjectsByType<Customer>(FindObjectsSortMode.None))
            if (c.IsWaiting && c.Order != null)
                return $"<b>PESANAN</b>\n<b>{c.Order.orderName}</b>\n<size=85%>{c.Order.Describe()}</size>";
        return "<b>PESANAN</b>\n<size=85%>Belum ada pelanggan yang menunggu</size>";
    }

    private void OnClear()
    {
        if (!IsOpen) return;
        station.ClearBowl();
        RebuildBowl();
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
        else statusText.text = msg;
    }

    void LateUpdate()
    {
        // efek "pop" potongan baru kembali ke ukuran normal
        foreach (var p in pieces)
            if (p != null) p.transform.localScale = Vector3.Lerp(p.transform.localScale, Vector3.one, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
    }

    // ------------------------------------------------------------------ gambar

    private static Sprite PieceSprite(IngredientData item) =>
        item.icon != null ? item.icon : Load("Cutting/" + item.name);

    private static Sprite BottleSprite(IngredientData item) =>
        item.icon != null ? item.icon : Load("Assembly/Botol_" + item.name);

    private static Sprite Load(string path)
    {
        if (cache.TryGetValue(path, out Sprite s) && s != null) return s;
        Sprite sp = Resources.Load<Sprite>(path);
        if (sp == null)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null) sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        cache[path] = sp;
        return sp;
    }

    private static Sprite Circle()
    {
        if (circleSprite != null) return circleSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        float r = n / 2f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    private TextMeshProUGUI Note(Transform parent, string text)
    {
        var t = UIBuilder.CreateText("Note", parent, text, 22, TextAlignmentOptions.Center, FontStyles.Italic);
        t.color = new Color(0.25f, 0.2f, 0.15f);
        t.gameObject.AddComponent<LayoutElement>().preferredWidth = 560;
        return t;
    }

    // ------------------------------------------------------------------ membangun UI

    private void Build()
    {
        root = UIBuilder.CreateOverlayCanvas("AssemblyCanvas", transform, 100);
        rootRect = (RectTransform)root.transform;

        var dim = UIBuilder.CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.55f));
        UIBuilder.Stretch(dim.rectTransform);

        var panel = UIBuilder.CreateImage("Panel", root.transform, new Color(0.93f, 0.85f, 0.72f, 1f));
        UIBuilder.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 940f));

        var title = UIBuilder.CreateText("Title", panel.transform, "MEJA RACIK", 44, TextAlignmentOptions.Center, FontStyles.Bold);
        title.color = new Color(0.3f, 0.2f, 0.12f);
        UIBuilder.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(800f, 60f));

        // Kartu pesanan (kiri atas)
        var ticket = UIBuilder.CreateImage("Ticket", panel.transform, new Color(1f, 0.99f, 0.93f, 1f));
        UIBuilder.Place(ticket.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -90f), new Vector2(330f, 300f));
        ticketText = UIBuilder.CreateText("TicketText", ticket.transform, "", 24, TextAlignmentOptions.TopLeft);
        ticketText.color = new Color(0.2f, 0.15f, 0.1f);
        UIBuilder.Stretch(ticketText.rectTransform, 18f);

        // Mangkuk (tengah)
        Sprite bowlSprite = Load("Assembly/Mangkuk");
        Image bowlImg = UIBuilder.CreateImage("Bowl", panel.transform, bowlSprite != null ? Color.white : new Color(0.8f, 0.92f, 1f));
        bowlImg.sprite = bowlSprite != null ? bowlSprite : Circle();
        bowlImg.raycastTarget = false;
        bowl = bowlImg.rectTransform;
        UIBuilder.Place(bowl, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, 90f), new Vector2(bowlSize, bowlSize));

        drizzleImage = UIBuilder.CreateImage("Drizzle", bowl, Color.white);
        drizzleImage.raycastTarget = false;
        UIBuilder.Stretch(drizzleImage.rectTransform);
        drizzleImage.gameObject.SetActive(false);

        bowlText = UIBuilder.CreateText("BowlText", panel.transform, "", 24, TextAlignmentOptions.Center);
        bowlText.color = new Color(0.2f, 0.15f, 0.1f);
        UIBuilder.Place(bowlText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, -185f), new Vector2(700f, 64f));

        statusText = UIBuilder.CreateText("Status", panel.transform, "", 22, TextAlignmentOptions.Center, FontStyles.Italic);
        statusText.color = new Color(0.55f, 0.25f, 0.1f);
        UIBuilder.Place(statusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, -235f), new Vector2(900f, 36f));

        // Wadah bahan (bawah)
        var binsGO = new GameObject("Bins", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        binsGO.transform.SetParent(panel.transform, false);
        binRow = (RectTransform)binsGO.transform;
        UIBuilder.Place(binRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-170f, 30f), new Vector2(1100f, 170f));
        var hl = binsGO.GetComponent<HorizontalLayoutGroup>();
        hl.spacing = 18; hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

        // Botol dressing (kanan atas)
        var bottlesGO = new GameObject("Bottles", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bottlesGO.transform.SetParent(panel.transform, false);
        bottleArea = (RectTransform)bottlesGO.transform;
        UIBuilder.Place(bottleArea, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -100f), new Vector2(380f, 260f));
        var hb = bottlesGO.GetComponent<HorizontalLayoutGroup>();
        hb.spacing = 40; hb.childAlignment = TextAnchor.UpperCenter;
        hb.childControlWidth = true; hb.childControlHeight = true;
        hb.childForceExpandWidth = false; hb.childForceExpandHeight = true;

        // Tombol (kanan bawah)
        AddButton(panel.transform, "Selesai & Bawa", new Color(0.20f, 0.50f, 0.78f), OnTake, new Vector2(-40f, 210f));
        AddButton(panel.transform, "Kosongkan", new Color(0.72f, 0.28f, 0.24f), OnClear, new Vector2(-40f, 130f));
        AddButton(panel.transform, "Tutup (Esc)", new Color(0.40f, 0.40f, 0.40f), Close, new Vector2(-40f, 50f));

        // Objek yang diseret (paling atas)
        ghostImage = UIBuilder.CreateImage("DragGhost", root.transform, Color.white);
        ghostImage.raycastTarget = false;
        ghostImage.preserveAspect = true;
        ghost = ghostImage.rectTransform;
        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.gameObject.SetActive(false);
    }

    private void AddButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action, Vector2 pos)
    {
        var b = UIBuilder.CreateButton(label, parent, label, color, action, 26);
        UIBuilder.Place((RectTransform)b.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), pos, new Vector2(300f, 66f));
    }
}
