using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Minigame MERACIK 3D. Pasang di objek Meja Racik (yang punya AssemblyStation).
/// Saat E ditekan: kamera mendekat, mangkuk + wadah bahan + botol dressing muncul di meja.
/// - Klik tahan wadah, seret ke atas mangkuk, lepas: potongan bahan jatuh ke mangkuk dengan fisika.
/// - Klik botol, lalu tahan dan seret mengikuti garis zigzag: dressing tertuang dan menempel di salad.
/// </summary>
public class AssemblyMinigame3D : MonoBehaviour
{
    [Header("Model")]
    public GameObject bowlPrefab;           // Mangkuk
    public GameObject containerPrefab;      // WadahKecil
    public float bowlScale = 0.35f;
    public float containerScale = 0.35f;
    public float bottleScale = 0.3f;
    [Tooltip("Pengali ukuran potongan bahan (dikalikan Model Scale di asset bahan)")]
    public float pieceScale = 0.3f;
    [Tooltip("Ukuran mangkuk, wadah, botol, dan potongan otomatis disesuaikan dengan meja (abaikan semua Scale di atas)")]
    public bool autoFit = true;
    [Range(0.2f, 0.8f)] public float bowlToTable = 0.45f;   // diameter mangkuk dibanding kedalaman meja
    [Range(0.1f, 1f)] public float pieceToBowl = 0.4f;      // ukuran potongan dibanding jari-jari mangkuk
    [Tooltip("Objek di meja yang disembunyikan selama minigame (mis. MediumBowl_02d di scene)")]
    public GameObject[] hideDuringMinigame;

    [Header("Posisi (kosong = otomatis di atas meja)")]
    public Transform workPoint;
    public float cameraHeight = 1.5f;
    public float cameraBack = 1.0f;
    [Tooltip("Kamera otomatis mengambil jarak supaya mangkuk, wadah, dan botol terlihat semua")]
    public bool autoCamera = true;
    [Range(35f, 89f)] public float cameraPitch = 60f;
    [Range(0.6f, 2f)] public float cameraPadding = 1.0f;

    [Header("Bahan")]
    public int piecesPerIngredient = 3;
    [Tooltip("Ketinggian jatuh potongan di atas mangkuk (dikali jari-jari mangkuk)")]
    public float dropHeight = 1.2f;

    [Header("Dressing zigzag (perbandingan terhadap jari-jari mangkuk)")]
    public float drizzleWidth = 0.06f;
    public float followRatio = 0.25f;
    public float startRatio = 0.25f;

    public static bool IsActive { get; private set; }

    // ------------------------------------------------------------------ status
    private AssemblyStation station;
    private Camera cam;
    private GameObject root, bowl;
    private Vector3 bowlCenter, right, fwd;
    private float bowlRadius, bowlTop;

    private class Slot { public GameObject go; public IngredientData item; public Vector3 home; }
    private readonly List<Slot> containers = new List<Slot>();
    private readonly List<Slot> bottles = new List<Slot>();
    private readonly List<GameObject> pieces = new List<GameObject>();
    private readonly List<(Rigidbody rb, float until)> settling = new List<(Rigidbody, float)>();

    private enum Mode { None, Ingredient, Bottle }
    private Mode mode = Mode.None;
    private Slot heldSlot;
    private GameObject held;

    // dressing
    private readonly List<Vector2> path = new List<Vector2>();     // titik zigzag relatif terhadap tengah mangkuk (right, fwd)
    private float pathLength, progress;
    private bool tracing;
    private LineRenderer guide, drizzle, stream;
    private GameObject startMarker;
    private readonly List<Vector2> drawn = new List<Vector2>();
    private readonly List<GameObject> seeds = new List<GameObject>();
    private static readonly Dictionary<AssemblyStation, List<Vector2>> savedDrizzle = new Dictionary<AssemblyStation, List<Vector2>>();

    // UI
    private GameObject ui;
    private TextMeshProUGUI statusText, bowlText, ticketText;

    // ------------------------------------------------------------------ mulai / selesai

