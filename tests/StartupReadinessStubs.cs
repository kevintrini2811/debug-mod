namespace UnityEngine {
 public class Object {
  static long next; static readonly List<Object> objects = new();
  public readonly IntPtr Pointer = (IntPtr)(++next);
  public Object(){objects.Add(this);} public static void Reset()=>objects.Clear();
  public T? TryCast<T>() where T:class=>this as T;
  public static implicit operator bool(Object? value)=>value!=null;
  public static T[] FindObjectsOfType<T>(bool includeInactive=false) where T:Object =>objects.OfType<T>().ToArray();
 }
 public sealed class GameObject:Object {public string name="";public bool activeInHierarchy=true;public Transform transform;public GameObject(){transform=new Transform();}public T[] GetComponents<T>() where T:Component=>FindObjectsOfType<T>(true).Where(c=>c.gameObject==this).ToArray();public T[] GetComponentsInChildren<T>(bool includeInactive) where T:Component=>FindObjectsOfType<T>(true).Where(c=>c.transform.IsChildOf(transform)).ToArray();}
 public sealed class Transform:Object {public Transform? Parent;public bool IsChildOf(Transform parent)=>this==parent || Parent?.IsChildOf(parent)==true;}
 public class Component:Object {
  public GameObject gameObject=new();public bool isActiveAndEnabled=true;public string name=>gameObject.name;public Transform transform=>gameObject.transform;
  public T? GetComponent<T>() where T:Component=>FindObjectsOfType<T>(true).FirstOrDefault(c=>c.gameObject==gameObject);
  public T[] GetComponentsInChildren<T>(bool includeInactive) where T:Component =>FindObjectsOfType<T>(true).Where(c=>c.transform.IsChildOf(transform)).ToArray();
 }
}
namespace UnityEngine.UI {public class Button:UnityEngine.Component {public bool interactable=true;public bool IsInteractable()=>interactable;}}
namespace Il2CppTekly.Common.Presentables {public enum PresentableState {Hidden,Showing,Shown,Hiding}}
namespace Il2CppTekly.TreeState {
 public enum ActivityMode {Inactive,Loading,ReadyToActivate,Active,PreUnload,Unloading}
 public class TreeState:UnityEngine.Object {public ActivityMode Mode=ActivityMode.Active;public TreeStateManager? Manager;}
 public class TreeStateManager:UnityEngine.Object {public bool m_initialized=true;public TreeState? Active;}
}
namespace Il2CppReloaded.TreeStateActivities {
 public class MainMenuActivity:UnityEngine.Component {public object? m_container=new();public Il2CppTekly.TreeState.ActivityMode Mode=Il2CppTekly.TreeState.ActivityMode.Active;public Il2CppTekly.TreeState.TreeState? TreeState;}
}
namespace Il2CppReloaded.UI {
 public class FakeTween {public bool isAlive;}
 public class CarouselGroup {public UnityEngine.Component? levelCarousel;}
 public class LevelSelectScreen:UnityEngine.Component {public bool m_levelSelectIsActive;public bool ReachedCarousel;public UnityEngine.UI.Button? m_backButton;public CarouselGroup? m_selectedCarouselGroup;public Dictionary<UnityEngine.Transform,FakeTween>? m_tweens=new();}
 public class MainMenuPanelView:Il2CppTekly.PanelViews.PanelView {public bool transitioning;public bool IsTransitioning()=>transitioning;public LevelSelectScreen? LevelSelect;}
}
namespace Il2CppTekly.DataModels.Binders {
 public class UnityButtonBinder:UnityEngine.Component {public object? m_buttonModel=new();public object? m_disposable=new();public UnityEngine.UI.Button? m_button;}
 public class ButtonBinder:UnityEngine.Component {public object? m_buttonModel=new();public object? m_disposable=new();public UnityEngine.UI.Button? m_button;}
}

namespace Il2CppTekly.PanelViews {public class PanelView:UnityEngine.Component {private Il2CppTekly.Common.Presentables.PresentableState state=Il2CppTekly.Common.Presentables.PresentableState.Shown;public bool throwOnState;public Il2CppTekly.Common.Presentables.PresentableState State{get=>throwOnState?throw new InvalidOperationException("native getter unavailable"):state;set=>state=value;}public bool IsAnimating;public string Id="";} public class PanelViewContainer:UnityEngine.Component{public PanelView[]? m_panels;}}
namespace Il2CppSource.UI {public class ConfirmPanelView:Il2CppTekly.PanelViews.PanelView{}public class GameplayOptionsPanelView:Il2CppTekly.PanelViews.PanelView{}}
namespace Il2CppReloaded.Input {
 public class InputLayer:UnityEngine.Object {public UnityEngine.GameObject gameObject=new();public bool IgnoreInput1;}
 public class InputNavigationManager:UnityEngine.Object {public static InputNavigationManager? _Instance_k__BackingField;public InputLayer? CurrentLayer;}
}

namespace UnityEngine {
 public class Canvas:Component {public bool enabled=true;public int sortingOrder;}
 public class CanvasGroup:Component {public bool enabled=true;public float alpha=1;public bool interactable=true,blocksRaycasts=true,ignoreParentGroups;}
 public class CanvasRenderer:Component {public bool cull;public float alpha=1;public float GetAlpha()=>alpha;}
}
