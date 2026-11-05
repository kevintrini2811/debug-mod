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
    private readonly Dictionary<ZombieType, (Sprite Sprite, Texture2D Texture)> zombieCards = new();
    private readonly HashSet<IntPtr> cardSprites = new();
    private Texture2D? atlas, packetTexture;
    private Texture2D? registeredAtlas;
    private readonly Dictionary<string, Sprite> registeredPackets = new();
    private Sprite? packet;
    internal Sprite PacketBackground => packet!;
    private readonly List<Sprite> rosterSprites = new();
    private readonly List<Texture2D> rosterTextures = new();
    internal Sprite Roster => rosterSprites[0];
    internal Sprite Counter => rosterSprites[1];
    internal Sprite RegisteredPacket(Sprite thumbnail) => registeredPackets.TryGetValue(thumbnail.name, out var packet) ? packet : thumbnail;
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
            registeredAtlas = LoadTexture("FusionMod.DebugCards.registered-zombie-packets.png");
            using (var index = Resource("FusionMod.DebugCards.registered-zombie-packets.json"))
                foreach (var entry in JsonSerializer.Deserialize<Packet[]>(index) ?? throw new InvalidDataException("Invalid zombie packet index."))
                {
                    var sprite = Retain(Sprite.Create(registeredAtlas, new Rect(entry.X, entry.Y, entry.Width, entry.Height), new Vector2(.5f, .5f), 100));
                    sprite.name = "DebugZombiePacket." + entry.Resource;
                    registeredPackets.Add(entry.Resource, sprite); cardSprites.Add(sprite.Pointer);
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
    internal Sprite EyesPacket()
    {
        var type = (ZombieType)(-1);
        if (zombieCards.TryGetValue(type, out var card)) return card.Sprite;
        Eyes();
        return CreateZombiePacket(type, eyesTexture!);
    }
    private Sprite CreateZombiePacket(ZombieType type, Texture2D portrait)
    {
        var texture = Retain(new Texture2D(128, 176, TextureFormat.RGBA32, false));
        try
        {
            var pixels = packetTexture!.GetPixels().ToArray();
            var source = portrait.GetPixels().ToArray();
            int sourceWidth = portrait.width, sourceHeight = portrait.height;
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
                    var a = source[y0 * sourceWidth + x0]; var b = source[y0 * sourceWidth + x1];
                    var c = source[y1 * sourceWidth + x0]; var d = source[y1 * sourceWidth + x1];
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
            zombieCards.Add(type, (sprite, texture)); cardSprites.Add(sprite.Pointer);
            return sprite;
        }
        catch { Object.Destroy(texture); throw; }
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
        foreach (var sprite in registeredPackets.Values) Object.Destroy(sprite);
        registeredPackets.Clear();
        if (registeredAtlas != null) Object.Destroy(registeredAtlas);
        registeredAtlas = null;
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
