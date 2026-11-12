using HarmonyLib;
using ReplantedModUi;
using Il2CppReloaded.DataModels;
using Il2CppReloaded.Gameplay;
using Il2CppSource.DataModels;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DebugMod;

// Native chooser entries and seed packets own selection, flights and the bank.
// Session metadata supplies only identities that native SeedType cannot express.
internal static class DebugPlantCards
{
    private static readonly Dictionary<IntPtr, Card> choices = new();
    private static readonly Dictionary<int, Card> bank = new();
    private static Card[] cards = Array.Empty<Card>();
    private static SeedChooserScreen? chooser;
    private static int page;
    private static GameObject? previous, next;
    private static Button? previousButton, nextButton;
    private static bool installing;
    internal static bool ExtraPage => page > 0;
    internal static bool HasBankType(SeedType type) => bank.Values.Any(card => card.NativeType == type);
    private const int PageSize = 48;
    internal static int CurrentPage => page;
    internal static int LastPage => DebugSeedChooser.ZombiePhase ? Math.Max(0, (DebugMode.ZombieCount - 1) / PageSize) : (cards.Length + PageSize - 1) / PageSize;
    internal static void ZombiesStarted()
    {
        page = 0; RefreshChoices(true);
        if (previousButton != null) previousButton.interactable = false;
        if (nextButton != null) nextButton.interactable = LastPage > 0;
    }

    internal static void Begin() => Clear();
    internal static int Cost(Card plant) => plant.Cost(DebugMode.SessionBoard ?? chooser?.mBoard
        ?? throw new InvalidOperationException("No active debug board for plant cost."));

    internal static void Prepare(SeedChooserScreen screen)
    {
        if (chooser?.Pointer == screen.Pointer || installing) return;
        installing = true;
        int originalCount = screen.mChosenSeeds.Count;
        try
        {
            chooser = screen;
            cards = LibraryCatalog.Plants(screen.mBoard).ToArray();
            foreach (var card in cards)
            {
                // Keep a real native type in every native callback. Pointer
                // identity distinguishes multiple variants of the same seed.
                var seed = new ChosenSeed { mSeedType = card.NativeType, mImitaterType = SeedType.None,
                    mSeedState = ChosenSeedState.SeedPacketHidden };
                screen.mChosenSeeds.Add(seed);
                choices.Add(seed.Pointer, card);
            }
            SetPage(0);
        }
        catch
        {
            while (screen.mChosenSeeds.Count > originalCount) screen.mChosenSeeds.RemoveAt(screen.mChosenSeeds.Count - 1);
            chooser = null; choices.Clear(); throw;
        }
        finally { installing = false; }
    }

    internal static bool Visible(ChosenSeed seed) => seed.mSeedState == ChosenSeedState.SeedInChooser;

    internal static bool OnPage(int index)
    {
        if (DebugSeedChooser.ZombiePhase) return index < DebugMode.ZombieCount && index / PageSize == page;
        int first = (int)SeedType.NumSeedsInChooser;
        return index < first ? page == 0 && index != (int)SeedType.Imitater
            : page > 0 && index - first >= (page - 1) * PageSize && index - first < page * PageSize;
    }

    internal static (bool Found, SeedType NativeType, SeedType Marker) CursorIdentity(CursorObject cursor)
    {
        if (DebugMode.Owns(cursor.mBoard) && cursor.CursorType == CursorType.PlantFromBank
            && bank.TryGetValue(cursor.mSeedBankIndex, out var card)) return (true, card.NativeType, card.Marker);
        return default;
    }

    internal static void SetPage(int value)
    {
        if (chooser == null || chooser.mSeedsInFlight != 0) return;
        page = Math.Clamp(value, 0, LastPage);
        RefreshChoices(true);
        DebugChooserNavigation.PageChanged(chooser);
        if (previousButton != null) previousButton.interactable = page > 0;
        if (nextButton != null) nextButton.interactable = page < LastPage;
    }

