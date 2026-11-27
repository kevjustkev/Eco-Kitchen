using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Minigame MEMOTONG 3D. Pasang di objek Meja Potong (yang punya CuttingStation).
/// Saat E ditekan: kamera mendekat ke meja, talenan + model bahan muncul, garis potong digambar di atas bahan.
/// Pemain menahan klik kiri di titik hijau lalu menyeret pisau 3D mengikuti garis.
/// Tomat terbelah dua setelah garis pertama (kalau Half Model 3D diisi), lalu menjadi irisan di akhir.
/// </summary>
public class CuttingMinigame3D : MonoBehaviour
{
    [Header("Model")]
    public GameObject boardPrefab;          // CuttingBoard_01
    public GameObject knifePrefab;          // Knife_04
    public float boardScale = 1f;
    public float knifeScale = 1f;
    [Tooltip("Putaran tambahan pisau supaya mata pisau menghadap ke bawah (sesuaikan dengan model pisau)")]
    public Vector3 knifeRotationOffset = new Vector3(0f, 0f, 0f);
    [Tooltip("Pengali ukuran semua model bahan di talenan")]
    public float ingredientScale = 1f;
    [Tooltip("Ukuran bahan & pisau otomatis disesuaikan dengan talenan (abaikan Ingredient/Knife Scale)")]
    public bool autoFitToBoard = true;
    [Range(0.2f, 0.9f)] public float foodToBoard = 0.45f;    // lebar bahan dibanding sisi pendek talenan
    [Range(0.3f, 1.5f)] public float knifeToBoard = 0.8f;    // panjang pisau dibanding sisi panjang talenan
    [Tooltip("Objek di meja yang disembunyikan selama minigame (mis. CuttingBoard_01 dan Knife_04 di scene)")]
    public GameObject[] hideDuringMinigame;

    [Header("Posisi (kosong = otomatis di atas meja)")]
    public Transform workPoint;
    public float cameraHeight = 1.6f;
    public float cameraBack = 1.1f;
    [Tooltip("Kamera otomatis mengambil jarak supaya seluruh talenan terlihat (abaikan Camera Height/Back)")]
    public bool autoCamera = true;
    [Range(35f, 89f)] public float cameraPitch = 65f;
    [Range(0.6f, 2f)] public float cameraPadding = 1.0f;

    [Header("Penilaian (perbandingan terhadap ukuran bahan)")]
    [Range(0.01f, 0.3f)] public float perfectRatio = 0.05f;
    [Range(0.02f, 0.5f)] public float goodRatio = 0.12f;
    [Range(0.05f, 1f)] public float failRatio = 0.35f;
    public int maxLines = 5;

    private float fit = 1f;
    public static bool IsActive { get; private set; }

    // ------------------------------------------------------------------ status
    private IngredientData ingredient;
    private Action<CutGrade> onDone;
    private Action onCancel;
    private Camera cam;

    private readonly List<GameObject> props = new List<GameObject>();
    private GameObject root, food, knife;
    private float size, planeY;
    private Vector3 foodBase;

    private class CutLine { public Vector3 a, b; public LineRenderer guide; public GameObject start, end; public int score = -1; }
    private readonly List<CutLine> lines = new List<CutLine>();
    private int current;
    private bool dragging;
    private float maxProgress, devSum, closeTimer = -1f, popupTimer;
    private int devCount;
    private CutGrade finalGrade;
    private LineRenderer trail;

    // UI
    private GameObject ui;
    private TextMeshProUGUI titleText, hintText, popupText, progressText;

    // ------------------------------------------------------------------ mulai / selesai

