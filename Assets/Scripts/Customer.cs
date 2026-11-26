using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pelanggan: berjalan ke counter, menampilkan pesanan dan bar kesabaran,
/// menerima salad (pemain menekan E sambil membawa salad), lalu pergi.
/// Pasang di prefab pelanggan (misalnya Capsule) yang punya Collider.
/// </summary>
public class Customer : Interactable
{
    private enum State { WalkingIn, Waiting, Leaving }

    [Header("Gerak")]
    public float walkSpeed = 3f;

    [Header("Gelembung Pesanan")]
    [Tooltip("Tinggi gelembung di atas posisi pelanggan")]
    public float bubbleHeight = 2.2f;

    private State state;
    private OrderData order;
    private float patience, patienceMax;
    private Vector3 counterPos, exitPos;

    private GameObject bubble;
    private TextMeshProUGUI bubbleText;
    private RectTransform barFill;
    private Image barFillImage;
    private GameObject barRoot;

    // Mega Order (pelanggan VIP dengan beberapa salad)
    private List<OrderData> vipOrders;
    private int vipIndex;
    public bool IsVip => vipOrders != null;

    public OrderData Order => order;
    /// <summary>True saat pelanggan sedang menunggu pesanan di counter.</summary>
    public bool IsWaiting => state == State.Waiting;

    /// <summary>Dipanggil DayManager saat pelanggan biasa dibuat.</summary>
    public void Setup(OrderData newOrder, Vector3 counterPosition, Vector3 exitPosition)
    {
        vipOrders = null;
        Init(newOrder, newOrder.patienceSeconds, counterPosition, exitPosition);
    }

    /// <summary>Dipanggil DayManager saat pelanggan VIP (Mega Order) dibuat.</summary>
    public void SetupVip(List<OrderData> orders, float patienceSeconds, Vector3 counterPosition, Vector3 exitPosition)
    {
        vipOrders = new List<OrderData>(orders);
        vipIndex = 0;
        displayName = "Pelanggan VIP";
        Init(vipOrders[0], patienceSeconds, counterPosition, exitPosition);
    }

    private void Init(OrderData firstOrder, float patienceSeconds, Vector3 counterPosition, Vector3 exitPosition)
    {
        order = firstOrder;
        patienceMax = Mathf.Max(5f, patienceSeconds);
        patience = patienceMax;

        // Pelanggan hanya bergerak mendatar, tinggi badan mengikuti posisi awal
        counterPos = new Vector3(counterPosition.x, transform.position.y, counterPosition.z);
        exitPos = new Vector3(exitPosition.x, transform.position.y, exitPosition.z);

        if (string.IsNullOrEmpty(displayName) || displayName == "Objek") displayName = "Pelanggan";

        BuildBubble();
        bubble.SetActive(false);
        state = State.WalkingIn;
    }

    void Update()
    {
        if (order == null) return;

        switch (state)
        {
            case State.WalkingIn:
                if (MoveTo(counterPos)) BeginWaiting();
                break;

            case State.Waiting:
                patience -= Time.deltaTime;
                UpdateBar();
                if (patience <= 0f) LeaveAngry();
                break;

            case State.Leaving:
                if (MoveTo(exitPos))
                {
                    if (DayManager.Instance != null) DayManager.Instance.CustomerGone(this);
                    Destroy(gameObject);
                }
                break;
        }
    }

    void LateUpdate()
    {
        // Gelembung selalu menghadap kamera
        if (bubble != null && bubble.activeSelf && Camera.main != null)
            bubble.transform.rotation = Camera.main.transform.rotation;
    }

    // ------------------------------------------------------------------ interaksi

    public override string GetPromptText()
    {
        switch (state)
        {
            case State.Waiting:
                var carry = PlayerCarry.Instance;
                return carry != null && carry.IsCarrying
                    ? "Tekan E untuk menyajikan salad"
                    : "Pelanggan menunggu pesanannya. Racik salad di Meja Racik dulu";
            case State.WalkingIn:
                return "Pelanggan sedang datang...";
            default:
                return "Terima kasih!";
        }
    }

    public override void Interact()
    {
        if (state != State.Waiting) return;

        var carry = PlayerCarry.Instance;
        if (carry == null || !carry.IsCarrying)
        {
            ShowMessage("Kamu belum membawa salad");
            return;
        }

        SaladBowl salad = carry.Drop();
        OrderResult result = OrderEvaluator.Evaluate(salad, order, patience / patienceMax);

        if (IsVip) ServeVip(result);
        else ApplyResult(result);
    }

    // ------------------------------------------------------------------ Mega Order

    private void ServeVip(OrderResult result)
    {
        PayAndReport(result);

        if (result.grade == OrderGrade.Bad)
        {
            ShowLeavingBubble(OrderGrade.Bad, "MEGA ORDER GAGAL", result.detail);
            ShowMessage($"Mega Order gagal: salad {vipIndex + 1} salah ({result.detail})");
            if (DayManager.Instance != null) DayManager.Instance.ReportMegaResult(false);
            state = State.Leaving;
            return;
        }

        vipIndex++;
        if (vipIndex >= vipOrders.Count)
        {
            ShowLeavingBubble(OrderGrade.Perfect, "MEGA ORDER SUKSES!", "Terima kasih!");
            ShowMessage("MEGA ORDER SUKSES!");
            if (DayManager.Instance != null) DayManager.Instance.ReportMegaResult(true);
            state = State.Leaving;
            return;
        }

        ShowMessage($"{OrderEvaluator.Label(result.grade)} Salad {vipIndex}/{vipOrders.Count} diterima. Lanjut ke salad berikutnya!");
        order = vipOrders[vipIndex];
        UpdateOrderBubble();
    }

