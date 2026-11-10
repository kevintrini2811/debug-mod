using HarmonyLib;
using Il2CppReloaded.Gameplay;
using System.Text.Json;
using UnityEngine;

namespace DebugMod;

// One explicitly armed native Basketball observation. No collision methods are
// called here: getter/query results are copied only when native code reaches them.
internal static class DebugBasketball
{
    internal const int MaximumTicks = 600;
    private sealed class Arm
    {
        internal readonly Board Board;
        internal readonly Projectile Shot;
        internal readonly Plant Expected;
        internal readonly IntPtr BoardPointer, ShotPointer, PlantPointer;
        internal readonly uint ShotId, PlantId;
        internal readonly string Token;
        internal readonly int Tick;
        internal int OwnerCalls;
        internal object? LastNullQuery;
        internal Arm(Board board, Projectile shot, Plant expected)
        {
            Board = board; Shot = shot; Expected = expected;
            BoardPointer = board.Pointer; ShotPointer = shot.Pointer; PlantPointer = expected.Pointer;
            ShotId = shot.DataID; PlantId = expected.DataID; Token = ApiBoard.Token; Tick = board.mMainCounter;
        }
        internal bool SameGeneration => Board.Pointer == BoardPointer && ApiBoard.Current?.Pointer == BoardPointer
            && ApiBoard.Token == Token && DebugMode.Owns(Board)
            && Shot.Pointer == ShotPointer && Shot.DataID == ShotId && Shot.mBoard?.Pointer == BoardPointer
            && Shot.mProjectileType == ProjectileType.Basketball
            && Expected.Pointer == PlantPointer && Expected.DataID == PlantId && Expected.mBoard?.Pointer == BoardPointer;
    }

    internal sealed class Owner
    {
        private readonly Arm arm;
        internal readonly Owner? Previous;
        internal readonly object ShotBefore, ExpectedBefore;
        internal readonly int Tick;
        internal Query? Claim;
        internal object? After;
        internal bool PostfixReached, OriginalRan, NestedSameShotObserved;
        private Owner(Arm arm, Owner? previous)
        {
            this.arm = arm; Previous = previous; Tick = arm.Board.mMainCounter;
            ShotBefore = ShotState(arm.Shot); ExpectedBefore = PlantState(arm.Expected);
        }
        internal bool Current => ReferenceEquals(active, arm) && arm.SameGeneration;
        internal bool Matches(Projectile shot) => Current && shot.Pointer == arm.ShotPointer && shot.DataID == arm.ShotId
            && shot.mBoard?.Pointer == arm.BoardPointer && shot.mProjectileType == ProjectileType.Basketball;
        internal static Owner? Begin(Projectile shot, Owner? previous)
        {
            Validate();
            if (active is not { } value || !value.SameGeneration || shot.Pointer != value.ShotPointer
                || shot.DataID != value.ShotId || shot.mBoard?.Pointer != value.BoardPointer
                || shot.mProjectileType != ProjectileType.Basketball || shot.mDead) return null;
            if (previous != null && previous.Matches(shot))
            { previous.NestedSameShotObserved = true; return null; }
            ++value.OwnerCalls;
            return new(value, previous);
        }
        internal void CaptureAfter(bool originalRan)
        {
            if (!Current) return;
            PostfixReached = true; OriginalRan = originalRan;
            After = new { tick = arm.Board.mMainCounter, shot = ShotState(arm.Shot), expected = PlantState(arm.Expected),
                target = Claim?.TargetAfter() };
        }
        internal void Finish(Exception? error)
        {
            if (!Current) return;
            var query = Claim?.Evidence;
            bool terminal = error != null || !PostfixReached || !OriginalRan || arm.Shot.mDead || Claim?.Target != null;
            if (!terminal)
            {
                if (query != null) arm.LastNullQuery = query;
                return;
            }
            result = new { board = arm.Token, projectile = arm.ShotId, expectedPlant = arm.PlantId,
                owner = "Projectile.UpdateLobMotion", ownerCalls = arm.OwnerCalls,
                before = new { tick = Tick, shot = ShotBefore, expected = ExpectedBefore },
                query, lastNullQuery = arm.LastNullQuery, afterNativePostfix = After,
                nativeBodyPostfixReached = PostfixReached, originalRan = OriginalRan,
                exceptionAtDiagnosticFinalizer = error?.GetType().FullName,
                nestedSameShotObserved = NestedSameShotObserved,
                hookEvidenceComplete = PostfixReached && OriginalRan && error == null && !NestedSameShotObserved && Claim?.Complete == true,
                note = "Observed hook boundaries only. Raw postfix precedes Harmony finalizers; inspect a later atomic board response for final HP. No automatic contact/pass verdict." };
            active = null;
        }
    }

