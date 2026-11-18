using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Menyimpan semua item yang dibawa pemain.
/// Kunci inventory adalah IngredientData (bukan teks), jadi tidak ada lagi
/// masalah salah ketik atau spasi tersembunyi.
/// Pasang di objek Player (satu saja di scene).
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    /// <summary>Dipanggil setiap isi inventory berubah (dipakai oleh UI).</summary>
    public event Action OnInventoryChanged;

    // Dictionary menjaga urutan masuk saat tidak ada penghapusan kunci;
    // urutan tampilan diatur lewat list ini agar konsisten.
    private readonly Dictionary<IngredientData, int> items = new Dictionary<IngredientData, int>();
    private readonly List<IngredientData> order = new List<IngredientData>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Ada lebih dari satu InventoryManager di scene. Komponen duplikat dihapus.");
            Destroy(this); // hapus komponennya saja, bukan seluruh GameObject
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddItem(IngredientData item, int amount = 1)
    {
        if (item == null || amount <= 0) return;

        if (items.ContainsKey(item))
        {
            items[item] += amount;
        }
        else
        {
            items[item] = amount;
            order.Add(item);
        }

        Debug.Log($"+{amount} {item.displayName} (total {items[item]})");
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Mengurangi item. Mengembalikan false jika jumlahnya tidak cukup.</summary>
    public bool RemoveItem(IngredientData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        if (GetCount(item) < amount) return false;

        items[item] -= amount;
        if (items[item] <= 0)
        {
            items.Remove(item);
            order.Remove(item);
        }

        Debug.Log($"-{amount} {item.displayName}");
        OnInventoryChanged?.Invoke();
        return true;
    }

    public int GetCount(IngredientData item)
    {
        if (item == null) return 0;
        return items.TryGetValue(item, out int count) ? count : 0;
    }

    public bool HasItem(IngredientData item, int amount = 1) => GetCount(item) >= amount;

    /// <summary>Daftar item yang sedang dimiliki, sesuai urutan didapat.</summary>
    public IReadOnlyList<IngredientData> GetAllItems() => order;

    public void Clear()
    {
        items.Clear();
        order.Clear();
        selectedIndex = 0;
        OnInventoryChanged?.Invoke();
    }

    // ---------------- Pemilihan item (Tab / scroll mouse) ----------------

    private int selectedIndex = 0;

    /// <summary>Item yang sedang dipilih pemain, atau null jika inventory kosong.</summary>
    public IngredientData SelectedItem
    {
        get
        {
            if (order.Count == 0) return null;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, order.Count - 1);
            return order[selectedIndex];
        }
    }

    /// <summary>Pindah pilihan. direction = 1 (berikutnya) atau -1 (sebelumnya).</summary>
    public void SelectNext(int direction)
    {
        if (order.Count == 0) return;
        selectedIndex = (selectedIndex + direction + order.Count) % order.Count;
        OnInventoryChanged?.Invoke();
    }
}