    private void UpdateOrderBubble()
    {
        string header = IsVip
            ? $"<color=#B7950B><b>MEGA ORDER</b></color>  {vipIndex + 1}/{vipOrders.Count}\n<b>{order.orderName}</b>"
            : $"<b>{order.orderName}</b>";
        bubbleText.text = $"{header}\n<size=75%>{order.Describe()}</size>";
    }

    private void PayAndReport(OrderResult result)
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddMoney(result.payment);
            EconomyManager.Instance.ChangeReputation(result.reputationChange);
        }
        if (DayManager.Instance != null) DayManager.Instance.ReportResult(result);
    }

    private void ShowLeavingBubble(OrderGrade grade, string label, string detail)
    {
        bubble.SetActive(true);
        barRoot.SetActive(false);
        string hex = ColorUtility.ToHtmlStringRGB(OrderEvaluator.GradeColor(grade));
        bubbleText.text = $"<color=#{hex}><b>{label}</b></color>\n<size=75%>{detail}</size>";
    }

    // ------------------------------------------------------------------ alur

    private void BeginWaiting()
    {
        state = State.Waiting;
        bubble.SetActive(true);
        barRoot.SetActive(true);
        UpdateOrderBubble();
        if (IsVip) ShowMessage($"Pelanggan VIP datang dengan MEGA ORDER: {vipOrders.Count} salad!");
        else if (!string.IsNullOrEmpty(order.customerLine)) ShowMessage($"Pelanggan: \"{order.customerLine}\"");
    }

    private void LeaveAngry()
    {
        ApplyResult(OrderEvaluator.Failed());
        if (IsVip)
        {
            ShowLeavingBubble(OrderGrade.Failed, "MEGA ORDER GAGAL", "VIP menunggu terlalu lama");
            if (DayManager.Instance != null) DayManager.Instance.ReportMegaResult(false);
        }
    }

    private void ApplyResult(OrderResult result)
    {
        PayAndReport(result);

        string label = OrderEvaluator.Label(result.grade);
        ShowMessage($"{label}  +{EconomyManager.Rupiah(result.payment)}  ({result.detail})");

        // Tampilkan hasil di gelembung saat pelanggan pergi
        bubble.SetActive(true);
        barRoot.SetActive(false);
        string hex = ColorUtility.ToHtmlStringRGB(OrderEvaluator.GradeColor(result.grade));
        bubbleText.text = $"<color=#{hex}><b>{label}</b></color>\n<size=75%>{result.detail}</size>";

        state = State.Leaving;
    }

    /// <summary>Bergerak mendatar menuju target. True jika sudah sampai.</summary>
    private bool MoveTo(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
        return (transform.position - target).sqrMagnitude < 0.0025f;
    }

    // ------------------------------------------------------------------ gelembung (dibuat lewat kode)

    private void BuildBubble()
    {
        bubble = new GameObject("OrderBubble", typeof(RectTransform), typeof(Canvas));
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = new Vector3(0f, bubbleHeight, 0f);

        // Skala dikompensasi agar ukuran gelembung tidak ikut skala badan pelanggan
        Vector3 s = transform.lossyScale;
        bubble.transform.localScale = new Vector3(0.01f / Mathf.Max(0.001f, s.x),
                                                  0.01f / Mathf.Max(0.001f, s.y),
                                                  0.01f / Mathf.Max(0.001f, s.z));

        var canvas = bubble.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        ((RectTransform)bubble.transform).sizeDelta = new Vector2(380, 230);

        Image bg = UIBuilder.CreateImage("Bg", bubble.transform, new Color(1f, 1f, 1f, 0.93f));
        UIBuilder.Stretch(bg.rectTransform);

        bubbleText = UIBuilder.CreateText("Text", bubble.transform, "", 34, TextAlignmentOptions.Center);
        bubbleText.color = new Color(0.12f, 0.12f, 0.12f);
        bubbleText.enableAutoSizing = true;
        bubbleText.fontSizeMin = 16;
        bubbleText.fontSizeMax = 34;
        UIBuilder.Stretch(bubbleText.rectTransform);
        bubbleText.rectTransform.offsetMin = new Vector2(14, 40);
        bubbleText.rectTransform.offsetMax = new Vector2(-14, -10);

        // Bar kesabaran di bagian bawah gelembung
        Image barBg = UIBuilder.CreateImage("PatienceBar", bubble.transform, new Color(0.25f, 0.25f, 0.25f, 1f));
        RectTransform brt = barBg.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.offsetMin = new Vector2(16, 12);
        brt.offsetMax = new Vector2(-16, 34);
        barRoot = barBg.gameObject;

        barFillImage = UIBuilder.CreateImage("Fill", barBg.transform, Color.green);
        barFill = barFillImage.rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = Vector2.one;
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;
    }

    private void UpdateBar()
    {
        float ratio = Mathf.Clamp01(patience / patienceMax);
        barFill.anchorMax = new Vector2(ratio, 1f);
        barFillImage.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.75f, 0.3f), ratio);
    }
}
