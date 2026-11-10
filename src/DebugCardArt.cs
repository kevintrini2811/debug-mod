using Il2CppReloaded.DataModels;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.Services;
using UnityEngine;
using System.Text.Json;
using Object = UnityEngine.Object;

namespace DebugMod;

// The session owns its native Unity assets. Managed dictionaries do not count
// as references for Resources.UnloadUnusedAssets during a scene transition.
// Borrowed UI models never own them; Dispose releases them explicitly.
internal sealed class DebugCardArt : IDisposable
{
    private readonly IDataService data;
    private readonly Dictionary<SeedType, AddressableSpriteValueModel> plants = new();
    private readonly ReplantedModUi.NativeZombiePortraits portraits;
    private readonly Dictionary<ZombieType, string> zombieNames = new();
    private string? language;
    private readonly Dictionary<string, AddressableSpriteValueModel> levels = new();
    private Texture2D? eyesTexture;
    private Sprite? eyes;
    private sealed record Packet(string Resource, int X, int Y, int Width, int Height);
    private readonly Dictionary<string, Sprite> fusionPortraits = new();
    private readonly Dictionary<IntPtr, (Sprite Sprite, Texture2D Texture)> zombieCards = new();
    private readonly HashSet<IntPtr> cardSprites = new();
    private Texture2D? atlas, packetTexture;
    private Sprite? packet;
    internal Sprite PacketBackground => packet!;
    private readonly List<Sprite> rosterSprites = new();
    private readonly List<Texture2D> rosterTextures = new();
    internal Sprite Roster => rosterSprites[0];
    internal Sprite Counter => rosterSprites[1];
    internal Sprite RegisteredPacket(Sprite thumbnail)
    {
        if (zombieCards.TryGetValue(thumbnail.Pointer, out var card)) return card.Sprite;
        return CreateZombiePacket(thumbnail);
    }
    internal bool OwnsCardSprite(Sprite? sprite) => sprite != null && (cardSprites.Contains(sprite.Pointer) || portraits.Owns(sprite));

