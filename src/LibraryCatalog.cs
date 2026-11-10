using Il2CppReloaded.Gameplay;
using ReplantedModUi;
using UnityEngine;

namespace DebugMod;

internal sealed record Card(SeedType NativeType, SeedType Marker, string Name, Func<Sprite?> Portrait,
    Func<Board, int> Cost, Func<Board, int, int, PlantingReason>? CanPlant, Func<Board, int, int, bool>? Place, string Owner = "", bool Ingredient = false);
internal sealed record ZombieCard(ZombieType NativeType, string Name, Func<Sprite?> Portrait,
    Func<Sprite?> Thumbnail, Func<Board, int, Zombie?> Spawn);

internal static class LibraryCatalog
{
    private static readonly Func<IEnumerable<(SeedType NativeType, SeedType Marker, string Name, Func<Sprite?> Portrait,
        Func<Board, int> Cost, Func<Board, int, int, PlantingReason>? CanPlant, string Owner)>>? plants =
        OptionalModApi.Bind<Func<IEnumerable<(SeedType, SeedType, string, Func<Sprite?>, Func<Board, int>,
            Func<Board, int, int, PlantingReason>?, string)>>>("PlantLibrary", "PlantLibrary.DebugCatalog", "Entries");
    private static readonly Func<IEnumerable<(ZombieType NativeType, string Name, Func<Sprite?> Portrait,
        Func<Sprite?> Thumbnail, Func<Board, int, Zombie?> Spawn)>>? zombies =
        OptionalModApi.Bind<Func<IEnumerable<(ZombieType, string, Func<Sprite?>, Func<Sprite?>, Func<Board, int, Zombie?>)>>>(
            "ZombieLibrary", "ZombieLibrary.DebugCatalog", "SelectionEntries");
    private static readonly Func<Board, SeedType, SeedType, bool, int, int, bool>? fusionPlace =
        OptionalModApi.Bind<Func<Board, SeedType, SeedType, bool, int, int, bool>>("FusionMod", "FusionMod.DebugFusionIntegration", "Place");
    private static readonly Func<Board, IEnumerable<(SeedType NativeType, SeedType Marker, string Name, string Portrait, int Cost)>>? ingredients =
        OptionalModApi.Bind<Func<Board, IEnumerable<(SeedType, SeedType, string, string, int)>>>("FusionMod", "FusionMod.DebugFusionIntegration", "Ingredients");

    private static readonly Func<SeedType, SeedType, string?>? packetResource =
        OptionalModApi.Bind<Func<SeedType, SeedType, string?>>("FusionMod", "FusionMod.DebugFusionIntegration", "PacketResource");

    private static readonly Func<Board, SeedType, SeedType, bool, int, int, int, bool>? fusionApiPlace =
        OptionalModApi.Bind<Func<Board, SeedType, SeedType, bool, int, int, int, bool>>("FusionMod", "FusionMod.DebugFusionIntegration", "PlaceApi");
    internal static bool? PlaceApi(Board board, Card? card, SeedType type, SeedType marker, int col, int row, int offsetY)
    {
        if (card != null && card.Owner != "fusion") return null;
        return fusionApiPlace?.Invoke(board, type, marker, card == null || card.Ingredient, col, row, offsetY)
            ?? (card == null ? fusionPlace?.Invoke(board, type, marker, true, col, row) : card.Place?.Invoke(board, col, row));
    }

    private static readonly Func<Board, int, int, object>? fusionSlots = BindSlots();
    private static Func<Board, int, int, object>? BindSlots()
    {
        const string owner = "FusionMod.DebugFusionIntegration";
        var method = OptionalModApi.Type("FusionMod", owner)?.GetMethod("Slots", OptionalModApi.Flags,
            null, new[] { typeof(Board), typeof(int), typeof(int) }, null);
        return method?.ReturnType == typeof(object)
            ? OptionalModApi.Bind<Func<Board, int, int, object>>("FusionMod", owner, "Slots") : null;
    }
    internal static object SlotDiagnostics(Board board, int col, int row) => new {
        route = fusionApiPlace != null ? "fusion-api" : fusionPlace != null ? "fusion-legacy" : "native",
        fusionApiBound = fusionApiPlace != null, fusionLegacyBound = fusionPlace != null,
        fusionSlotsBound = fusionSlots != null, owner = fusionSlots?.Invoke(board, col, row)
    };

    internal static IEnumerable<Card> Plants(Board board)
    {
        foreach (var item in ingredients?.Invoke(board) ?? Enumerable.Empty<(SeedType NativeType, SeedType Marker, string Name, string Portrait, int Cost)>())
            yield return new(item.NativeType, item.Marker, item.Name, () => DebugMode.Art.Fusion(item.Portrait),
                _ => item.Cost, null, (value, col, row) => fusionPlace!(value, item.NativeType, item.Marker, true, col, row), "fusion", true);
        if (plants == null) yield break;
        foreach (var item in plants().OrderBy(item => item.Name))
        {
            string? resource = item.Owner == "fusion" ? packetResource?.Invoke(item.NativeType, item.Marker) : null;
            yield return new(item.NativeType, item.Marker, item.Name,
                resource == null ? item.Portrait : () => DebugMode.Art.Fusion(resource), item.Cost, item.CanPlant,
                item.Owner == "fusion" && fusionPlace != null
                    ? (value, col, row) => fusionPlace(value, item.NativeType, item.Marker, false, col, row) : null, item.Owner);
        }
    }

    internal static ZombieCard[] Zombies() => zombies?.Invoke().Select(item => new ZombieCard(item.NativeType,
        item.Name, item.Portrait, item.Thumbnail, item.Spawn)).ToArray() ?? Array.Empty<ZombieCard>();
}
