using System.Collections.Generic;
using UnityEngine;

public enum OrderGrade { Perfect, Good, Bad, Failed }

public struct OrderResult
{
    public OrderGrade grade;
    public int payment;
    public int reputationChange;
    public string detail;
}

/// <summary>
/// Menilai salad terhadap pesanan sesuai GDD:
/// Perfect = semua sesuai.
/// Good    = hanya SATU kesalahan kecil (jumlah bahan selisih 1, atau lupa dressing).
/// Bad     = lebih dari satu kesalahan, ATAU ada kesalahan berat:
///           bahan yang diminta tidak ada sama sekali, ada bahan yang tidak dipesan,
///           ada bahan yang dilarang, dressing salah jenis, atau memberi dressing padahal tidak mau.
/// Failed = pelanggan pergi karena menunggu terlalu lama.
/// </summary>
public static class OrderEvaluator
{
    public const int MaxMistakesForGood = 1;

    public static OrderResult Evaluate(SaladBowl bowl, OrderData order, float patienceRatio)
    {
        int mistakes = 0;
        bool severe = false;   // ada kesalahan berat -> otomatis Bad
        var notes = new List<string>();

        // 1. Bahan yang diminta
        foreach (var req in order.ingredients)
        {
            if (req == null || req.ingredient == null) continue;
            int have = bowl.CountOf(req.ingredient);

            if (have == 0)
            {
                severe = true;
                mistakes += req.amount;
                notes.Add($"tidak ada {req.ingredient.displayName}");
            }
            else if (have < req.amount)
            {
                mistakes += req.amount - have;
                notes.Add($"kurang {req.amount - have} {req.ingredient.displayName}");
            }
            else if (have > req.amount)
            {
                mistakes += have - req.amount;
                notes.Add($"kelebihan {have - req.amount} {req.ingredient.displayName}");
            }
        }

        // 2. Bahan yang tidak diminta
        var checkedItems = new HashSet<IngredientData>();
        foreach (var item in bowl.ingredients)
        {
            if (item == null || !checkedItems.Add(item) || order.Requires(item)) continue;

            int n = bowl.CountOf(item);
            severe = true;
            if (order.forbidden.Contains(item))
            {
                mistakes += 2 * n;
                notes.Add($"ada {item.displayName}, padahal tidak mau");
            }
            else
            {
                mistakes += n;
                notes.Add($"{item.displayName} tidak dipesan");
            }
        }

        // 3. Dressing
        switch (order.dressingRule)
        {
            case OrderData.DressingRule.Tertentu:
                if (bowl.dressing == null)
                {
                    mistakes++;
                    notes.Add("dressing lupa");
                }
                else if (bowl.dressing != order.dressing)
                {
                    severe = true;
                    mistakes++;
                    notes.Add($"dressing salah (diminta {(order.dressing != null ? order.dressing.displayName : "tanpa dressing")})");
                }
                break;
            case OrderData.DressingRule.Tanpa:
                if (bowl.dressing != null)
                {
                    severe = true;
                    mistakes++;
                    notes.Add("pelanggan tidak mau dressing");
                }
                break;
        }

        var result = new OrderResult();
        if (mistakes == 0)
        {
            result.grade = OrderGrade.Perfect;
            // Tip bertahap: semakin cepat disajikan, semakin besar bonusnya (dibulatkan ke Rp 500)
            int tip = Mathf.RoundToInt(order.perfectBonus * Mathf.Clamp01(patienceRatio) / 500f) * 500;
            result.payment = order.basePrice + tip;
            result.reputationChange = 5;
        }
        else if (!severe && mistakes <= MaxMistakesForGood)
        {
            result.grade = OrderGrade.Good;
            result.payment = Mathf.RoundToInt(order.basePrice * 0.6f);
            result.reputationChange = 1;
        }
        else
        {
            result.grade = OrderGrade.Bad;
            result.payment = Mathf.RoundToInt(order.basePrice * 0.2f);
            result.reputationChange = -5;
        }

        result.detail = notes.Count == 0 ? "sesuai pesanan" : string.Join(", ", notes);
        return result;
    }

    public static OrderResult Failed()
    {
        return new OrderResult
        {
            grade = OrderGrade.Failed,
            payment = 0,
            reputationChange = -8,
            detail = "pelanggan pergi karena menunggu terlalu lama"
        };
    }

    public static string Label(OrderGrade grade)
    {
        switch (grade)
        {
            case OrderGrade.Perfect: return "PERFECT!";
            case OrderGrade.Good: return "GOOD";
            case OrderGrade.Bad: return "BAD";
            default: return "GAGAL";
        }
    }

    public static Color GradeColor(OrderGrade grade)
    {
        switch (grade)
        {
            case OrderGrade.Perfect: return new Color(0.15f, 0.65f, 0.25f);
            case OrderGrade.Good: return new Color(0.85f, 0.6f, 0.1f);
            default: return new Color(0.8f, 0.2f, 0.2f);
        }
    }
}
