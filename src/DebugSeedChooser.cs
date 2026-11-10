using HarmonyLib;
using Il2CppReloaded.DataModels;
using Il2CppReloaded.Gameplay;
using Il2CppSource.DataModels;
using Il2CppTekly.Common.Presentables;
using Il2CppTekly.DataModels.Models;
using Il2CppTekly.DataModels.Binders;
using Il2CppTekly.PanelViews;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using SeedList = Il2CppSystem.Collections.Generic.List<Il2CppReloaded.Gameplay.ChosenSeed>;

namespace DebugMod;

// Both decks use the existing chooser, bindings, card flights and panel animator.
// Zombie indices are temporary chooser slots, never registered plants or save data.
internal static class DebugSeedChooser
{
    private enum Phase { Plants, HidingPlants, Zombies, Done }
    private static Phase phase;
    private static SeedChooserScreen? screen;
    private static SeedList? plantSeeds;
    private static readonly Dictionary<IntPtr, int> zombieIndices = new();
    internal static SeedChooserScreen? SelectionScreen => phase is Phase.Plants or Phase.Zombies ? screen : null;
    internal static int ZombieIndex(ChosenSeed seed) => zombieIndices.TryGetValue(seed.Pointer, out int index) ? index : -1;
    private static int? confirmedPlantCount;
    private static PanelView? panel;
    private sealed class EntriesState
    {
        internal IntPtr Seeds;
        internal readonly Dictionary<int, SeedChooserEntryModel> All = new();
        internal readonly HashSet<int> Visible = new();
    }
    private static readonly Dictionary<IntPtr, EntriesState> entries = new();
    private static readonly Dictionary<IntPtr, IntPtr> imitaterDialogs = new();

    internal static Sprite? CardBackground { get; private set; }
    internal static bool ZombiePhase => phase == Phase.Zombies && screen != null;
    internal static SeedChooserScreen? PlantScreen => phase == Phase.Plants ? screen : null;
    internal static Board? SessionBoard => DebugMode.SessionBoard;
    // UpdateWidth queries Board.GetNumSeedsInBank again. Capacity is ten
    // while choosing, then exactly the committed deck size during gameplay.
    internal static int BankCapacity => confirmedPlantCount ?? 10;
    internal static void ApiBankCount(int count) => confirmedPlantCount = count;
    internal static bool Owns(SeedChooserScreen? value) => value != null && DebugMode.Owns(value.mBoard);
    internal static bool ApiReady(SeedChooserScreen value) => value.Visible && value.mSeedsInFlight == 0
        && value.mChooseState == SeedChooserState.ChooseNormal
        && (!Owns(value) || ((phase is Phase.Plants or Phase.Zombies)
            && BoundPanel(value) is { State: PresentableState.Shown }));

    internal static void Begin() { confirmedPlantCount = null; phase = Phase.Plants; DebugPlantCards.Begin(); }

    internal static void Update()
    {
        if (phase != Phase.HidingPlants || screen == null || panel == null) return;
        // The gameplay tree can hide the parent panel container while the
        // nested chooser keeps State == Shown. Effective visibility owns this
        // boundary; waiting only for the child's state can never complete.
        if (panel.gameObject.activeInHierarchy && panel.State != PresentableState.Hidden) return;
        phase = Phase.Zombies;
        var seeds = new SeedList();
        zombieIndices.Clear();
        for (int i = 0; i < Math.Max((int)SeedType.NumSeedsInChooser, DebugMode.ZombieCount); ++i)
        {
            var original = plantSeeds![Math.Min(i, (int)SeedType.NumSeedsInChooser - 1)];
            int x = original.mX, y = original.mY;
            if (i < DebugMode.ZombieCount) screen.GetSeedPositionInChooser(i % 48, ref x, ref y);
            var zombieSeed = new ChosenSeed
            {
                // Pointer identity owns the zombie choice. Every native callback
                // receives a valid ordinary seed, including beyond 49 entries.
                mSeedType = SeedType.Peashooter, mImitaterType = SeedType.None,
                mX = x, mY = y, mStartX = x, mStartY = y, mEndX = x, mEndY = y,
                mSeedState = i < DebugMode.ZombieCount ? ChosenSeedState.SeedInChooser : ChosenSeedState.SeedPacketHidden
            };
            seeds.Add(zombieSeed); zombieIndices.Add(zombieSeed.Pointer, i);
        }
        screen.mChosenSeeds = seeds;
        DebugPlantCards.ZombiesStarted();
        ResetBank(0);
        screen.mDirty = true;
        screen.Visible = true;
        screen.mApp.ShowSeedChooserScreen();
        Mod.Instance.LoggerInstance.Msg("Debug: Zombie selection opened.");
    }