    internal static void RefreshChoices(bool layout = false)
    {
        if (chooser == null || DebugSeedChooser.SelectionScreen?.Pointer != chooser.Pointer) return;
        bool changed = false;
        // The native chooser has 49 slots; NumSeedTypes also counts
        // four special plants which have no chooser cards.
        int first = (int)SeedType.NumSeedsInChooser;
        for (int i = 0; i < chooser.mChosenSeeds.Count; ++i)
        {
            var seed = chooser.mChosenSeeds[i];
            if (seed.mSeedState is ChosenSeedState.SeedInBank or ChosenSeedState.SeedFlyingToBank
                or ChosenSeedState.SeedFlyingToChooser) continue;
            bool native = i < first;
            bool visible = DebugSeedChooser.ZombiePhase ? OnPage(i) : native ? page == 0 || seed.mSeedType == SeedType.Imitater
                : page > 0 && i - first >= (page - 1) * PageSize && i - first < page * PageSize;
            var state = visible ? ChosenSeedState.SeedInChooser : ChosenSeedState.SeedPacketHidden;
            bool stateChanged = seed.mSeedState != state;
            changed |= stateChanged;
            seed.mSeedState = state;
            // OnPositionChange owns mX/mY in screen coordinates. Never compare
            // those to chooser-local endpoints every frame: that dirties and
            // rebuilds the entire native list indefinitely.
            if (!visible || !layout && !stateChanged) continue;
            int position = DebugSeedChooser.ZombiePhase ? i % PageSize : native ? i : (i - first) % PageSize;
            int x = seed.mX, y = seed.mY;
            chooser.GetSeedPositionInChooser(position, ref x, ref y);
            seed.mX = seed.mStartX = seed.mEndX = x;
            seed.mY = seed.mStartY = seed.mEndY = y;
        }
        if (changed) chooser.mDirty = true;
    }

    internal static void Entry(SeedChooserEntryModel model)
    {
        if (!Visible(model.m_chosenSeed) || !choices.TryGetValue(model.m_chosenSeed.Pointer, out var card)) return;
        model.m_nameModel.Value = model.m_toolTipModel.Value = card.Name;
        model.m_sunCostModel.Value = Cost(card);
        model.m_isImitaterModel.Value = false;
        model.m_notSuggestedModel.Value = false;
        model.m_thumbnail.Reference = null;
        model.m_thumbnail.Value = DebugMode.Art.TrackCard(card.Portrait()) ?? DebugSeedChooser.CardBackground;
    }

    internal static void ClearApiBank() => bank.Clear();
    internal static string? ApiName(ChosenSeed seed) => choices.TryGetValue(seed.Pointer, out var card) ? card.Name : null;

    internal static void CaptureBank(SeedChooserScreen screen)
    {
        bank.Clear();
        for (int i = 0; i < screen.mChosenSeeds.Count; ++i)
        {
            var seed = screen.mChosenSeeds[i];
            if (seed.mSeedState == ChosenSeedState.SeedInBank && choices.TryGetValue(seed.Pointer, out var card))
                bank.Add(seed.mSeedIndexInBank, card);
        }
    }

    internal static void BankEntry(SeedBankEntryModel model)
    {
        if (!DebugMode.Owns(model.m_gameplayActivity)) return;
        Card? card = null;
        if (DebugSeedChooser.PlantScreen is { } screen)
        {
            for (int i = 0; i < screen.mChosenSeeds.Count; ++i)
            {
                var seed = screen.mChosenSeeds[i];
                if (seed.mSeedIndexInBank == model.Packet.mIndex
                    && seed.mSeedState is ChosenSeedState.SeedInBank or ChosenSeedState.SeedFlyingToBank)
                { choices.TryGetValue(seed.Pointer, out card); break; }
            }
        }
        else bank.TryGetValue(model.Packet.mIndex, out card);
        if (card == null) return;
        model.m_nameModel.Value = card.Name;
        // OnTick embeds the native upgrade/base-plant check. The finished
        // debug identity has no such requirement; reflect the actual packet.
        model.m_activeModel.Value = model.Packet.mActive;
        model.m_canAffordModel.Value = model.Packet.mBoard.CanTakeSunMoney(Cost(card), 0);
        model.m_showDisabled.Value = !model.m_canAffordModel.Value || !model.Packet.mActive;
        model.m_sunCostModel.Value = Cost(card);
        model.m_isImitaterModel.Value = false;
        model.m_visibleCost.Value = true;
        model.m_thumbnail.Reference = null;
        model.m_thumbnail.Value = DebugMode.Art.TrackCard(card.Portrait()) ?? DebugSeedChooser.CardBackground;
    }