    internal sealed class OwnerScope
    {
        internal readonly Owner? Previous, Frame;
        internal OwnerScope(Owner? previous, Owner? frame) { Previous = previous; Frame = frame; }
    }

    internal sealed class Query
    {
        internal readonly Owner Owner;
        internal readonly Projectile Shot;
        internal object? ProjectileRect, Candidate;
        internal bool ProjectileRectRan, CandidateRectRan;
        internal Plant? Target;
        private uint targetId;
        private IntPtr targetPointer, targetBoard;
        internal object? Evidence;
        internal bool Complete;
        internal Query(Owner owner, Projectile shot) { Owner = owner; Shot = shot; }
        internal void Return(Plant? target, bool originalRan)
        {
            if (!Owner.Matches(Shot)) return;
            Target = target;
            if (target != null) { targetId = target.DataID; targetPointer = target.Pointer; targetBoard = target.mBoard?.Pointer ?? IntPtr.Zero; }
            Complete = originalRan && ProjectileRectRan && CandidateRectRan;
            Evidence = new { tick = Shot.mBoard.mMainCounter, originalRan, shot = ShotState(Shot),
                projectileRect = ProjectileRect, lastGeometricCandidate = Candidate,
                effectiveReturn = target == null ? null : PlantState(target),
                expectedReturn = target != null && active is { } value && target.Pointer == value.PlantPointer
                    && target.DataID == value.PlantId && target.mBoard?.Pointer == value.BoardPointer };
            Owner.Claim = this;
        }
        internal object? TargetAfter() => Target == null ? null : new {
            sameGeneration = Target.Pointer == targetPointer && Target.DataID == targetId && Target.mBoard?.Pointer == targetBoard,
            state = Target.Pointer == targetPointer && Target.DataID == targetId && Target.mBoard?.Pointer == targetBoard ? PlantState(Target) : null };
    }

    internal sealed class QueryScope
    {
        internal readonly Query? Previous, Frame;
        internal QueryScope(Query? previous, Query? frame) { Previous = previous; Frame = frame; }
    }

    private static Arm? active;
    private static object? result;
    private static string? inactiveReason;
    [ThreadStatic] private static Owner? current;
    [ThreadStatic] private static Query? queryCurrent;

    internal static void Reset()
    { active = null; result = null; inactiveReason = null; current = null; queryCurrent = null; }

    internal static void Validate()
    {
        try
        {
            if (active is not { } value) return;
            long elapsed = (long)value.Board.mMainCounter - value.Tick;
            if (!value.SameGeneration || elapsed < 0 || elapsed > MaximumTicks || value.Shot.mDead
                || value.Expected.mDead || value.Expected.mPlantHealth <= 0)
            {
                Reset(); inactiveReason = "Board/generation/lifetime changed or the 600-tick observation expired.";
            }
        }
        catch (Exception error) { Failed("lifetime-validation", error); }
    }

