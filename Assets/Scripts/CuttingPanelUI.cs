using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum CutGrade { Perfect, Good, Bad }

/// <summary>
/// Panel memotong ala Cooking Mama: pemain menyeret mouse (kursor berubah jadi pisau)
/// mengikuti garis putus-putus di atas bahan. Ketepatan menentukan nilai Perfect / Good / Bad.
///
/// Gambar diambil otomatis dari folder Assets/Resources/Cutting/ :
///   knife.png, Talenan.png, dan gambar bahan sesuai NAMA ASSET bahannya
///   (misalnya Selada.png, Tomat.png, SeladaPotong.png, TomatPotong.png).
/// Kalau kolom Icon di asset bahan diisi, Icon itu yang dipakai.
/// Pasang di objek GameManager. Seluruh tampilan dibuat lewat kode.
/// </summary>
public class CuttingPanelUI : MonoBehaviour
{
    public static CuttingPanelUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.active;

    [Header("Penilaian (jarak rata-rata dari garis, dalam piksel layar 1920x1080)")]
    public float perfectDistance = 14f;
    public float goodDistance = 32f;
    [Tooltip("Kalau pisau keluar sejauh ini dari garis, goresan dianggap gagal dan harus diulang")]
    public float failDistance = 80f;
    [Tooltip("Jarak maksimal dari titik hijau untuk mulai memotong")]
    public float startRadius = 45f;
    [Tooltip("Jumlah garis maksimal (jumlah garis = Chops Required pada bahan)")]
    public int maxLines = 5;

    [Header("Gambar (opsional, kosong = ambil dari Resources/Cutting)")]
    public Sprite knifeSprite;
    public Sprite boardSprite;
    [Tooltip("Titik ujung pisau pada gambar (0-1). Untuk knife.png bawaan: 0.015, 0.934")]
    public Vector2 knifeTip = new Vector2(0.015f, 0.934f);
    public float knifeSize = 120f;

    // ------------------------------------------------------------------ status
    private bool active;
    private IngredientData ingredient;
    private Action<CutGrade> onDone;
    private Action onCancel;

    private class CutLine
    {
        public Vector2 a, b;
        public readonly List<Image> dots = new List<Image>();
        public Image startMark, endMark;
        public int score = -1;
    }

    private readonly List<CutLine> lines = new List<CutLine>();
    private readonly List<GameObject> sessionObjects = new List<GameObject>();
    private readonly List<GameObject> trail = new List<GameObject>();
    private int current;
    private bool dragging;
    private float maxProgress, devSum;
    private int devCount;
    private Vector2 lastTrailPos;
    private float closeTimer = -1f, popupTimer;
    private CutGrade finalGrade;

    // ------------------------------------------------------------------ tampilan
    private GameObject root;
    private RectTransform rootRect, board, knife;
    private Image foodImage;
    private TextMeshProUGUI titleText, hintText, popupText, progressText;
    private static Sprite circleSprite;

