using System.Text.Json;
using DebugMod;
using Il2CppReloaded.Gameplay;
using Il2CppSource.Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

static class Program
{
    static int checks;
    static void Check(bool value,string message) {++checks;if(!value)throw new Exception(message);}
    static JsonElement Args(string? player=null,string token="current")=>JsonSerializer.SerializeToElement(
        player==null?new Dictionary<string,object?>{{"board",token}}:new Dictionary<string,object?>{{"board",token},{"player",JsonSerializer.Deserialize<JsonElement>(player)}});
    static JsonElement Read(string? player=null)=>JsonSerializer.SerializeToElement(DebugCommands.Run(Args(player)));
    static void Error(string code,JsonElement args)
    {
        try{DebugCommands.Run(args);throw new Exception("expected error");}
        catch(ApiError e){Check(e.Code==code,"exact guard code");}
    }
    public static void Main()
    {
        var board=ApiBoard.Current;
        Error("STALE_BOARD",Args(token:"old"));
        DebugMode.Active=false;Error("DEBUG_REQUIRED",Args());DebugMode.Active=true;
        foreach(var player in new[]{"-1","2","1.5"})Error("INVALID_ARGUMENT",Args(player));
        Check(board.CursorObjects!.Reads==0&&board.CursorPreviews!.Reads==0&&Mouse.current!.position.Reads==0&&DebugPlantCards.Calls==0
            &&board.GridXCalls.Count==0&&board.GridYCalls.Count==0&&board.RowCountReads==0,
            "all guards precede indexed or input reads");
        var empty=Read();
        Check(empty.GetProperty("board").GetString()=="current"&&empty.GetProperty("player").GetInt32()==0,"default player and current board");
        Check(empty.GetProperty("cursor").ValueKind==JsonValueKind.Null&&empty.GetProperty("gridPreview").ValueKind==JsonValueKind.Null,
            "missing cursor and preview remain null");
        Check(empty.GetProperty("gamepad").ValueKind==JsonValueKind.Null,"missing selected-player gamepad slot remains null");
        Check(empty.GetProperty("paused").GetBoolean()&&!empty.GetProperty("boardPaused").GetBoolean(),"global and board pause independently read");
        Check(empty.GetProperty("frame").GetInt32()==87&&DateTimeOffset.TryParse(empty.GetProperty("utc").GetString(),out _),"actual frame and parseable UTC");
        var input=empty.GetProperty("unityInput");
        Check(!input.GetProperty("focused").GetBoolean(),"actual Unity focus is independent of native/X11 focus");
        Check(input.GetProperty("source").GetString()=="UnityEngine.InputSystem.Mouse.current.position","exact existing input owner named");
        Check(input.GetProperty("mouse").GetProperty("y").GetSingle()==537&&input.GetProperty("screen").GetProperty("height").GetInt32()==1017,
            "InputSystem bottom-left pixels preserved raw");
        var native=empty.GetProperty("nativeMouse");
        Check(native.GetProperty("widget").GetProperty("x").GetInt32()==120&&native.GetProperty("previous").GetProperty("x").GetInt32()==80
            &&native.GetProperty("boardOrigin").GetProperty("x").GetInt32()==40,"native widget and board-local coordinates remain separate");
        Check(native.GetProperty("units").GetProperty("previous").GetString()!.Contains("board-local"),"native units explicit");
        Check(native.GetProperty("boardLocal").GetProperty("x").GetInt32()==80
            &&native.GetProperty("boardLocal").GetProperty("y").GetInt32()==120,"nonzero board offset removed from player zero widget");
        Check(board.GridXCalls.SequenceEqual(new[]{(80,120)})&&board.GridYCalls.SequenceEqual(new[]{(80,120)}),
            "both native conversion owners receive exact board-local point");
        Check(native.GetProperty("grid").GetProperty("col").GetInt32()==3
            &&native.GetProperty("grid").GetProperty("row").GetInt32()==2&&native.GetProperty("grid").GetProperty("inBounds").GetBoolean(),
            "native results exposed without a diagnostic grid formula");

        var sprite=new SpriteRenderer();var mesh=new MeshRenderer();mesh.name="mesh";mesh.enabled=false;
        var controller=new PreviewController {m_previewDrawer=new(){m_previewSprite=sprite},Renderers=new Renderer[]{sprite,mesh}};
        var cursor=new CursorObject {mBoard=board,PlayerIndex=1,mPreviewController=controller};
        var preview=new CursorPreview {mBoard=board,PlayerIndex=1,mController=controller};
        board.CursorObjects[1]=cursor;board.CursorPreviews![1]=preview;
        var before=(cursor.CursorType,cursor.mType,cursor.mImitaterType,cursor.mSeedBankIndex,cursor.mX,cursor.mY,
            preview.mGridX,preview.mGridY,preview.mX,preview.mY,preview.mVisible,sprite.enabled,mesh.enabled,controller.transform.position,
            sprite.transform.position,sprite.bounds,Il2Cpp.TimeUtil.Paused,board.mPaused,Mouse.current!.position.Value);
        var one=Read("1");
        Check(one.GetProperty("player").GetInt32()==1&&one.GetProperty("nativeMouse").GetProperty("widget").GetProperty("x").GetInt32()==180,
            "selected player indexes actual multiplayer owner");
        Check(one.GetProperty("nativeMouse").GetProperty("boardLocal").GetProperty("x").GetInt32()==140
            &&one.GetProperty("nativeMouse").GetProperty("boardLocal").GetProperty("y").GetInt32()==180
            &&board.GridXCalls.Last()==(140,180)&&board.GridYCalls.Last()==(140,180),"player one conversion never falls back to player zero");
        var cs=one.GetProperty("cursor");
        Check(cs.GetProperty("playerIndex").GetInt32()==1,"actual cursor owner reported independently of requested player");
        Check(cs.GetProperty("sameBoard").GetBoolean()&&cs.GetProperty("identity").GetProperty("found").GetBoolean()
            &&cs.GetProperty("identity").GetProperty("marker").GetString()=="Chomper","actual effective card identity exposed");
        Check(cs.GetProperty("raw").GetProperty("seedBankIndex").GetInt32()==7&&cs.GetProperty("raw").GetProperty("type").GetString()=="Puffshroom"
            &&cs.GetProperty("raw").GetProperty("imitaterType").GetString()=="Chomper","raw cursor identity not inferred from card art");
        var grid=one.GetProperty("gridPreview");
        Check(grid.GetProperty("gridX").GetInt32()==-1&&grid.GetProperty("gridY").GetInt32()==-2&&!grid.GetProperty("visible").GetBoolean(),
            "uninitialized and out-of-board native preview coordinates preserved");
        Check(one.GetProperty("nativeMouse").GetProperty("grid").GetProperty("inBounds").GetBoolean(),
            "current tool point remains independent of invisible stale preview");
        var renderers=cs.GetProperty("preview").GetProperty("renderers");
        Check(renderers[0].GetProperty("native").GetBoolean()&&!renderers[1].GetProperty("native").GetBoolean(),"native anchor distinguished from other renderers");
        Check(renderers[0].GetProperty("sprite").GetString()=="source-sprite"&&renderers[1].GetProperty("sprite").ValueKind==JsonValueKind.Null,
            "sprite metadata only for SpriteRenderer");
        Check(renderers[0].GetProperty("bounds").GetProperty("center").GetProperty("y").GetSingle()==2
            &&renderers[1].GetProperty("enabled").GetBoolean()==false,"existing bounds and disabled states reported without enabling");
        var after=(cursor.CursorType,cursor.mType,cursor.mImitaterType,cursor.mSeedBankIndex,cursor.mX,cursor.mY,
            preview.mGridX,preview.mGridY,preview.mX,preview.mY,preview.mVisible,sprite.enabled,mesh.enabled,controller.transform.position,
            sprite.transform.position,sprite.bounds,Il2Cpp.TimeUtil.Paused,board.mPaused,Mouse.current!.position.Value);
        Check(before==after,"cursor native positions rendering pause and input remain unchanged");
        var widgetOwner=board.mApp!.WidgetManager!;
        var unchanged=(board.mX,board.mY,widgetOwner.LastMouseX![0],widgetOwner.LastMouseX[1],
            widgetOwner.LastMouseY![0],widgetOwner.LastMouseY[1],board.mPrevMouseX![0],board.mPrevMouseY![1],board.GridCol,board.GridRow,board.Rows);
        foreach(var target in new[]{(-1,-2),(9,2),(3,5),(3,6)})
        {
            board.GridCol=target.Item1;board.GridRow=target.Item2;
            var outside=Read("1").GetProperty("nativeMouse").GetProperty("grid");
            Check(outside.GetProperty("col").GetInt32()==target.Item1&&outside.GetProperty("row").GetInt32()==target.Item2
                &&!outside.GetProperty("inBounds").GetBoolean(),"outside native values remain raw, never clamped");
        }
        board.GridCol=3;board.GridRow=5;board.Rows=6;
        Check(Read("1").GetProperty("nativeMouse").GetProperty("grid").GetProperty("inBounds").GetBoolean(),"native sixth row accepted on six-row board");
        board.GridCol=3;board.GridRow=2;board.Rows=5;
        Read("1");Read("0");
        Check(unchanged==(board.mX,board.mY,widgetOwner.LastMouseX[0],widgetOwner.LastMouseX[1],
            widgetOwner.LastMouseY[0],widgetOwner.LastMouseY[1],board.mPrevMouseX[0],board.mPrevMouseY[1],board.GridCol,board.GridRow,board.Rows),
            "repeated inspection preserves widget, board offset, previous point and native grid state");
        var savedX=widgetOwner.LastMouseX;var savedY=widgetOwner.LastMouseY;
        foreach(bool missingX in new[]{true,false})
        {
            widgetOwner.LastMouseX=missingX?null:savedX;widgetOwner.LastMouseY=missingX?savedY:null;
            int xCalls=board.GridXCalls.Count,yCalls=board.GridYCalls.Count,rowReads=board.RowCountReads;
            var missing=Read("1").GetProperty("nativeMouse");
            Check(missing.GetProperty("widget").ValueKind==JsonValueKind.Null
                &&missing.GetProperty("boardLocal").ValueKind==JsonValueKind.Null&&missing.GetProperty("grid").ValueKind==JsonValueKind.Null,
                "either missing widget axis leaves all derived positions null");
            Check(board.GridXCalls.Count==xCalls&&board.GridYCalls.Count==yCalls&&board.RowCountReads==rowReads,
                "missing widget data never invokes native grid readers");
        }
        widgetOwner.LastMouseX=savedX;widgetOwner.LastMouseY=savedY;
        DebugPlantCards.Found=false;cursor.mImitaterType=SeedType.None;
        var ordinary=Read("1").GetProperty("cursor");
        Check(!ordinary.GetProperty("identity").GetProperty("found").GetBoolean()
            &&ordinary.GetProperty("raw").GetProperty("type").GetString()=="Puffshroom","ordinary native card allowed with found false");
        controller.Exists=false;Check(Read("1").GetProperty("cursor").GetProperty("preview").ValueKind==JsonValueKind.Null,"destroyed controller remains null");
        controller.Exists=true;controller.m_previewDrawer=null;
        Check(!Read("1").GetProperty("cursor").GetProperty("preview").GetProperty("renderers")[0].GetProperty("native").GetBoolean(),
            "missing native drawer does not create one");

        int calls=DebugPlantCards.Calls;
        cursor.mBoard=new Board();preview.mBoard=new Board();
        var foreign=Read("1");
        Check(!foreign.GetProperty("cursor").GetProperty("sameBoard").GetBoolean()
            &&foreign.GetProperty("cursor").GetProperty("raw").ValueKind==JsonValueKind.Null
            &&foreign.GetProperty("cursor").GetProperty("identity").ValueKind==JsonValueKind.Null,"foreign cursor has no dependent reads");
        Check(!foreign.GetProperty("gridPreview").GetProperty("sameBoard").GetBoolean()
            &&foreign.GetProperty("gridPreview").GetProperty("controller").ValueKind==JsonValueKind.Null,"foreign preview has no controller traversal");
        Check(foreign.GetProperty("cursor").GetProperty("playerIndex").ValueKind==JsonValueKind.Null,"foreign cursor owner remains null");
        Check(calls==DebugPlantCards.Calls,"foreign cursor never invokes identity owner");
        cursor.mBoard=null;preview.mBoard=null;
        Check(!Read("1").GetProperty("cursor").GetProperty("sameBoard").GetBoolean(),"null owner guarded before identity read");
        var gamepad0=new GamepadCursor {mBoard=board,PlayerIndex=0,m_gridX=2,m_gridY=1,mX=160,mY=120};
        var gamepad1=new GamepadCursor {mBoard=board,PlayerIndex=1,m_gridX=4,m_gridY=5,mX=320,mY=400};
        board.GamepadCursors=new GamepadCursor?[]{gamepad0,gamepad1};
        var pad0=Read().GetProperty("gamepad");var pad1=Read("1").GetProperty("gamepad");
        Check(pad0.GetProperty("sameBoard").GetBoolean()&&pad0.GetProperty("playerIndex").GetInt32()==0
            &&pad0.GetProperty("enabled").GetBoolean()&&pad0.GetProperty("gridX").GetInt32()==2
            &&pad0.GetProperty("x").GetSingle()==160,"player zero exposes its own native DPad slot");
        Check(pad1.GetProperty("playerIndex").GetInt32()==1&&pad1.GetProperty("gridX").GetInt32()==4
            &&pad1.GetProperty("gridY").GetInt32()==5&&pad1.GetProperty("y").GetSingle()==400,
            "player one never observes player zero gamepad state");
        Check(pad0.GetProperty("units").GetProperty("grid").GetString()!.Contains("stored native DPad"),"gamepad grid provenance is explicit");
        int enabledReads=gamepad0.EnabledReads;
        Error("STALE_BOARD",Args(token:"old"));Error("INVALID_ARGUMENT",Args("2"));
        Check(gamepad0.EnabledReads==enabledReads,"request guards precede native enabled getter");
        widgetOwner.LastMouseX![0]=board.mX;widgetOwner.LastMouseY![0]=board.mY;
        board.GridCol=board.GridRow=-1;
        gamepad0.m_gridX=gamepad0.m_gridY=0;gamepad0.mX=gamepad0.mY=0;
        var unsynced=Read();
        Check(unsynced.GetProperty("nativeMouse").GetProperty("grid").GetProperty("col").GetInt32()==-1
            &&unsynced.GetProperty("gamepad").GetProperty("gridX").GetInt32()==0
            &&unsynced.GetProperty("gamepad").GetProperty("x").GetSingle()==0,
            "unsynchronized widget and uninitialized native anchor remain distinct raw observations");
        gamepad0.NativeEnabled=false;
        Check(!Read().GetProperty("gamepad").GetProperty("enabled").GetBoolean(),"disabled native slot is observed without enabling it");
        gamepad0.NativeEnabled=true;gamepad0.m_gridX=-1;gamepad0.m_gridY=99;gamepad0.mX=-50;gamepad0.mY=-200;
        var padBefore=(gamepad0.mBoard,gamepad0.PlayerIndex,gamepad0.NativeEnabled,gamepad0.m_gridX,gamepad0.m_gridY,gamepad0.mX,gamepad0.mY,
            widgetOwner.LastMouseX[0],widgetOwner.LastMouseY[0],board.mX,board.mY,Il2Cpp.TimeUtil.Paused,board.mPaused);
        var rawPad=Read().GetProperty("gamepad");Read();
        Check(rawPad.GetProperty("gridX").GetInt32()==-1&&rawPad.GetProperty("gridY").GetInt32()==99
            &&rawPad.GetProperty("x").GetSingle()==-50,"outside native grid and position values are never clamped");
        Check(padBefore==(gamepad0.mBoard,gamepad0.PlayerIndex,gamepad0.NativeEnabled,gamepad0.m_gridX,gamepad0.m_gridY,gamepad0.mX,gamepad0.mY,
            widgetOwner.LastMouseX[0],widgetOwner.LastMouseY[0],board.mX,board.mY,Il2Cpp.TimeUtil.Paused,board.mPaused),
            "repeated gamepad inspection preserves native slot, widget and pause state");
        enabledReads=gamepad0.EnabledReads;gamepad0.mBoard=new Board();
        var foreignPad=Read().GetProperty("gamepad");
        Check(!foreignPad.GetProperty("sameBoard").GetBoolean()&&foreignPad.GetProperty("playerIndex").ValueKind==JsonValueKind.Null
            &&foreignPad.GetProperty("enabled").ValueKind==JsonValueKind.Null&&foreignPad.GetProperty("gridX").ValueKind==JsonValueKind.Null,
            "foreign gamepad board exposes no dependent state");
        Check(gamepad0.EnabledReads==enabledReads,"foreign gamepad never invokes enabled getter");
        gamepad0.mBoard=board;gamepad0.PlayerIndex=1;
        var wrongPlayer=Read().GetProperty("gamepad");
        Check(wrongPlayer.GetProperty("sameBoard").GetBoolean()&&wrongPlayer.GetProperty("playerIndex").GetInt32()==1
            &&wrongPlayer.GetProperty("enabled").ValueKind==JsonValueKind.Null&&wrongPlayer.GetProperty("x").ValueKind==JsonValueKind.Null,
            "wrong player slot reports the mismatch without reading dependent fields");
        Check(gamepad0.EnabledReads==enabledReads,"wrong player gamepad never invokes enabled getter");
        gamepad0.mBoard=null;
        Check(!Read().GetProperty("gamepad").GetProperty("sameBoard").GetBoolean()&&gamepad0.EnabledReads==enabledReads,
            "null gamepad board is guarded");
        board.GamepadCursors=null;
        Check(Read().GetProperty("gamepad").ValueKind==JsonValueKind.Null,"null gamepad array stays null");
        board.GamepadCursors=Array.Empty<GamepadCursor?>();
        Check(Read().GetProperty("gamepad").ValueKind==JsonValueKind.Null,"empty gamepad array is not indexed");
        board.GamepadCursors=new GamepadCursor?[]{gamepad0};
        Check(Read("1").GetProperty("gamepad").ValueKind==JsonValueKind.Null&&gamepad0.EnabledReads==enabledReads,
            "short gamepad array never falls back to another player");
        board.GamepadCursors=new GamepadCursor?[]{null};
        Check(Read().GetProperty("gamepad").ValueKind==JsonValueKind.Null,"null native gamepad slot is not created");
        Mouse.current=null;board.mApp=null;board.mPrevMouseX=null;
        var absent=Read();
        Check(absent.GetProperty("unityInput").GetProperty("mouse").ValueKind==JsonValueKind.Null,"missing InputSystem Mouse stays null");
        Check(absent.GetProperty("nativeMouse").GetProperty("widget").ValueKind==JsonValueKind.Null
            &&absent.GetProperty("nativeMouse").GetProperty("previous").ValueKind==JsonValueKind.Null,"missing native owners remain null");
        Check(absent.GetProperty("nativeMouse").GetProperty("boardLocal").ValueKind==JsonValueKind.Null
            &&absent.GetProperty("nativeMouse").GetProperty("grid").ValueKind==JsonValueKind.Null,"missing app leaves derived tool point null");
        board.CursorObjects=null;board.CursorPreviews=null;
        Check(Read().GetProperty("cursor").ValueKind==JsonValueKind.Null,"missing multiplayer owner not instantiated");
        Console.WriteLine($"Cursor diagnostics: {checks} checks passed; actual readonly handler, inert SDK boundaries.");
    }
}
