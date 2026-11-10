using DebugMod;
using Il2CppReloaded.Input;
using Il2CppTekly.PanelViews;
using Il2CppReloaded.TreeStateActivities;
using Il2CppReloaded.UI;
using Il2CppTekly.Common.Presentables;
using Il2CppTekly.DataModels.Binders;
using Il2CppTekly.TreeState;
using UnityEngine;
using Object=UnityEngine.Object;

static class Tests {
 static int checks;
 sealed class Fixture {
  internal MainMenuPanelView view=new();internal MainMenuActivity menu=new();internal UnityButtonBinder binder=new();internal UnityEngine.UI.Button button=new();
  internal Fixture(){menu.TreeState=new(){Manager=new()};menu.TreeState.Manager.Active=menu.TreeState;binder.m_button=button;binder.transform.Parent=view.transform;button.transform.Parent=view.transform;InputNavigationManager._Instance_k__BackingField=new(){CurrentLayer=new()};InputNavigationManager._Instance_k__BackingField.CurrentLayer.gameObject.transform.Parent=view.transform;}
 }
 static Fixture Fresh(){Object.Reset();return new Fixture();}
 static void Assert(bool value,string description){checks++;if(!value)throw new Exception(description);}
 static bool Ready(bool selection=false)=>NativeMenuReadiness.TryGet(out _,out _,selection);
 static void Reject(Action<Fixture> mutate,string name){var f=Fresh();mutate(f);Assert(!Ready(),name);}
 // Verbatim old ApiMenuReady expression, before this fix, evaluated against the same objects.
 static bool OldReady()=>Object.FindObjectsOfType<MainMenuPanelView>().Any(view=>view && view.gameObject.activeInHierarchy)
  && Object.FindObjectsOfType<MainMenuActivity>().Any(menu=>menu.m_container!=null && menu.TreeState?.Manager!=null);
 static Fixture Selection(){var f=Fresh();var screen=new LevelSelectScreen{m_levelSelectIsActive=true,ReachedCarousel=true,m_backButton=new(),m_selectedCarouselGroup=new(){levelCarousel=new Component()}};f.view.LevelSelect=screen;screen.transform.Parent=f.view.transform;screen.m_backButton.transform.Parent=f.view.transform;screen.m_selectedCarouselGroup.levelCarousel.transform.Parent=screen.transform;InputNavigationManager._Instance_k__BackingField!.CurrentLayer!.gameObject.transform.Parent=screen.transform;return f;}
 static void RejectSelection(Action<Fixture> mutate,string name){var f=Selection();mutate(f);Assert(!Ready(true),name);}
 static (GameObject root,PanelViewContainer container,PanelView[] panels) DismissedSplash(){
  var root=new GameObject{name="SplashScreenPanel(Clone)"};var container=new PanelViewContainer{gameObject=root};
  var names=new[]{"Splash1","P_LegalScreen","P_LegalScreen_MustAccept","P_LegalScreen_PCP","P_LegalScreen_UA"};
  var panels=names.Select(name=>new PanelView{State=PresentableState.Hidden,gameObject=new(){name=name,activeInHierarchy=false}}).ToArray();
  foreach(var panel in panels){panel.transform.Parent=root.transform;var canvas=new Canvas();canvas.gameObject.activeInHierarchy=false;canvas.transform.Parent=panel.transform;}
  container.m_panels=panels;return(root,container,panels);
 }
 static void Main(){
  var f=Fresh();Assert(Ready(),"fully ready native menu");Assert(NativeMenuReadiness.TryGet(out var owner,out _) && owner==f.menu,"exact owner returned");
  new GameObject{name="SplashScreenPanel"};Assert(OldReady(),"old gate reproduces splash race");Assert(!Ready(),"visible splash blocks launch");
  Reject(_=>new GameObject{name="SplashScreenPanel(Clone)"},"clone splash");
  f=Fresh();new GameObject{name="SplashScreenPanel",activeInHierarchy=false};Assert(Ready(),"inactive splash no longer covers menu");
  Reject(x=>x.view.gameObject.activeInHierarchy=false,"inactive view");Reject(_=>new MainMenuPanelView(),"ambiguous views");
  foreach(var state in new[]{PresentableState.Hidden,PresentableState.Showing,PresentableState.Hiding})Reject(x=>x.view.State=state,"unsettled panel "+state);
  Reject(x=>x.view.IsAnimating=true,"panel animation");Reject(x=>x.view.transitioning=true,"native menu transition");
  Reject(x=>x.view.LevelSelect=new(){m_levelSelectIsActive=true},"level selector active");
  Reject(x=>x.menu.m_container=null,"no DI container");Reject(x=>x.menu.TreeState=null,"no state");
  foreach(var mode in Enum.GetValues<ActivityMode>().Where(m=>m!=ActivityMode.Active)){
   Reject(x=>x.menu.Mode=mode,"inactive activity "+mode);Reject(x=>x.menu.TreeState!.Mode=mode,"inactive state "+mode);
  }
  Reject(x=>x.menu.TreeState!.Manager=null,"no manager");Reject(x=>x.menu.TreeState!.Manager!.m_initialized=false,"uninitialized manager");
  Reject(x=>x.menu.TreeState!.Manager!.Active=new(),"different active state");
  Reject(x=>new MainMenuActivity{TreeState=x.menu.TreeState},"ambiguous active owners");
  Reject(x=>x.binder.m_buttonModel=null,"unbound model");Reject(x=>x.binder.m_disposable=null,"no binding subscription");
  Reject(x=>x.binder.m_button=null,"no bound button");Reject(x=>x.binder.isActiveAndEnabled=false,"disabled binder");
  Reject(x=>x.binder.transform.Parent=null,"foreign global binder");Reject(x=>x.button.transform.Parent=null,"foreign button");
  Reject(x=>x.button.gameObject.activeInHierarchy=false,"hidden button");Reject(x=>x.button.isActiveAndEnabled=false,"disabled button");Reject(x=>x.button.interactable=false,"blocked interaction");
  f=Fresh();f.binder.m_buttonModel=null;var native=new ButtonBinder{m_button=f.button};native.transform.Parent=f.view.transform;Assert(Ready(),"native ButtonBase binder route");
  var snapshot=System.Text.Json.JsonSerializer.Serialize(NativeMenuReadiness.Snapshot());Assert(snapshot.Contains("ButtonBinder:"),"scalar bound button evidence");
  f=Selection();Assert(!Ready(),"startup still rejects level selection");Assert(Ready(true),"explicit settled native level selection");
  f.binder.m_buttonModel=null;Assert(Ready(true),"selection uses exact native back button rather than hidden menu buttons");
  var child=new PanelView{gameObject=f.view.LevelSelect!.m_selectedCarouselGroup!.levelCarousel!.gameObject};Assert(Ready(true),"exact selected carousel panel");
  RejectSelection(x=>x.view.transitioning=true,"selection animator transition");RejectSelection(x=>x.view.State=PresentableState.Showing,"selection panel showing");
  RejectSelection(x=>x.view.LevelSelect!.ReachedCarousel=false,"carousel not reached");RejectSelection(x=>x.view.LevelSelect!.m_tweens![x.view.LevelSelect.m_selectedCarouselGroup!.levelCarousel!.transform]=new(){isAlive=true},"live carousel tween");
  RejectSelection(x=>x.view.LevelSelect!.m_backButton!.interactable=false,"selection back button blocked");RejectSelection(x=>x.view.LevelSelect!.m_backButton!.transform.Parent=null,"foreign selection back button");
  RejectSelection(x=>x.view.LevelSelect!.m_selectedCarouselGroup!.levelCarousel!.gameObject.activeInHierarchy=false,"hidden selected carousel");
  RejectSelection(x=>x.view.LevelSelect!.m_tweens=null,"missing tween state");
  f=Selection();f.view.LevelSelect!.m_tweens![new Transform()]=new(){isAlive=true};Assert(Ready(true),"unrelated decorative tween does not block selection");
  RejectSelection(x=>{var childModal=new PanelView();childModal.transform.Parent=x.view.LevelSelect!.transform;},"unrecognized nested selection modal");
  RejectSelection(_=>new PanelView(),"foreign visible modal");RejectSelection(x=>{var modal=new Il2CppSource.UI.ConfirmPanelView();modal.transform.Parent=x.view.LevelSelect!.transform;},"nested native confirm modal");
  RejectSelection(_=>InputNavigationManager._Instance_k__BackingField!.CurrentLayer!.IgnoreInput1=true,"blocked top input layer");
  RejectSelection(_=>InputNavigationManager._Instance_k__BackingField!.CurrentLayer!.gameObject.transform.Parent=null,"foreign top input layer");
  RejectSelection(_=>InputNavigationManager._Instance_k__BackingField=null,"missing input singleton");
  Reject(_=>new PanelView(),"main menu foreign modal");Reject(_=>InputNavigationManager._Instance_k__BackingField=null,"main menu missing navigation");
  f=Selection();new GameObject{name="SplashScreenPanel"};Assert(!Ready(true),"selection never bypasses splash");
  f=Fresh();var splash=DismissedSplash();Assert(splash.root.activeInHierarchy && Ready(),"native persistent root with all five hidden inactive panels");
  foreach(var state in new[]{PresentableState.Showing,PresentableState.Shown,PresentableState.Hiding}){Fresh();splash=DismissedSplash();splash.panels[0].State=state;Assert(!Ready(),"splash native state "+state);}
  Fresh();splash=DismissedSplash();splash.panels[0].IsAnimating=true;Assert(!Ready(),"animating hidden splash panel");
  Fresh();splash=DismissedSplash();splash.panels[0].gameObject.activeInHierarchy=true;Assert(!Ready(),"hidden splash has not completed native deactivation");
  Fresh();splash=DismissedSplash();splash.panels[0].transform.Parent=null;Assert(!Ready(),"foreign registered splash panel");
  Fresh();splash=DismissedSplash();splash.container.m_panels=Array.Empty<PanelView>();Assert(!Ready(),"empty splash registry");
  Fresh();splash=DismissedSplash();splash.container.m_panels=null;Assert(!Ready(),"missing splash registry");
  Fresh();splash=DismissedSplash();new PanelViewContainer{gameObject=splash.root};Assert(!Ready(),"ambiguous splash containers");
  Fresh();splash=DismissedSplash();splash.container.m_panels=new[]{splash.panels[0],splash.panels[0]};Assert(!Ready(),"duplicate splash panel references");
  Fresh();splash=DismissedSplash();var unownedCanvas=new Canvas();unownedCanvas.transform.Parent=splash.root.transform;Assert(!Ready(),"unbound splash canvas");
  Fresh();splash=DismissedSplash();var alphaOnly=new CanvasGroup{alpha=0};alphaOnly.transform.Parent=splash.root.transform;splash.panels[0].State=PresentableState.Hiding;Assert(!Ready(),"zero alpha never bypasses Hiding state");
  Fresh();splash=DismissedSplash();var firstSnapshot=System.Text.Json.JsonSerializer.Serialize(NativeMenuReadiness.Snapshot());splash.panels[0].State=PresentableState.Hiding;var secondSnapshot=System.Text.Json.JsonSerializer.Serialize(NativeMenuReadiness.Snapshot());Assert(firstSnapshot!=secondSnapshot && secondSnapshot.Contains("Hiding"),"splash state changes remain raw witnesses");
  Assert(secondSnapshot.Contains("managerActive") && secondSnapshot.Contains("boundInteractiveButtons") && secondSnapshot.Contains("visiblePanels") && secondSnapshot.Contains("canvasGroups"),"all readiness groups captured despite blocking splash");
  for(int i=0;i<5;i++){Fresh();splash=DismissedSplash();splash.panels[i].State=PresentableState.Hiding;Assert(!Ready(),"each registered native panel remains guarded "+i);}
  Fresh();splash=DismissedSplash();splash.panels[0].throwOnState=true;Assert(!Ready(),"unknown native splash state fails closed");
  var faultSnapshot=System.Text.Json.JsonSerializer.Serialize(NativeMenuReadiness.Snapshot());Assert(faultSnapshot.Contains("native getter unavailable") && faultSnapshot.Contains("managerActive") && faultSnapshot.Contains("canvasRenderers"),"per-field getter failure preserves unrelated raw signals");
  Console.WriteLine($"PASS {checks} actual NativeMenuReadiness assertions");
 }
}
