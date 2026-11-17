using System.Text.Json;
using DebugMod;
using Il2CppReloaded.Gameplay;

static class InspectionTests
{
    static int checks;
    static void Check(bool value, string text) { if (!value) throw new Exception(text); ++checks; }
    static void Main()
    {
        var board = new Board();
        var own = new GridItem { DataID = 0xf1234567, mSunCount = -521094, mGridX = -243351284, mGridItemCounter = 120, mPosX = 17, mVisible = false };
        var foreign = new GridItem { DataID = 3, mSunCount = 55, mGridX = 2, mGridY = 3, mVisible = true };
        var dead = new GridItem { DataID = 4, mDead = true };
        board.m_gridItems.Add(own); board.m_gridItems.Add(foreign); board.m_gridItems.Add(dead); board.m_gridItems.Add(null);
        var before = JsonSerializer.Serialize(board, new JsonSerializerOptions { IncludeFields = true });
        var result = JsonSerializer.SerializeToElement(GridItemInspection.Read(board, "current-board"));
        Check(result.GetProperty("board").GetString() == "current-board", "native board token returned");
        Check(result.GetProperty("allocated").GetInt32() == 4 && result.GetProperty("capacity").GetInt32() == 128, "allocated count includes dead/pending slots, capacity explicit");
        var items = result.GetProperty("items"); Check(items.GetArrayLength() == 2, "dead and null entries omitted");
        Check(items[0].GetProperty("DataID").GetUInt32() == 0xf1234567, "full unsigned native ID preserved");
        Check(items[0].GetProperty("mSunCount").GetInt32() == -521094 && items[0].GetProperty("mGridX").GetInt32() == -243351284, "own provenance markers preserved exactly");
        Check(items[0].GetProperty("mGridItemCounter").GetInt32() == 120 && items[0].GetProperty("mPosX").GetSingle() == 17, "rate and payload observable");
        Check(items[1].GetProperty("mSunCount").GetInt32() == 55 && items[1].GetProperty("mVisible").GetBoolean(), "foreign items are visible to read without rewriting fields");
        Check(JsonSerializer.Serialize(board, new JsonSerializerOptions { IncludeFields = true }) == before && board.m_gridItems.NativeWrites == 0, "inspection changes no board field or native pool state");
        Console.WriteLine($"PASS {checks} production grid-item read assertions; no game executed");
    }
}
namespace Il2CppReloaded.Gameplay
{
    enum GridItemType { ZenTool }
    enum GridItemState { Normal }
    sealed class Items : List<GridItem?> { public new int Capacity => 128; public int NativeWrites = 0; }
    sealed class Board { public Items m_gridItems = new(); }
    sealed class GridItem
    {
        public uint DataID;
        public bool mDead, mVisible;
        public GridItemType mGridItemType = GridItemType.ZenTool;
        public GridItemState mGridItemState = GridItemState.Normal;
        public int mSunCount, mGridX, mGridY, mGridItemCounter, mX = -1000, mY = -1000;
        public float mPosX, mPosY = 0, mGoalX = 0, mGoalY = 0;
    }
}
