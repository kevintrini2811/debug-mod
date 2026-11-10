using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DebugMod;
using FusionMod;
using Il2CppReloaded.Gameplay;

static class Program
{
    static int checks;
    static void Check(bool value,string message) {++checks;if(!value)throw new Exception(message);}
    static JsonElement Json(object? value)=>JsonSerializer.SerializeToElement(value);

    static void FakeFusion(string mode)
    {
        var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FusionMod"),AssemblyBuilderAccess.Run);
        var type=assembly.DefineDynamicModule("fixture").DefineType("FusionMod.DebugFusionIntegration",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
        void Method(string name,Type result,Type[] parameters,Action<ILGenerator> body)
        { var method=type.DefineMethod(name,MethodAttributes.Public|MethodAttributes.Static,result,parameters);var il=method.GetILGenerator();body(il);il.Emit(OpCodes.Ret); }
        var native=new[]{typeof(Board),typeof(SeedType),typeof(SeedType),typeof(bool),typeof(int),typeof(int)};
        Method("Place",typeof(bool),native,il=>il.Emit(OpCodes.Ldc_I4_1));
        Method("PlaceApi",typeof(bool),native.Append(typeof(int)).ToArray(),il=>il.Emit(OpCodes.Ldc_I4_1));
        Method("PacketResource",typeof(string),new[]{typeof(SeedType),typeof(SeedType)},il=>il.Emit(OpCodes.Ldnull));
        Method("Ingredients",typeof(IEnumerable<(SeedType,SeedType,string,string,int)>),new[]{typeof(Board)},il=>il.Emit(OpCodes.Ldnull));
        if(mode=="present")Method("Slots",typeof(object),new[]{typeof(Board),typeof(int),typeof(int)},il=>il.Emit(OpCodes.Ldstr,"actual-bound-owner"));
        if(mode=="wrong-signature")Method("Slots",typeof(object),new[]{typeof(Board),typeof(string),typeof(int)},il=>il.Emit(OpCodes.Ldstr,"wrong-signature"));
        if(mode=="wrong-return")Method("Slots",typeof(string),new[]{typeof(Board),typeof(int),typeof(int)},il=>il.Emit(OpCodes.Ldstr,"wrong-return"));
        type.CreateType();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Bindings(string mode)
    {
        var snapshot=Json(LibraryCatalog.SlotDiagnostics(new Board(),1,1));
        bool present=mode!="absent",modern=mode=="present";
        Check(snapshot.GetProperty("route").GetString()==(present?"fusion-api":"native"),"reports actual existing API route");
        Check(snapshot.GetProperty("fusionApiBound").GetBoolean()==present,"API delegate presence is not guessed");
        Check(snapshot.GetProperty("fusionLegacyBound").GetBoolean()==present,"legacy delegate presence reported");
        Check(snapshot.GetProperty("fusionSlotsBound").GetBoolean()==modern,"optional diagnostics tolerates absent/older Fusion");
        Check(snapshot.GetProperty("owner").ValueKind==(modern?JsonValueKind.String:JsonValueKind.Null),"absent owner stays null");
        var placed=LibraryCatalog.PlaceApi(new Board(),null,SeedType.Puffshroom,SeedType.None,1,1,40);
        Check(placed.HasValue==present && (!present || placed.GetValueOrDefault()),"existing placement delegate remains usable when diagnostic is unavailable");
        if(modern)Check(snapshot.GetProperty("owner").GetString()=="actual-bound-owner","invokes only the resolved fixed owner");
    }

    static void Owner()
    {
        var board=new Board();
        FusionMod.DebugMode.Enabled=false;
        Check(DebugFusionIntegration.Slots(board,1,1)==null,"Fusion owner itself rejects non-debug board");
        FusionMod.DebugMode.Enabled=true;
        Check(DebugFusionIntegration.Slots(board,-1,1)==null && DebugFusionIntegration.Slots(board,1,5)==null,
              "Fusion owner rejects invalid cells without querying native terrain");
        for(int slot=0;slot<3;++slot)
        {
            var p=board.AddPlant(1,1,SeedType.Puffshroom,SeedType.None);
            ShroomSlots.Position(p,1,1,slot);
        }
        board.m_plants[0].DataID=0xf1230001u;
        var wrapper=Json(board.m_plants[0].DataID);
        Check(wrapper.ValueKind==JsonValueKind.Object && !wrapper.EnumerateObject().Any(),
              "native-like DataID wrapper alone serializes as an empty object");
        var before=JsonSerializer.Serialize(board.m_plants.Select(p=>new{p.DataID,p.mX,p.mY,p.mDead,p.mIsOnBoard,p.mSquished,p.mOnBungeeState}));
        var result=Json(DebugFusionIntegration.Slots(board,1,1));
        Check(result.GetProperty("count").GetInt32()==3,"reports production occupancy on a paused board");
        Check(result.GetProperty("occupantIds").GetArrayLength()==3,"retains full current IDs");
        Check(result.GetProperty("occupantIds").EnumerateArray().Select(id=>id.GetUInt32())
              .SequenceEqual(board.m_plants.Select(p=>(uint)p.DataID)),"occupant wrapper IDs normalize to full uint JSON numbers");
        Check(result.GetProperty("candidates")[0].GetProperty("id").GetUInt32()==0xf1230001u,
              "candidate wrapper ID preserves high generation bits as a JSON number");
        var candidates=result.GetProperty("candidates").EnumerateArray().ToArray();
        Check(candidates.All(p=>!p.GetProperty("notOnGround").GetBoolean()&&p.GetProperty("live").GetBoolean()&&p.GetProperty("occupant").GetBoolean()),"native and owner predicates reported independently");
        Check(candidates.All(p=>!p.GetProperty("sourceState").GetProperty("squishOwned").GetBoolean()
              &&!p.GetProperty("sourceState").GetProperty("squishAirborne").GetBoolean()
              &&!p.GetProperty("sourceState").GetProperty("retained").GetBoolean()),"plain identity diagnostic reports exact own predicates");
        Check(result.GetProperty("notOnGroundPatches").GetArrayLength()==0,"unpatched target reports empty metadata");
        var method=typeof(Program).GetMethod(nameof(FixturePostfix),BindingFlags.Static|BindingFlags.NonPublic)!;
        HarmonyLib.Harmony.Info=new HarmonyLib.Patches();
        HarmonyLib.Harmony.Info.Postfixes.Add(new HarmonyLib.Patch {owner="own-ground-fixture",priority=400,index=7,PatchMethod=method,before=new[]{"later-owner"}});
        var patches=Json(DebugFusionIntegration.Slots(board,1,1)).GetProperty("notOnGroundPatches");
        Check(patches.GetArrayLength()==1&&patches[0].GetProperty("kind").GetString()=="postfix"
              &&patches[0].GetProperty("owner").GetString()=="own-ground-fixture"
              &&patches[0].GetProperty("priority").GetInt32()==400&&patches[0].GetProperty("index").GetInt32()==7,
              "fixed target patch owner and ordering metadata survive JSON");
        Check(patches[0].GetProperty("method").GetString()==typeof(Program).FullName+"."+nameof(FixturePostfix)
              &&patches[0].GetProperty("assembly").GetString()==typeof(Program).Assembly.GetName().Name,
              "patch method and assembly identify the actual supplied caller");
        Check(HarmonyLib.Harmony.LastTarget?.DeclaringType==typeof(Plant)&&HarmonyLib.Harmony.LastTarget.Name==nameof(Plant.NotOnGround),
              "only fixed Plant.NotOnGround metadata is queried");
        HarmonyLib.Harmony.Info=null;
        Check(candidates.Select(p=>p.GetProperty("slot").GetInt32()).Distinct().Count()==3,"reports exact source slot owner");
        board.m_plants[1].OffGround=true;
        var off=Json(DebugFusionIntegration.Slots(board,1,1));
        Check(off.GetProperty("count").GetInt32()==2,"native off-ground excludes candidate from actual owner");
        Check(off.GetProperty("candidates")[1].GetProperty("notOnGround").GetBoolean()&&!off.GetProperty("candidates")[1].GetProperty("live").GetBoolean(),"diagnosis exposes discriminator");
        board.m_plants[1].OffGround=false;
        var after=JsonSerializer.Serialize(board.m_plants.Select(p=>new{p.DataID,p.mX,p.mY,p.mDead,p.mIsOnBoard,p.mSquished,p.mOnBungeeState}));
        Check(before==after&&board.mPaused,"inspection does not mutate position, lifecycle flags or pause");
        var dead=new Plant {mBoard=null!,mPlantCol=1,mRow=1,mDead=true};
        var missingBoard=new Plant {mBoard=null!,mPlantCol=1,mRow=1};
        var foreignBoard=new Plant {mBoard=new Board(),mPlantCol=1,mRow=1};
        board.m_plants.AddRange(new[]{dead,missingBoard,foreignBoard});
        var guarded=Json(DebugFusionIntegration.Slots(board,1,1)).GetProperty("candidates");
        foreach(var candidate in guarded.EnumerateArray().Where(p=>new[]{dead.DataID,missingBoard.DataID,foreignBoard.DataID}.Contains(p.GetProperty("id").GetUInt32())))
            Check(candidate.GetProperty("slot").ValueKind==JsonValueKind.Null,"dead/null-board/foreign-board candidate keeps raw flags without projecting slot");
        Check(guarded[3].GetProperty("dead").GetBoolean()&&!guarded[4].GetProperty("sameBoard").GetBoolean()
              &&!guarded[5].GetProperty("sameBoard").GetBoolean(),"diagnostic retains unsafe candidate raw state");
    }

    static void FixturePostfix() {}

    static void Main(string[] args)
    {
        string mode=args.Single();Check(new[]{"absent","present","old","wrong-signature","wrong-return"}.Contains(mode),"fixed binding fixture mode");
        if(mode!="absent")FakeFusion(mode);
        Owner();Bindings(mode);Console.WriteLine($"Slot diagnostics {mode}: {checks} checks passed.");
    }
}
