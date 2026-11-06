using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DebugMod;

// Use the game's EventSystem and InputSystem UI module for every debug action.
// IMGUI can render in this build without receiving matching mouse events.
internal sealed class DebugUi : IDisposable
{
    private readonly GameObject root;
    private readonly TMP_FontAsset font;
    private readonly RectTransform content;
    private readonly bool deck;
    private Image? bankBackground;
    private Image? deckBackground, brainPaper;
    private TMP_Text? brainAmount;
    private readonly List<RectTransform> deckCards = new();
    private Rect deckScreen;
    internal bool ContainsDeck(Vector2 point) => deck && deckScreen.Contains(point);
    private readonly CanvasGroup group;
    private readonly List<(Image Image, Func<Sprite?> Sprite)> previews = new();

    internal DebugUi(bool modal)
    {
        font = Object.FindObjectsOfType<TMP_Text>(true).FirstOrDefault(text => text.font != null)?.font
            ?? TMP_Settings.defaultFontAsset
            ?? throw new InvalidOperationException("No UI font is available for debug mode.");
        deck = !modal;
        root = new GameObject("FusionDebugUI");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        content = new GameObject("Content").AddComponent<RectTransform>();
        content.SetParent(root.transform, false);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        Resize();
        root.AddComponent<GraphicRaycaster>();
        group = root.AddComponent<CanvasGroup>();
        if (modal)
        {
            var shade = Element("Modal", new Rect(0, 0, 1100, 760));
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .8f);
        }
    }

    internal void SetVisible(bool visible) => root.SetActive(visible);

    internal void SetPlacing(bool placing)
    {
        group.alpha = placing ? .2f : 1;
        group.interactable = group.blocksRaycasts = !placing;
    }

    internal Image Picture(Rect bounds, Sprite sprite, Color tint, bool preserveAspect = false)
    {
        var image = Element("Artwork", bounds).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = tint;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    internal void Resize()
    {
        root.GetComponent<Canvas>().scaleFactor =
            Math.Max(.5f, Math.Min(Screen.width / 1100f, Screen.height / 760f));
        if (deck)
        {
            content.localScale = Vector3.one;
            content.anchoredPosition = Vector2.zero;
            float fallback = root.GetComponent<Canvas>().scaleFactor;
            deckScreen = new Rect(4 * fallback, Screen.height - 760 * fallback, 1092 * fallback, 110 * fallback);
            if (bankBackground == null || !bankBackground || !bankBackground.gameObject.activeInHierarchy)
                bankBackground = Object.FindObjectsOfType<Image>().FirstOrDefault(image => image.sprite != null
                    && image.sprite.name == "SPR_MainHud_SeedBank_Background" && image.gameObject.activeInHierarchy
                    && !image.transform.IsChildOf(root.transform));
            if (bankBackground != null && bankBackground && bankBackground.gameObject.activeInHierarchy)
                LayoutDeck(bankBackground);

        }
        foreach (var preview in previews)
        {
            var sprite = preview.Sprite();
            if (preview.Image.sprite != sprite) preview.Image.sprite = sprite;
            preview.Image.enabled = sprite != null;
        }
    }

    internal void Deck(Sprite background, Sprite counter)
    {
        deckBackground = Picture(new Rect(4, 650, 1092, 110), background, Color.white);
        brainPaper = Picture(new Rect(12, 650, 90, 110), counter, Color.white);
        brainAmount = Label(new Rect(12, 719, 78, 34), "∞", 25);
        brainAmount.color = new Color(.16f, .08f, .03f, 1);
        brainAmount.alignment = TextAlignmentOptions.Center;
    }

    private static Rect ScreenBounds(RectTransform rect, Canvas canvas)
    {
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        rect.GetWorldCorners(corners);
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var lower = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        var upper = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return new Rect(lower.x, lower.y, upper.x - lower.x, upper.y - lower.y);
    }

    private void Position(RectTransform rect, Rect screen)
    {
        float scale = root.GetComponent<Canvas>().scaleFactor;
        rect.anchoredPosition = new Vector2(screen.x / scale, (screen.yMax - Screen.height) / scale);
        rect.sizeDelta = new Vector2(screen.width / scale, screen.height / scale);
    }

    private void LayoutDeck(Image background)
    {
        if (deckBackground == null || brainPaper == null || brainAmount == null) return;
        // The packet background excludes the sun bank. Measure both separately;
        // never stretch the cards with the full bank's independent X/Y scales.
        var bank = background.transform.parent;
        var sun = bank.parent.Find("SunAmount_Background");
        var sunRect = sun?.TryCast<RectTransform>();
        if (sunRect == null) return;
        var native = ScreenBounds(background.rectTransform, background.canvas);
        var sunBounds = ScreenBounds(sunRect, background.canvas);
        if (native.width <= 0 || native.height <= 0 || sunBounds.width <= 0) return;
        float shift = -Math.Min(native.y, sunBounds.y);
        deckScreen = new Rect(Math.Min(native.x, sunBounds.x), 0,
            Math.Max(native.xMax, sunBounds.xMax) - Math.Min(native.x, sunBounds.x),
            Math.Max(native.yMax, sunBounds.yMax) + shift);
        Position(deckBackground.rectTransform, new Rect(native.x, native.y + shift, native.width, native.height));
        var counterBounds = sunBounds;
        counterBounds.y += shift;
        Position(brainPaper.rectTransform, counterBounds);

        var label = sunRect.GetComponentsInChildren<TMP_Text>().FirstOrDefault(text => text.name == "SunAmountLabel");
        if (label != null)
        {
            var amount = ScreenBounds(label.rectTransform, background.canvas);
            amount.y += shift;
            Position(brainAmount.rectTransform, amount);
            brainAmount.font = label.font;
            brainAmount.fontSize = label.fontSize * amount.height / label.rectTransform.rect.height
                / root.GetComponent<Canvas>().scaleFactor;

        }

        var layout = bank.GetComponent<HorizontalLayoutGroup>();
        var cards = bank.GetComponentsInChildren<Image>()
            .Where(image => image.name == "SeedBackground" && image.sprite != null && image.gameObject.activeInHierarchy)
            .OrderBy(image => ScreenBounds(image.rectTransform, image.canvas).x).ToArray();
        if (cards.Length == 0 || layout == null) return;
        var first = cards[0];
        var packet = ScreenBounds(first.rectTransform, first.canvas);
        // GenerateSimpleSprite preserves aspect for the mod's complete packets.
        if (first.preserveAspect || DebugMode.Art.OwnsCardSprite(first.sprite))
        {
            float fit = Math.Min(packet.width / first.sprite.rect.width, packet.height / first.sprite.rect.height);
            var size = new Vector2(first.sprite.rect.width * fit, first.sprite.rect.height * fit);
            packet = new Rect(packet.center.x - size.x / 2, packet.center.y - size.y / 2, size.x, size.y);
        }
        if (packet.width <= 0 || packet.height <= 0) return;
        var bankRect = bank.TryCast<RectTransform>();
        if (bankRect == null || bankRect.rect.width <= 0) return;
        float pixelsPerUnit = ScreenBounds(bankRect, background.canvas).width / bankRect.rect.width;
        var ancestor = first.transform;
        while (ancestor.parent != null && ancestor.parent.Pointer != bank.Pointer) ancestor = ancestor.parent;
        var item = ancestor.TryCast<RectTransform>();
        if (item == null) return;
        float step = ScreenBounds(item, background.canvas).width + layout.spacing * pixelsPerUnit;
        if (cards.Length > 1)
            step = ScreenBounds(cards[1].rectTransform, cards[1].canvas).center.x
                - ScreenBounds(first.rectTransform, first.canvas).center.x;
        packet.y += shift;
        float canvasScale = root.GetComponent<Canvas>().scaleFactor;
        for (int i = 0; i < deckCards.Count; ++i)
        {
            var rect = deckCards[i];
            rect.anchoredPosition = new Vector2((packet.x + i * step) / canvasScale,
                (packet.yMax - Screen.height) / canvasScale);
            rect.localScale = new Vector3(packet.width / (128 * canvasScale), packet.height / (176 * canvasScale), 1);
        }
    }

    private RectTransform Element(string name, Rect bounds)
    {
        var rect = new GameObject(name).AddComponent<RectTransform>();
        rect.SetParent(content, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(bounds.x, -bounds.y);
        rect.sizeDelta = new Vector2(bounds.width, bounds.height);
        return rect;
    }

    internal TMP_Text Label(Rect bounds, string text, float size = 20)
    {
        var label = Element("Label", bounds).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.text = text;
        label.color = Color.white;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }

    internal Button Button(Rect bounds, string text, Action action, bool enabled = true)
    {
        var rect = Element("Button", bounds);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.18f, .28f, .12f, 1);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.interactable = enabled;
        button.onClick.AddListener((UnityAction)(() => action()));
        var nav = button.navigation;
        nav.mode = Navigation.Mode.Automatic;
        button.navigation = nav;
        Label(new Rect(bounds.x + 8, bounds.y, bounds.width - 16, bounds.height), text);
        return button;
    }

    internal void Card(Rect bounds, string text, Func<Sprite?> sprite, Action action,
        bool selected = false, bool enabled = true, Func<Sprite?>? badge = null)
    {
        int firstChild = content.childCount;
        var button = Button(bounds, "", action, enabled);
        var frame = button.GetComponent<Image>();
        // The full-size packet is also the button's raycast target. A disabled
        // frame plus a non-raycast sibling leaves the button unreachable.
        frame.sprite = sprite();
        frame.preserveAspect = false;
        frame.raycastTarget = true;
        frame.color = selected ? new Color(1, .65f, .25f, 1) : Color.white;
        previews.Add((frame, sprite));
        if (badge != null)
        {
            var icon = Element("HeadBadge", new Rect(bounds.x + bounds.width - 39, bounds.y + 4, 34, 34))
                .gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = badge(); icon.enabled = icon.sprite != null;
            previews.Add((icon, badge));
        }
        var caption = Label(new Rect(bounds.x + 5, bounds.y + bounds.height - 31, bounds.width - 10, 27), text, 13);
        caption.color = new Color(.16f, .08f, .03f, 1);
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 7;
        caption.fontSizeMax = 13;
        caption.enableWordWrapping = true;
        caption.overflowMode = TextOverflowModes.Ellipsis;
        caption.alignment = TextAlignmentOptions.Center;
        if (deck)
        {
            int lastChild = content.childCount;
            var card = Element("ZombieCard", bounds);
            for (int i = firstChild; i < lastChild; ++i)
            {
                var child = content.GetChild(firstChild).Cast<RectTransform>();
                child.SetParent(card, false);
                child.anchoredPosition -= new Vector2(bounds.x, -bounds.y);
            }
            card.anchoredPosition = new Vector2(105 + deckCards.Count * 80, -657);
            card.localScale = Vector3.one * (100f / 176);
            deckCards.Add(card);
        }
    }

    public void Dispose()
    {
        // Stop raycasts immediately; Unity destroys the object at frame end.
        if (!root) return;
        root.SetActive(false);
        Object.Destroy(root);
    }
}