    internal static object Execute(JsonElement args)
    {
        var board = ApiBoard.Require();
        if (!DebugMode.Owns(board)) throw new ApiError("DEBUG_REQUIRED", "Basketball inspection requires a temporary debug session.");
        bool arm = args.TryGetProperty("arm", out var armValue), reset = args.TryGetProperty("reset", out var resetValue);
        if (arm && reset || arm && armValue.ValueKind != JsonValueKind.True || reset && resetValue.ValueKind != JsonValueKind.True)
            throw new ApiError("INVALID_ARGUMENT", "Use arm:true or reset:true, not both.");
        if (arm || reset || args.TryGetProperty("board", out _))
            if (!args.TryGetProperty("board", out var token) || token.ValueKind != JsonValueKind.String || token.GetString() != ApiBoard.Token)
                throw new ApiError("STALE_BOARD", "Basketball observation belongs to a different board.");
        if (!arm && (args.TryGetProperty("projectile", out _) || args.TryGetProperty("plant", out _)))
            throw new ApiError("INVALID_ARGUMENT", "Projectile and plant identities require arm:true.");
        Validate();
        if (arm)
        {
            uint Id(string name) => args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out uint id) && id != 0
                ? id : throw new ApiError("INVALID_ARGUMENT", name + ": expected a positive full entity ID.");
            uint shotId = Id("projectile"), plantId = Id("plant");
            Projectile? shot = null; Plant? expected = null;
            for (int i = 0; i < board.m_projectiles.Count; ++i)
                if (board.m_projectiles[i] is { } candidate && candidate.DataID == shotId && !candidate.mDead
                    && candidate.mBoard?.Pointer == board.Pointer && candidate.mProjectileType == ProjectileType.Basketball) shot = candidate;
            for (int i = 0; i < board.m_plants.Count; ++i)
                if (board.m_plants[i] is { } candidate && candidate.DataID == plantId && !candidate.mDead
                    && candidate.mPlantHealth > 0 && candidate.mBoard?.Pointer == board.Pointer) expected = candidate;
            if (shot == null || expected == null) throw new ApiError("ENTITY_NOT_FOUND", "A live Basketball and plant on this debug board are required.");
            if (active != null || result != null) throw new ApiError("INVALID_STATE", "Reset the previous Basketball observation before arming another.");
            inactiveReason = null; active = new(board, shot, expected);
        }
        else if (reset) Reset();
        return new { board = ApiBoard.Token, armed = active != null, maximumTicks = MaximumTicks,
            ownerCalls = active?.OwnerCalls ?? 0, lastNullQuery = active?.LastNullQuery, outcome = result, inactiveReason };
    }

    private static object ShotState(Projectile shot) => new { id = (uint)shot.DataID, type = shot.mProjectileType.ToString(),
        sameBoard = shot.mBoard?.Pointer == ApiBoard.Current?.Pointer, motion = shot.mMotionType.ToString(),
        row = shot.mRow, x = shot.mX, y = shot.mY, width = shot.mWidth, height = shot.mHeight,
        posX = shot.mPosX, posY = shot.mPosY, posZ = shot.mPosZ, velX = shot.mVelX, velY = shot.mVelY,
        velZ = shot.mVelZ, accZ = shot.mAccZ, age = shot.mProjectileAge, dead = shot.mDead };
    private static object PlantState(Plant plant) => new { id = (uint)plant.DataID, type = plant.mSeedType.ToString(), marker = plant.mImitaterType.ToString(),
        sameBoard = plant.mBoard?.Pointer == ApiBoard.Current?.Pointer,
        col = plant.mPlantCol, row = plant.mRow, x = plant.mX, y = plant.mY, width = plant.mWidth, height = plant.mHeight,
        health = plant.mPlantHealth, dead = plant.mDead, squished = plant.mSquished, onBoard = plant.mIsOnBoard,
        startRow = plant.mStartRow, targetX = plant.mTargetX, shootingCounter = plant.mShootingCounter };
    private static object Rectangle(Rect rect, bool originalRan) => new { x = rect.x, y = rect.y, width = rect.width, height = rect.height, originalRan };

    // A failed observer must not replace a native exception or turn a successful
    // native call into a failure. Retain the diagnostic failure explicitly.
    private static void Failed(string stage, Exception error)
    {
        result = new { board = active?.Token ?? ApiBoard.Token, hookEvidenceComplete = false,
            diagnosticFailure = new { stage, exception = error.GetType().FullName } };
        active = null;
    }

    internal static void BeginOwner(Projectile shot, out OwnerScope? state)
    {
        state = null;
        if (active == null && current == null) return;
        var previous = current;
        try { current = Owner.Begin(shot, previous); }
        catch (Exception error) { Failed("owner-prefix", error); current = null; }
        state = new(previous, current);
    }
    internal static void AfterOwner(bool originalRan, OwnerScope? state)
    {
        try { state?.Frame?.CaptureAfter(originalRan); }
        catch (Exception error) { Failed("owner-postfix", error); }
    }
    internal static void EndOwner(OwnerScope? state, Exception? error)
    {
        if (state == null) return;
        try { state.Frame?.Finish(error); }
        catch (Exception diagnosticError) { Failed("owner-finalizer", diagnosticError); }
        finally { current = state.Previous; }
    }
    internal static void BeginQuery(Projectile shot, out QueryScope? state)
    {
        state = null;
        if (current == null && queryCurrent == null) return;
        var previous = queryCurrent;
        try { queryCurrent = current is { } owner && owner.Matches(shot) ? new(owner, shot) : null; }
        catch (Exception error) { Failed("query-prefix", error); queryCurrent = null; }
        state = new(previous, queryCurrent);
    }
    internal static void AfterQuery(Plant? target, bool originalRan, QueryScope? state)
    {
        try { state?.Frame?.Return(target, originalRan); }
        catch (Exception error) { Failed("query-postfix", error); }
    }
    internal static void EndQuery(QueryScope? state)
    { if (state != null) queryCurrent = state.Previous; }
    internal static void ObserveProjectileRect(Projectile shot, Rect rect, bool? originalRan, Exception? getterError)
    {
        try
        {
            if (queryCurrent is { } query && query.Owner.Matches(shot))
            {
                if (getterError != null || !originalRan.HasValue)
                { Failed("projectile-rectangle", getterError ?? new InvalidOperationException("Getter postfix did not complete.")); return; }
                query.ProjectileRect = Rectangle(rect, originalRan.Value); query.ProjectileRectRan = originalRan.Value;
            }
        }
        catch (Exception error) { Failed("projectile-rectangle", error); }
    }
    internal static void ObservePlantRect(Plant plant, Rect rect, bool? originalRan, Exception? getterError)
    {
        try
        {
            if (queryCurrent is { } query && query.Owner.Matches(query.Shot) && plant.mBoard?.Pointer == query.Shot.mBoard?.Pointer)
            {
                if (getterError != null || !originalRan.HasValue)
                { Failed("plant-rectangle", getterError ?? new InvalidOperationException("Getter postfix did not complete.")); return; }
                query.Candidate = new { plant = PlantState(plant), rectangle = Rectangle(rect, originalRan.Value) }; query.CandidateRectRan = originalRan.Value;
            }
        }
        catch (Exception error) { Failed("plant-rectangle", error); }
    }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.UpdateLobMotion))]
