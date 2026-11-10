using System.Text.Json;
using DebugMod;
using Il2CppReloaded.Gameplay;
using UnityEngine;

static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    { ++checks; if (!condition) throw new Exception(message); }
    private static JsonElement Call(string json = "{}") => JsonSerializer.SerializeToElement(DebugBasketball.Execute(JsonDocument.Parse(json).RootElement));
    private static JsonElement Outcome() => Call().GetProperty("outcome");
    private static (Board Board, Projectile Shot, Plant Plant, Plant Sentinel) Fresh()
    {
        DebugBasketball.Reset(); ApiBoard.Token = "live";
        var board = new Board(); ApiBoard.Current = board;
        var shot = new Projectile { mBoard = board };
        var plant = new Plant { mBoard = board };
        var sentinel = new Plant { mBoard = board, Pointer = (IntPtr)4, DataID = 22, mPlantHealth = 4000, mSeedType = Seed.Wallnut };
        board.m_projectiles.Add(shot); board.m_plants.AddRange(new[] { plant, sentinel });
        return (board, shot, plant, sentinel);
    }
    private static void Arm() => Call("{\"board\":\"live\",\"arm\":true,\"projectile\":11,\"plant\":21}");
    private static void Reject(string json, string code)
    {
        try { Call(json); throw new Exception("Request accepted unexpectedly."); }
        catch (ApiError error) { Check(error.Code == code, "correct rejection: " + code); }
    }
    private static void ProjectileRect(Projectile shot, Rect rect, bool originalRan)
    {
        DebugBasketballProjectileRectPatch.Postfix(originalRan, out var state);
        DebugBasketballProjectileRectPatch.Finalizer(shot, rect, state, null);
    }
    private static void PlantRect(Plant plant, Rect rect, bool originalRan)
    {
        DebugBasketballPlantRectPatch.Postfix(originalRan, out var state);
        DebugBasketballPlantRectPatch.Finalizer(plant, rect, state, null);
    }
    private static void RefMutation(ref Rect rectangle) => rectangle = new Rect(-100000, -100000, 0, 0);
    private static void GetterBoundaries()
    {
        // Exercise the actual observer around a competing ref-result postfix.
        // This does not emulate native projectile physics or IL2CPP detours.
        foreach (bool plantGetter in new[] { true, false })
        foreach (bool observerFirst in new[] { true, false })
        {
            var f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out var owner);
            DebugBasketballQueryPatch.Prefix(f.Shot, out var query);
            var rectangle = new Rect(310, 264, 60, 80);
            bool? state;
            if (observerFirst)
            {
                if (plantGetter) DebugBasketballPlantRectPatch.Postfix(true, out state);
                else DebugBasketballProjectileRectPatch.Postfix(true, out state);
                RefMutation(ref rectangle);
            }
            else
            {
                RefMutation(ref rectangle);
                if (plantGetter) DebugBasketballPlantRectPatch.Postfix(true, out state);
                else DebugBasketballProjectileRectPatch.Postfix(true, out state);
            }
            if (plantGetter)
            {
                ProjectileRect(f.Shot, new Rect(310, 264, 40, 40), true);
                DebugBasketballPlantRectPatch.Finalizer(f.Plant, rectangle, state, null);
            }
            else
            {
                DebugBasketballProjectileRectPatch.Finalizer(f.Shot, rectangle, state, null);
                PlantRect(f.Plant, new Rect(310, 264, 60, 80), true);
            }
            DebugBasketballQueryPatch.Postfix(f.Plant, true, query); DebugBasketballQueryPatch.Finalizer(query);
            DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
            var evidence = Outcome().GetProperty("query");
            var recorded = plantGetter ? evidence.GetProperty("lastGeometricCandidate").GetProperty("rectangle") : evidence.GetProperty("projectileRect");
            Check(recorded.GetProperty("width").GetInt32() == 0 && recorded.GetProperty("height").GetInt32() == 0,
                "getter finalizer copies actual final ref-result in either postfix order");
            Check(recorded.GetProperty("originalRan").GetBoolean(), "completed original marker survives final-result mutation");
        }
        foreach (bool plantGetter in new[] { true, false })
        {
            var f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out var owner);
            DebugBasketballQueryPatch.Prefix(f.Shot, out var query);
            ProjectileRect(f.Shot, new Rect(310, 264, 40, 40), true);
            PlantRect(f.Plant, new Rect(310, 264, 60, 80), true);
            if (plantGetter) PlantRect(f.Plant, new Rect(5, 6, 17, 18), false);
            else ProjectileRect(f.Shot, new Rect(5, 6, 17, 18), false);
            DebugBasketballQueryPatch.Postfix(f.Plant, true, query); DebugBasketballQueryPatch.Finalizer(query);
            DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
            var evidence = Outcome().GetProperty("query");
            var recorded = plantGetter ? evidence.GetProperty("lastGeometricCandidate").GetProperty("rectangle") : evidence.GetProperty("projectileRect");
            Check(!recorded.GetProperty("originalRan").GetBoolean() && recorded.GetProperty("width").GetInt32() == 17,
                "skipped getter overwrites previous successful result with actual fallback, including false originalRan");
            Check(!Outcome().GetProperty("hookEvidenceComplete").GetBoolean(), "skipped getter never inherits complete evidence");
        }
        foreach (bool plantGetter in new[] { true, false })
        foreach (string failure in new[] { "missing-postfix", "getter-error", "later-postfix-error" })
        {
            var f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out var owner);
            DebugBasketballQueryPatch.Prefix(f.Shot, out var query);
            bool? state = null;
            if (failure == "later-postfix-error")
            {
                if (plantGetter) DebugBasketballPlantRectPatch.Postfix(true, out state);
                else DebugBasketballProjectileRectPatch.Postfix(true, out state);
            }
            Exception? original = failure == "missing-postfix" ? null : new ApplicationException(failure);
            if (original != null)
            {
                try
                {
                    try { throw original; }
                    catch (ApplicationException actual)
                    {
                        if (plantGetter) DebugBasketballPlantRectPatch.Finalizer(f.Plant, default, state, actual);
                        else DebugBasketballProjectileRectPatch.Finalizer(f.Shot, default, state, actual);
                        throw;
                    }
                }
                catch (ApplicationException actual) { Check(ReferenceEquals(original, actual), "void getter finalizer preserves actual native/postfix exception"); }
            }
            else if (plantGetter) DebugBasketballPlantRectPatch.Finalizer(f.Plant, default, state, null);
            else DebugBasketballProjectileRectPatch.Finalizer(f.Shot, default, state, null);
            Check(Outcome().GetProperty("diagnosticFailure").GetProperty("stage").GetString() == (plantGetter ? "plant-rectangle" : "projectile-rectangle"),
                "missing completion and exceptions produce diagnostic failure without successful geometry");
            DebugBasketballQueryPatch.Finalizer(query); DebugBasketballOwnerPatch.Finalizer(owner, original);
        }
        // Outside an armed query, failures in unrelated getters remain inert.
        DebugBasketball.Reset();
        DebugBasketballPlantRectPatch.Finalizer(new Plant(), default, null, new ApplicationException("unrelated"));
        DebugBasketballProjectileRectPatch.Finalizer(new Projectile(), default, null, new ApplicationException("unrelated"));
        Check(Outcome().ValueKind == JsonValueKind.Null, "unarmed getter finalizers do not manufacture diagnostic failures");
    }
    private static void GetterScopes()
    {
        foreach (bool plantGetter in new[] { true, false })
        {
            var f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out var owner);
            DebugBasketballQueryPatch.Prefix(f.Shot, out var outer);
            var foreignBoard = new Board { Pointer = (IntPtr)99 };
            var foreignShot = new Projectile { mBoard = foreignBoard, Pointer = (IntPtr)98, DataID = 98 };
            var foreignPlant = new Plant { mBoard = foreignBoard, Pointer = (IntPtr)97, DataID = 97 };
            if (plantGetter) DebugBasketballPlantRectPatch.Finalizer(foreignPlant, default, null, new ApplicationException("foreign getter"));
            else DebugBasketballProjectileRectPatch.Finalizer(foreignShot, default, null, new ApplicationException("foreign getter"));
            Check(Call().GetProperty("armed").GetBoolean() && Outcome().ValueKind == JsonValueKind.Null,
                "foreign getter failure cannot disarm the matching query");
            DebugBasketballQueryPatch.Prefix(foreignShot, out var nested);
            if (plantGetter) DebugBasketballPlantRectPatch.Finalizer(f.Plant, default, null, new ApplicationException("nested getter"));
            else DebugBasketballProjectileRectPatch.Finalizer(f.Shot, default, null, new ApplicationException("nested getter"));
            DebugBasketballQueryPatch.Finalizer(nested);
            Check(Call().GetProperty("armed").GetBoolean() && Outcome().ValueKind == JsonValueKind.Null,
                "nested foreign query failure cannot be attributed to its outer query");
            ProjectileRect(f.Shot, new Rect(310, 264, 40, 40), true);
            PlantRect(f.Plant, new Rect(310, 264, 60, 80), true);
            DebugBasketballQueryPatch.Postfix(f.Plant, true, outer); DebugBasketballQueryPatch.Finalizer(outer);
            DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
            Check(Outcome().GetProperty("hookEvidenceComplete").GetBoolean(), "outer getter context remains usable after nested foreign failure");
        }
        foreach (bool plantGetter in new[] { true, false })
        foreach (string change in new[] { "shot-generation", "plant-generation", "shot-pointer", "plant-pointer", "board", "token", "reset" })
        {
            var f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out var owner);
            DebugBasketballQueryPatch.Prefix(f.Shot, out var query);
            bool? state;
            if (plantGetter) DebugBasketballPlantRectPatch.Postfix(true, out state);
            else DebugBasketballProjectileRectPatch.Postfix(true, out state);
            if (change == "shot-generation") ++f.Shot.DataID;
            if (change == "plant-generation") ++f.Plant.DataID;
            if (change == "shot-pointer") f.Shot.Pointer = (IntPtr)98;
            if (change == "plant-pointer") f.Plant.Pointer = (IntPtr)97;
            if (change == "board") ApiBoard.Current = new Board { Pointer = (IntPtr)99 };
            if (change == "token") ApiBoard.Token = "new";
            if (change == "reset") DebugBasketball.Reset();
            if (plantGetter) DebugBasketballPlantRectPatch.Finalizer(f.Plant, default, state, new ApplicationException("stale getter"));
            else DebugBasketballProjectileRectPatch.Finalizer(f.Shot, default, state, new ApplicationException("stale getter"));
            DebugBasketballQueryPatch.Finalizer(query); DebugBasketballOwnerPatch.Finalizer(owner, null);
            Check(Outcome().ValueKind == JsonValueKind.Null && !Call().GetProperty("armed").GetBoolean(),
                "late getter finalizer cannot publish geometry or errors after invalidation: " + change);
        }
        {
            var f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out var owner);
            DebugBasketballQueryPatch.Prefix(f.Shot, out var query);
            DebugBasketballPlantRectPatch.Postfix(true, out var state);
            f.Plant.ThrowHealth = true;
            DebugBasketballPlantRectPatch.Finalizer(f.Plant, new Rect(310, 264, 60, 80), state, null);
            f.Plant.ThrowHealth = false;
            DebugBasketballQueryPatch.Finalizer(query); DebugBasketballOwnerPatch.Finalizer(owner, null);
            Check(Outcome().GetProperty("diagnosticFailure").GetProperty("stage").GetString() == "plant-rectangle"
                && !Outcome().GetProperty("hookEvidenceComplete").GetBoolean(), "getter diagnostic copy failure cannot escape or manufacture successful geometry");
        }
    }
    private static void Query(Projectile shot, Plant candidate, Plant? target, bool getters = true, bool original = true)
    {
        DebugBasketballQueryPatch.Prefix(shot, out var query);
        try
        {
            if (getters)
            {
                ProjectileRect(shot, new Rect(310, 264, 40, 40), true);
                PlantRect(candidate, new Rect(310, 264, 60, 80), true);
            }
            DebugBasketballQueryPatch.Postfix(target, original, query);
        }
        finally { DebugBasketballQueryPatch.Finalizer(query); }
    }
    public static void Main()
    {
        GetterBoundaries();
        GetterScopes();
        var f = Fresh();
        DebugBasketballOwnerPatch.Prefix(f.Shot, out var unarmed);
        Query(f.Shot, f.Plant, f.Plant);
        DebugBasketballOwnerPatch.Postfix(true, unarmed); DebugBasketballOwnerPatch.Finalizer(unarmed, null);
        Check(unarmed == null && Outcome().ValueKind == JsonValueKind.Null, "unarmed hooks retain no data");
        f.Board.Debug = false; Reject("{}", "DEBUG_REQUIRED"); f.Board.Debug = true;
        Reject("{\"board\":\"stale\",\"arm\":true}", "STALE_BOARD");
        Reject("{\"board\":\"live\",\"arm\":false}", "INVALID_ARGUMENT");
        Reject("{\"board\":\"live\",\"arm\":true,\"reset\":true}", "INVALID_ARGUMENT");
        Reject("{\"projectile\":11}", "INVALID_ARGUMENT");
        Reject("{\"board\":\"live\",\"arm\":true,\"projectile\":0,\"plant\":21}", "INVALID_ARGUMENT");
        Reject("{\"board\":\"live\",\"arm\":true,\"projectile\":11,\"plant\":22.5}", "INVALID_ARGUMENT");
        f.Shot.mProjectileType = ProjectileType.Pea;
        Reject("{\"board\":\"live\",\"arm\":true,\"projectile\":11,\"plant\":21}", "ENTITY_NOT_FOUND");
        f.Shot.mProjectileType = ProjectileType.Basketball; f.Plant.mDead = true;
        Reject("{\"board\":\"live\",\"arm\":true,\"projectile\":11,\"plant\":21}", "ENTITY_NOT_FOUND");
        f.Plant.mDead = false; Arm();
        Check(Call().GetProperty("armed").GetBoolean() && Outcome().ValueKind == JsonValueKind.Null, "arming and reads do not invent hook evidence");
        Check(f.Plant.mPlantHealth == 300 && !f.Shot.mDead && f.Board.mMainCounter == 0, "arming is observational");
        DebugBasketballOwnerPatch.Prefix(f.Shot, out var owner);
        Query(f.Shot, f.Plant, f.Plant);
        // Test collaborator supplies a native owner's externally observed writes;
        // this fixture does not implement collision, physics or Fusion damage.
        f.Plant.mPlantHealth = 225; f.Shot.mDead = true;
        DebugBasketballOwnerPatch.Postfix(true, owner);
        f.Plant.mPlantHealth = 300; // An external later finalizer restores it.
        DebugBasketballOwnerPatch.Finalizer(owner, null);
        var result = Outcome();
        Check(result.GetProperty("afterNativePostfix").GetProperty("expected").GetProperty("health").GetInt32() == 225, "raw postfix snapshot survives later restoration");
        Check(f.Plant.mPlantHealth == 300 && f.Shot.mDead && f.Sentinel.mPlantHealth == 4000, "diagnostic finalizer changes no gameplay field");
        Check(result.GetProperty("query").GetProperty("expectedReturn").GetBoolean(), "actual expected return recorded");
        Check(result.GetProperty("hookEvidenceComplete").GetBoolean(), "all reached hook boundaries recorded");
        Check(result.GetProperty("query").GetProperty("projectileRect").GetProperty("width").GetInt32() == 40
            && result.GetProperty("query").GetProperty("lastGeometricCandidate").GetProperty("rectangle").GetProperty("width").GetInt32() == 60,
            "nonempty native ground rectangles remain unchanged");
        Reject("{\"board\":\"live\",\"arm\":true,\"projectile\":11,\"plant\":21}", "ENTITY_NOT_FOUND");
        f.Shot.mDead = false;
        Reject("{\"board\":\"live\",\"arm\":true,\"projectile\":11,\"plant\":21}", "INVALID_STATE");

        f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
        Query(f.Shot, f.Plant, f.Sentinel);
        f.Sentinel.mPlantHealth -= 75; f.Shot.mDead = true;
        DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
        result = Outcome(); var claim = result.GetProperty("query");
        Check(claim.GetProperty("lastGeometricCandidate").GetProperty("plant").GetProperty("id").GetUInt32() == 21, "physical candidate identity retained");
        Check(claim.GetProperty("effectiveReturn").GetProperty("id").GetUInt32() == 22 && !claim.GetProperty("expectedReturn").GetBoolean(), "wrong actual target never filtered to expected target");
        Check(result.GetProperty("afterNativePostfix").GetProperty("target").GetProperty("state").GetProperty("health").GetInt32() == 3925, "wrong target's actual decrease recorded");

        f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
        Query(f.Shot, f.Plant, null); DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
        Check(Call().GetProperty("armed").GetBoolean() && Call().GetProperty("lastNullQuery").ValueKind == JsonValueKind.Object, "nonterminal null query keeps only bounded last-null witness");
        DebugBasketballOwnerPatch.Prefix(f.Shot, out owner); Query(f.Shot, f.Plant, null);
        f.Shot.mDead = true; DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
        Check(Outcome().GetProperty("query").GetProperty("effectiveReturn").ValueKind == JsonValueKind.Null, "ground fallback retains null target, not claimed contact");
        Check(!Call().GetProperty("armed").GetBoolean(), "one terminal outcome disarms");

        foreach (bool getterMissing in new[] { true, false })
        {
            f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
            Query(f.Shot, f.Plant, f.Plant, getters: !getterMissing, original: getterMissing);
            DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
            Check(!Outcome().GetProperty("hookEvidenceComplete").GetBoolean(), "missing getter or skipped native query remains unverified");
        }
        f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
        DebugBasketballOwnerPatch.Postfix(false, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
        Check(!Outcome().GetProperty("originalRan").GetBoolean() && !Outcome().GetProperty("hookEvidenceComplete").GetBoolean(), "skipped native owner cannot imply a native event");
        f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
        DebugBasketballOwnerPatch.Finalizer(owner, new InvalidOperationException("native owner failed"));
        Check(!Outcome().GetProperty("nativeBodyPostfixReached").GetBoolean() && Outcome().GetProperty("afterNativePostfix").ValueKind == JsonValueKind.Null, "exception does not invent a completed body snapshot");
        Check(Outcome().GetProperty("exceptionAtDiagnosticFinalizer").GetString() == typeof(InvalidOperationException).FullName, "observed exception preserved as evidence");

        f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
        var foreign = new Projectile { mBoard = f.Board, DataID = 99, Pointer = (IntPtr)99 };
        DebugBasketballOwnerPatch.Prefix(foreign, out var nested);
        Query(foreign, f.Sentinel, f.Sentinel); DebugBasketballOwnerPatch.Postfix(true, nested); DebugBasketballOwnerPatch.Finalizer(nested, null);
        Query(f.Shot, f.Plant, f.Plant); DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
        Check(Outcome().GetProperty("query").GetProperty("effectiveReturn").GetProperty("id").GetUInt32() == 21, "nested foreign owner cannot consume or overwrite armed owner");
        f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
        DebugBasketballQueryPatch.Prefix(f.Shot, out var outerQuery);
        Query(foreign, f.Sentinel, f.Sentinel);
        ProjectileRect(f.Shot, new Rect(310, 264, 40, 40), true);
        PlantRect(f.Plant, new Rect(310, 264, 60, 80), true);
        DebugBasketballQueryPatch.Postfix(f.Plant, true, outerQuery); DebugBasketballQueryPatch.Finalizer(outerQuery);
        DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
        Check(Outcome().GetProperty("query").GetProperty("expectedReturn").GetBoolean(), "nested foreign query restores outer getter context");

        foreach (string change in new[] { "shot-generation", "plant-generation", "board", "token", "expiration", "reset" })
        {
            f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
            if (change == "shot-generation") ++f.Shot.DataID;
            if (change == "plant-generation") ++f.Plant.DataID;
            if (change == "board") ApiBoard.Current = new Board { Pointer = (IntPtr)99 };
            if (change == "token") ApiBoard.Token = "new";
            if (change == "expiration") f.Board.mMainCounter = 601;
            if (change == "reset") DebugBasketball.Reset();
            DebugBasketball.Validate(); DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
            Check(!Call().GetProperty("armed").GetBoolean() && Outcome().ValueKind == JsonValueKind.Null, "invalidated scope stays inactive: " + change);
        }
        f = Fresh(); Arm(); f.Board.mMainCounter = 600;
        Check(Call().GetProperty("armed").GetBoolean(), "fixed 600-tick bound is inclusive");
        f.Board.mMainCounter = 601;
        Check(!Call().GetProperty("armed").GetBoolean(), "missing owner hooks expire without a success record");

        f = Fresh(); f.Shot.DataID = 0xfffffffe; f.Plant.DataID = 0xffffffff;
        Call("{\"board\":\"live\",\"arm\":true,\"projectile\":4294967294,\"plant\":4294967295}");
        DebugBasketballOwnerPatch.Prefix(f.Shot, out owner); Query(f.Shot, f.Plant, f.Plant);
        DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
        Check(Outcome().GetProperty("projectile").GetUInt32() == 0xfffffffe
            && Outcome().GetProperty("expectedPlant").GetUInt32() == 0xffffffff, "full unsigned IDs are never narrowed to Int32");
        f = Fresh(); f.Shot.mBoard = new Board { Pointer = (IntPtr)99 };
        Reject("{\"board\":\"live\",\"arm\":true,\"projectile\":11,\"plant\":21}", "ENTITY_NOT_FOUND");
        f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
        DebugBasketballOwnerPatch.Prefix(f.Shot, out nested); Query(f.Shot, f.Plant, f.Plant);
        DebugBasketballOwnerPatch.Postfix(true, nested); DebugBasketballOwnerPatch.Finalizer(nested, null);
        f.Shot.mDead = true; DebugBasketballOwnerPatch.Postfix(true, owner); DebugBasketballOwnerPatch.Finalizer(owner, null);
        Check(Outcome().GetProperty("nestedSameShotObserved").GetBoolean() && !Outcome().GetProperty("hookEvidenceComplete").GetBoolean(), "reentrant same-shot owners remain explicitly unverified");
        f = Fresh(); Arm(); DebugBasketballOwnerPatch.Prefix(f.Shot, out owner);
        f.Plant.ThrowHealth = true;
        DebugBasketballOwnerPatch.Postfix(true, owner);
        var originalError = new ApplicationException("original native error");
        try
        {
            try { throw originalError; }
            catch { DebugBasketballOwnerPatch.Finalizer(owner, originalError); throw; }
        }
        catch (ApplicationException actual) { Check(ReferenceEquals(actual, originalError), "diagnostic failure neither replaces nor suppresses original exception"); }
        f.Plant.ThrowHealth = false;
        Check(Outcome().GetProperty("diagnosticFailure").GetProperty("stage").GetString() == "owner-postfix"
            && !Outcome().GetProperty("hookEvidenceComplete").GetBoolean(), "observer failure is explicitly reported, not successful evidence");
        Console.WriteLine($"PASS {checks} Basketball diagnostic assertions.");
    }
}
