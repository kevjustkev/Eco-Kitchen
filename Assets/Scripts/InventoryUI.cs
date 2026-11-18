using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// Menampilkan isi inventory secara otomatis, item yang sedang dipilih (tanda >),
/// dan salad yang sedang dibawa.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public TMP_Text inventoryText;
    public string title = "INVENTORY";

    private bool invSubscribed, carrySubscribed;

    void OnEnable() => TrySubscribe();
    void Start() { TrySubscribe(); Refresh(); }

    void OnDisable()
    {
        if (invSubscribed && InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
        if (carrySubscribed && PlayerCarry.Instance != null)
            PlayerCarry.Instance.OnCarryChanged -= Refresh;
        invSubscribed = carrySubscribed = false;
    }

    private void TrySubscribe()
    {
        if (!invSubscribed && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += Refresh;
            invSubscribed = true;
        }
        if (!carrySubscribed && PlayerCarry.Instance != null)
        {
            PlayerCarry.Instance.OnCarryChanged += Refresh;
            carrySubscribed = true;
        }
    }

    public void Refresh()
    {
        if (inventoryText == null) return;

        var sb = new StringBuilder(title).Append('\n');
        var inv = InventoryManager.Instance;

        if (inv == null || inv.GetAllItems().Count == 0)
        {
            sb.Append("(kosong)\n");
        }
        else
        {
            IngredientData selected = inv.SelectedItem;
            foreach (IngredientData item in inv.GetAllItems())
            {
                sb.Append(item == selected ? "> " : "   ");
                sb.Append(item.displayName).Append(": ").Append(inv.GetCount(item)).Append('\n');
            }
        }

        var carry = PlayerCarry.Instance;
        if (carry != null && carry.IsCarrying)
            sb.Append("\nMembawa salad:\n").Append(carry.CarriedSalad.Describe());

        inventoryText.text = sb.ToString();
    }
}