    private static Stream Resource(string name) => typeof(DebugCardArt).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException($"Missing debug card asset: {name}");
    internal static T Retain<T>(T asset) where T : Object
    {
        asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        return asset;
    }
    private static Texture2D LoadTexture(string name, bool readable = false)
    {
        using var stream = Resource(name);
        using var bytes = new MemoryStream(); stream.CopyTo(bytes);
        var texture = Retain(new Texture2D(2, 2, TextureFormat.RGBA32, false));
        if (!ImageConversion.LoadImage(texture, bytes.ToArray(), !readable))
        { Object.Destroy(texture); throw new InvalidDataException($"Invalid debug card asset: {name}"); }
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    internal Sprite? TrackCard(Sprite? sprite)
    { if (sprite != null) cardSprites.Add(sprite.Pointer); return sprite; }

    internal Sprite Fusion(string resource) => fusionPortraits.TryGetValue(resource, out var sprite)
        ? sprite : throw new InvalidDataException($"Missing debug packet atlas entry: {resource}");

    internal DebugCardArt(IDataService data)
    {
        this.data = data;
        portraits = new ReplantedModUi.NativeZombiePortraits();
        try
        {
            foreach (string name in new[] { "zombie-chooser-SPR_MainHud_SeedBank_Background.png", "zombie-chooser-SPR_MainHud_SeedBank_SunAmount.png" })
            {
                var texture = LoadTexture("FusionMod.DebugCards." + name);
                rosterTextures.Add(texture);
                var decoration = Retain(Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f), 100));
                decoration.name = "DebugZombieChooser." + name["zombie-chooser-".Length..^4];
                rosterSprites.Add(decoration);
            }
            atlas = LoadTexture("FusionMod.DebugCards.fusion-packets.png");
            packetTexture = LoadTexture("FusionMod.DebugCards.packet-background.png", true);
            packet = Retain(Sprite.Create(packetTexture, new Rect(0, 0, 128, 176), new Vector2(.5f, .5f), 100));
            using var stream = Resource("FusionMod.DebugCards.fusion-packets.json");
            foreach (var entry in JsonSerializer.Deserialize<Packet[]>(stream) ?? throw new InvalidDataException("Invalid packet atlas index."))
            {
                var sprite = Retain(Sprite.Create(atlas, new Rect(entry.X, entry.Y, entry.Width, entry.Height), new Vector2(.5f, .5f), 100));
                fusionPortraits.Add(entry.Resource, sprite); cardSprites.Add(sprite.Pointer);
            }
        }
        catch { Dispose(); throw; }
    }
    internal void Update()
    {
        string? current = ReplantedModUi.NativeLocalization.LanguageLabel();
        if (language == current) return;
        language = current;
        zombieNames.Clear();
    }
    internal bool OwnsZombieSprite(Sprite? sprite) => sprite != null
        && (eyes?.Pointer == sprite.Pointer || portraits.Owns(sprite));

    internal string ZombieName(ZombieType type)
    {
        if (!zombieNames.TryGetValue(type, out string? name))
            zombieNames[type] = name = ResolveZombieName(type);
        return name;
    }

    private string ResolveZombieName(ZombieType type)
    {
        string Localize(string key) => ReplantedModUi.NativeLocalization.Localize(key).TrimStart('$');
        var head = type switch
        {
            ZombieType.PeaHead => SeedType.Peashooter, ZombieType.WallnutHead => SeedType.Wallnut,
            ZombieType.JalapenoHead => SeedType.Jalapeno, ZombieType.GatlingHead => SeedType.Gatlingpea,
            ZombieType.SquashHead => SeedType.Squash, ZombieType.TallnutHead => SeedType.Tallnut,
            _ => SeedType.None
        };
        if (head != SeedType.None)
            return Localize(data.GetPlantDefinition(head).PlantName)
                + (ReplantedModUi.NativeLocalization.German ? "-" : " ") + Localize("$ZOMBIE");
        string? special = type switch
        {
            ZombieType.RedeyeGargantuar => "$REDEYED_GARGANTUAR",
            ZombieType.Target => "$VS_MODE_TARGET_ZOMBIE",
            ZombieType.TrashCan => "$VS_MODE_TRASH_CAN_ZOMBIE",
            ZombieType.Gravestone => "$VS_MODE_ZOMBIE_GRAVESTONE",
            _ => null
        };
        if (special != null) return Localize(special);
        if (type == ZombieType.Zombatar) return "Zombatar";
        var entries = data.ZombieAlmanacData;
        int count = entries.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<Il2CppReloaded.Data.AlmanacEntryData>>().Count;
        for (int i = 0; i < count; ++i)
            if (entries[i].ZombieType == type) return Localize(entries[i].EntryName);
        return Localize(data.GetZombieDefinition(type).ZombieName);
    }

    internal Sprite? Level(Il2CppReloaded.Data.LevelEntryData level)
    {
        string key = level.BackgroundPrefab.AssetGUID;
        if (!levels.TryGetValue(key, out var model))
        {
            model = new AddressableSpriteValueModel { Reference = level.EntryThumbnail };
            levels.Add(key, model);
        }
        return model.Value;
    }

    internal Sprite? Plant(SeedType type)
    {
        if (!plants.TryGetValue(type, out var model))
        {
            var definition = data.GetPlantDefinition(type);
            var reference = definition.PreviewSprite;
            if (reference == null || !reference.RuntimeKeyIsValid()) reference = definition.PlantImage;
            // This native constructor ignores its sprite argument. The same
            // Reference setter used by the normal chooser owns async loading.
            model = new AddressableSpriteValueModel { Reference = reference };
            plants.Add(type, model);
        }
        return model.Value;
    }

    internal Sprite? ZombiePortrait(ZombieType type) => portraits.Get(type);
    internal Sprite Zombie(ZombieType type) => portraits.Packet(type);
    internal Sprite EyesPacket() => RegisteredPacket(Eyes());

    // Registry sprites can be atlas regions on non-readable textures. Read back
    // their actual rectangle once, without changing or owning the borrowed asset.
    private static Texture2D ReadPortrait(Sprite portrait)
    {
        var source = portrait.texture;
        var rect = portrait.packed ? portrait.textureRect : portrait.rect;
        var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Texture2D? readable = null;
        try
        {
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(rect, 0, 0, false);
            if (portrait.packed && portrait.packingRotation != SpritePackingRotation.None)
            {
                var pixels = readable.GetPixels().ToArray();
                var unrotated = (Color[])pixels.Clone();
                bool flipX = portrait.packingRotation is SpritePackingRotation.FlipHorizontal or SpritePackingRotation.Rotate180;
                bool flipY = portrait.packingRotation is SpritePackingRotation.FlipVertical or SpritePackingRotation.Rotate180;
                for (int y = 0; y < readable.height; ++y)
                    for (int x = 0; x < readable.width; ++x)
                        unrotated[y * readable.width + x] = pixels[
                            (flipY ? readable.height - 1 - y : y) * readable.width
                            + (flipX ? readable.width - 1 - x : x)];
                readable.SetPixels(unrotated);
            }
            readable.Apply(false, false);
            return readable;
        }
        catch { if (readable != null) Object.Destroy(readable); throw; }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
    }

    private Sprite CreateZombiePacket(Sprite portrait)
    {
        var readable = ReadPortrait(portrait);
        var texture = Retain(new Texture2D(128, 176, TextureFormat.RGBA32, false));
        try
        {
            var pixels = packetTexture!.GetPixels().ToArray();
            var source = readable.GetPixels().ToArray();
            int stride = readable.width;
            int minX = stride, minY = readable.height, maxX = -1, maxY = -1;
            for (int y = 0; y < readable.height; ++y)
                for (int x = 0; x < stride; ++x)
                    if (source[y * stride + x].a > 0)
                    {
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                    }
            if (maxX < 0) { minX = minY = 0; maxX = stride - 1; maxY = readable.height - 1; }
            int sourceWidth = maxX - minX + 1, sourceHeight = maxY - minY + 1;
            float scale = Math.Min(104f / sourceWidth, 123f / sourceHeight);
            int width = Math.Max(1, (int)(sourceWidth * scale)), height = Math.Max(1, (int)(sourceHeight * scale));
            int left = (128 - width) / 2, bottom = 25 + (123 - height) / 2;
            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                {
                    float sx = (x + .5f) * sourceWidth / width - .5f, sy = (y + .5f) * sourceHeight / height - .5f;
                    int x0 = Math.Clamp((int)sx, 0, sourceWidth - 1), y0 = Math.Clamp((int)sy, 0, sourceHeight - 1);
                    int x1 = Math.Min(x0 + 1, sourceWidth - 1), y1 = Math.Min(y0 + 1, sourceHeight - 1);
                    float fx = Math.Clamp(sx - x0, 0, 1), fy = Math.Clamp(sy - y0, 0, 1);
                    var a = source[(minY + y0) * stride + minX + x0]; var b = source[(minY + y0) * stride + minX + x1];
                    var c = source[(minY + y1) * stride + minX + x0]; var d = source[(minY + y1) * stride + minX + x1];
                    float mix(float av, float bv, float cv, float dv) => (av + (bv - av) * fx) * (1 - fy) + (cv + (dv - cv) * fx) * fy;
                    var foreground = new Color(mix(a.r, b.r, c.r, d.r), mix(a.g, b.g, c.g, d.g),
                        mix(a.b, b.b, c.b, d.b), mix(a.a, b.a, c.a, d.a));
                    int index = (bottom + y) * 128 + left + x;
                    var background = pixels[index];
                    pixels[index] = new Color(foreground.r * foreground.a + background.r * (1 - foreground.a),
                        foreground.g * foreground.a + background.g * (1 - foreground.a),
                        foreground.b * foreground.a + background.b * (1 - foreground.a),
                        foreground.a + background.a * (1 - foreground.a));
                }
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Retain(Sprite.Create(texture, new Rect(0, 0, 128, 176), new Vector2(.5f, .5f), 100));
            sprite.name = "DebugZombiePacket.Dynamic." + portrait.name;
            texture.name = sprite.name;
            zombieCards.Add(portrait.Pointer, (sprite, texture)); cardSprites.Add(sprite.Pointer);
            return sprite;
        }
        catch { Object.Destroy(texture); throw; }
        finally { Object.Destroy(readable); }
    }

    // Complete native portraits already include their variant heads.
    internal Sprite? ZombieHead(ZombieType type) => null;

    internal Sprite Eyes()
    {
        if (eyes != null) return eyes;
        using var stream = typeof(DebugCardArt).Assembly.GetManifestResourceStream("FusionMod.DebugCards.eyes-zombie.png")
            ?? throw new InvalidDataException("Missing eyes zombie card preview.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var texture = Retain(new Texture2D(2, 2, TextureFormat.RGBA32, false));
        try
        {
            if (!ImageConversion.LoadImage(texture, bytes.ToArray(), false)) throw new InvalidDataException("Invalid eyes zombie card preview.");
            eyes = Retain(Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, 0), 100));
            eyesTexture = texture;
            return eyes;
        }
        catch { Object.Destroy(texture); throw; }
    }

    public void Dispose()
    {
        foreach (var sprite in rosterSprites) Object.Destroy(sprite);
        foreach (var texture in rosterTextures) Object.Destroy(texture);
        rosterSprites.Clear(); rosterTextures.Clear();
        foreach (var sprite in fusionPortraits.Values) Object.Destroy(sprite);
        fusionPortraits.Clear(); cardSprites.Clear();
        foreach (var card in zombieCards.Values) { Object.Destroy(card.Sprite); Object.Destroy(card.Texture); }
        zombieCards.Clear();
        if (atlas != null) Object.Destroy(atlas);
        if (packet != null) Object.Destroy(packet);
        if (packetTexture != null) Object.Destroy(packetTexture);
        atlas = packetTexture = null; packet = null;
        foreach (var model in plants.Values) model.Dispose();
        portraits.Dispose();
        foreach (var model in levels.Values) model.Dispose();
        levels.Clear();
        plants.Clear();
        zombieNames.Clear();
        if (eyes != null) Object.Destroy(eyes);
        if (eyesTexture != null) Object.Destroy(eyesTexture);
        eyes = null; eyesTexture = null;
    }
}