    internal static PanelView? BoundPanel(SeedChooserScreen value)
    {
        // Main-menu and gameplay panels can coexist. A prefab name does not
        // identify the instance affected by the gameplay tree transition.
        foreach (var binding in Object.FindObjectsOfType<BinderContainer>())
        {
            if (!binding.m_hasBound || !binding.gameObject.activeInHierarchy) continue;
            string key = binding.ResolveFullKey();
            if (string.IsNullOrEmpty(key) || !binding.TryGet(key, out IModel model)) continue;
            var entry = model?.TryCast<SeedChooserEntryModel>();
            var chooserModel = model?.TryCast<SeedChooserDataModel>();
            if (entry?.m_seedChooserScreen?.Pointer != value.Pointer
                && chooserModel?.m_seedChooserScreen?.Pointer != value.Pointer) continue;
            var owner = binding.GetComponentInParent<PanelView>();
            if (owner != null && owner.Id == "seedChooser") return owner;
        }
        return null;
    }

    private static ChosenSeed[] Selected() => Enumerable.Range(0, screen!.mChosenSeeds.Count)
        .Select(i => screen.mChosenSeeds[i]).Where(seed => seed.mSeedState == ChosenSeedState.SeedInBank)
        .OrderBy(seed => seed.mSeedIndexInBank).ToArray();

    private static void ResetBank(int selected)
    {
        screen!.mIsReady = false;
        screen.mSeedsInFlight = 0;
        for (int i = 0; i < screen.m_seedBankInfos.Count; ++i)
        {
            var info = screen.m_seedBankInfos[i];
            info.mSeedsInBank = i == 0 ? selected : 0;
            info.mIsReady = false;
            info.DirtyBank = true;
            for (int j = 0; j < info.mSeedBank.mSeedPackets.Length; ++j)
                {
                // None has no PlantDefinition; SetPacketType calls GetRefreshTime unconditionally.
                var packet = info.mSeedBank.mSeedPackets[j];
                packet.mPacketType = packet.mImitaterType = SeedType.None;
                packet.mRefreshCounter = packet.mRefreshTime = 0;
                packet.mActive = packet.mRefreshing = packet.mFlying = false;
            }
            info.mSeedBank.mIsDirty = true;
        }
    }

    // Native ChosenSeed.Selected resolves by SeedType, which cannot distinguish
    // fusion cards. Bank mutations use the actual chosen object and allocator.
    internal static bool Choose(SeedChooserScreen value, ChosenSeed seed, int player)
    {
        if (!Owns(value)) return true;
        if (phase is not (Phase.Plants or Phase.Zombies) || player != 0 || !value.Visible) return false;
        screen = value;
        if (seed.mSeedState == ChosenSeedState.SeedInBank) return Remove(value, seed.mSeedIndexInBank, player);
        if (seed.mSeedState != ChosenSeedState.SeedInChooser || Selected().Length >= 10) return false;
        value.AddChosenSeedToBank(seed, player);
        seed.mFlying = seed.mFlashing = false;
        value.mDirty = true;
        return false;
    }

    internal static bool Remove(SeedChooserScreen value, int slot, int player)
    {
        if (!Owns(value)) return true;
        if (phase is not (Phase.Plants or Phase.Zombies) || player != 0) return false;
        screen = value;
        var selected = Selected();
        var removed = selected.FirstOrDefault(seed => seed.mSeedIndexInBank == slot && seed.mPlayerIndex == player);
        if (removed == null) return false;
        removed.mSeedState = ChosenSeedState.SeedPacketHidden;
        removed.mSeedIndexInBank = -1;
        removed.mFlying = removed.mFlashing = false;
        ResetBank(0);
        foreach (var seed in selected)
            if (seed.Pointer != removed.Pointer) value.AddChosenSeedToBank(seed, player);
        if (ZombiePhase)
        {
            removed.mSeedState = ChosenSeedState.SeedInChooser;
            int x = removed.mEndX, y = removed.mEndY;
            value.GetSeedPositionInChooser(ZombieIndex(removed) % 48, ref x, ref y);
            removed.mStartX = removed.mEndX = x;
            removed.mStartY = removed.mEndY = y;
        }
        else DebugPlantCards.RefreshChoices();
        value.mDirty = true;
        return false;
    }

