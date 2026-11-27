using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Daftar bagian pakaian karakter ithappy (Creative Characters FREE) untuk diacak.
/// Buat lewat: Project > klik kanan > Create > Eco Kitchen > Character Parts Library.
/// Isinya terisi otomatis dari folder ithappy (atau klik ⋮ > "Isi Otomatis dari Folder ithappy").
/// </summary>
[CreateAssetMenu(fileName = "CharacterPartsLibrary", menuName = "Eco Kitchen/Character Parts Library")]
public class CharacterPartsLibrary : ScriptableObject
{
    [Tooltip("Material untuk semua bagian (Color.mat dari ithappy). Kosong = pakai material bawaan model.")]
    public Material material;

    [Header("Tubuh & wajah")]
    public Mesh[] bodies;
    public Mesh[] faces;

    [Header("Atasan")]
    [Tooltip("Baju terusan (atas + bawah sekaligus)")]
    public Mesh[] outfits;
    [Tooltip("Baju atasan / jaket (dipadukan dengan celana)")]
    public Mesh[] outwear;

    [Header("Bawahan")]
    public Mesh[] pants;
    public Mesh[] shorts;

    [Header("Kaki")]
    public Mesh[] shoes;
    public Mesh[] socks;

    [Header("Kepala")]
    public Mesh[] hairstyles;
    public Mesh[] hairstylesSingle;
    public Mesh[] hats;
    public Mesh[] hatsSingle;

    [Header("Aksesori")]
    public Mesh[] glasses;
    public Mesh[] faceAccessories;
    public Mesh[] gloves;

    [Header("Peluang muncul (0 = tidak pernah, 1 = selalu)")]
    [Range(0f, 1f)] public float outfitChance = 0.3f;     // baju terusan, sisanya atasan + celana
    [Range(0f, 1f)] public float shortsChance = 0.3f;     // celana pendek, sisanya celana panjang
    [Range(0f, 1f)] public float hatChance = 0.3f;        // memakai topi (tanpa rambut)
    [Range(0f, 1f)] public float glassesChance = 0.25f;
    [Range(0f, 1f)] public float accessoryChance = 0.2f;  // kumis, headphone, dll.
    [Range(0f, 1f)] public float glovesChance = 0f;

    [Tooltip("Bagian yang namanya mengandung kata ini tidak akan dipakai")]
    public string[] excludeNames = { "Pacifier", "Clown" };

    /// <summary>Memilih satu mesh acak dari satu atau beberapa daftar (melewati yang dikecualikan).</summary>
    public Mesh Pick(params Mesh[][] lists)
    {
        var candidates = new List<Mesh>();
        foreach (var list in lists)
        {
            if (list == null) continue;
            foreach (var m in list)
                if (m != null && !IsExcluded(m.name)) candidates.Add(m);
        }
        return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
    }

    public bool IsExcluded(string meshName)
    {
        if (excludeNames == null) return false;
        string n = meshName.ToLower();
        foreach (var e in excludeNames)
            if (!string.IsNullOrEmpty(e) && n.Contains(e.ToLower())) return true;
        return false;
    }

#if UNITY_EDITOR
    void Reset() => AutoFill();

    [ContextMenu("Isi Otomatis dari Folder ithappy")]
    public void AutoFill()
    {
        string root = null;
        foreach (var guid in AssetDatabase.FindAssets("Creative_Characters_FREE t:Folder"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith("Creative_Characters_FREE")) { root = p; break; }
        }
        if (root == null)
        {
            Debug.LogError("[CharacterPartsLibrary] Folder Creative_Characters_FREE tidak ditemukan. Sudah import pack ithappy?");
            return;
        }

        material = AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/Color.mat");
        bodies = Load(root, "Body");
        faces = Load(root, "Faces");
        outfits = Load(root, "Outfit");
        outwear = Load(root, "Outwear");
        pants = Load(root, "Pants");
        shorts = Load(root, "Shorts");
        shoes = Load(root, "Shoes");
        socks = Load(root, "Socks");
        hairstyles = Load(root, "Hairstyle");
        hairstylesSingle = Load(root, "Hairstyle Single");
        hats = Load(root, "Hat");
        hatsSingle = Load(root, "Hat Single");
        glasses = Load(root, "Glasses");
        faceAccessories = Load(root, "Face Accessories");
        gloves = Load(root, "Gloves");

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CharacterPartsLibrary] Terisi: {outfits.Length} baju terusan, {outwear.Length} atasan, {pants.Length + shorts.Length} celana, " +
                  $"{shoes.Length} sepatu, {hairstyles.Length + hairstylesSingle.Length} rambut, {hats.Length + hatsSingle.Length} topi");
    }

    private static Mesh[] Load(string root, string folder)
    {
        string path = root + "/Meshes/" + folder;
        var list = new List<Mesh>();
        if (!AssetDatabase.IsValidFolder(path)) return list.ToArray();
        foreach (var guid in AssetDatabase.FindAssets("", new[] { path }))
        {
            string file = AssetDatabase.GUIDToAssetPath(guid);
            if (!file.ToLower().EndsWith(".fbx")) continue;
            if (System.IO.Path.GetDirectoryName(file).Replace('\\', '/') != path) continue;   // jangan ambil subfolder lain
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(file))
                if (o is Mesh m) list.Add(m);
        }
        return list.ToArray();
    }
#endif
}
