using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Layar akhir permainan: MENANG (Mega Order sukses) atau GAME OVER (bangkrut).
/// Dibuat otomatis lewat kode. Pasang di objek GameManager.
/// Syarat tombol "Main Lagi": scene MainGame sudah dimasukkan ke daftar scene build.
/// </summary>
public class GameEndUI : MonoBehaviour
{
    private GameObject root;
    private UnityEngine.UI.Image panelImage;
    private TextMeshProUGUI titleText, subtitleText, statsText;
    private bool subscribed;

    void Awake()
    {
        root = UIBuilder.CreateOverlayCanvas("GameEndCanvas", transform, 200);

        var dim = UIBuilder.CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.75f));
        UIBuilder.Stretch(dim.rectTransform);

        panelImage = UIBuilder.CreateImage("Panel", root.transform, new Color(0.10f, 0.12f, 0.10f, 0.97f));
        UIBuilder.Place(panelImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 760f));

        titleText = UIBuilder.CreateText("Title", panelImage.transform, "", 60, TextAlignmentOptions.Center, FontStyles.Bold);
        UIBuilder.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(840f, 80f));

        subtitleText = UIBuilder.CreateText("Subtitle", panelImage.transform, "", 28, TextAlignmentOptions.Center, FontStyles.Italic);
        UIBuilder.Place(subtitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(800f, 90f));

        statsText = UIBuilder.CreateText("Stats", panelImage.transform, "", 30, TextAlignmentOptions.TopLeft);
        UIBuilder.Place(statsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(640f, 340f));

        var again = UIBuilder.CreateButton("PlayAgain", panelImage.transform, "Main Lagi", new Color(0.2f, 0.55f, 0.3f), Restart, 30);
        UIBuilder.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-15f, 35f), new Vector2(300f, 80f));

        var quit = UIBuilder.CreateButton("Quit", panelImage.transform, "Keluar", new Color(0.55f, 0.25f, 0.22f), Quit, 30);
        UIBuilder.Place((RectTransform)quit.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(15f, 35f), new Vector2(300f, 80f));

        root.SetActive(false);
    }

    void Start() => TrySubscribe();
    void OnEnable() => TrySubscribe();

    void OnDisable()
    {
        if (subscribed && DayManager.Instance != null) DayManager.Instance.OnGameEnded -= Show;
        subscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed || DayManager.Instance == null) return;
        DayManager.Instance.OnGameEnded += Show;
        subscribed = true;
    }

    private void Show(bool win, DayStats total)
    {
        var eco = EconomyManager.Instance;
        var day = DayManager.Instance;
        var garden = GardenManager.Instance;

        if (win)
        {
            titleText.text = "<color=#58D68D>MEGA ORDER SUKSES!</color>";
            subtitleText.text = "Restoranmu kini mandiri, menguntungkan, dan ramah lingkungan.\nSustainable Success!";
        }
        else
        {
            titleText.text = "<color=#EC7063>BANGKRUT</color>";
            subtitleText.text = "Uang restoran habis dan operasional tidak bisa dilanjutkan.\nKelola belanja dan manfaatkan kebunmu!";
        }

        statsText.text =
            $"Bertahan selama: <b>{(day != null ? day.CurrentDay : 0)} hari</b>\n" +
            $"Pelanggan dilayani: <b>{total.served}</b>\n" +
            $"   Perfect: <color=#58D68D>{total.perfect}</color>   Good: <color=#F5B041>{total.good}</color>   Bad: <color=#EC7063>{total.bad}</color>\n" +
            $"Pelanggan pergi kecewa: {total.failed}\n" +
            $"Total pendapatan: <b>{EconomyManager.Rupiah(total.income)}</b>\n" +
            (eco != null ? $"Uang akhir: <b>{EconomyManager.Rupiah(eco.Money)}</b>   Reputasi: {eco.Reputation}/100\n" : "") +
            (garden != null ? $"Level kebun: {garden.Level} ({garden.UnlockedPlots} petak)" : "");

        root.SetActive(true);
        UIState.SetUIOpen(true);
        Time.timeScale = 0f; // hentikan permainan
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        UIState.SetUIOpen(false);

        int index = SceneManager.GetActiveScene().buildIndex;
        if (index < 0)
        {
            Debug.LogError("Scene belum masuk daftar build. Buka File > Build Profiles (atau Build Settings) lalu tambahkan scene MainGame.");
            return;
        }
        SceneManager.LoadScene(index);
    }

    private void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