    private readonly Color dotIdle = new Color(1f, 1f, 1f, 0.35f);
    private readonly Color dotActive = new Color(1f, 1f, 1f, 0.95f);
    private readonly Color colPerfect = new Color(0.30f, 0.85f, 0.40f, 1f);
    private readonly Color colGood = new Color(1f, 0.78f, 0.25f, 1f);
    private readonly Color colBad = new Color(0.95f, 0.35f, 0.30f, 1f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (knifeSprite == null) knifeSprite = LoadSprite("knife");
        if (boardSprite == null) boardSprite = LoadSprite("Talenan");
        Build();
        root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ buka / tutup

    /// <summary>Membuka panel untuk memotong satu bahan.</summary>
    public void Open(IngredientData ing, Action<CutGrade> done, Action cancel)
    {
        ingredient = ing;
        onDone = done;
        onCancel = cancel;
        active = true;
        root.SetActive(true);
        UIState.SetUIOpen(true);
        Cursor.visible = false; // diganti pisau

        ClearSession();
        titleText.text = $"Potong {ing.displayName}";
        SetHint("Tahan klik kiri di titik hijau, lalu ikuti garis sampai ujung");
        popupText.text = "";

        Sprite food = ing.icon != null ? ing.icon : LoadSprite(ing.name);
        SetFood(food, ing);

        CreateLines(Mathf.Clamp(ing.chopsRequired, 1, Mathf.Max(1, maxLines)));
        current = 0;
        dragging = false;
        closeTimer = -1f;
        Highlight();
    }

    private void Close()
    {
        active = false;
        dragging = false;
        ClearTrail();
        ClearSession();
        root.SetActive(false);
        UIState.SetUIOpen(false); // kursor muncul lagi
    }

    private void Cancel()
    {
        Close();
        onCancel?.Invoke();
    }

    private void Finish()
    {
        Close();
        onDone?.Invoke(finalGrade);
    }

    // ------------------------------------------------------------------ input

    void Update()
    {
        if (!active) return;

        UpdateKnife();

        if (popupTimer > 0f)
        {
            popupTimer -= Time.unscaledDeltaTime;
            if (popupTimer <= 0f && closeTimer < 0f) popupText.text = "";
        }

        if (closeTimer >= 0f)
        {
            closeTimer -= Time.unscaledDeltaTime;
            if (closeTimer <= 0f) Finish();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
        if (!ScreenToBoard(Input.mousePosition, out Vector2 p)) return;

        CutLine line = lines[current];

        if (Input.GetMouseButtonDown(0))
        {
            if ((p - line.a).magnitude <= startRadius)
            {
                dragging = true;
                maxProgress = 0f; devSum = 0f; devCount = 0;
                ClearTrail();
                lastTrailPos = line.a;
            }
            else SetHint("Mulai dari titik hijau di ujung garis");
        }

        if (!dragging) return;

        if (Input.GetMouseButtonUp(0))
        {
            dragging = false;
            ClearTrail();
            SetHint("Pisau terangkat sebelum ujung garis. Ulangi dari titik hijau");
            return;
        }

        if (!Input.GetMouseButton(0)) return;

        Vector2 ab = line.b - line.a;
        float len = ab.magnitude;
        Vector2 dir = ab / len;
        Vector2 ap = p - line.a;
        float t = Vector2.Dot(ap, dir) / len;                 // progres sepanjang garis (0..1)
        float dev = Mathf.Abs(dir.x * ap.y - dir.y * ap.x);   // jarak tegak lurus dari garis

        if (dev > failDistance)
        {
            dragging = false;
            ClearTrail();
            SetHint("Terlalu jauh dari garis. Ulangi dari titik hijau");
            return;
        }

        if (t >= maxProgress - 0.02f)
        {
            maxProgress = Mathf.Max(maxProgress, Mathf.Clamp01(t));
            devSum += dev;
            devCount++;
            AddTrail(p, dev);
        }

        if (maxProgress >= 0.97f) CompleteLine();
    }

    private void CompleteLine()
    {
        CutLine line = lines[current];
        float avg = devSum / Mathf.Max(1, devCount);
        line.score = avg <= perfectDistance ? 2 : (avg <= goodDistance ? 1 : 0);

        Color c = line.score == 2 ? colPerfect : (line.score == 1 ? colGood : colBad);
        foreach (var d in line.dots) { d.color = c; d.rectTransform.sizeDelta = new Vector2(12, 12); }
        line.startMark.gameObject.SetActive(false);
        line.endMark.gameObject.SetActive(false);

        ShowPopup(line.score == 2 ? "Perfect!" : (line.score == 1 ? "Good" : "Meleset"), c);
        PlayPlayerChop();

        dragging = false;
        ClearTrail();
        current++;

        if (current >= lines.Count)
        {
            int total = 0;
            foreach (var l in lines) total += l.score;
            float average = (float)total / lines.Count;
            finalGrade = average >= 1.67f ? CutGrade.Perfect : (average >= 0.84f ? CutGrade.Good : CutGrade.Bad);

            // tampilkan versi terpotong
            if (ingredient.cutResult != null)
            {
                Sprite cut = ingredient.cutResult.icon != null ? ingredient.cutResult.icon : LoadSprite(ingredient.cutResult.name);
                if (cut != null) SetFood(cut, ingredient.cutResult);
            }
            foreach (var l in lines) foreach (var d in l.dots) d.gameObject.SetActive(false);

            string label = finalGrade == CutGrade.Perfect ? "PERFECT!" : (finalGrade == CutGrade.Good ? "GOOD" : "BAD");
            Color fc = finalGrade == CutGrade.Perfect ? colPerfect : (finalGrade == CutGrade.Good ? colGood : colBad);
            ShowPopup(label, fc, 60);
            SetHint("");
            progressText.text = "";
            closeTimer = 1.1f;
        }
        else
        {
            Highlight();
        }
    }

    // ------------------------------------------------------------------ garis

    private void CreateLines(int count)
    {
        float half = 165f;
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(-150f, 150f, (i + 1f) / (count + 1f));
            float tilt = (i % 2 == 0 ? 1f : -1f) * 18f;
            var line = new CutLine { a = new Vector2(x + tilt, half), b = new Vector2(x - tilt, -half) };

            const int dotCount = 18;
            for (int k = 0; k <= dotCount; k++)
            {
                Vector2 pos = Vector2.Lerp(line.a, line.b, k / (float)dotCount);
                line.dots.Add(CreateCircle("Dot", board, pos, 8f, dotIdle, sessionObjects));
            }
            line.startMark = CreateCircle("Start", board, line.a, 34f, colPerfect, sessionObjects);
            line.endMark = CreateCircle("End", board, line.b, 20f, new Color(1f, 1f, 1f, 0.8f), sessionObjects);
            lines.Add(line);
        }
    }

    private void Highlight()
    {
        for (int i = 0; i < lines.Count; i++)
        {
            CutLine l = lines[i];
            if (l.score >= 0) continue;
            bool isCurrent = i == current;
            foreach (var d in l.dots) d.color = isCurrent ? dotActive : dotIdle;
            l.startMark.gameObject.SetActive(isCurrent);
            l.endMark.gameObject.SetActive(isCurrent);
        }
        progressText.text = $"Garis {current + 1} / {lines.Count}";
    }

    private void AddTrail(Vector2 p, float dev)
    {
        if ((p - lastTrailPos).magnitude < 9f) return;
        lastTrailPos = p;
        Color c = dev <= perfectDistance ? colPerfect : (dev <= goodDistance ? colGood : colBad);
        trail.Add(CreateCircle("Trail", board, p, 10f, c, null).gameObject);
    }

    private void ClearTrail()
    {
        foreach (var t in trail) if (t != null) Destroy(t);
        trail.Clear();
    }

    private void ClearSession()
    {
        foreach (var o in sessionObjects) if (o != null) Destroy(o);
        sessionObjects.Clear();
        lines.Clear();
    }

    // ------------------------------------------------------------------ bantuan

    private void SetFood(Sprite sprite, IngredientData data)
    {
        if (sprite != null)
        {
            foodImage.sprite = sprite;
            foodImage.color = Color.white;
        }
        else
        {
            foodImage.sprite = Circle();
            foodImage.color = data != null && data.color != Color.white ? data.color : new Color(0.6f, 0.85f, 0.45f);
        }
        foodImage.preserveAspect = true;
        foodImage.transform.localScale = Vector3.one * 1.08f; // efek "pop" kecil
    }

    private void SetHint(string text) => hintText.text = text;

    private void ShowPopup(string text, Color color, float size = 44f)
    {
        popupText.text = text;
        popupText.color = color;
        popupText.fontSize = size;
        popupTimer = 0.8f;
    }

    private void UpdateKnife()
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, Input.mousePosition, null, out Vector2 lp))
            knife.anchoredPosition = lp;

        // pisau sedikit menekan saat memotong
        float target = dragging ? -12f : 0f;
        float z = Mathf.LerpAngle(knife.localEulerAngles.z, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        knife.localEulerAngles = new Vector3(0f, 0f, z);

        // efek pop pada gambar bahan kembali normal
        foodImage.transform.localScale = Vector3.Lerp(foodImage.transform.localScale, Vector3.one,
                                                      1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
    }

    private bool ScreenToBoard(Vector2 screen, out Vector2 local) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screen, null, out local);

    private void PlayPlayerChop()
    {
        var anim = FindFirstObjectByType<PlayerAnimation>();
        if (anim != null) anim.PlayChop();
    }

    private static Sprite LoadSprite(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        var asSprite = Resources.Load<Sprite>("Cutting/" + fileName);
        if (asSprite != null) return asSprite;
        var tex = Resources.Load<Texture2D>("Cutting/" + fileName);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite Circle()
    {
        if (circleSprite != null) return circleSprite;
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        float r = s / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    private Image CreateCircle(string name, RectTransform parent, Vector2 pos, float size, Color color, List<GameObject> track)
    {
        Image img = UIBuilder.CreateImage(name, parent, color);
        img.sprite = Circle();
        img.raycastTarget = false;
        UIBuilder.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        track?.Add(img.gameObject);
        return img;
    }

    // ------------------------------------------------------------------ membangun UI

    private void Build()
    {
        root = UIBuilder.CreateOverlayCanvas("CuttingCanvas", transform, 120);
        rootRect = (RectTransform)root.transform;

        var dim = UIBuilder.CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.6f));
        UIBuilder.Stretch(dim.rectTransform);

        titleText = UIBuilder.CreateText("Title", root.transform, "", 46, TextAlignmentOptions.Center, FontStyles.Bold);
        UIBuilder.Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 400f), new Vector2(1000f, 70f));

        progressText = UIBuilder.CreateText("Progress", root.transform, "", 28, TextAlignmentOptions.Center);
        UIBuilder.Place(progressText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 345f), new Vector2(600f, 40f));

        // Talenan
        Image boardImg = UIBuilder.CreateImage("Board", root.transform, boardSprite != null ? Color.white : new Color(0.80f, 0.58f, 0.36f));
        if (boardSprite != null) boardImg.sprite = boardSprite;
        boardImg.raycastTarget = false;
        board = boardImg.rectTransform;
        UIBuilder.Place(board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1000f, 500f));

        // Bahan di tengah talenan
        foodImage = UIBuilder.CreateImage("Food", board, Color.white);
        foodImage.raycastTarget = false;
        UIBuilder.Place(foodImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 400f));

        popupText = UIBuilder.CreateText("Popup", root.transform, "", 44, TextAlignmentOptions.Center, FontStyles.Bold);
        popupText.outlineWidth = 0.2f;
        popupText.outlineColor = new Color32(0, 0, 0, 255);
        UIBuilder.Place(popupText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(800f, 80f));

        hintText = UIBuilder.CreateText("Hint", root.transform, "", 26, TextAlignmentOptions.Center, FontStyles.Italic);
        hintText.color = new Color(1f, 0.92f, 0.6f);
        UIBuilder.Place(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(1200f, 40f));

        var cancel = UIBuilder.CreateButton("Cancel", root.transform, "Batal (Esc)", new Color(0.35f, 0.35f, 0.35f), Cancel, 24);
        UIBuilder.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -370f), new Vector2(240f, 60f));

        // Pisau (kursor), paling atas
        Image knifeImg = UIBuilder.CreateImage("Knife", root.transform, Color.white);
        knifeImg.raycastTarget = false;
        if (knifeSprite != null) knifeImg.sprite = knifeSprite;
        else knifeImg.color = new Color(0.85f, 0.85f, 0.9f);
        knife = knifeImg.rectTransform;
        knife.anchorMin = knife.anchorMax = new Vector2(0.5f, 0.5f);
        knife.pivot = knifeTip; // ujung pisau tepat di posisi mouse
        knife.sizeDelta = knifeSprite != null ? new Vector2(knifeSize, knifeSize) : new Vector2(14f, 80f);
    }
}