internal static class DebugBasketballOwnerPatch
{
    internal static void Prefix(Projectile __instance, out DebugBasketball.OwnerScope? __state) => DebugBasketball.BeginOwner(__instance, out __state);
    internal static void Postfix(bool __runOriginal, DebugBasketball.OwnerScope? __state) => DebugBasketball.AfterOwner(__runOriginal, __state);
    internal static void Finalizer(DebugBasketball.OwnerScope? __state, Exception? __exception) => DebugBasketball.EndOwner(__state, __exception);
}
[HarmonyPatch(typeof(Projectile), nameof(Projectile.FindCollisionTargetPlant))]
internal static class DebugBasketballQueryPatch
{
    internal static void Prefix(Projectile __instance, out DebugBasketball.QueryScope? __state) => DebugBasketball.BeginQuery(__instance, out __state);
    internal static void Postfix(Plant? __result, bool __runOriginal, DebugBasketball.QueryScope? __state) => DebugBasketball.AfterQuery(__result, __runOriginal, __state);
    internal static void Finalizer(DebugBasketball.QueryScope? __state) => DebugBasketball.EndQuery(__state);
}
[HarmonyPatch(typeof(Projectile), nameof(Projectile.GetProjectileRect))]
internal static class DebugBasketballProjectileRectPatch
{
    // Preserve completed/skipped originals separately from an unreached postfix.
    // Copy the result only after all getter postfixes, including ref mutators.
    internal static void Postfix(bool __runOriginal, out bool? __state) => __state = __runOriginal;
    internal static void Finalizer(Projectile __instance, Rect __result, bool? __state, Exception? __exception) =>
        DebugBasketball.ObserveProjectileRect(__instance, __result, __state, __exception);
}
[HarmonyPatch(typeof(Plant), nameof(Plant.GetPlantRect))]
internal static class DebugBasketballPlantRectPatch
{
    internal static void Postfix(bool __runOriginal, out bool? __state) => __state = __runOriginal;
    internal static void Finalizer(Plant __instance, Rect __result, bool? __state, Exception? __exception) =>
        DebugBasketball.ObservePlantRect(__instance, __result, __state, __exception);
}
