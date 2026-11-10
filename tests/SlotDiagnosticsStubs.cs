using Il2CppReloaded.Gameplay;
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n) {}}
 public sealed class Patch {public string owner="fixture-owner";public int priority,index;public string[] before=[],after=[];public System.Reflection.MethodInfo PatchMethod=null!;}
 public sealed class Patches {public List<Patch> Prefixes=[],Postfixes=[],Transpilers=[],Finalizers=[];}
 public static class Harmony {public static Patches? Info; public static System.Reflection.MethodBase? LastTarget;public static Patches? GetPatchInfo(System.Reflection.MethodBase target) {LastTarget=target;return Info;}}
}
namespace UnityEngine { public sealed class Sprite {} }
namespace Il2CppReloaded.Gameplay {
 public enum PlantingReason { Ok,NotHere } public enum PlantOnBungeeState { NotOnBungee,RisingWithBungee }
 public enum GameScenes { Playing,Other } public enum GameMode { Adventure,ChallengeZenGarden }
 public enum ZombieType { Normal } public sealed class Zombie {}
 public sealed class App { public GameScenes GameScene=GameScenes.Playing;public GameMode GameMode; }
 public sealed class Board {
  static int next;public IntPtr Pointer=(IntPtr)(++next);
  public App mApp=new();public bool mPaused=true;public List<Plant> m_plants=new();
  public int GetNumRows()=>5; public int GridToPixelX(int c,int r)=>40+c*80; public int GridToPixelY(int c,int r)=>80+r*100;
  public Lawn GetPlantsOnLawn(int c,int r)=>new() {NormalPlant=m_plants.FirstOrDefault(p=>p.mPlantCol==c&&p.mRow==r&&!p.mDead&&p.mIsOnBoard&&!p.mSquished&&!p.NotOnGround())};
  public Plant AddPlant(int c,int r,SeedType type,SeedType marker) { var p=new Plant {mBoard=this,mPlantCol=c,mRow=r,mSeedType=type,mImitaterType=marker,mX=GridToPixelX(c,r),mY=GridToPixelY(c,r)};m_plants.Add(p);return p; }
 }
 public sealed class Lawn {public Plant? NormalPlant;}
 public readonly struct NativeDataID {
  private readonly uint value;
  private NativeDataID(uint value) {this.value=value;}
  public static implicit operator uint(NativeDataID id)=>id.value;
  public static implicit operator NativeDataID(uint value)=>new(value);
 }
 public sealed class Plant {
  static int next;public IntPtr Pointer=(IntPtr)(++next);public NativeDataID DataID=(uint)next;
  public int mPlantHealth=300;
  public Board mBoard=null!;public int mPlantCol,mRow,mRenderOrder,mShadowRenderOrder;
  public SeedType mSeedType=SeedType.Puffshroom,mImitaterType=SeedType.None;
  public bool mIsOnBoard=true,mDead,mSquished,OffGround;public PlantOnBungeeState mOnBungeeState;
  public float mX,mY;public bool NotOnGround()=>OffGround;public int CalcRenderOrder()=>100;public void UpdateReanim(){}
 }
}
namespace LawnGlove {public static class PlantPlacementRules {public static SeedType TerrainType(SeedType t)=>t;}}
namespace FusionMod {
 internal static class DebugMode {public static bool Enabled=true;public static bool Owns(Board board)=>Enabled;}
 internal static class FusionService {public static PlantingReason CheckNativePlacement(Board b,int c,int r,SeedType t)=>PlantingReason.Ok;}
 internal static class PlayerCursorScope {public static (int X,int Y) Position(Board b)=>(160,220);}
 internal static class PuffJumpers {public static bool Retained(Plant p)=>false;}
 internal static class NibbleShroom {public static bool Activated(Plant p)=>false;}
 internal static class Squish {public static bool Owns(Plant p)=>FusionPlant.From(p)==FusionPlant.Squish;public static bool Airborne(Plant p)=>Owns(p)&&p.OffGround;}
}
namespace PlantLibrary {
 internal sealed record Definition(Identity Identity);internal readonly record struct Identity(SeedType NativeType,SeedType Marker);
 internal static class Plants {public static Definition? Find(Plant p)=>null;}
}
namespace DebugMod {
 internal static class DebugMode { public static Artwork Art=new();public sealed class Artwork {public UnityEngine.Sprite? Fusion(string p)=>null;} }
}