    internal static void Update()
    {
        DebugChooserNavigation.Update();
        bool visible = DebugSeedChooser.SelectionScreen != null;
        var bound = visible && chooser != null ? DebugSeedChooser.BoundPanel(chooser) : null;
        if (bound != null)
        {
            foreach (var text in bound.GetComponentsInChildren<Il2CppTMPro.TMP_Text>(true))
                if (!string.IsNullOrEmpty(text.text) && text.text.StartsWith("$", StringComparison.Ordinal))
                    text.text = text.text.TrimStart('$');
            // Zombie selection is entered while the panel is hidden, so the
            // active binding is only available after the panel appears again.
            // Move controls out of the now-hidden Imitater wrapper at that point.
            if (DebugSeedChooser.ZombiePhase)
            {
                ReparentZombieButton(previous, bound.transform);
                ReparentZombieButton(next, bound.transform);
            }
        }
        if (previous != null) previous.SetActive(visible);
        if (next != null) next.SetActive(visible);
        if (!visible || previous != null || chooser == null) return;
        var panel = DebugSeedChooser.BoundPanel(chooser);
        if (panel == null) return;
        RectTransform? anchor = null;
        foreach (var binding in panel.GetComponentsInChildren<Il2CppTekly.DataModels.Binders.BinderContainer>(true))
        {
            if (!binding.m_hasBound || !binding.TryGet(binding.ResolveFullKey(), out Il2CppTekly.DataModels.Models.IModel model)) continue;
            var entry = model?.TryCast<SeedChooserEntryModel>();
            if (entry?.m_chosenSeed?.mSeedType != SeedType.Imitater) continue;
            // The Imitator wrapper is stable while its card model rebinds.
            anchor = binding.transform.parent.TryCast<RectTransform>();
            if (anchor != null) break;
        }
        var source = Resources.FindObjectsOfTypeAll<Il2Cpp.TimeCycler>()
            .FirstOrDefault(cycler => cycler.GetComponent<Button>() != null
                && cycler.m_cycles != null && cycler.m_cycles.Length > 1);
        if (DebugSeedChooser.ZombiePhase) anchor ??= panel.GetComponent<RectTransform>();
        if (anchor == null || source == null) return;
        var staging = new GameObject("DebugChooserPageStaging"); staging.SetActive(false);
        try
        {
            previous = PageButton(source, staging.transform, anchor, true);
            next = PageButton(source, staging.transform, anchor, false);
            previousButton = previous.GetComponent<Button>(); nextButton = next.GetComponent<Button>();
            SetPage(page);
            Mod.Instance.LoggerInstance.Msg($"Debug: Native card arrows ready; {(cards.Length + PageSize - 1) / PageSize + 1} tabs.");
        }
        catch { DestroyButtons(); throw; }
        finally { Object.Destroy(staging); }
    }

    private static GameObject PageButton(Il2Cpp.TimeCycler source, Transform staging, RectTransform anchor, bool back)
    {
        var control = NativeUi.Clone(source.gameObject, staging, anchor, back ? "DebugPreviousTab" : "DebugNextTab");
        var button = control.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener((UnityAction)(() => SetPage(page + (back ? -1 : 1))));
        foreach (var label in control.GetComponentsInChildren<Il2CppTMPro.TMP_Text>(true)) label.gameObject.SetActive(false);
        var icon = source.m_cycles.First(cycle => Math.Abs(cycle.TimeScale - 1.5f) < .01f).CurrentSpeedIndicator;
        foreach (var cycle in source.m_cycles)
            if (cycle.CurrentSpeedIndicator != null)
                foreach (var child in control.GetComponentsInChildren<Transform>(true))
                    if (child.name == cycle.CurrentSpeedIndicator.name) child.gameObject.SetActive(child.name == icon.name);
        var layout = control.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;
        PositionPageButton(control, back);
        control.transform.SetAsLastSibling();
        control.SetActive(true);
        return control;
    }

    private static void ReparentZombieButton(GameObject? control, Transform panel)
    {
        if (control == null) return;
        // Images render only below their Canvas. The PanelView itself sits
        // above that Canvas, while the hidden Imitater wrapper sits below it.
        var canvas = control.GetComponentInParent<Canvas>(true);
        if (canvas == null || !canvas.transform.IsChildOf(panel)
            || control.transform.parent?.Pointer == canvas.transform.Pointer) return;
        control.transform.SetParent(canvas.transform, true);
        control.transform.SetAsLastSibling();
    }