    public void Begin(IngredientData raw, Action<CutGrade> done, Action cancel)
    {
        ingredient = raw; onDone = done; onCancel = cancel;
        cam = Minigame3DUtil.GetCamera();
        IsActive = true;
        UIState.SetUIOpen(true);
        Debug.Log($"[Minigame3D] Mulai memotong {raw.displayName} di {name}");
        if (boardPrefab == null) Debug.LogWarning("[Minigame3D] Board Prefab kosong (isi CuttingBoard_01)");
        if (knifePrefab == null) Debug.LogWarning("[Minigame3D] Knife Prefab kosong (isi Knife_04). Kursor biasa dipakai sebagai gantinya.");
        if (raw.model3D == null) Debug.LogWarning($"[Minigame3D] Model 3D di asset {raw.name} kosong");
        if (raw.cutResult == null) Debug.LogWarning($"[Minigame3D] Cut Result di asset {raw.name} kosong");

        root = new GameObject("CuttingMinigame3D");
        Vector3 work = workPoint != null ? workPoint.position : Minigame3DUtil.TableTop(gameObject);

        // talenan
        GameObject board = Minigame3DUtil.Spawn(boardPrefab, work, transform.rotation, boardScale, root.transform);
        float top = work.y;
        if (board != null)
        {
            Minigame3DUtil.PlaceOnSurface(board, work);
            top = Minigame3DUtil.GetBounds(board).max.y;
        }

        if (hideDuringMinigame != null) foreach (var h in hideDuringMinigame) if (h != null) h.SetActive(false);

        // bahan utuh
        fit = 1f;
        food = SpawnFood(raw.model3D, raw, new Vector3(work.x, top, work.z), Quaternion.identity);
        if (autoFitToBoard && food != null && board != null)
        {
            Bounds bb = Minigame3DUtil.GetBounds(board), f0 = Minigame3DUtil.GetBounds(food);
            float want = Mathf.Min(bb.size.x, bb.size.z) * foodToBoard;
            float have = Mathf.Max(f0.size.x, f0.size.z, 0.0001f);
            fit = want / have;
            food.transform.localScale *= fit;
            Minigame3DUtil.PlaceOnSurface(food, new Vector3(work.x, top, work.z));
        }
        Bounds fb = food != null ? Minigame3DUtil.GetBounds(food) : new Bounds(new Vector3(work.x, top + 0.1f, work.z), Vector3.one * 0.3f);
        size = Mathf.Max(fb.size.x, fb.size.z, 0.05f);
        foodBase = new Vector3(fb.center.x, fb.min.y, fb.center.z);
        planeY = fb.max.y + size * 0.03f;

        // pisau
        knife = Minigame3DUtil.Spawn(knifePrefab, fb.center, Quaternion.identity, knifeScale, root.transform);
        if (autoFitToBoard && knife != null && board != null)
        {
            Bounds bb = Minigame3DUtil.GetBounds(board), kb = Minigame3DUtil.GetBounds(knife);
            float want = Mathf.Max(bb.size.x, bb.size.z) * knifeToBoard;
            float have = Mathf.Max(kb.size.x, kb.size.y, kb.size.z);
            if (have > 0.0001f) knife.transform.localScale *= want / have;
        }
        if (knife != null && knifeRotationOffset == Vector3.zero)
            knifeRotationOffset = Minigame3DUtil.LongAxisToForward(knife);   // pisau berbaring searah garis

        // kamera close-up di sisi pemain
        Vector3 toward = PlayerDirection(work);
        Vector3 cp; Quaternion cr;
        if (autoCamera)
        {
            Bounds area = board != null ? Minigame3DUtil.GetBounds(board) : fb;
            float radius = Mathf.Max(area.extents.x, area.extents.z, size);
            Minigame3DUtil.FramePose(cam, new Vector3(fb.center.x, top, fb.center.z), toward, radius, cameraPitch, cameraPadding, out cp, out cr);
        }
        else Minigame3DUtil.CloseUpPose(fb.center, toward, cameraHeight, cameraBack, out cp, out cr);
        var focus = CameraFocus.Get();
        if (focus != null) focus.FocusOn(cp, cr);

        CreateLines(fb, toward, Mathf.Clamp(raw.chopsRequired, 1, Mathf.Max(1, maxLines)));
        trail = Minigame3DUtil.CreateLine("Trail", root.transform, Color.white, size * 0.025f);

        BuildUI();
        titleText.text = $"Potong {raw.displayName}";
        hintText.text = "Tahan klik kiri di titik hijau, lalu seret pisau mengikuti garis. Esc = batal";
        current = 0; dragging = false; closeTimer = -1f;
        Highlight();
    }

