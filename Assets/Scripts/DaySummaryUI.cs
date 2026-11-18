using UnityEngine;
using TMPro;

/// <summary>
/// Layar ringkasan di akhir hari. Dibuat otomatis lewat kode.
/// Pasang di objek GameManager.
/// </summary>
public class DaySummaryUI : MonoBehaviour
{
    private GameObject root;
    private TextMeshProUGUI titleText, bodyText;
    private bool subscribed;

    void Awake()
    {
        root = UIBuilder.CreateOverlayCanvas("DaySummaryCanvas", transform, 110);

        var dim = UIBuilder.CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.65f));
        UIBuilder.Stretch(dim.rectTransform);

        var panel = UIBuilder.CreateImage("Panel", root.transform, new Color(0.10f, 0.12f, 0.10f, 0.97f));
        UIBuilder.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 640f));

        titleText = UIBuilder.CreateText("Title", panel.transform, "", 48, TextAlignmentOptions.Center, FontStyles.Bold);
        UIBuilder.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(760f, 70f));

        bodyText = UIBuilder.CreateText("Body", panel.transform, "", 30, TextAlignmentOptions.TopLeft);
        UIBuilder.Place(bodyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(660f, 380f));

        var btn = UIBuilder.CreateButton("NextDayButton", panel.transform, "Mulai Hari Berikutnya",
                                         new Color(0.2f, 0.55f, 0.3f), OnNextDay, 30);
        UIBuilder.Place((RectTransform)btn.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(420f, 80f));

        root.SetActive(false);
    }

    void Start() => TrySubscribe();
    void OnEnable() => TrySubscribe();

    void OnDisable()
    {
        if (subscribed && DayManager.Instance != null) DayManager.Instance.OnDayEnded -= Show;
        subscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed || DayManager.Instance == null) return;
        DayManager.Instance.OnDayEnded += Show;
        subscribed = true;
    }

    private void Show(int day, DayStats s)
    {
        var eco = EconomyManager.Instance;
        titleText.text = $"Hari {day} Selesai";
        bodyText.text =
            $"Pelanggan dilayani: <b>{s.served}</b>\n" +
            $"   Perfect: <color=#58D68D>{s.perfect}</color>   Good: <color=#F5B041>{s.good}</color>   Bad: <color=#EC7063>{s.bad}</color>\n" +
            $"Pelanggan pergi kecewa: <color=#EC7063>{s.failed}</color>\n\n" +
            $"Pendapatan hari ini: <b>{EconomyManager.Rupiah(s.income)}</b>\n" +
            (eco != null ? $"Total uang: <b>{EconomyManager.Rupiah(eco.Money)}</b>\nReputasi: {eco.Reputation}/100" : "");

        root.SetActive(true);
        UIState.SetUIOpen(true);
    }

    private void OnNextDay()
    {
        root.SetActive(false);
        UIState.SetUIOpen(false);
        if (DayManager.Instance != null) DayManager.Instance.StartNextDay();
    }
}
