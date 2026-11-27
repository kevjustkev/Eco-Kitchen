using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mengacak pakaian karakter ithappy (Base_Mesh) saat muncul.
/// Pasang di model karakter (objek yang berisi anak Body, Faces, Outerwear, Pants, Shoes, Hat, ...).
/// Untuk pelanggan: Randomize On Start dicentang. Untuk player: tidak perlu script ini
/// (pakai karakter hasil simpan dari Character Customization Tool).
/// </summary>
public class CharacterOutfit : MonoBehaviour
{
    public CharacterPartsLibrary library;
    public bool randomizeOnStart = true;
    [Tooltip("Ekspresi wajah awal: usual / happy / angry (kosong = acak)")]
    public string startFace = "usual";

    private static readonly string[] SlotNames =
        { "Accessories", "Body", "Faces", "Full_body", "Glasses", "Gloves", "Hairstyle", "Hat", "Mustache", "Outerwear", "Pants", "Shoes", "T_Shirt" };

    private readonly Dictionary<string, SkinnedMeshRenderer> slots = new Dictionary<string, SkinnedMeshRenderer>();

    void Start()
    {
        if (randomizeOnStart) Randomize();
    }

    [ContextMenu("Acak Sekarang")]
    public void Randomize()
    {
        if (library == null)
        {
            Debug.LogWarning($"[CharacterOutfit] {name}: Library belum diisi.");
            return;
        }
        Collect();
        if (slots.Count == 0)
        {
            Debug.LogWarning($"[CharacterOutfit] {name}: tidak menemukan anak Body/Outerwear/Pants... Pasang script ini di model Base_Mesh.");
            return;
        }

        var L = library;
        foreach (var n in SlotNames)
            if (n != "Body" && n != "Faces") Set(n, null);

        Mesh body = L.Pick(L.bodies);
        if (body != null) Set("Body", body);
        SetFace(startFace);

        // baju: terusan, atau atasan + celana
        if (Random.value < L.outfitChance && L.Pick(L.outfits) != null)
            Set("Outerwear", L.Pick(L.outfits));
        else
        {
            Set("Outerwear", L.Pick(L.outwear, L.outfits));
            bool pendek = Random.value < L.shortsChance && L.Pick(L.shorts) != null;
            Set("Pants", pendek ? L.Pick(L.shorts) : L.Pick(L.pants, L.shorts));
        }

        Set("Shoes", L.Pick(L.shoes) ?? L.Pick(L.socks));

        // kepala: topi (tanpa rambut) atau rambut
        if (Random.value < L.hatChance && L.Pick(L.hats, L.hatsSingle) != null)
            Set("Hat", L.Pick(L.hats, L.hatsSingle));
        else
            Set("Hairstyle", L.Pick(L.hairstyles, L.hairstylesSingle));

        if (Random.value < L.glassesChance) Set("Glasses", L.Pick(L.glasses));
        if (Random.value < L.accessoryChance) Set("Accessories", L.Pick(L.faceAccessories));
        if (Random.value < L.glovesChance) Set("Gloves", L.Pick(L.gloves));
    }

    /// <summary>Ganti ekspresi wajah: "usual", "happy", atau "angry".</summary>
    public void SetFace(string emotion)
    {
        if (library == null || library.faces == null || library.faces.Length == 0) return;
        if (slots.Count == 0) Collect();
        Mesh chosen = null;
        if (!string.IsNullOrEmpty(emotion))
            foreach (var f in library.faces)
                if (f != null && f.name.ToLower().Contains(emotion.ToLower())) { chosen = f; break; }
        if (chosen == null) chosen = library.Pick(library.faces);
        Set("Faces", chosen);
    }

    private void Collect()
    {
        slots.Clear();
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var n in SlotNames)
                if (smr.name.StartsWith(n) && !slots.ContainsKey(n)) { slots[n] = smr; break; }
    }

    private void Set(string slot, Mesh mesh)
    {
        if (!slots.TryGetValue(slot, out var smr) || smr == null) return;
        if (mesh == null)
        {
            smr.enabled = false;
            return;
        }
        smr.sharedMesh = mesh;
        smr.localBounds = mesh.bounds;
        if (library.material != null) smr.sharedMaterial = library.material;
        smr.enabled = true;
    }
}