    private void End()
    {
        IsActive = false;
        if (root != null) Destroy(root);
        if (ui != null) Destroy(ui);
        lines.Clear();
        Cursor.visible = true;
        if (hideDuringMinigame != null) foreach (var h in hideDuringMinigame) if (h != null) h.SetActive(true);
        var focus = CameraFocus.Get();
        if (focus != null) focus.Release();
        UIState.SetUIOpen(false);
    }

    private void Cancel() { End(); onCancel?.Invoke(); }
    private void Finish() { End(); onDone?.Invoke(finalGrade); }

    // ------------------------------------------------------------------ input

    void Update()
    {
        if (!IsActive || root == null) return;

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
        if (!Minigame3DUtil.MouseOnPlane(cam, planeY, out Vector3 p)) return;

        CutLine line = lines[current];
        Vector3 dir = (line.b - line.a); float len = dir.magnitude; dir /= len;
        UpdateKnife(p, dir);
        // titik hijau berdenyut supaya jelas harus mulai dari mana
        if (!dragging && line.start != null)
            line.start.transform.localScale = Vector3.one * size * 0.12f * (1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.3f);

        if (Input.GetMouseButtonDown(0))
        {
            if (Flat(p - line.a).magnitude <= size * 0.15f)
            {
                dragging = true; maxProgress = 0f; devSum = 0f; devCount = 0;
                trail.positionCount = 0;
            }
            else hintText.text = "Mulai dari titik hijau di ujung garis";
        }

        if (!dragging) return;

        if (Input.GetMouseButtonUp(0))
        {
            dragging = false; trail.positionCount = 0;
            hintText.text = "Pisau terangkat sebelum ujung garis. Ulangi dari titik hijau";
            return;
        }

        Vector3 ap = Flat(p - line.a);
        float t = Vector3.Dot(ap, Flat(dir)) / len;
        float dev = Vector3.Cross(Flat(dir).normalized, ap).magnitude;

        if (dev > size * failRatio)
        {
            dragging = false; trail.positionCount = 0;
            hintText.text = "Terlalu jauh dari garis. Ulangi dari titik hijau";
            return;
        }

        if (t >= maxProgress - 0.02f)
        {
            maxProgress = Mathf.Max(maxProgress, Mathf.Clamp01(t));
            devSum += dev; devCount++;
            AddTrail(new Vector3(p.x, planeY + size * 0.01f, p.z), dev);
        }

        if (maxProgress >= 0.97f) CompleteLine();
    }

    private void UpdateKnife(Vector3 p, Vector3 lineDir)
    {
        if (knife == null) return;
        float lift = dragging ? size * 0.02f : size * 0.25f;
        Vector3 target = new Vector3(p.x, planeY + lift, p.z);
        knife.transform.position = Vector3.Lerp(knife.transform.position, target, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
        Quaternion look = Quaternion.LookRotation(Flat(lineDir).normalized, Vector3.up) * Quaternion.Euler(knifeRotationOffset);
        knife.transform.rotation = Quaternion.Slerp(knife.transform.rotation, look, 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime));
    }

