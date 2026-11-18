using System;
using UnityEngine;

/// <summary>
/// Salad (mangkuk) yang sedang dibawa pemain. Pemain hanya bisa membawa satu salad.
/// Pasang di objek Player.
/// </summary>
public class PlayerCarry : MonoBehaviour
{
    public static PlayerCarry Instance { get; private set; }

    [Tooltip("Opsional: objek mangkuk di tangan pemain yang muncul saat membawa salad")]
    public GameObject carryVisual;

    public SaladBowl CarriedSalad { get; private set; }
    public bool IsCarrying => CarriedSalad != null;

    public event Action OnCarryChanged;

    void Awake()
    {
        Instance = this;
        UpdateVisual();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool Pick(SaladBowl salad)
    {
        if (salad == null || IsCarrying) return false;
        CarriedSalad = salad;
        UpdateVisual();
        OnCarryChanged?.Invoke();
        return true;
    }

    public SaladBowl Drop()
    {
        SaladBowl s = CarriedSalad;
        CarriedSalad = null;
        UpdateVisual();
        OnCarryChanged?.Invoke();
        return s;
    }

    private void UpdateVisual()
    {
        if (carryVisual != null) carryVisual.SetActive(IsCarrying);
    }
}
