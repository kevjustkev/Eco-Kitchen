/// <summary>
/// Status global UI. Saat sebuah panel terbuka (misalnya panel Meja Racik),
/// pemain tidak bisa bergerak dan tidak bisa berinteraksi dengan objek lain.
/// </summary>
public static class UIState
{
    public static bool IsUIOpen { get; private set; }

    public static void SetUIOpen(bool open)
    {
        IsUIOpen = open;
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
    }
}