// A visual-only native preview anchor. Never create a zombie for cursor art:
// doing so would run spawning, combat, audio and loot callbacks.
internal sealed class DebugZombiePreview : IDisposable
{
    private readonly Il2CppReloaded.Gameplay.ReloadedObject anchor;
    private Il2CppSource.Controllers.PreviewController? controller;
    private GameObject? root;
    private SpriteRenderer? renderer;
    private SpriteRenderer? lane;
    private Sprite? laneSprite;
    internal DebugZombiePreview(Il2CppReloaded.Gameplay.Board board) => anchor = new()
    { mBoard = board, mApp = board.mApp, mVisible = true, mRenderOrder = 400000 };

    internal void Update(Sprite? sprite, float x, float y, bool entrance, float laneX, int rowHeight)
    {
        // The caller supplies the row's foot line; the sprite uses a bottom
        // pivot. Never apply a second portrait-height or row offset here.
        anchor.mX = x + 40; anchor.mY = y;
        if (controller == null || !controller)
        {
            controller = anchor.mApp.CreatePreviewController(Il2CppReloaded.Gameplay.SeedType.Peashooter, anchor);
            if (controller == null || !controller) return;
            var native = controller.m_previewDrawer.m_previewSprite;
            root = new GameObject("FusionDebugZombiePreview") { layer = native.gameObject.layer };
            root.transform.SetParent(controller.transform, false);
            renderer = root.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = native.sharedMaterial;
            renderer.sortingLayerID = native.sortingLayerID;
            renderer.sortingOrder = native.sortingOrder;
            renderer.color = new Color(1, 1, 1, .45f);
            var band = new GameObject("FusionDebugSpawnLane") { layer = root.layer };
            band.transform.SetParent(controller.transform, false);
            lane = band.AddComponent<SpriteRenderer>();
            laneSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            lane.sprite = laneSprite;
            lane.sharedMaterial = native.sharedMaterial;
            lane.sortingLayerID = native.sortingLayerID;
            lane.sortingOrder = native.sortingOrder - 1;
            lane.color = new Color(.2f, .65f, 1f, .18f);
            controller.SetPosition(0, 0);
            controller.ShowPreview();
            native.enabled = false;
        }
        renderer!.sprite = sprite;
        lane!.enabled = entrance;
        lane.transform.localPosition = new Vector3((laneX + 360 - x - 40) * 2.82f, rowHeight * 1.41f, 0);
        lane.transform.localScale = new Vector3(720 * 2.82f, rowHeight * 2.82f, 1);
        renderer.enabled = sprite != null;
        if (sprite != null)
        {
            var bounds = sprite.bounds;
            var fit = ZombiePreviewLayout.Fit(bounds.center.x, bounds.min.y, bounds.size.y);
            root!.transform.localScale = Vector3.one * fit.Scale;
            root.transform.localPosition = new Vector3(fit.X, fit.Y, 0);
        }
        controller.UpdatePosition();
    }

    internal object Snapshot()
    {
        var sprite = renderer?.sprite;
        return new { anchor = new { x = anchor.mX, y = anchor.mY },
            enabled = renderer != null && renderer.enabled,
            active = root != null && root.activeInHierarchy,
            sprite = sprite?.name,
            pivot = sprite == null ? null : new { x = sprite.pivot.x, y = sprite.pivot.y },
            position = root == null ? null : new { x = root.transform.position.x, y = root.transform.position.y, z = root.transform.position.z },
            bounds = renderer == null ? null : new { x = renderer.bounds.center.x, y = renderer.bounds.center.y,
                width = renderer.bounds.size.x, height = renderer.bounds.size.y },
            foot = sprite == null || root == null ? null : Foot(root.transform.TransformPoint(
                new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, 0))) };
    }
    private static object Foot(Vector3 point) => new { x = point.x, y = point.y, z = point.z };

    public void Dispose()
    {
        if (root != null && root) { root.SetActive(false); Object.Destroy(root); }
        if (lane != null && lane) Object.Destroy(lane.gameObject);
        if (laneSprite != null) Object.Destroy(laneSprite);
        if (controller != null && controller) controller.Die();
        root = null; controller = null; lane = null; laneSprite = null;
    }
}
