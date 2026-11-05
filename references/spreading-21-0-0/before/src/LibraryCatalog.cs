using Il2CppReloaded.Gameplay;
using ReplantedModUi;
using UnityEngine;

namespace DebugMod;

internal sealed record Card(SeedType NativeType, SeedType Marker, string Name, Func<Sprite?> Portrait,
    Func<Board, int> Cost, Func<Board, int, int, PlantingReason>? CanPlant, Func<Board, int, int, bool>? Place);
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
            "ZombieLibrary", "ZombieLibrary.DebugCatalog", "Entries");
    private static readonly Func<Board, SeedType, SeedType, bool, int, int, bool>? fusionPlace =
        OptionalModApi.Bind<Func<Board, SeedType, SeedType, bool, int, int, bool>>("FusionMod", "FusionMod.DebugFusionIntegration", "Place");
    private static readonly Func<Board, IEnumerable<(SeedType NativeType, SeedType Marker, string Name, string Portrait, int Cost)>>? ingredients =
        OptionalModApi.Bind<Func<Board, IEnumerable<(SeedType, SeedType, string, string, int)>>>("FusionMod", "FusionMod.DebugFusionIntegration", "Ingredients");

    internal static bool? PlaceNative(Board board, SeedType type, int col, int row) => fusionPlace?.Invoke(board, type, SeedType.None, true, col, row);

    internal static IEnumerable<Card> Plants(Board board)
    {
        foreach (var item in ingredients?.Invoke(board) ?? Enumerable.Empty<(SeedType NativeType, SeedType Marker, string Name, string Portrait, int Cost)>())
            yield return new(item.NativeType, item.Marker, item.Name, () => DebugMode.Art.Fusion(item.Portrait),
                _ => item.Cost, null, (value, col, row) => fusionPlace!(value, item.NativeType, item.Marker, true, col, row));
        if (plants == null) yield break;
        foreach (var item in plants().OrderBy(item => item.Name))
            yield return new(item.NativeType, item.Marker, item.Name, item.Portrait, item.Cost, item.CanPlant,
                item.Owner == "fusion" && fusionPlace != null
                    ? (value, col, row) => fusionPlace(value, item.NativeType, item.Marker, false, col, row) : null);
    }

    internal static ZombieCard[] Zombies() => zombies?.Invoke().Select(item => new ZombieCard(item.NativeType,
        item.Name, item.Portrait, item.Thumbnail, item.Spawn)).ToArray() ?? Array.Empty<ZombieCard>();
}
