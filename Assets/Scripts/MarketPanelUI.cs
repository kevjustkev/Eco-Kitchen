using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel belanja Market + upgrade kebun. Dibuat otomatis lewat kode.
/// Pasang di objek GameManager.
/// </summary>
public class MarketPanelUI : MonoBehaviour
{
    public static MarketPanelUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.market != null;

    private MarketStation market;

    private GameObject root;
    private TextMeshProUGUI moneyText, statusText, upgradeText;
    private Button upgradeButton;
    private RectTransform listRoot;
    private readonly List<GameObject> rows = new List<GameObject>();

    private readonly Color panelColor = new Color(0.10f, 0.12f, 0.10f, 0.97f);
    private readonly Color rowColor = new Color(1f, 1f, 1f, 0.06f);
    private readonly Color buyColor = new Color(0.27f, 0.58f, 0.32f);
    private readonly Color upgradeColor = new Color(0.80f, 0.58f, 0.20f);
    private readonly Color closeColor = new Color(0.35f, 0.35f, 0.35f);

    void Awake()
    {
        Instance = this;
        Build();
        root.SetActive(false);
    }

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Open(MarketStation m)
    {
        market = m;
        root.SetActive(true);
        UIState.SetUIOpen(true);
        statusText.text = "";
        Refresh();
    }

    public void Close()
    {
        market = null;
        root.SetActive(false);
        UIState.SetUIOpen(false);
    }

    // ------------------------------------------------------------------ isi

    private void Refresh()
    {
        if (!IsOpen) return;

        var eco = EconomyManager.Instance;
        moneyText.text = eco != null ? $"Uang: <b>{EconomyManager.Rupiah(eco.Money)}</b>" : "";

        foreach (var r in rows) Destroy(r);
        rows.Clear();

        foreach (var m in market.itemsForSale)
        {
            if (m == null || m.item == null) continue;
            rows.Add(CreateRow(m));
        }

        var garden = GardenManager.Instance;
        if (garden == null)
        {
            upgradeText.text = "Upgrade kebun belum tersedia (GardenManager belum dipasang)";
            upgradeButton.interactable = false;
        }
        else if (garden.IsMaxLevel)
        {
            upgradeText.text = $"Kebun level {garden.Level} ({garden.UnlockedPlots} petak), sudah maksimal";
            upgradeButton.interactable = false;
        }
        else
        {
            upgradeText.text = $"Kebun level {garden.Level} ({garden.UnlockedPlots} petak)  →  " +
                               $"level {garden.Level + 1} ({garden.NextLevelPlots} petak): <b>{EconomyManager.Rupiah(garden.NextUpgradeCost)}</b>";
            upgradeButton.interactable = true;
        }
    }

    private GameObject CreateRow(MarketStation.MarketItem m)
    {
        var row = UIBuilder.CreateImage("Row", listRoot, rowColor);
        var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 8, 8);
        hlg.spacing = 14;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        var rowLe = row.gameObject.AddComponent<LayoutElement>();
        rowLe.preferredHeight = 70;
        rowLe.minHeight = 70;

        IngredientSource src = MarketStation.FindSourceFor(m.item);
        string stock = src == null ? "<color=#EC7063>tidak ada peti</color>"
                     : src.IsInfinite ? "tak terbatas" : $"stok peti: {src.stock}";

        var name = UIBuilder.CreateText("Name", row.transform,
            $"<b>{m.item.displayName}</b>   {EconomyManager.Rupiah(market.GetPrice(m))}\n<size=75%>{stock}</size>",
            26, TextAlignmentOptions.Left);
        name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        AddBuyButton(row.transform, "Beli 1", m, 1);
        AddBuyButton(row.transform, "Beli 5", m, 5);
        return row.gameObject;
    }

    private void AddBuyButton(Transform parent, string label, MarketStation.MarketItem m, int amount)
    {
        var btn = UIBuilder.CreateButton("Buy", parent, label, buyColor, () =>
        {
            market.TryBuy(m, amount, out string msg);
            statusText.text = msg;
            Refresh();
        }, 24);
        var le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 130;
        le.minWidth = 110;
    }

    private void OnUpgrade()
    {
        if (GardenManager.Instance == null) return;
        GardenManager.Instance.TryUpgrade(out string msg);
        statusText.text = msg;
        Refresh();
    }

    // ------------------------------------------------------------------ tampilan

    private void Build()
    {
        root = UIBuilder.CreateOverlayCanvas("MarketCanvas", transform, 100);

        var dim = UIBuilder.CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.55f));
        UIBuilder.Stretch(dim.rectTransform);

        var panel = UIBuilder.CreateImage("Panel", root.transform, panelColor);
        UIBuilder.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 820f));

        var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(40, 40, 30, 30);
        vlg.spacing = 12;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        AddFixed(UIBuilder.CreateText("Title", panel.transform, "MARKET", 44, TextAlignmentOptions.Center, FontStyles.Bold), 56);
        moneyText = AddFixed(UIBuilder.CreateText("Money", panel.transform, "", 30, TextAlignmentOptions.Center), 40);
        AddFixed(UIBuilder.CreateText("ListLabel", panel.transform,
            "BAHAN & BENIH  (hasil belanja masuk ke peti masing-masing)", 22, TextAlignmentOptions.Left, FontStyles.Bold), 30);

        var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        list.transform.SetParent(panel.transform, false);
        var lv = list.GetComponent<VerticalLayoutGroup>();
        lv.spacing = 8;
        lv.childControlWidth = true;
        lv.childControlHeight = true;
        lv.childForceExpandWidth = true;
        lv.childForceExpandHeight = false;
        list.GetComponent<LayoutElement>().flexibleHeight = 1;
        listRoot = (RectTransform)list.transform;

        AddFixed(UIBuilder.CreateText("UpgradeLabel", panel.transform, "UPGRADE KEBUN", 22, TextAlignmentOptions.Left, FontStyles.Bold), 30);
        upgradeText = AddFixed(UIBuilder.CreateText("UpgradeInfo", panel.transform, "", 24, TextAlignmentOptions.Left), 36);

        statusText = AddFixed(UIBuilder.CreateText("Status", panel.transform, "", 24, TextAlignmentOptions.Center, FontStyles.Italic), 34);
        statusText.color = new Color(1f, 0.9f, 0.5f);

        var actions = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        actions.transform.SetParent(panel.transform, false);
        var hlg = actions.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        actions.GetComponent<LayoutElement>().preferredHeight = 70;

        upgradeButton = UIBuilder.CreateButton("Upgrade", actions.transform, "Upgrade Kebun", upgradeColor, OnUpgrade, 28);
        UIBuilder.CreateButton("Close", actions.transform, "Tutup (Esc)", closeColor, Close, 28);
    }

    private static TextMeshProUGUI AddFixed(TextMeshProUGUI t, float height)
    {
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        return t;
    }
}