    internal static object ApiSelect(SeedChooserScreen value, int[] indices, bool confirm)
    {
        bool owned = Owns(value);
        if (!ApiReady(value))
            throw new ApiError("CHOOSER_NOT_READY", "Seed chooser is not ready.");
        if (indices.Length == 0 && !(owned && ZombiePhase) || indices.Length > (owned ? 10 : value.mBoard.GetNumSeedsInBank())
            || indices.Distinct().Count() != indices.Length || indices.Any(i => i < 0 || i >= value.mChosenSeeds.Count))
            throw new ApiError("INVALID_SELECTION", "Invalid chooser indices or selection size.");
        var chosen = indices.Select(i => value.mChosenSeeds[i]).ToArray();
        if (!owned && chosen.Any(seed => seed.mSeedState == ChosenSeedState.SeedPacketHidden))
            throw new ApiError("PLANT_LOCKED", "A selected plant is unavailable in this chooser.");
        if (owned) { screen = value; ResetBank(0); }
        else
        {
            foreach (var seed in value.mChosenSeeds)
                if (seed.mSeedState == ChosenSeedState.SeedInBank) value.ClickedSeedInBank(seed, 0);
        }
        foreach (var seed in value.mChosenSeeds)
            if (seed.mSeedState == ChosenSeedState.SeedInBank)
            { seed.mSeedState = ChosenSeedState.SeedInChooser; seed.mSeedIndexInBank = -1; }
        foreach (var seed in chosen)
        {
            seed.mSeedState = ChosenSeedState.SeedInChooser;
            value.AddChosenSeedToBank(seed, 0);
            seed.mFlying = seed.mFlashing = false;
            seed.mSeedState = ChosenSeedState.SeedInBank;
        }
        value.mSeedsInFlight = 0; value.mDirty = true;
        if (confirm)
        {
            if (!owned || Confirm(value)) value.OnStartButton();
        }
        return new { selected = indices, confirmed = confirm };
    }

    internal static bool Close(SeedChooserScreen value) => !Owns(value) || phase == Phase.Done || Confirm(value);

    internal static bool Confirm(SeedChooserScreen value)
    {
        if (!Owns(value)) return true;
        if (phase is Phase.HidingPlants or Phase.Done) return false;
        screen = value;
        if (screen.mSeedsInFlight != 0 || screen.mChooseState != SeedChooserState.ChooseNormal) return false;
        var selected = Selected();
        if (selected.Length > 10 || phase == Phase.Plants && selected.Length == 0) return false;
        if (phase == Phase.Plants)
        {
            panel = BoundPanel(value);
            if (panel == null)
            {
                Mod.Instance.LoggerInstance.Error("Debug: bound seedChooser panel is unavailable; keeping plant selection open.");
                return false;
            }
            plantSeeds = screen.mChosenSeeds;
            DebugPlantCards.CaptureBank(screen);
            phase = Phase.HidingPlants;
            screen.Visible = false;
            Mod.Instance.LoggerInstance.Msg($"Debug: Plants confirmed; native panel {panel.Id}, state {panel.State}.");
            screen.mApp.HideSeedChooserScreen();
        }
        else
        {
            DebugZombieChooserTheme.Restore();
            DebugMode.SetZombies(selected.Select(ZombieIndex));
            screen.mChosenSeeds = plantSeeds;
            var plants = Selected();
            int count = plants.Length;
            confirmedPlantCount = count;
            phase = Phase.Done;
            // Native close copies the original plant choices, hides the panel,
            // ends choosing and lets CutScene.Update finish the ordinary intro.
            screen.CloseSeedChooser();
            // Native finalization visits only its 49 original entries. Rebuild
            // the bank through the native allocator for all selected variants.
            ResetBank(0);
            foreach (var seed in plants) screen.AddChosenSeedToBank(seed, 0);
            // ResetBank/AddChosenSeedToBank mark the native bank dirty. Its
            // next model tick rebuilds live entries with the restored identities.
            screen.mBoard.mSeedBank.UpdateWidth();
            Mod.Instance.LoggerInstance.Msg($"Debug: Seed bank ready; {count} plants, {screen.mBoard.mSeedBank.mNumPackets} card slots.");
        }
        return false;
    }