    private static void PositionPageButton(GameObject control, bool back)
    {
        var rect = control.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(back ? -55 : 55, DebugSeedChooser.ZombiePhase ? -80 : 45);
        rect.sizeDelta = new Vector2(100, 80);
        rect.localRotation = Quaternion.identity;
        var scale = rect.localScale; scale.x = (back ? -1 : 1) * Math.Abs(scale.x); rect.localScale = scale;
    }

    internal static PlantingReason Placement(Board board, Card card, int col, int row)
    {
        if (col < 0 || col >= 9 || row < 0 || row >= board.GetNumRows()) return PlantingReason.NotHere;
        bool previous = CheckingPlacement;
        try
        {
            CheckingPlacement = true;
            return card.CanPlant?.Invoke(board, col, row) ?? board.CanPlantAt(col, row, card.NativeType);
        }
        finally { CheckingPlacement = previous; }
    }

    internal static bool MouseDown(Board value, int x, int y, int clicks, int player)
    {
        if (!DebugMode.Owns(value) || value.mPaused || value.mApp.GameScene != GameScenes.Playing
            || player != 0 || value.mCursorObject.CursorType != CursorType.PlantFromBank
            || !bank.TryGetValue(value.mCursorObject.mSeedBankIndex, out var card)) return true;
        if (clicks < 0) { value.ClearCursor(true, player); return false; }
        if (value.mSeedBank.ContainsPoint(x, y)) return true;
        int col = value.PlantingPixelToGridX(x, y, card.NativeType), row = value.PlantingPixelToGridY(x, y, card.NativeType);
        if (col < 0 || col >= 9 || row < 0 || row >= value.GetNumRows()) return false;
        if (card.Place != null)
        {
            bool placed;
            try { CheckingPlacement = true; placed = card.Place(value, col, row); }
            finally { CheckingPlacement = false; }
            if (!placed) return false;
        }
        else
        {
            if (Placement(value, card, col, row) != PlantingReason.Ok) return false;
            var created = value.AddPlant(col, row, card.NativeType, card.Marker);
            if (created == null || created.mDead || !created.mIsOnBoard) return false;
        }
        int index = value.mCursorObject.mSeedBankIndex;
        value.TakeSunMoney(Cost(card), player);
        value.mSeedBank.mSeedPackets[index].WasPlanted(player);
        value.ClearCursor(true, player);
        return false;
    }

    [ThreadStatic] internal static bool CheckingPlacement;
    internal static void CheckCursorTerrain(Board board, int col, int row, SeedType type, ref PlantingReason result)
    {
        if (CheckingPlacement || !DebugMode.Owns(board) || board.mCursorObject.CursorType != CursorType.PlantFromBank
            || !bank.TryGetValue(board.mCursorObject.mSeedBankIndex, out var card)
            || card.NativeType != type || card.CanPlant == null) return;
        try { CheckingPlacement = true; result = Placement(board, card, col, row); }
        finally { CheckingPlacement = false; }
    }

    private static void DestroyButtons()
    {
        if (previous != null) { previous.SetActive(false); Object.Destroy(previous); }
        if (next != null) { next.SetActive(false); Object.Destroy(next); }
        previous = next = null; previousButton = nextButton = null;
    }

    internal static void Clear()
    {
        DestroyButtons(); choices.Clear(); bank.Clear();
        cards = Array.Empty<Card>(); chooser = null; page = 0;
    }
}


[HarmonyPatch(typeof(Board), nameof(Board.PlantingRequirementsMet))]
internal static class DebugRegisteredRequirementsPatch
{
    private static void Postfix(Board __instance, SeedType theSeedType, ref bool __result)
    { if (DebugMode.Owns(__instance) && DebugPlantCards.HasBankType(theSeedType)) __result = true; }
}
[HarmonyPatch(typeof(Board), nameof(Board.MouseDown))]
internal static class DebugRegisteredPlacementPatch
{
    [HarmonyPriority(Priority.First + 1100)]
    private static bool Prefix(Board __instance, int x, int y, int theClickCount, int playerIndex) =>
        DebugPlantCards.MouseDown(__instance, x, y, theClickCount, playerIndex);
}

[HarmonyPatch(typeof(Board), nameof(Board.CanPlantAt))]
internal static class DebugRegisteredTerrainPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Board __instance, int theGridX, int theGridY, SeedType theType, ref PlantingReason __result) =>
        DebugPlantCards.CheckCursorTerrain(__instance, theGridX, theGridY, theType, ref __result);
}
