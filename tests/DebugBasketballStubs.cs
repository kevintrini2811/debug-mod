namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] internal sealed class HarmonyPatch : Attribute
    { public HarmonyPatch(Type type, string method) { } }
}
namespace UnityEngine
{
    internal struct Rect
    {
        internal float x, y, width, height;
        internal Rect(float x, float y, float width, float height)
        { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
}
namespace Il2CppReloaded.Gameplay
{
    internal enum ProjectileType { Basketball, Pea }
    internal enum Motion { Lobbed }
    internal enum Seed { Puffshroom, Chomper, Wallnut }
    internal sealed class Board
    {
        internal IntPtr Pointer = (IntPtr)1;
        internal int mMainCounter;
        internal bool Debug = true;
        internal List<Projectile> m_projectiles = new();
        internal List<Plant> m_plants = new();
    }
    internal sealed class Projectile
    {
        internal IntPtr Pointer = (IntPtr)2;
        internal uint DataID = 11;
        internal Board mBoard = null!;
        internal ProjectileType mProjectileType = ProjectileType.Basketball;
        internal Motion mMotionType = Motion.Lobbed;
        internal int mRow = 2, mWidth = 40, mHeight = 40, mProjectileAge = 120;
        internal float mX = 310, mY = 264, mPosX = 306, mPosY = 206, mPosZ = 65, mVelX = -4, mVelY, mVelZ = 7, mAccZ = .115f;
        internal bool mDead;
        public void UpdateLobMotion() => throw new Exception("Diagnostics must not call native motion.");
        public Plant? FindCollisionTargetPlant() => throw new Exception("Diagnostics must not call native query.");
        public UnityEngine.Rect GetProjectileRect() => throw new Exception("Diagnostics must not call geometry getters.");
    }
    internal sealed class Plant
    {
        internal IntPtr Pointer = (IntPtr)3;
        internal uint DataID = 21;
        internal Board mBoard = null!;
        internal Seed mSeedType = Seed.Puffshroom, mImitaterType = Seed.Chomper;
        internal int mPlantCol = 3, mRow = 2, mWidth = 80, mHeight = 80, mStartRow = 100, mTargetX, mShootingCounter = 1;
        private int health = 300;
        internal bool ThrowHealth;
        internal int mPlantHealth { get => ThrowHealth ? throw new InvalidOperationException("diagnostic read failed") : health; set => health = value; }
        internal float mX = 300, mY = 264;
        internal bool mDead, mSquished, mIsOnBoard;
        public UnityEngine.Rect GetPlantRect() => throw new Exception("Diagnostics must not call geometry getters.");
    }
}
namespace DebugMod
{
    internal static class ApiBoard
    {
        internal static Il2CppReloaded.Gameplay.Board? Current;
        internal static string Token = "live";
        internal static Il2CppReloaded.Gameplay.Board Require() => Current ?? throw new ApiError("BOARD_NOT_READY", "No board.");
    }
    internal static class DebugMode
    { internal static bool Owns(Il2CppReloaded.Gameplay.Board board) => board.Debug; }
    internal sealed class ApiError : Exception
    {
        internal string Code;
        internal ApiError(string code, string message) : base(message) { Code = code; }
    }
}