    internal static void Updated(SeedChooserScreen value)
    {
        if (!Owns(value) || phase is Phase.Done or Phase.HidingPlants) return;
        screen = value;
        int count = Selected().Length;
        int total = count;
        bool ready = value.mSeedsInFlight == 0 && total <= 10 && (ZombiePhase || total > 0);
        value.mIsReady = ready;
        value.m_seedBankInfos[0].mIsReady = ready;
        value.mNumSeedsToChoose = 10;
        // Borrow the actual card frame from the loaded native chooser prefab.
        CardBackground ??= DebugMode.Art.PacketBackground;
    }

    internal static bool Entries(SeedChooserDataModel model)
    {
        var chooser = model.m_seedChooserScreen;
        if (!Owns(chooser)) return true;
        screen = chooser;
        if (!ZombiePhase) DebugPlantCards.Prepare(chooser);
        if (!entries.TryGetValue(model.Pointer, out var state))
            entries.Add(model.Pointer, state = new EntriesState());
        bool reset = state.Seeds != chooser.mChosenSeeds.Pointer;
        var visible = new List<int>();
        int count = chooser.mChosenSeeds.Count;
        for (int i = 0; i < count; ++i)
        {
            var seed = chooser.mChosenSeeds[i];
            if (DebugPlantCards.OnPage(i)) visible.Add(i);
        }
        bool allChanged = reset;
        bool visibleChanged = reset || !state.Visible.SetEquals(visible);
        bool allDisabled = model.m_entriesModel.DisableModifiedTrigger;
        bool visibleDisabled = model.m_entriesUnlockedModel.DisableModifiedTrigger;
        model.m_entriesModel.DisableModifiedTrigger = model.m_entriesUnlockedModel.DisableModifiedTrigger = true;
        try
        {
            if (reset)
            {
                model.m_entriesUnlockedModel.Clear();
                model.m_entriesModel.Clear();
                state.All.Clear(); state.Visible.Clear();
                state.Seeds = chooser.mChosenSeeds.Pointer;
            }
            // Fixed all.N bindings (including Imitator) keep their models.
            // Extra models are created once, on first visit, and stay owned by all.
            for (int i = 0; i < count; ++i)
            {
                if (state.All.ContainsKey(i) || i >= (int)SeedType.NumSeedsInChooser && !visible.Contains(i)) continue;
                var seed = chooser.mChosenSeeds[i];
                var definition = model.m_dataService.GetPlantDefinition(seed.mSeedType);
                var entry = new SeedChooserEntryModel(definition, seed, chooser, model, false, i);
                Entry(entry);
                state.All.Add(i, entry);
                model.m_entriesModel.Add(i, entry.Cast<IModel>());
                allChanged = true;
            }
            if (visibleChanged)
            {
                // One native notification after the complete replacement, rather
                // than a ListBinder rebuild for every individual Add/Remove.
                model.m_entriesUnlockedModel.Clear();
                foreach (int i in visible)
                    model.m_entriesUnlockedModel.Add(i.ToString(), state.All[i].Cast<IModel>(), ReferenceType.Shared);
                state.Visible.Clear(); state.Visible.UnionWith(visible);
            }
        }
        finally
        {
            model.m_entriesModel.DisableModifiedTrigger = allDisabled;
            model.m_entriesUnlockedModel.DisableModifiedTrigger = visibleDisabled;
            if (allChanged && !allDisabled) model.m_entriesModel.m_modified.Emit(model.m_entriesModel);
            if (visibleChanged && !visibleDisabled) model.m_entriesUnlockedModel.m_modified.Emit(model.m_entriesUnlockedModel);
        }
        model.m_totalModel.Value = ZombiePhase ? DebugMode.ZombieCount : (int)SeedType.NumSeedsInChooser;
        model.m_chooseStateModel.Value = (int)chooser.mChooseState;
        model.m_isReadyModel.Value = chooser.mIsReady;
        return false;
    }

