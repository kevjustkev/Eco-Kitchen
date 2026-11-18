using UnityEngine;
using TMPro;

/// <summary>
/// HUD di kanan atas: hari, sisa waktu, uang, reputasi. Dibuat otomatis lewat kode.
/// Pasang di objek GameManager.
/// </summary>
public class HUDUI : MonoBehaviour
{
    private TextMeshProUGUI hudText;

    void Awake()
    {
        GameObject canvas = UIBuilder.CreateOverlayCanvas("HUDCanvas", transform, 10);

        var bg = UIBuilder.CreateImage("HUDPanel", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
        UIBuilder.Place(bg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(560f, 210f));

        hudText = UIBuilder.CreateText("HUDText", bg.transform, "", 28, TextAlignmentOptions.TopLeft);
        UIBuilder.Stretch(hudText.rectTransform, 16f);
    }

    void Update()
    {
        var day = DayManager.Instance;
        var eco = EconomyManager.Instance;

        string dayLine = "Hari -";
        string timeLine = "";
        if (day != null)
        {
            dayLine = $"<b>Hari {day.CurrentDay}</b>";
            if (day.IsDayRunning && day.TimeLeft > 0f)
            {
                int t = Mathf.CeilToInt(day.TimeLeft);
                timeLine = $"Sisa waktu: {t / 60:00}:{t % 60:00}";
            }
            else if (day.IsDayRunning)
            {
                timeLine = "<color=#F5B041>Restoran tutup, layani pelanggan terakhir</color>";
            }
            else
            {
                timeLine = "Hari selesai";
            }
        }

        string moneyLine = eco != null ? $"Uang: <b>{EconomyManager.Rupiah(eco.Money)}</b>" : "";
        string repLine = eco != null ? $"Reputasi: {eco.Reputation}/100" : "";

        string megaLine = day != null ? $"<size=75%>{day.GetMegaProgressText()}</size>" : "";
        hudText.text = $"{dayLine}\n{timeLine}\n{moneyLine}\n{repLine}\n{megaLine}";
    }
}