    private void CompleteLine()
    {
        CutLine line = lines[current];
        float avg = devSum / Mathf.Max(1, devCount);
        line.score = avg <= size * perfectRatio ? 2 : (avg <= size * goodRatio ? 1 : 0);

        Color c = ScoreColor(line.score);
        line.guide.material = Minigame3DUtil.LineMaterial();
        line.guide.startColor = line.guide.endColor = c;
        line.guide.textureMode = LineTextureMode.Stretch;
        line.start.SetActive(false);
        line.end.SetActive(false);

        ShowPopup(line.score == 2 ? "Perfect!" : (line.score == 1 ? "Good" : "Meleset"), c);
        var anim = FindFirstObjectByType<PlayerAnimation>();
        if (anim != null) anim.PlayChop();

        // tomat terbelah dua setelah garis pertama
        if (current == 0 && ingredient.halfModel3D != null && food != null) SplitInHalf();

        dragging = false;
        trail.positionCount = 0;
        current++;

        if (current >= lines.Count)
        {
            int total = 0; foreach (var l in lines) total += l.score;
            float average = (float)total / lines.Count;
            finalGrade = average >= 1.67f ? CutGrade.Perfect : (average >= 0.84f ? CutGrade.Good : CutGrade.Bad);
            ShowResultPieces();
            foreach (var l in lines) if (l.guide != null) l.guide.gameObject.SetActive(false);
            ShowPopup(finalGrade == CutGrade.Perfect ? "PERFECT!" : (finalGrade == CutGrade.Good ? "GOOD" : "BAD"), ScoreColor(finalGrade == CutGrade.Perfect ? 2 : finalGrade == CutGrade.Good ? 1 : 0), 64);
            hintText.text = ""; progressText.text = "";
            if (knife != null) knife.SetActive(false);
            closeTimer = 1.2f;
        }
        else Highlight();
    }

    // ------------------------------------------------------------------ visual bahan

    private GameObject SpawnFood(GameObject model, IngredientData data, Vector3 surface, Quaternion rot)
    {
        if (model == null) return null;
        float s = ingredientScale * (data != null ? data.modelScale : 1f) * fit;
        GameObject go = Minigame3DUtil.Spawn(model, surface, rot, s, root.transform);
        Minigame3DUtil.MakeDoubleSided(go);   // model yang normalnya terbalik tidak terlihat tembus
        Minigame3DUtil.PlaceOnSurface(go, surface);
        props.Add(go);
        return go;
    }

    private void SplitInHalf()
    {
        Bounds b = Minigame3DUtil.GetBounds(food);
        Vector3 surface = foodBase;
        food.SetActive(false);
        float gap = b.size.x * 0.35f;
        // dua belahan berbaring dengan sisi potong menghadap ke atas
        Vector3 side = cam != null ? Flat(cam.transform.right).normalized : Vector3.right;
        SpawnFood(ingredient.halfModel3D, ingredient, surface + side * gap, Quaternion.LookRotation(Flat(cam.transform.forward).normalized) * Quaternion.Euler(0f, 0f, -90f));
        SpawnFood(ingredient.halfModel3D, ingredient, surface - side * gap, Quaternion.LookRotation(Flat(cam.transform.forward).normalized) * Quaternion.Euler(0f, 180f, -90f));
    }

