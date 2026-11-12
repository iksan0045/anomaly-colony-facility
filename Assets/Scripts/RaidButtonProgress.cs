using UnityEngine;
using UnityEngine.UI;

/// Menempel pada tombol RAID: progres raid latar belakang tampil sebagai isian di dalam tombol.
/// Memegang teks dan status interaktif tombol (hapus pengaturan teks RAID lama di UIManager).
/// Pemakaian (UIManager, setelah tombol dan labelnya dibuat):
///     raidButton.gameObject.AddComponent<RaidButtonProgress>().Init(raidButton, raidLabel);
public class RaidButtonProgress : MonoBehaviour
{
    Button button;
    Text label;
    RectTransform fill;
    Image fillImg;
    int cooldownMax;

    public void Init(Button b, Text l)
    {
        button = b;
        label = l;

        var go = new GameObject("RaidFill", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(b.transform, false);
        go.transform.SetAsFirstSibling();           // di belakang teks
        fill = (RectTransform)go.transform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fillImg = go.GetComponent<Image>();
        fillImg.raycastTarget = false;
    }

    void Update()
    {
        var raid = RaidManager.Instance;
        if (raid == null || button == null || label == null) return;

        if (raid.InProgress)
        {
            button.interactable = true;   // tetap bisa dibuka untuk melihat squad
            label.text = "RAID IN PROGRESS  " + Mathf.RoundToInt(raid.Progress * 100f) + "%";
            Fill(raid.Progress, UITheme.Active.buttonHighlight);
        }
        else if (raid.ResultPending)
        {
            button.interactable = true;
            label.text = "RAID COMPLETE - VIEW RESULT";
            float pulse = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            Fill(1f, new Color(0.45f, 0.85f, 0.45f, pulse));
        }
        else if (raid.CooldownLeft > 0)
        {
            cooldownMax = Mathf.Max(cooldownMax, raid.CooldownLeft);
            button.interactable = false;
            label.text = "RAID  (recovering " + raid.CooldownLeft + ")";
            Fill(1f - (float)raid.CooldownLeft / cooldownMax, new Color(0.5f, 0.5f, 0.55f, 0.6f));
        }
        else
        {
            cooldownMax = 0;
            button.interactable = true;
            label.text = "RAID  (ready)";
            Fill(0f, Color.clear);
        }
    }

    void Fill(float amount, Color c)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
        fillImg.color = new Color(c.r, c.g, c.b, c.a > 0f && c.a < 1f ? c.a : 0.6f);
        if (c.a <= 0f) fillImg.color = Color.clear;
    }
}
