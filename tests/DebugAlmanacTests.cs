using DebugMod;
using Il2CppReloaded.DataModels;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
var provider = new AppDataProvider();
DebugAlmanac.Bind(provider);
DebugMode.Active = true;
DebugAlmanac.Update();
Check(provider.m_platformDataModel.m_isAlmanacAvailableModel.Value, "debug reveals almanac");
Check(provider.m_almanacModel.m_plantsModel.Refreshes == 1, "entry refresh uses native lock path");
DebugAlmanac.Update();
Check(provider.m_almanacModel.m_plantsModel.Refreshes == 1, "unchanged session has no refresh");
DebugMode.Active = false;
DebugAlmanac.Update();
Check(!provider.m_platformDataModel.m_isAlmanacAvailableModel.Value, "session exit restores native availability");
Check(provider.m_almanacModel.m_zombiesModel.Refreshes == 2, "exit preserves owned model instances");
// Profile reset must update existing locks, without replacing pending handles.
DebugAlmanac.RefreshCurrent();
Check(provider.m_almanacModel.m_plantsModel.Refreshes == 3, "profile reset refreshes existing entries");
DebugAlmanac.Unbind(provider);
DebugAlmanac.RefreshCurrent();
Check(provider.m_almanacModel.m_plantsModel.Refreshes == 3, "unbound provider cannot be touched");
DebugAlmanac.Bind(provider);
DebugMode.Active = true;
DebugAlmanac.Update();
int before = provider.m_almanacModel.m_plantsModel.Refreshes;
DebugAlmanac.Shutdown();
DebugMode.Active = false;
DebugAlmanac.Update(); DebugAlmanac.RefreshCurrent(); DebugAlmanac.Refresh(provider);
var late = new AppDataProvider(); DebugAlmanac.Bind(late);
DebugAlmanac.RefreshCurrent();
Check(provider.m_almanacModel.m_plantsModel.Refreshes == before, "shutdown cannot reload disposed native provider");
Check(late.m_almanacModel.m_plantsModel.Refreshes == 0, "late bind cannot revive retired provider");
Console.WriteLine("PASS almanac locks preserve entry ownership; session/profile refresh, unbind and shutdown lifetime.");

namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type t, string n) {} public HarmonyPatch(Type t, string n, Type[] a) {} }
}
namespace DebugMod {
 internal static class DebugMode { internal static bool Active; internal static bool Owns(object x) => Active; }
 internal static class ApiUnlocks { internal static bool? Entry(object x) => null; }
}
namespace Il2CppReloaded.Data { public sealed class AlmanacEntryData {} }
namespace Il2CppReloaded.Services { public sealed class UserService { public bool IsAlmanacAvailable() => false; public bool IsLocked(Il2CppReloaded.Data.AlmanacEntryData x) => true; } }
namespace Il2CppReloaded.TreeStateActivities { public sealed class GameplayActivity { public bool CanShowAlmanac() => false; } }
namespace Il2CppTekly.Extensions.DataProviders { public class RootModelDataProvider { public IntPtr Pointer = (IntPtr)42; } }
namespace Il2CppSource.DataModels {
 public class BoolModel { public bool Value; }
 public class PlatformDataModel { public BoolModel m_isAlmanacAvailableModel = new(); public Il2CppReloaded.Services.UserService m_userService = new(); }
 public class SeedChooserDataModel { public BoolModel m_isAlmanacAvailableModel = new(); }
}
namespace Il2CppReloaded.DataModels {
 public class AppDataProvider : Il2CppTekly.Extensions.DataProviders.RootModelDataProvider { public Il2CppSource.DataModels.PlatformDataModel m_platformDataModel = new(); public AlmanacModel m_almanacModel = new(); }
 public class Data { public object PlantAlmanacData = new(); public object ZombieAlmanacData = new(); }
 public class AlmanacModel { public Data m_dataService = new(); public Entries m_plantsModel = new(); public Entries m_zombiesModel = new(); }
 public class Entries { public int Refreshes; public void UpdateEntries(object x) => throw new Exception("must never replace entry models and pending handles"); public void UpdateEntriesLockedState(object x) => ++Refreshes; }
 public class AlmanacEntryModel { public object Value = new(); public bool m_locked; }
}