    private void ShowResultPieces()
    {
        Vector3 center = foodBase;
        foreach (var p in props) if (p != null) p.SetActive(false);

        IngredientData cut = ingredient.cutResult;
        if (cut == null || cut.model3D == null)
        {
            if (food != null) food.SetActive(true);
            return;
        }
        int count = lines.Count + 1;
        Vector3 side = cam != null ? Flat(cam.transform.right).normalized : Vector3.right;
        var rnd = new System.Random(3);
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : (i / (float)(count - 1) - 0.5f);
            Vector3 pos = center + side * t * size * 1.1f + Flat(cam.transform.forward).normalized * (float)(rnd.NextDouble() - 0.5) * size * 0.2f;
            pos.y = center.y + i * size * 0.01f;
            SpawnFood(cut.model3D, cut, pos, Quaternion.Euler((float)(rnd.NextDouble() * 10 - 5), (float)(rnd.NextDouble() * 360), 0f));
        }
    }

    // ------------------------------------------------------------------ garis

    private void CreateLines(Bounds fb, Vector3 toward, int count)
    {
        // garis potong tegak lurus terhadap arah pandang (terlihat vertikal di layar), dari jauh ke dekat
        Vector3 forward = -Flat(toward).normalized;            // menjauhi kamera
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float half = Mathf.Max(fb.extents.x, fb.extents.z) * 1.15f;
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(-fb.extents.x * 0.75f, fb.extents.x * 0.75f, (i + 1f) / (count + 1f));
            Vector3 c = new Vector3(fb.center.x, planeY, fb.center.z) + right * x;
            var line = new CutLine { a = c + forward * half, b = c - forward * half };
            line.guide = Minigame3DUtil.CreateLine("CutGuide", root.transform, new Color(1f, 1f, 1f, 0.9f), size * 0.035f, true);
            line.guide.positionCount = 2;
            line.guide.SetPosition(0, line.a); line.guide.SetPosition(1, line.b);
            line.guide.material.mainTextureScale = new Vector2(Mathf.Max(2f, half * 2f / (size * 0.06f)), 1f);
            line.start = Minigame3DUtil.Marker("Start", root.transform, line.a, size * 0.09f, new Color(0.3f, 0.85f, 0.4f));
            line.end = Minigame3DUtil.Marker("End", root.transform, line.b, size * 0.06f, new Color(0.95f, 0.4f, 0.35f));
            lines.Add(line);
        }
    }

    private void Highlight()
    {
        for (int i = 0; i < lines.Count; i++)
        {
            CutLine l = lines[i];
            if (l.score >= 0) continue;
            bool on = i == current;
            l.guide.startColor = l.guide.endColor = on ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.3f);
            l.start.SetActive(on); l.end.SetActive(on);
        }
        progressText.text = $"Garis {current + 1} / {lines.Count}";
    }

    private void AddTrail(Vector3 p, float dev)
    {
        int n = trail.positionCount;
        if (n > 0 && (trail.GetPosition(n - 1) - p).magnitude < size * 0.02f) return;
        trail.positionCount = n + 1;
        trail.SetPosition(n, p);
        trail.startColor = trail.endColor = dev <= size * perfectRatio ? ScoreColor(2) : (dev <= size * goodRatio ? ScoreColor(1) : ScoreColor(0));
    }

    private static Color ScoreColor(int s) =>
        s == 2 ? new Color(0.30f, 0.85f, 0.40f) : (s == 1 ? new Color(1f, 0.78f, 0.25f) : new Color(0.95f, 0.35f, 0.30f));

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private Vector3 PlayerDirection(Vector3 work)
    {
        var player = FindFirstObjectByType<PlayerMovement>();
        Vector3 d = player != null ? player.transform.position - work : -transform.forward;
        return Flat(d);
    }

    // ------------------------------------------------------------------ UI

    private void ShowPopup(string text, Color c, float sizePx = 48f)
    {
        popupText.text = text; popupText.color = c; popupText.fontSize = sizePx; popupTimer = 0.8f;
    }

    private void BuildUI()
    {
        ui = UIBuilder.CreateOverlayCanvas("CuttingUI3D", null, 120);
        titleText = Text("Title", "", 44, new Vector2(0f, 470f), FontStyles.Bold);
        progressText = Text("Progress", "", 28, new Vector2(0f, 420f), FontStyles.Normal);
        popupText = Text("Popup", "", 48, new Vector2(0f, 340f), FontStyles.Bold);
        popupText.outlineWidth = 0.2f; popupText.outlineColor = new Color32(0, 0, 0, 255);
        hintText = Text("Hint", "", 26, new Vector2(0f, -440f), FontStyles.Italic);
        hintText.color = new Color(1f, 0.92f, 0.6f);
        var cancel = UIBuilder.CreateButton("Cancel", ui.transform, "Batal (Esc)", new Color(0.35f, 0.35f, 0.35f), Cancel, 24);
        UIBuilder.Place((RectTransform)cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(240f, 60f));
        Cursor.visible = knife == null; // pisau 3D menggantikan kursor
        Cursor.lockState = CursorLockMode.None;
    }

    private TextMeshProUGUI Text(string name, string text, float size, Vector2 pos, FontStyles style)
    {
        var t = UIBuilder.CreateText(name, ui.transform, text, size, TextAlignmentOptions.Center, style);
        t.outlineWidth = 0.15f; t.outlineColor = new Color32(0, 0, 0, 200);
        UIBuilder.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(1400f, 60f));
        return t;
    }
}
