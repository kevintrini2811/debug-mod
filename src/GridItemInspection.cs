using Il2CppReloaded.Gameplay;

namespace DebugMod;

// A native-board read for pool diagnostics. It neither allocates native slots
// nor invokes mod callbacks, controllers or item updates.
internal static class GridItemInspection
{
    internal static object Read(Board board, string token)
    {
        var items = new List<object>();
        for (int i = 0; i < board.m_gridItems.Count; ++i)
        {
            var item = board.m_gridItems[i];
            if (item == null || item.mDead) continue;
            items.Add(new {
                DataID = (uint)item.DataID, type = item.mGridItemType.ToString(), state = item.mGridItemState.ToString(),
                item.mSunCount, item.mGridX, item.mGridY, item.mGridItemCounter,
                item.mPosX, item.mPosY, item.mGoalX, item.mGoalY, item.mX, item.mY, item.mVisible
            });
        }
        return new { board = token, allocated = board.m_gridItems.Count, capacity = board.m_gridItems.Capacity, items };
    }
}
