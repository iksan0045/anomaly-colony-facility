using UnityEngine;
using UnityEngine.UI;

/// Satu asset tema untuk seluruh UI (UIManager, RaidPanel, tombol dinamis).
/// Buat lewat Create > Facility > UI Theme, lalu drag ke UIManager.theme (objek "UI Canvas").
/// Tanpa asset, dipakai tema bawaan yang sama persis dengan tampilan sebelumnya.
[CreateAssetMenu(menuName = "Facility/UI Theme", fileName = "UITheme")]
public class UITheme : ScriptableObject
{
    static UITheme active;

    /// Tema yang sedang dipakai. Diatur UIManager; jika belum ada, dibuat tema bawaan.
    public static UITheme Active
    {
        get
        {
            if (active == null)
            {
                active = CreateInstance<UITheme>();
                active.hideFlags = HideFlags.HideAndDontSave;
            }
            return active;
        }
        set { active = value; }
    }

    [Header("Font & Text")]
    [Tooltip("Kosong = font bawaan Unity (LegacyRuntime).")]
    public Font font;
    public Color textColor = Color.white;

    [Header("Panel")]
    [Tooltip("Sprite 9-slice (atur Border di Sprite Editor). Kosong = warna polos.")]
    public Sprite panelSprite;
    [Tooltip("Tint panel. Pakai putih jika sprite sudah berwarna.")]
    public Color panelColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
    [Tooltip("Tint window raid.")]
    public Color windowColor = new Color(0.10f, 0.11f, 0.14f, 1f);
    [Tooltip("Latar gelap di belakang window raid.")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Button")]
    [Tooltip("Sprite 9-slice. Kosong = warna polos.")]
    public Sprite buttonSprite;
    [Tooltip("Opsional. Jika salah satu sprite status diisi, tombol memakai Sprite Swap, bukan Color Tint.")]
    public Sprite buttonHoverSprite;
    public Sprite buttonPressedSprite;
    public Sprite buttonDisabledSprite;
    [Tooltip("Tint tombol normal. Pakai putih jika sprite sudah berwarna.")]
    public Color buttonColor = new Color(0.25f, 0.27f, 0.32f, 1f);
    [Tooltip("Tint tombol terpilih (mis. protokol raid yang aktif).")]
    public Color buttonHighlight = new Color(0.20f, 0.45f, 0.75f, 1f);

    public Font GetFont()
    {
        return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public void StylePanel(Image img, bool window = false)
    {
        if (img == null) return;
        img.sprite = panelSprite;
        img.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = window ? windowColor : panelColor;
    }

    public void StyleButton(Button b)
    {
        if (b == null) return;
        var img = b.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = buttonSprite;
            img.type = buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = buttonColor;
        }

        if (buttonHoverSprite != null || buttonPressedSprite != null || buttonDisabledSprite != null)
        {
            b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState
            {
                highlightedSprite = buttonHoverSprite,
                selectedSprite = buttonHoverSprite,
                pressedSprite = buttonPressedSprite,
                disabledSprite = buttonDisabledSprite,
            };
        }
        else
        {
            b.transition = Selectable.Transition.ColorTint;
        }
    }

    public void StyleText(Text t)
    {
        if (t == null) return;
        t.font = GetFont();
        t.color = textColor;
    }
}