    internal static void Entry(SeedChooserEntryModel model)
    {
        if (!Owns(model.m_seedChooserScreen)) return;
        model.SetLocked(false);
        if (!ZombiePhase)
        {
            DebugPlantCards.Entry(model);
            model.m_nameModel.Value = (model.m_nameModel.Value ?? "").TrimStart('$');
            model.m_toolTipModel.Value = (model.m_toolTipModel.Value ?? "").TrimStart('$');
            return;
        }
        int index = ZombieIndex(model.m_chosenSeed);
        if (index < 0 || index >= DebugMode.ZombieCount)
        {
            model.m_nameModel.Value = model.m_toolTipModel.Value = "";
            model.m_thumbnail.Reference = null;
            model.m_thumbnail.Value = null;
            return;
        }
        model.m_nameModel.Value = DebugMode.ZombieName(index);
        model.m_toolTipModel.Value = DebugMode.ZombieName(index);
        model.m_sunCostModel.Value = 0;
        model.m_isImitaterModel.Value = false;
        model.m_notSuggestedModel.Value = false;
        model.m_thumbnail.Reference = null;
        model.m_thumbnail.Value = DebugMode.ZombieArt(index) ?? CardBackground;
    }

    internal static void BankEntry(SeedBankEntryModel model)
    {
        if (!DebugMode.Owns(model.m_gameplayActivity)) return;
        if (!ZombiePhase)
        {
            DebugPlantCards.BankEntry(model);
            model.m_nameModel.Value = (model.m_nameModel.Value ?? "").TrimStart('$');
            return;
        }
        var selected = screen == null ? null : Enumerable.Range(0, screen.mChosenSeeds.Count)
            .Select(i => screen.mChosenSeeds[i]).FirstOrDefault(seed => seed.mSeedIndexInBank == model.Packet.mIndex
                && seed.mSeedState is ChosenSeedState.SeedInBank or ChosenSeedState.SeedFlyingToBank);
        if (selected == null) return;
        int index = ZombieIndex(selected);
        if (index < 0 || index >= DebugMode.ZombieCount) return;
        model.m_nameModel.Value = DebugMode.ZombieName(index);
        model.m_sunCostModel.Value = 0;
        model.m_isImitaterModel.Value = false;
        model.m_thumbnail.Reference = null;
        model.m_thumbnail.Value = DebugMode.ZombieArt(index) ?? CardBackground;
    }

    internal static bool RefreshImitater(SeedChooserDataModel model)
    {
        IntPtr dialog = ZombiePhase ? IntPtr.Zero : model.m_imitaterDialog?.Pointer ?? IntPtr.Zero;
        if (imitaterDialogs.TryGetValue(model.Pointer, out var previous) && previous == dialog) return false;
        imitaterDialogs[model.Pointer] = dialog;
        return true;
    }

    internal static void Clear()
    {
        DebugZombieChooserTheme.Clear();
        DebugPlantCards.Clear(); zombieIndices.Clear();
        entries.Clear(); imitaterDialogs.Clear();
        phase = Phase.Done;
        confirmedPlantCount = null;
        screen = null; plantSeeds = null; panel = null; CardBackground = null;
    }
}

