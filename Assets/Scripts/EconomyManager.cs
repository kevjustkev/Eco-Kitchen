using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Menyimpan uang dan reputasi restoran. Pasang di objek GameManager.
/// </summary>
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    public int startingMoney = 100000;
    [Range(0, 100)] public int startingReputation = 50;

    public int Money { get; private set; }
    public int Reputation { get; private set; }

    /// <summary>Dipanggil setiap uang atau reputasi berubah.</summary>
    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        Money = startingMoney;
        Reputation = startingReputation;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddMoney(int amount)
    {
        if (amount == 0) return;
        Money += amount;
        OnChanged?.Invoke();
    }

    /// <summary>Membayar sesuatu. Mengembalikan false jika uang tidak cukup.</summary>
    public bool TrySpend(int amount)
    {
        if (amount < 0 || Money < amount) return false;
        Money -= amount;
        OnChanged?.Invoke();
        return true;
    }

    public void ChangeReputation(int delta)
    {
        if (delta == 0) return;
        Reputation = Mathf.Clamp(Reputation + delta, 0, 100);
        OnChanged?.Invoke();
    }

    /// <summary>Format uang: 15000 -> "Rp 15.000"</summary>
    public static string Rupiah(int amount)
    {
        string sign = amount < 0 ? "-" : "";
        return sign + "Rp " + Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
    }
}
