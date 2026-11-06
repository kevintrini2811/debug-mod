using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DebugMod;

// Borrow live native Images only during zombie selection. Never recolor shared
// textures or native prefabs; restore every borrowed property before returning.
internal static class DebugZombieChooserTheme
{
    private sealed record Borrowed(Image Image, Sprite? Sprite, Color Color, bool Enabled);
    private static readonly Dictionary<IntPtr, Borrowed> borrowed = new();
    private static readonly Dictionary<IntPtr, Sprite> replacements = new();
    private static readonly HashSet<IntPtr> ownedSprites = new();
    private static readonly List<Texture2D> textures = new();
    private static float nextScan;
    private static readonly HashSet<string> decorations = new()
    {
        "SPR_MainHud_SeedchooserBackground", "SPR_MainHud_SeedBank_Background",
        "SPR_MainHud_SeedBank_SunAmount", "SPR_MainHud_Seedchooser_CostBackground",
        "SPR_MainHud_Seedchooser_CostBackground_Discount"
    };

    private static bool InBank(Transform transform)
    {
        bool bank = false;
        for (var parent = transform; parent != null; parent = parent.parent)
        {
            if (parent.name == "SeedBank") bank = true;
            if (parent.name == "P_Gameplay_MainHUD") return bank;
        }
        return false;
    }

    private static Sprite Replacement(Sprite source)
    {
        if (replacements.TryGetValue(source.Pointer, out var existing)) return existing;
        string resource = "FusionMod.DebugCards.zombie-chooser-" + source.name + ".png";
        using var stream = typeof(DebugZombieChooserTheme).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException("Missing zombie chooser decoration: " + resource);
        using var bytes = new MemoryStream(); stream.CopyTo(bytes);
        var texture = DebugCardArt.Retain(new Texture2D(2, 2, TextureFormat.RGBA32, false));
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes.ToArray(), true))
                throw new InvalidDataException("Invalid zombie chooser decoration: " + resource);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pivot = source.pivot;
            var sprite = DebugCardArt.Retain(Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(pivot.x / source.rect.width, pivot.y / source.rect.height), source.pixelsPerUnit,
                0, SpriteMeshType.FullRect, source.border));
            sprite.name = "DebugZombieChooser." + source.name;
            textures.Add(texture); replacements.Add(source.Pointer, sprite); ownedSprites.Add(sprite.Pointer);
            return sprite;
        }
        catch { Object.Destroy(texture); throw; }
    }

    internal static void Update()
    {
        if (!DebugSeedChooser.ZombiePhase) { Restore(); return; }
        // Bindings can create new cost panels and page controls asynchronously.
        // Scan at 10 Hz only while this temporary chooser is open.
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .1f;
        var screen = DebugSeedChooser.SelectionScreen;
        var panel = screen == null ? null : DebugSeedChooser.BoundPanel(screen);
        if (panel == null) return;
        foreach (var image in Object.FindObjectsOfType<Image>())
        {
            if (!image.transform.IsChildOf(panel.transform) && !InBank(image.transform)) continue;
            var source = image.sprite;
            bool collector = image.name == "SunCollectorRotatingFace";
            bool empty = image.name == "SeedBackground_Empty";
            if (!collector && !empty && (source == null || ownedSprites.Contains(source.Pointer)
                || !decorations.Contains(source.name))) continue;
            if (!borrowed.ContainsKey(image.Pointer))
                borrowed.Add(image.Pointer, new(image, source, image.color, image.enabled));
            if (collector) image.enabled = false;
            else if (empty) image.color = new Color(.6f, .56f, .68f, image.color.a);
            else image.sprite = Replacement(source!);
        }
    }

    // Sun Collector crops and caches its rotating face from this live image.
    // Give that crop the borrowed native source, never our temporary brain art.
    internal static Sprite CollectorSource(Sprite source) => borrowed.Values
        .FirstOrDefault(value => value.Image && value.Image.sprite?.Pointer == source.Pointer)?.Sprite ?? source;

    internal static void Restore()
    {
        foreach (var value in borrowed.Values)
        {
            if (!value.Image) continue;
            // A native rebind may already replace our sprite. Preserve that
            // newer binding rather than restoring an obsolete card's artwork.
            if (value.Image.sprite != null && ownedSprites.Contains(value.Image.sprite.Pointer))
                value.Image.sprite = value.Sprite;
            value.Image.color = value.Color;
            value.Image.enabled = value.Enabled;
        }
        borrowed.Clear(); nextScan = 0;
    }

    internal static void Clear()
    {
        Restore();
        foreach (var sprite in replacements.Values) Object.Destroy(sprite);
        foreach (var texture in textures) Object.Destroy(texture);
        replacements.Clear(); textures.Clear(); ownedSprites.Clear();
    }
}