[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.OnStartButton))]
internal static class DebugNativeConfirmPatch
{
    private static bool Prefix(SeedChooserScreen __instance) => DebugSeedChooser.Confirm(__instance);
}
[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.Update))]
internal static class DebugNativeChooserTickPatch
{
    private static void Postfix(SeedChooserScreen __instance) => DebugSeedChooser.Updated(__instance);
}
[HarmonyPatch(typeof(SeedChooserDataModel), nameof(SeedChooserDataModel.UpdateEntries))]
internal static class DebugNativeEntriesPatch
{
    private static bool Prefix(SeedChooserDataModel __instance) => DebugSeedChooser.Entries(__instance);
}
[HarmonyPatch(typeof(SeedChooserEntryModel), nameof(SeedChooserEntryModel.OnTick))]
internal static class DebugNativeEntryTickPatch
{
    private static void Postfix(SeedChooserEntryModel __instance) => DebugSeedChooser.Entry(__instance);
}
[HarmonyPatch(typeof(SeedBankEntryModel), nameof(SeedBankEntryModel.OnTick))]
internal static class DebugNativeBankTickPatch
{
    private static void Postfix(SeedBankEntryModel __instance) => DebugSeedChooser.BankEntry(__instance);
}
[HarmonyPatch(typeof(SeedChooserDataModel), nameof(SeedChooserDataModel.UpdateImitaterEntries))]
internal static class DebugNativeZombieImitaterPatch
{
    private static bool Prefix(SeedChooserDataModel __instance, out (SeedList Seeds, bool Disabled)? __state)
    {
        __state = null;
        if (!DebugSeedChooser.Owns(__instance.m_seedChooserScreen)) return true;
        if (!DebugSeedChooser.RefreshImitater(__instance)) return false;
        if (!DebugSeedChooser.ZombiePhase)
        {
            // NumSeedTypes includes four non-chooser types. ImitaterDialog owns
            // only NumSeedsInChooser (49) entries. Custom cards
            // are selectable directly and must never index beyond that dialog.
            var screen = __instance.m_seedChooserScreen;
            __state = (screen.mChosenSeeds, __instance.m_imitaterEntriesModel.DisableModifiedTrigger);
            __instance.m_imitaterEntriesModel.DisableModifiedTrigger = true;
            var native = new SeedList();
            for (int i = 0; i < (int)SeedType.NumSeedsInChooser; ++i) native.Add(__state.Value.Seeds[i]);
            screen.mChosenSeeds = native;
            return true;
        }
        __instance.m_imitaterEntriesModel.Clear();
        return false;
    }
    private static void Finalizer(SeedChooserDataModel __instance, (SeedList Seeds, bool Disabled)? __state)
    {
        if (__state is not { } state) return;
        __instance.m_seedChooserScreen.mChosenSeeds = state.Seeds;
        __instance.m_imitaterEntriesModel.DisableModifiedTrigger = state.Disabled;
        if (!state.Disabled) __instance.m_imitaterEntriesModel.m_modified.Emit(__instance.m_imitaterEntriesModel);
    }
}
[HarmonyPatch(typeof(SeedChooserDataModel), nameof(SeedChooserDataModel.OnTick))]
internal static class DebugNativeChooserModelTickPatch
{
    private static void Postfix(SeedChooserDataModel __instance)
    {
        if (DebugSeedChooser.Owns(__instance.m_seedChooserScreen))
            __instance.m_isImitaterUnlocked.Value = !DebugSeedChooser.ZombiePhase;
    }
}

[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.CloseSeedChooser))]
internal static class DebugNativeClosePatch
{
    private static bool Prefix(SeedChooserScreen __instance) => DebugSeedChooser.Close(__instance);
}
[HarmonyPatch(typeof(Board), nameof(Board.ChooseSeedsOnCurrentLevel))]
internal static class DebugNativeChooseSeedsPatch
{
    private static bool Prefix(Board __instance, ref bool __result)
    { if (!DebugMode.Owns(__instance)) return true; __result = true; return false; }
}

[HarmonyPatch(typeof(ChosenSeed), nameof(ChosenSeed.Selected))]
internal static class DebugChosenIdentityPatch
{
    private static bool Prefix(ChosenSeed __instance, SeedChooserScreen seedChooserScreen, int playerIndex) =>
        DebugSeedChooser.Choose(seedChooserScreen, __instance, playerIndex);
}
[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.ClickedSeedInChooser))]
internal static class DebugChooseIdentityPatch
{
    private static bool Prefix(SeedChooserScreen __instance, ChosenSeed theChosenSeed, int playerIndex) =>
        DebugSeedChooser.Choose(__instance, theChosenSeed, playerIndex);
}
[HarmonyPatch(typeof(SeedChooserDataModel), nameof(SeedChooserDataModel.UpdateZombieEntries))]
internal static class DebugUnusedVersusEntriesPatch
{
    private static bool Prefix(SeedChooserDataModel __instance) => !DebugSeedChooser.Owns(__instance.m_seedChooserScreen);
}

[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.ClickedSeedInBank), new[] { typeof(SeedPacket), typeof(int) })]
internal static class DebugNativeBankRemovalPatch
{
    private static bool Prefix(SeedChooserScreen __instance, SeedPacket thePacket, int playerIndex)
    {
        return DebugSeedChooser.Remove(__instance, thePacket.mIndex, playerIndex);
    }
}

[HarmonyPatch(typeof(SeedChooserScreen), nameof(SeedChooserScreen.ClickedSeedInBank), new[] { typeof(ChosenSeed), typeof(int) })]
internal static class DebugNativeChosenRemovalPatch
{
    private static bool Prefix(SeedChooserScreen __instance, ChosenSeed seed, int playerIndex) =>
        DebugSeedChooser.Remove(__instance, seed.mSeedIndexInBank, playerIndex);
}