    public void Begin(AssemblyStation s)
    {
        station = s;
        cam = Minigame3DUtil.GetCamera();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Debug.Log($"[Minigame3D] Mulai meracik di {name}");
        if (bowlPrefab == null) Debug.LogWarning("[Minigame3D] Bowl Prefab kosong (isi Mangkuk)");
        if (containerPrefab == null) Debug.LogWarning("[Minigame3D] Container Prefab kosong (isi WadahKecil)");
        IsActive = true;
        UIState.SetUIOpen(true);

        if (station.bowlVisual != null) station.bowlVisual.SetActive(false);   // diganti mangkuk 3D
        root = new GameObject("AssemblyMinigame3D");
        Bounds table = Minigame3DUtil.TableBounds(gameObject);
        Vector3 work = workPoint != null ? workPoint.position : new Vector3(table.center.x, table.max.y, table.center.z);
        if (hideDuringMinigame != null) foreach (var h in hideDuringMinigame) if (h != null) h.SetActive(false);

        Vector3 toward = PlayerDirection(work);
        fwd = -toward.normalized;                       // menjauhi kamera
        right = Vector3.Cross(Vector3.up, fwd);

        // mangkuk
        bowl = Minigame3DUtil.Spawn(bowlPrefab, work, Quaternion.LookRotation(fwd), bowlScale, root.transform);
        if (bowl != null)
        {
            if (autoFit) Minigame3DUtil.FitSize(bowl, Mathf.Min(table.size.x, table.size.z) * bowlToTable, true);
            Minigame3DUtil.MakeDoubleSided(bowl);
            work += fwd * Mathf.Min(table.size.x, table.size.z) * bowlToTable * 0.25f;   // agak ke belakang, beri ruang untuk wadah
            Minigame3DUtil.PlaceOnSurface(bowl, work);
            Bounds bb = Minigame3DUtil.GetBounds(bowl);
            bowlRadius = Mathf.Max(bb.extents.x, bb.extents.z);
            bowlTop = bb.max.y;
            bowlCenter = new Vector3(bb.center.x, bb.min.y, bb.center.z);
            foreach (var mf in bowl.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;   // agar potongan bisa jatuh ke dalam
        }
        else
        {
            bowlRadius = 0.3f; bowlTop = work.y + 0.15f; bowlCenter = work;
        }

        // kamera close-up
        Vector3 cp; Quaternion cr;
        if (autoCamera)
        {
            Vector3 center = bowlCenter - fwd * bowlRadius * 0.6f + right * bowlRadius * 0.5f;
            Minigame3DUtil.FramePose(cam, center, toward, bowlRadius * 2.6f, cameraPitch, cameraPadding, out cp, out cr);
        }
        else
        {
            Vector3 look = bowlCenter - fwd * bowlRadius * 0.9f;
            Minigame3DUtil.CloseUpPose(look, toward, cameraHeight, cameraBack, out cp, out cr);
        }
        var focus = CameraFocus.Get();
        if (focus != null) focus.FocusOn(cp, cr);

        BuildUI();
        RebuildTable();
        RespawnBowlContents();
        statusText.text = "Seret wadah bahan ke mangkuk. Klik botol dressing, lalu tuang mengikuti garis zigzag.";
        RefreshTexts();
    }

    public void End()
    {
        IsActive = false;
        mode = Mode.None;
        if (root != null) Destroy(root);
        if (ui != null) Destroy(ui);
        containers.Clear(); bottles.Clear(); pieces.Clear(); settling.Clear(); seeds.Clear();
        var focus = CameraFocus.Get();
        if (focus != null) focus.Release();
        UIState.SetUIOpen(false);
        CancelInvoke();
        if (hideDuringMinigame != null) foreach (var h in hideDuringMinigame) if (h != null) h.SetActive(true);
        if (station != null) station.UpdateVisual();
        station = null;
    }

    // ------------------------------------------------------------------ meja: wadah & botol

    private void RebuildTable()
    {
        foreach (var c in containers) if (c.go != null) Destroy(c.go);
        foreach (var b in bottles) if (b.go != null) Destroy(b.go);
        containers.Clear(); bottles.Clear();

        var inv = InventoryManager.Instance;
        if (inv == null) return;

        var bahan = new List<IngredientData>();
        var dressing = new List<IngredientData>();
        foreach (var item in inv.GetAllItems())
        {
            if (item == null || inv.GetCount(item) <= 0) continue;
            if (item.isDressing) dressing.Add(item);
            else if (item.canGoInBowl) bahan.Add(item);
        }

        // wadah bahan berjajar di depan mangkuk (lebih dekat ke kamera)
        float spacing = bowlRadius * 1.05f;
        for (int i = 0; i < bahan.Count; i++)
        {
            float off = (i - (bahan.Count - 1) / 2f) * spacing;
            Vector3 pos = bowlCenter - fwd * bowlRadius * 1.75f + right * off;
            GameObject c = Minigame3DUtil.Spawn(containerPrefab, pos, Quaternion.LookRotation(fwd), containerScale, root.transform);
            if (autoFit) Minigame3DUtil.FitSize(c, bowlRadius * 0.9f, true);
            Minigame3DUtil.MakeDoubleSided(c);
            if (c == null) c = new GameObject("Wadah");
            c.transform.SetParent(root.transform, true);
            Minigame3DUtil.PlaceOnSurface(c, new Vector3(pos.x, bowlCenter.y, pos.z));
            FillContainer(c, bahan[i]);
            containers.Add(new Slot { go = c, item = bahan[i], home = c.transform.position });
        }

        // botol dressing di kanan mangkuk
        for (int i = 0; i < dressing.Count; i++)
        {
            Vector3 pos = bowlCenter + right * bowlRadius * 1.9f + fwd * (i - (dressing.Count - 1) / 2f) * bowlRadius * 0.9f;
            GameObject b = Minigame3DUtil.Spawn(dressing[i].model3D, pos, Quaternion.LookRotation(fwd), bottleScale * dressing[i].modelScale, root.transform);
            if (autoFit) Minigame3DUtil.FitSize(b, bowlRadius * 1.5f);
            if (b == null) b = Minigame3DUtil.Marker(dressing[i].displayName, root.transform, pos, bowlRadius * 0.5f, DressingColor(dressing[i]));
            Minigame3DUtil.PlaceOnSurface(b, new Vector3(pos.x, bowlCenter.y, pos.z));
            bottles.Add(new Slot { go = b, item = dressing[i], home = b.transform.position });
        }
    }

    private GameObject SpawnPiece(IngredientData item, Vector3 pos, Quaternion rot, float frac, Transform parent)
    {
        GameObject go = Minigame3DUtil.Spawn(item.model3D, pos, rot, pieceScale * item.modelScale, parent);
        if (go == null) return null;
        if (autoFit) Minigame3DUtil.FitSize(go, bowlRadius * frac);
        Minigame3DUtil.MakeDoubleSided(go);
        return go;
    }

    private void FillContainer(GameObject c, IngredientData item)
    {
        if (item.model3D == null) return;
        Bounds b = Minigame3DUtil.GetBounds(c);
        var rnd = new System.Random(item.name.GetHashCode());
        for (int k = 0; k < 3; k++)
        {
            Vector3 p = new Vector3(b.center.x + (float)(rnd.NextDouble() - 0.5) * b.extents.x * 0.6f,
                                    b.min.y + b.size.y * 0.35f + k * b.size.y * 0.1f,
                                    b.center.z + (float)(rnd.NextDouble() - 0.5) * b.extents.z * 0.6f);
            SpawnPiece(item, p, Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f), pieceToBowl * 0.7f, c.transform);
        }
    }

