using System.Text.Json;
using Il2CppReloaded.Gameplay;

namespace UnityEngine
{
    public readonly record struct Vector2(float x,float y);
    public readonly record struct Vector3(float x,float y,float z);
    public readonly record struct Bounds(Vector3 center,Vector3 size);
    public static class Application { public static bool Focused=false;public static bool isFocused=>Focused; }
    public static class Time { public static int frameCount=87; }
    public static class Screen { public static int width=1920,height=1017; }
    public sealed class GameObject { public bool activeInHierarchy=true; }
    public sealed class Transform { public Vector3 position=new(10,20,30); }
    public sealed class Sprite { public string name="source-sprite"; }
    public sealed class NativeType { public string Name="Renderer"; }
    public class Renderer
    {
        static int next; public IntPtr Pointer=(IntPtr)(++next);
        public string name="native"; public bool enabled=true;
        public GameObject gameObject=new(); public Transform transform=new();
        public Bounds bounds=new(new(1,2,3),new(4,5,6));
        public NativeType GetIl2CppType()=>new(){Name=GetType().Name};
        public T? TryCast<T>() where T:class => this as T;
    }
    public sealed class SpriteRenderer:Renderer { public Sprite? sprite=new(); }
    public sealed class MeshRenderer:Renderer { }
}
namespace UnityEngine.InputSystem
{
    public sealed class PositionControl
    {
        public UnityEngine.Vector2 Value=new(780,537);
        public int Reads;
        public UnityEngine.Vector2 ReadValue() { ++Reads; return Value; }
    }
    public sealed class Mouse
    {
        public static Mouse? current=new();
        public PositionControl position=new();
    }
}
namespace Il2Cpp { public static class TimeUtil { public static bool Paused=true;public static bool IsTimePaused()=>Paused; } }
namespace Il2CppSource.Controllers
{
    public sealed class PreviewDrawer { public UnityEngine.SpriteRenderer? m_previewSprite; }
    public sealed class PreviewController
    {
        public bool Exists=true;
        public UnityEngine.GameObject gameObject=new(); public UnityEngine.Transform transform=new();
        public PreviewDrawer? m_previewDrawer;
        public UnityEngine.Renderer[] Renderers=Array.Empty<UnityEngine.Renderer>();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T:class => Renderers.Cast<T>().ToArray();
        public static implicit operator bool(PreviewController value)=>value.Exists;
    }
}
namespace Il2CppReloaded.Gameplay
{
    // SDK MultiplayerType has bounded player indexing, not IEnumerable.
    public sealed class MultiplayerType<T>
    {
        private readonly T[] values;
        public int Reads;
        public MultiplayerType(T first,T second) {values=new[]{first,second};}
        public T this[int player] {get {++Reads;return values[player];} set=>values[player]=value;}
    }
    public enum SeedType { None,Puffshroom,Chomper,Squash }
    public enum CursorType { Normal,PlantFromBank,Glove }
    public sealed class WidgetManager { public MultiplayerType<int>? LastMouseX=new(120,180),LastMouseY=new(200,260); }
    public sealed class App { public WidgetManager? WidgetManager=new(); }
    public sealed class Board
    {
        static int next;public IntPtr Pointer=(IntPtr)(++next);
        public App? mApp=new();public bool mPaused;
        public int mX=40,mY=80;
        public MultiplayerType<int>? mPrevMouseX=new(80,140),mPrevMouseY=new(120,180);
        public MultiplayerType<CursorObject?>? CursorObjects=new(null,null);
        public MultiplayerType<CursorPreview?>? CursorPreviews=new(null,null);
        public GamepadCursor?[]? GamepadCursors=new GamepadCursor?[2];
        // Deliberately return supplied native results, never reproduce a grid
        // formula here. Tests verify the handler's actual conversion arguments.
        public int GridCol=3,GridRow=2,Rows=5;
        public readonly List<(int X,int Y)> GridXCalls=new(),GridYCalls=new();
        public int RowCountReads;
        public int PixelToGridX(float x,float y) {GridXCalls.Add(((int)x,(int)y));return GridCol;}
        public int PixelToGridY(int x,int y) {GridYCalls.Add((x,y));return GridRow;}
        public int GetNumRows() {++RowCountReads;return Rows;}
    }
    public sealed class GamepadCursor
    {
        public Board? mBoard;
        public int PlayerIndex,m_gridX,m_gridY;
        public float mX,mY;
        public bool NativeEnabled=true;
        public int EnabledReads;
        public bool Enabled {get {++EnabledReads;return NativeEnabled;}}
    }
    public sealed class CursorObject
    {
        public Board? mBoard;
        public SeedType mType=SeedType.Puffshroom,mImitaterType=SeedType.Chomper;
        public int mSeedBankIndex=7,PlayerIndex;
        public CursorType CursorType=CursorType.PlantFromBank;
        public float mX=17,mY=23;
        public Il2CppSource.Controllers.PreviewController? mPreviewController;
    }
    public sealed class CursorPreview
    {
        public Board? mBoard;public int PlayerIndex,mGridX=-1,mGridY=-2;
        public float mX=-99,mY=42;public bool mVisible;
        public Il2CppSource.Controllers.PreviewController? mController;
    }
}
namespace DebugMod
{
    public sealed class ApiError:Exception
    {public string Code;public ApiError(string code,string message):base(message){Code=code;}}
    internal static class ApiBoard {internal static string Token="current";internal static Board Current=new();}
    internal static class DebugMode {internal static bool Active=true;internal static bool Owns(Board b)=>Active&&b.Pointer==ApiBoard.Current.Pointer;}
    internal static class DebugPlantCards
    {
        internal static int Calls;internal static bool Found=true;
        internal static (bool Found,SeedType NativeType,SeedType Marker) CursorIdentity(CursorObject c)
        {++Calls;return Found?(true,SeedType.Puffshroom,SeedType.Chomper):default;}
    }
    internal static partial class DebugCommands
    {
        // External command/session boundaries. The real cursor handler is linked above.
        private static Board EntityBoard(JsonElement args)
        {
            if(args.GetProperty("board").GetString()!=ApiBoard.Token)throw new ApiError("STALE_BOARD","stale");
            return ApiBoard.Current;
        }
        internal static int Int(JsonElement args,string key,int min,int max)
        {
            if(!args.GetProperty(key).TryGetInt32(out int value)||value<min||value>max)throw new ApiError("INVALID_ARGUMENT","player");
            return value;
        }
        internal static object Run(JsonElement args)=>Cursor(args);
    }
}
