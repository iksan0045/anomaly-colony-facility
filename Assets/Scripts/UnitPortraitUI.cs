using UnityEngine;
using UnityEngine.UI;

/// Helper UI: menempelkan foto profil unit di sisi kiri sebuah tombol (daftar unit).
/// Tidak melakukan apa-apa jika unit tidak punya portrait.
public static class UnitPortraitUI
{
    public static void Attach(Button button, Text label, UnitData unit, float size = 22f)
    {
        if (button == null || unit == null) return;
        Sprite sprite = unit.Portrait;
        if (sprite == null) return;

        var go = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(button.transform, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;   // klik tetap diterima tombol

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(3f, 0f);

        if (label != null)   // geser teks supaya tidak menimpa ikon
        {
            var lr = label.rectTransform;
            lr.offsetMin = new Vector2(size + 8f, lr.offsetMin.y);
        }
    }
}
