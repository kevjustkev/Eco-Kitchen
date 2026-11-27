using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fungsi bantu untuk minigame 3D: memunculkan model, menghitung ukuran, posisi mouse di dunia 3D,
/// garis (LineRenderer), dan penanda.
/// </summary>
public static class Minigame3DUtil
{
    private static Material lineMaterial;

    /// <summary>Kamera utama. Kalau kamera tidak ber-tag MainCamera, pakai kamera aktif pertama.</summary>
    public static Camera GetCamera()
    {
        if (Camera.main != null) return Camera.main;
        var c = Object.FindFirstObjectByType<Camera>();
        if (c != null) Debug.LogWarning("[Minigame3D] Tidak ada kamera ber-tag MainCamera. Memakai kamera: " + c.name + ". Sebaiknya set Tag kamera = MainCamera.");
        else Debug.LogError("[Minigame3D] Tidak ada kamera di scene!");
        return c;
    }

    /// <summary>Material tanpa cahaya untuk garis dan penanda (mengikuti warna vertex/warna material).</summary>
    public static Material LineMaterial()
    {
        if (lineMaterial != null) return lineMaterial;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        lineMaterial = new Material(sh);
        return lineMaterial;
    }

    /// <summary>Memunculkan salinan model, membuang semua collider bawaannya.</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale, Transform parent)
    {
        if (prefab == null) return null;
        GameObject go = Object.Instantiate(prefab, position, rotation, parent);
        go.transform.localScale = prefab.transform.localScale * scale;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) Object.Destroy(rb);
        return go;
    }

    /// <summary>Kotak pembatas gabungan semua renderer.</summary>
    public static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// <summary>Menaruh objek supaya dasarnya tepat di ketinggian y dan tengahnya di (x, z).</summary>
    public static void PlaceOnSurface(GameObject go, Vector3 point)
    {
        Bounds b = GetBounds(go);
        Vector3 offset = new Vector3(point.x - b.center.x, point.y - b.min.y, point.z - b.center.z);
        go.transform.position += offset;
    }

    /// <summary>Posisi mouse di bidang datar setinggi y.</summary>
    public static bool MouseOnPlane(Camera cam, float y, out Vector3 point)
    {
        point = Vector3.zero;
        if (cam == null) return false;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        if (!plane.Raycast(ray, out float d)) return false;
        point = ray.GetPoint(d);
        return true;
    }

    /// <summary>Apakah mouse menunjuk objek (dicek dengan kotak pembatas, tanpa collider).</summary>
    public static bool MouseOver(Camera cam, GameObject go)
    {
        if (cam == null || go == null) return false;
        Bounds b = GetBounds(go);
        b.Expand(0.02f);
        return b.IntersectRay(cam.ScreenPointToRay(Input.mousePosition));
    }

    public static LineRenderer CreateLine(string name, Transform parent, Color color, float width, bool dashed = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.material = LineMaterial();
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = width;
        lr.useWorldSpace = true;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.positionCount = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        if (dashed)
        {
            lr.textureMode = LineTextureMode.Tile;
            lr.material = DashMaterial();
        }
        return lr;
    }

    private static Material dashMaterial;

    private static Material DashMaterial()
    {
        if (dashMaterial != null) return dashMaterial;
        var tex = new Texture2D(8, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        for (int x = 0; x < 8; x++) tex.SetPixel(x, 0, x < 4 ? Color.white : new Color(1, 1, 1, 0));
        tex.Apply();
        dashMaterial = new Material(LineMaterial()) { mainTexture = tex };
        return dashMaterial;
    }

    /// <summary>Bola kecil tanpa collider sebagai penanda.</summary>
    public static GameObject Marker(string name, Transform parent, Vector3 position, float size, Color color)
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = name;
        Object.Destroy(s.GetComponent<Collider>());
        s.transform.SetParent(parent, true);
        s.transform.position = position;
        s.transform.localScale = Vector3.one * size;
        var r = s.GetComponent<Renderer>();
        r.material = new Material(LineMaterial()) { color = color };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return s;
    }

    public static void DestroyAll(List<GameObject> list)
    {
        foreach (var o in list) if (o != null) Object.Destroy(o);
        list.Clear();
    }

    /// <summary>Posisi kamera close-up: di sisi pemain, di atas titik kerja, menghadap ke bawah.</summary>
    public static void CloseUpPose(Vector3 workPoint, Vector3 towardViewer, float height, float back, out Vector3 pos, out Quaternion rot)
    {
        towardViewer.y = 0f;
        if (towardViewer.sqrMagnitude < 0.001f) towardViewer = Vector3.back;
        towardViewer.Normalize();
        pos = workPoint + Vector3.up * height + towardViewer * back;
        rot = Quaternion.LookRotation((workPoint - pos).normalized, Vector3.up);
    }

    /// <summary>Mengubah skala objek supaya sisi terbesarnya (atau lebar horizontal) = size.</summary>
    public static void FitSize(GameObject go, float size, bool horizontalOnly = false)
    {
        if (go == null || size <= 0f) return;
        Bounds b = GetBounds(go);
        float have = horizontalOnly ? Mathf.Max(b.size.x, b.size.z) : Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (have > 0.00001f) go.transform.localScale *= size / have;
    }

    /// <summary>Menampilkan kedua sisi permukaan (untuk model yang normalnya terbalik sehingga terlihat tembus).</summary>
    public static void MakeDoubleSided(GameObject go)
    {
        if (go == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            foreach (var m in r.materials)            // salinan material, aset asli tidak berubah
            {
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                m.doubleSidedGI = true;
            }
    }

    /// <summary>Posisi kamera supaya area berjari-jari radius di sekitar center terlihat utuh.</summary>
    public static void FramePose(Camera cam, Vector3 center, Vector3 towardViewer, float radius, float pitchDeg, float padding,
                                 out Vector3 pos, out Quaternion rot)
    {
        towardViewer.y = 0f;
        if (towardViewer.sqrMagnitude < 0.001f) towardViewer = Vector3.back;
        towardViewer.Normalize();
        float vfov = cam != null ? cam.fieldOfView : 60f;
        float aspect = cam != null ? Mathf.Max(0.5f, cam.aspect) : 16f / 9f;
        float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov * Mathf.Deg2Rad / 2f) * aspect) * Mathf.Rad2Deg;
        float half = Mathf.Min(vfov, hfov) * 0.5f * Mathf.Deg2Rad;
        float dist = radius * padding / Mathf.Sin(half);
        float p = pitchDeg * Mathf.Deg2Rad;
        pos = center + (towardViewer * Mathf.Cos(p) + Vector3.up * Mathf.Sin(p)) * dist;
        rot = Quaternion.LookRotation((center - pos).normalized, Vector3.up);
        if (cam != null && cam.nearClipPlane > dist * 0.2f) cam.nearClipPlane = Mathf.Max(0.01f, dist * 0.05f);
    }

    /// <summary>Putaran supaya sumbu terpanjang model (mis. pisau) mengarah ke depan (Z).</summary>
    public static Vector3 LongAxisToForward(GameObject go)
    {
        if (go == null) return Vector3.zero;
        Quaternion old = go.transform.rotation;
        go.transform.rotation = Quaternion.identity;
        Bounds b = GetBounds(go);
        go.transform.rotation = old;
        if (b.size.y >= b.size.x && b.size.y >= b.size.z) return new Vector3(90f, 0f, 0f);
        if (b.size.x >= b.size.z) return new Vector3(0f, 90f, 0f);
        return Vector3.zero;
    }

    /// <summary>Titik kerja otomatis: permukaan atas renderer terbesar (alas meja) di objek ini.</summary>
    public static Vector3 TableTop(GameObject station)
    {
        Bounds b = TableBounds(station);
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    /// <summary>Kotak pembatas renderer terbesar (alas meja) di objek ini.</summary>
    public static Bounds TableBounds(GameObject station)
    {
        Renderer best = null; float bestArea = 0f;
        foreach (var r in station.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r is LineRenderer) continue;
            float area = r.bounds.size.x * r.bounds.size.z;
            if (area > bestArea) { bestArea = area; best = r; }
        }
        if (best != null) return best.bounds;
        Vector3 t = TopCenter(station);
        return new Bounds(t - Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));
    }

    /// <summary>Titik kerja otomatis: tengah permukaan atas collider/renderer meja.</summary>
    public static Vector3 TopCenter(GameObject station)
    {
        // pakai collider padat (bukan trigger) dulu, karena trigger area interaksi biasanya jauh lebih besar dari meja
        foreach (var col in station.GetComponentsInChildren<Collider>())
            if (!col.isTrigger && col.enabled)
            {
                Bounds cb = col.bounds;
                return new Vector3(cb.center.x, cb.max.y, cb.center.z);
            }
        Bounds b = GetBounds(station);
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }
}