    // ------------------------------------------------------------------ input

    void Update()
    {
        if (!IsActive || root == null) return;

        // potongan yang sudah tenang dibekukan agar tidak bergetar
        for (int i = settling.Count - 1; i >= 0; i--)
            if (Time.time >= settling[i].until)
            {
                if (settling[i].rb != null) settling[i].rb.isKinematic = true;
                settling.RemoveAt(i);
            }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (mode == Mode.Bottle) { CancelPour("Batal menuang dressing"); return; }
            if (mode == Mode.Ingredient) { DropHeld(false); return; }
            End(); return;
        }

        switch (mode)
        {
            case Mode.None: UpdateIdle(); break;
            case Mode.Ingredient: UpdateIngredient(); break;
            case Mode.Bottle: UpdatePour(); break;
        }
    }

    private void UpdateIdle()
    {
        // efek sorot saat mouse di atas wadah / botol
        foreach (var s in containers) Hover(s);
        foreach (var s in bottles) Hover(s);

        if (!Input.GetMouseButtonDown(0)) return;
        var inv = InventoryManager.Instance;

        foreach (var s in containers)
        {
            if (!Minigame3DUtil.MouseOver(cam, s.go)) continue;
            if (inv.GetCount(s.item) <= 0) { statusText.text = $"{s.item.displayName} habis"; return; }
            if (station.Bowl.IsFull) { statusText.text = $"Mangkuk penuh (maksimal {SaladBowl.MaxIngredients} bahan)"; return; }
            heldSlot = s;
            held = SpawnPiece(s.item, s.home, Quaternion.identity, pieceToBowl, root.transform);
            if (held == null) held = Minigame3DUtil.Marker("Held", root.transform, s.home, bowlRadius * 0.3f, new Color(0.55f, 0.8f, 0.45f));
            mode = Mode.Ingredient;
            return;
        }

        foreach (var s in bottles)
        {
            if (!Minigame3DUtil.MouseOver(cam, s.go)) continue;
            if (station.Bowl.dressing != null) { statusText.text = $"Mangkuk sudah berisi {station.Bowl.dressing.displayName}"; return; }
            if (inv.GetCount(s.item) <= 0) { statusText.text = $"{s.item.displayName} habis"; return; }
            heldSlot = s;
            StartPour();
            return;
        }
    }

    private void Hover(Slot s)
    {
        if (s.go == null) return;
        bool over = Minigame3DUtil.MouseOver(cam, s.go);
        Vector3 target = s.home + Vector3.up * (over ? bowlRadius * 0.08f : 0f);
        s.go.transform.position = Vector3.Lerp(s.go.transform.position, target, 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime));
    }

    // ---- bahan

    private void UpdateIngredient()
    {
        if (Minigame3DUtil.MouseOnPlane(cam, bowlTop + bowlRadius * 0.3f, out Vector3 p) && held != null)
            held.transform.position = Vector3.Lerp(held.transform.position, p, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));

        if (Input.GetMouseButtonUp(0)) DropHeld(true);
    }

    private void DropHeld(bool tryAdd)
    {
        Vector3 pos = held != null ? held.transform.position : bowlCenter;
        if (held != null) Destroy(held);
        held = null;
        mode = Mode.None;

        if (!tryAdd || heldSlot == null) return;
        Vector3 flat = pos - bowlCenter; flat.y = 0f;
        if (flat.magnitude > bowlRadius * 0.95f) { statusText.text = "Lepaskan bahan di atas mangkuk"; return; }

        IngredientData item = heldSlot.item;
        if (station.TryAdd(item, out string msg))
        {
            DropPieceIntoBowl(item, pos);
            for (int i = 1; i < Mathf.Max(1, piecesPerIngredient); i++)
                DropPieceIntoBowl(item, RandomInBowl(0.6f, i));
        }
        statusText.text = msg;
        RebuildTable();
        RefreshTexts();
    }

    private Vector3 RandomInBowl(float spread, int i)
    {
        float a = Random.value * Mathf.PI * 2f;
        float r = Mathf.Sqrt(Random.value) * bowlRadius * spread;
        return bowlCenter + right * Mathf.Cos(a) * r + fwd * Mathf.Sin(a) * r + Vector3.up * (bowlTop - bowlCenter.y + i * bowlRadius * 0.15f);
    }

    private void DropPieceIntoBowl(IngredientData item, Vector3 at)
    {
        Vector3 flat = at - bowlCenter; flat.y = 0f;
        if (flat.magnitude > bowlRadius * 0.55f) flat = flat.normalized * bowlRadius * 0.55f;
        Vector3 start = bowlCenter + flat + Vector3.up * (bowlTop - bowlCenter.y + bowlRadius * dropHeight * Random.Range(0.8f, 1.2f));

        GameObject piece = SpawnPiece(item, start, Random.rotation, pieceToBowl, root.transform);
        if (piece == null) piece = Minigame3DUtil.Marker(item.displayName, root.transform, start, bowlRadius * 0.25f, new Color(0.55f, 0.8f, 0.45f));
        foreach (var mf in piece.GetComponentsInChildren<MeshFilter>())
            if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<BoxCollider>();
        var rb = piece.AddComponent<Rigidbody>();
        rb.mass = 0.05f; rb.linearDamping = 0.5f; rb.angularDamping = 0.8f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        pieces.Add(piece);
        settling.Add((rb, Time.time + 3f));
    }

    private void RespawnBowlContents()
    {
        foreach (var p in pieces) if (p != null) Destroy(p);
        pieces.Clear(); settling.Clear();
        int k = 0;
        foreach (var item in station.Bowl.ingredients)
            for (int i = 0; i < Mathf.Max(1, piecesPerIngredient); i++)
                DropPieceIntoBowl(item, RandomInBowl(0.55f, k++));

        if (station.Bowl.dressing != null && savedDrizzle.TryGetValue(station, out var pts))
        {
            drawn.Clear(); drawn.AddRange(pts);
            Invoke(nameof(RedrawSavedDrizzle), 1.5f);   // tunggu potongan jatuh dulu
        }
    }

    private void RedrawSavedDrizzle()
    {
        if (!IsActive || station == null || station.Bowl.dressing == null) return;
        drizzle = Minigame3DUtil.CreateLine("Drizzle", root.transform, DressingColor(station.Bowl.dressing), bowlRadius * drizzleWidth);
        drizzle.positionCount = drawn.Count;
        for (int i = 0; i < drawn.Count; i++) drizzle.SetPosition(i, OnSurface(drawn[i]));
        if (IsSesame(station.Bowl.dressing)) SprinkleSeeds();
    }

    // ---- dressing zigzag

    private void StartPour()
    {
        mode = Mode.Bottle;
        tracing = false;
        progress = 0f;
        drawn.Clear();
        heldSlot.go.SetActive(false);
        held = Minigame3DUtil.Spawn(heldSlot.item.model3D, heldSlot.home, Quaternion.identity, bottleScale * heldSlot.item.modelScale, root.transform);
        if (autoFit) Minigame3DUtil.FitSize(held, bowlRadius * 1.5f);
        if (held == null) held = Minigame3DUtil.Marker("Bottle", root.transform, heldSlot.home, bowlRadius * 0.4f, DressingColor(heldSlot.item));

        path.Clear();
        float x = bowlRadius * 0.55f, top = bowlRadius * 0.45f;
        for (int i = 0; i < 6; i++) path.Add(new Vector2(i % 2 == 0 ? -x : x, Mathf.Lerp(top, -top, i / 5f)));
        pathLength = 0f;
        for (int i = 1; i < path.Count; i++) pathLength += Vector2.Distance(path[i - 1], path[i]);

        guide = Minigame3DUtil.CreateLine("ZigzagGuide", root.transform, new Color(1f, 1f, 1f, 0.95f), bowlRadius * 0.03f, true);
        int samples = 60;
        guide.positionCount = samples + 1;
        for (int i = 0; i <= samples; i++) guide.SetPosition(i, OnSurface(PointAt(pathLength * i / samples)) + Vector3.up * bowlRadius * 0.02f);
        guide.material.mainTextureScale = new Vector2(pathLength / (bowlRadius * 0.08f), 1f);

        startMarker = Minigame3DUtil.Marker("Start", root.transform, OnSurface(path[0]), bowlRadius * 0.14f, new Color(0.3f, 0.85f, 0.4f));
        drizzle = Minigame3DUtil.CreateLine("Drizzle", root.transform, DressingColor(heldSlot.item), bowlRadius * drizzleWidth);
        stream = Minigame3DUtil.CreateLine("Stream", root.transform, DressingColor(heldSlot.item), bowlRadius * drizzleWidth * 0.5f);

        statusText.text = $"Tahan klik di titik hijau, lalu tuang {heldSlot.item.displayName} mengikuti zigzag. Esc / klik kanan = batal";
    }

    private void UpdatePour()
    {
        if (!Minigame3DUtil.MouseOnPlane(cam, bowlTop + bowlRadius * 0.15f, out Vector3 p)) return;
        Vector2 local = ToLocal(p);
        bool holding = Input.GetMouseButton(0);

        // botol mengikuti mouse, miring saat menuang
        if (held != null)
        {
            Vector3 hp = p + Vector3.up * bowlRadius * 0.9f;
            held.transform.position = Vector3.Lerp(held.transform.position, hp, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            Quaternion tilt = Quaternion.LookRotation(fwd) * Quaternion.Euler(tracing && holding ? 150f : 15f, 0f, 0f);
            held.transform.rotation = Quaternion.Slerp(held.transform.rotation, tilt, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }

        Vector2 current = PointAt(progress);
        if (startMarker != null)
        {
            startMarker.transform.position = OnSurface(current) + Vector3.up * bowlRadius * 0.03f;
            startMarker.transform.localScale = Vector3.one * bowlRadius * 0.14f * (1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.15f);
        }

        if (Input.GetMouseButtonDown(1)) { CancelPour("Batal menuang dressing"); return; }

        if (Input.GetMouseButtonDown(0))
        {
            if ((local - current).magnitude <= bowlRadius * startRatio) tracing = true;
            else statusText.text = "Mulai menuang dari titik hijau";
        }
        if (tracing && Input.GetMouseButtonUp(0))
        {
            tracing = false;
            statusText.text = "Lanjutkan dari titik hijau sampai garis selesai";
        }

        bool pouring = tracing && holding;
        stream.positionCount = pouring ? 2 : 0;
        if (!pouring) return;

        float s = ProjectOnPath(local, progress, out float dist);
        if (dist > bowlRadius * followRatio)
        {
            tracing = false;
            statusText.text = "Terlalu jauh dari garis, kembali ke titik hijau";
            return;
        }
        if (s > progress && s - progress < bowlRadius * 0.8f) progress = s;

        Vector3 hit = OnSurface(local);
        stream.SetPosition(0, p + Vector3.up * bowlRadius * 0.6f);
        stream.SetPosition(1, hit);

        if (drawn.Count == 0 || (drawn[drawn.Count - 1] - local).magnitude > bowlRadius * 0.04f)
        {
            drawn.Add(local);
            drizzle.positionCount = drawn.Count;
            drizzle.SetPosition(drawn.Count - 1, hit + Vector3.up * bowlRadius * 0.01f);
        }

        statusText.text = $"Menuang {heldSlot.item.displayName}... {Mathf.Min(100, Mathf.FloorToInt(progress / pathLength * 100f))}%";
        if (progress >= pathLength * 0.97f) FinishPour();
    }

    private void FinishPour()
    {
        IngredientData dressing = heldSlot.item;
        if (station.TryAdd(dressing, out string msg))
        {
            savedDrizzle[station] = new List<Vector2>(drawn);
            if (IsSesame(dressing)) SprinkleSeeds();
            if (guide != null) Destroy(guide.gameObject);
            if (startMarker != null) Destroy(startMarker);
            if (stream != null) Destroy(stream.gameObject);
            if (held != null) Destroy(held);
            mode = Mode.None; tracing = false;
            statusText.text = msg;
            RebuildTable();
            RefreshTexts();
        }
        else CancelPour(msg);
    }

    private void CancelPour(string message)
    {
        if (guide != null) Destroy(guide.gameObject);
        if (startMarker != null) Destroy(startMarker);
        if (stream != null) Destroy(stream.gameObject);
        if (drizzle != null && (station == null || station.Bowl.dressing == null)) Destroy(drizzle.gameObject);
        if (held != null) Destroy(held);
        if (heldSlot != null && heldSlot.go != null) heldSlot.go.SetActive(true);
        drawn.Clear();
        mode = Mode.None; tracing = false;
        statusText.text = message;
    }

    private void SprinkleSeeds()
    {
        var rnd = new System.Random(5);
        for (int i = 0; i < drawn.Count; i += 2)
        {
            Vector2 j = drawn[i] + new Vector2((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * bowlRadius * 0.05f;
            Color c = rnd.NextDouble() < 0.65 ? new Color(0.98f, 0.95f, 0.86f) : new Color(0.22f, 0.17f, 0.13f);
            seeds.Add(Minigame3DUtil.Marker("Seed", root.transform, OnSurface(j) + Vector3.up * bowlRadius * 0.025f, bowlRadius * 0.025f, c));
        }
    }

    // ------------------------------------------------------------------ geometri jalur

    private Vector2 ToLocal(Vector3 world)
    {
        Vector3 d = world - bowlCenter;
        return new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, fwd));
    }

    /// <summary>Titik di permukaan isi mangkuk (salad / dasar mangkuk) di bawah posisi lokal.</summary>
    private Vector3 OnSurface(Vector2 local)
    {
        Vector3 above = bowlCenter + right * local.x + fwd * local.y + Vector3.up * (bowlTop - bowlCenter.y + bowlRadius);
        if (Physics.Raycast(above, Vector3.down, out RaycastHit hit, bowlRadius * 4f))
            return hit.point + Vector3.up * bowlRadius * 0.01f;
        return new Vector3(above.x, bowlCenter.y + (bowlTop - bowlCenter.y) * 0.3f, above.z);
    }

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
            if (sHere >= around - bowlRadius * 0.1f && sHere <= around + bowlRadius * 1.2f && d < bestDist) { bestDist = d; best = sHere; }
            acc += seg;
        }
        distance = bestDist;
        return best;
    }

    // ------------------------------------------------------------------ tombol

    private void OnClear()
    {
        if (mode != Mode.None) return;
        station.ClearBowl();
        savedDrizzle.Remove(station);
        foreach (var p in pieces) if (p != null) Destroy(p);
        pieces.Clear(); settling.Clear();
        if (drizzle != null) Destroy(drizzle.gameObject);
        foreach (var s in seeds) if (s != null) Destroy(s);
        seeds.Clear();
        statusText.text = "Mangkuk dikosongkan, bahan kembali ke inventory";
        RebuildTable();
        RefreshTexts();
    }

    private void OnTake()
    {
        if (mode != Mode.None) return;
        if (station.TryTakeBowl(out string msg))
        {
            savedDrizzle.Remove(station);
            if (NotificationUI.Instance != null) NotificationUI.Instance.Show(msg);
            End();
        }
        else statusText.text = msg;
    }

    // ------------------------------------------------------------------ bantuan

    private Vector3 PlayerDirection(Vector3 work)
    {
        var player = FindFirstObjectByType<PlayerMovement>();
        Vector3 d = player != null ? player.transform.position - work : -transform.forward;
        d.y = 0f;
        if (d.sqrMagnitude < 0.001f) d = -transform.forward;
        return d;
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

    private void RefreshTexts()
    {
        if (station == null) return;
        bowlText.text = $"<b>{station.Bowl.Describe()}</b>   <size=80%>({station.Bowl.ingredients.Count}/{SaladBowl.MaxIngredients} bahan)</size>";
        string ticket = "<b>PESANAN</b>\n<size=85%>Belum ada pelanggan yang menunggu</size>";
        foreach (var c in FindObjectsByType<Customer>(FindObjectsSortMode.None))
            if (c.IsWaiting && c.Order != null) { ticket = $"<b>PESANAN</b>\n<b>{c.Order.orderName}</b>\n<size=85%>{c.Order.Describe()}</size>"; break; }
        ticketText.text = ticket;
    }

    private void BuildUI()
    {
        ui = UIBuilder.CreateOverlayCanvas("AssemblyUI3D", null, 110);

        var title = UIBuilder.CreateText("Title", ui.transform, "MEJA RACIK", 44, TextAlignmentOptions.Center, FontStyles.Bold);
        title.outlineWidth = 0.2f; title.outlineColor = new Color32(0, 0, 0, 220);
        UIBuilder.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 60f));

        var ticketBg = UIBuilder.CreateImage("Ticket", ui.transform, new Color(1f, 0.99f, 0.93f, 0.95f));
        UIBuilder.Place(ticketBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(330f, 260f));
        ticketText = UIBuilder.CreateText("TicketText", ticketBg.transform, "", 24, TextAlignmentOptions.TopLeft);
        ticketText.color = new Color(0.2f, 0.15f, 0.1f);
        UIBuilder.Stretch(ticketText.rectTransform, 16f);

        var barBg = UIBuilder.CreateImage("Bar", ui.transform, new Color(0f, 0f, 0f, 0.55f));
        UIBuilder.Place(barBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1200f, 110f));
        bowlText = UIBuilder.CreateText("BowlText", barBg.transform, "", 26, TextAlignmentOptions.Center);
        UIBuilder.Place(bowlText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1160f, 40f));
        statusText = UIBuilder.CreateText("Status", barBg.transform, "", 22, TextAlignmentOptions.Center, FontStyles.Italic);
        statusText.color = new Color(1f, 0.92f, 0.6f);
        UIBuilder.Place(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(1160f, 40f));

        Button(" Selesai & Bawa", new Color(0.20f, 0.50f, 0.78f), OnTake, new Vector2(-30f, 230f));
        Button("Kosongkan", new Color(0.72f, 0.28f, 0.24f), OnClear, new Vector2(-30f, 150f));
        Button("Tutup (Esc)", new Color(0.40f, 0.40f, 0.40f), End, new Vector2(-30f, 70f));
    }

    private void Button(string label, Color color, UnityEngine.Events.UnityAction action, Vector2 pos)
    {
        var b = UIBuilder.CreateButton(label, ui.transform, label, color, action, 26);
        UIBuilder.Place((RectTransform)b.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), pos, new Vector2(300f, 66f));
    }
}
