using UnityEngine;
using TMPro;

/// <summary>
/// Menampilkan pesan singkat di layar (misalnya "Selada dipotong menjadi Selada Potong")
/// lalu hilang otomatis. Opsional: jika tidak dipasang, pesan tetap muncul di Console.
/// </summary>
public class NotificationUI : MonoBehaviour
{
    public static NotificationUI Instance { get; private set; }

    public TMP_Text messageText;
    public float showDuration = 2f;

    private float hideTime;

    void Awake()
    {
        Instance = this;
        if (messageText != null) messageText.text = "";
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string message)
    {
        if (messageText == null) return;
        messageText.text = message;
        hideTime = Time.time + showDuration;
    }

    void Update()
    {
        if (messageText != null && messageText.text.Length > 0 && Time.time >= hideTime)
            messageText.text = "";
    }
}
