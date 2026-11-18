using UnityEngine;
using TMPro;

/// <summary>
/// Menampilkan teks "Tekan E untuk ..." (dan "Tekan Q untuk ..." jika ada) saat pemain dekat objek.
/// Pasang di SATU objek saja (misalnya InteractionCanvas).
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    public PlayerInteractionDetector detector;
    public GameObject promptObject;
    public TMP_Text promptText;

    void Start()
    {
        if (detector == null) detector = FindFirstObjectByType<PlayerInteractionDetector>();
        if (detector == null) Debug.LogError("InteractionPromptUI: Detector belum dihubungkan!");
        if (promptObject == null) Debug.LogError("InteractionPromptUI: Prompt Object belum dihubungkan!");
        if (promptText == null) Debug.LogError("InteractionPromptUI: Prompt Text belum dihubungkan!");

        if (promptObject != null) promptObject.SetActive(false);
    }

    void Update()
    {
        if (detector == null || promptObject == null || promptText == null) return;

        Interactable target = detector.CurrentTarget;
        bool show = target != null && !UIState.IsUIOpen;

        if (promptObject.activeSelf != show) promptObject.SetActive(show);
        if (!show) return;

        string text = target.GetPromptText();
        string secondary = target.GetSecondaryPromptText();
        if (!string.IsNullOrEmpty(secondary)) text += "\n" + secondary;

        promptText.text = text;
    }
}
