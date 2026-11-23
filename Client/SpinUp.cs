using System.Collections.Generic;
using System.Linq;
using BepInEx;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace Hawks.Arges {
[BepInPlugin("team.aminus.arges.client","M135 Arges spin-up","1.0.4")]
public sealed partial class SpinUp : BaseUnityPlugin {
 const string WeaponId="686a13500000000000000001";
 // The source start clip lasts 29 frames at 30 fps.
 const float ReadyTime=29f/30f;
 static SpinUp instance;
 readonly Dictionary<Player.FirearmController,Gate> gates=new Dictionary<Player.FirearmController,Gate>();
 sealed class Gate { public Player Owner; public string Item; public bool Held,PreSpin,ReleasedToGame; public float Charge,MotorStart; public Transform Rotor; public AudioSource Motor; public AudioClip[] Clicks; }
  float[] inspectionAngles;
 sealed class Look {public Player Owner;public Gate Gate;public float Wait;public bool Started;public Quaternion Base;}
 readonly Dictionary<Player.FirearmController,Look> looks=new Dictionary<Player.FirearmController,Look>();
  Harmony patch;
 static readonly HashSet<int> refreshed=new HashSet<int>();
 static void IconRequest(EFT.InventoryLogic.Item item,ref bool forcedGeneration){
  if((item.TemplateId==WeaponId || item.TemplateId=="686a13500000000000000002") && refreshed.Add(IconsHash.GetItemHash(item)))forcedGeneration=true;
 }

 static void HidePreviewTool(GameObject root){
  if(!root || !root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="M135"))return;
  var tone=root.GetComponent<ArgesPreviewRestore>() ?? root.AddComponent<ArgesPreviewRestore>();
  tone.ApplyTone(root);
  var meshes=root.GetComponentsInChildren<MeshRenderer>(true);
  foreach(var mesh in meshes){
   if(mesh.name!="EquipTool" || !mesh.enabled || !mesh.transform.parent || mesh.transform.parent.name!="weapon_bolt")continue;
   if(!root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="M135"))return;
   var restore=root.GetComponent<ArgesPreviewRestore>() ?? root.AddComponent<ArgesPreviewRestore>();
   restore.Hidden.Add(mesh);mesh.enabled=false;
  }
 }
 static void HideIconTool(GameObject model){HidePreviewTool(model);}

 static void FitArgesIcon(GameObject model, Camera camera,float cameraAspect){
  if(!model || !camera || !camera.orthographic)return;
  var nodes=model.GetComponentsInChildren<Transform>();
  var body=nodes.FirstOrDefault(t=>t.name=="M135");
  if(!body)return;
  // Align the visible mesh, including the live rig rotation, before measuring it.
  Quaternion target=camera.transform.rotation*Quaternion.Euler(6,-16,0);
  model.transform.rotation=target*Quaternion.Inverse(body.rotation)*model.transform.rotation;
  var meshes=model.GetComponentsInChildren<MeshRenderer>().Where(m=>m.enabled && m.name!="EquipTool").ToArray();
  if(meshes.Length==0)return;
  var lo=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var hi=-lo;
  foreach(var m in meshes){var box=m.localBounds;for(int i=0;i<8;i++){
   var sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
   Vector2 v=camera.transform.InverseTransformPoint(m.transform.TransformPoint(box.center+Vector3.Scale(box.extents,sign)));
   lo=Vector2.Min(lo,v);hi=Vector2.Max(hi,v);
  }}
  var center=(lo+hi)*.5f;
  model.transform.position-=camera.transform.right*center.x+camera.transform.up*center.y;
  camera.orthographicSize=Mathf.Max((hi.y-lo.y)*.5f,(hi.x-lo.x)*.5f/Mathf.Max(.01f,cameraAspect))*1.08f;
 }
 static bool PackRounds(EFT.InventoryLogic.StackSlot __instance,bool simulate,ref Diz.LanguageExtensions.OperationResult<EFT.InventoryLogic.ContainerAddResult> __result){
  if(!simulate || !MountedFeed(__instance.ParentItem))return true;
  __result=new EFT.InventoryLogic.NotRaidModdableError(__instance.ParentItem);return false;
 }
 static void PositionPreview(GameObject itemGameObject,ref Vector3? initialRotation){
  if(!itemGameObject)return;
  HidePreviewTool(itemGameObject);
  if(itemGameObject.name.StartsWith("mag_m135_arges",System.StringComparison.OrdinalIgnoreCase)){
   var magazinePivot=itemGameObject.GetComponent<PreviewPivot>();
   if(magazinePivot)initialRotation=(Quaternion.Euler(-3,9,0)*magazinePivot.Icon.rotation).eulerAngles;
   return;
  }
  if(!itemGameObject.GetComponent<WeaponPrefab>())return;
  var model=itemGameObject.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="M135" && t.gameObject.activeInHierarchy);
  var pivot=itemGameObject.GetComponent<PreviewPivot>();if(!model||!pivot)return;
  var meshes=itemGameObject.GetComponentsInChildren<MeshRenderer>().Where(m=>m.enabled && m.name!="EquipTool").ToArray();
  if(meshes.Length==0)return;var bounds=meshes[0].bounds;foreach(var mesh in meshes)bounds.Encapsulate(mesh.bounds);
  pivot.pivotPosition=itemGameObject.transform.InverseTransformPoint(bounds.center);pivot.scale=Vector3.one*(meshes.Any(m=>m.name=="Feed box decals")?.60f:1f);
  initialRotation=(Quaternion.Euler(6,-16,0)*Quaternion.Inverse(model.rotation)*itemGameObject.transform.rotation).eulerAngles;
 }
 static bool CameraFit(Camera camera,Bounds bounds,string itemTemplateId){
  if(itemTemplateId!=WeaponId||!camera)return true;
  float tan=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);float distance=.45f;
  for(int i=0;i<8;i++){var corner=Quaternion.Inverse(camera.transform.rotation)*Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
   distance=Mathf.Max(distance,1.12f*Mathf.Abs(corner.y)/tan-corner.z,1.12f*Mathf.Abs(corner.x)/(tan*Mathf.Max(.1f,camera.aspect))-corner.z);}
  camera.transform.position=bounds.center-camera.transform.forward*distance;return false;
 }
 static void Examine(Player.FirearmController __instance,bool __result){
  if(!__result||__instance.Item?.TemplateId!=WeaponId)return;
  var owner=Traverse.Create(__instance).Field("_player").GetValue<Player>();
  var root=__instance.HandsHierarchy?.Self;
  if(!owner||!root)return;
  if(instance.gates.TryGetValue(__instance,out var firing)){firing.Held=false;firing.ReleasedToGame=false;if(firing.Motor)firing.Motor.Stop();instance.gates.Remove(__instance);}
  var rotor=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="ArgesRotor");
  instance.looks[__instance]=new Look{Owner=owner,Gate=new Gate{Rotor=rotor},Wait=Time.unscaledTime+1,Base=rotor?rotor.localRotation:Quaternion.identity};
 }
 const int DrawFrames=57;
 static readonly int DrawState=Animator.StringToHash("OUT TO IDLE ARM");
 readonly Vector3[] drawPositions=new Vector3[DrawFrames];
 readonly Quaternion[] drawRotations=new Quaternion[DrawFrames];
 readonly float[] drawScales=new float[DrawFrames];
 MeshRenderer originalTool;
 Transform visibleTool;
 bool originalSuppressed;

 void ReadDrawTrack(){
  using(var stream=typeof(SpinUp).Assembly.GetManifestResourceStream("Arges.EquipToolTrack"))
  using(var reader=new System.IO.BinaryReader(stream)){
   for(int i=0;i<DrawFrames;i++){
    drawPositions[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
    drawRotations[i]=new Quaternion(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
    drawScales[i]=reader.ReadSingle();
   }
  }
 }
 void ClearDrawTool(){
  if(visibleTool){visibleTool.gameObject.SetActive(false);Destroy(visibleTool.gameObject);}
  if(originalTool)originalTool.forceRenderingOff=originalSuppressed;
  visibleTool=null;originalTool=null;
 }
 void UpdateDrawTool(){
  var player=Comfort.Common.Singleton<GameWorld>.Instantiated?Comfort.Common.Singleton<GameWorld>.Instance.MainPlayer:null;
  if(!player || player.PointOfView!=EPointOfView.FirstPerson ||
     !(player.HandsController is Player.FirearmController controller) || controller.Item?.TemplateId!=WeaponId){
   if(visibleTool || originalTool)ClearDrawTool();
   return;
  }
  var animator=player.HandsAnimator?.Animator;
  int layer=animator?.GetLayerIndex("Hands")??-1;
  float time=-1;
  if(layer>=0){
   var state=animator.GetCurrentAnimatorStateInfo(layer);
   if(animator.IsInTransition(layer)){
    var next=animator.GetNextAnimatorStateInfo(layer);
    if(next.shortNameHash==DrawState)state=next;
   }
   if(state.shortNameHash==DrawState)time=state.normalizedTime;
  }
  if(time<0){if(visibleTool || originalTool)ClearDrawTool();return;}
  var root=controller.HandsHierarchy?.Self;
  if(!root)return;
  if(visibleTool && !visibleTool.IsChildOf(root.transform))ClearDrawTool();
  if(!visibleTool){
   originalTool=root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name=="EquipTool" && r.transform.parent?.name=="weapon_bolt");
   if(!originalTool)return;
   var gun=originalTool.transform.parent.parent;
   visibleTool=Instantiate(originalTool.gameObject,gun,false).transform;
   visibleTool.name="Arges draw wrench";
   var renderer=visibleTool.GetComponent<MeshRenderer>();
   renderer.enabled=true;renderer.forceRenderingOff=false;
   // Preview pooling can leave the original on the icon layer.
   var body=gun.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(m=>m.name=="Body_Standard");
   if(body)visibleTool.gameObject.layer=body.gameObject.layer;
   originalSuppressed=originalTool.forceRenderingOff;originalTool.forceRenderingOff=true;
  }
  float frame=Mathf.Clamp01(time)*(DrawFrames-1);
  int a=(int)frame,b=Mathf.Min(a+1,DrawFrames-1);
  visibleTool.localPosition=Vector3.Lerp(drawPositions[a],drawPositions[b],frame-a);
  visibleTool.localRotation=Quaternion.Slerp(drawRotations[a],drawRotations[b],frame-a);
  visibleTool.localScale=Vector3.one;
  visibleTool.gameObject.SetActive(drawScales[a]>.5f);
 }
 void LateUpdate(){
  UpdateGripPort();
  UpdateDrawTool();
  UpdateLaser();
  foreach(var pair in looks.ToArray()){
   var l=pair.Value;var c=pair.Key;
   if(!l.Owner||l.Owner.HandsController!=c){looks.Remove(c);continue;}
   var anim=l.Owner.HandsAnimator?.Animator;var layer=anim==null?-1:anim.GetLayerIndex("Hands");
   if(layer>=0){
    var state=anim.GetCurrentAnimatorStateInfo(layer);
    if(state.shortNameHash==Animator.StringToHash("LOOK")){
     l.Started=true;var pos=Mathf.Clamp01(state.normalizedTime)*(inspectionAngles.Length-1);
     int n=(int)pos;float angle=Mathf.Lerp(inspectionAngles[n],inspectionAngles[Mathf.Min(n+1,inspectionAngles.Length-1)],pos-n);
     if(l.Gate.Rotor)l.Gate.Rotor.localRotation=l.Base*Quaternion.AngleAxis(-angle,Vector3.up);
     continue;
    }
   }
   if(l.Started||Time.unscaledTime>=l.Wait)looks.Remove(c);
  }
 }
 static IEnumerable<CodeInstruction> FeedStats(IEnumerable<CodeInstruction> instructions){
  var field=AccessTools.Field(typeof(EFT.InventoryLogic.Item),"Attributes");
  foreach(var op in instructions){
   if(op.LoadsField(field)){op.opcode=System.Reflection.Emit.OpCodes.Call;op.operand=AccessTools.Method(typeof(SpinUp),nameof(VisibleStats));}
   yield return op;
  }
 }
 static List<EFT.InventoryLogic.ItemAttribute> VisibleStats(EFT.InventoryLogic.Item item){
  if(item.TemplateId!="686a13500000000000000002")return item.Attributes;
  return item.Attributes.Where(a=>a.Id is EFT.InventoryLogic.EItemAttributeId id &&
   (id==EFT.InventoryLogic.EItemAttributeId.Size||id==EFT.InventoryLogic.EItemAttributeId.ContainerSize||
    id==EFT.InventoryLogic.EItemAttributeId.RaidModdable||id==EFT.InventoryLogic.EItemAttributeId.Ergonomics||
    id==EFT.InventoryLogic.EItemAttributeId.MaxCount||id==EFT.InventoryLogic.EItemAttributeId.MalfFeedChance||
    id==EFT.InventoryLogic.EItemAttributeId.CompatibleWith)).ToList();
 }
 void Awake(){
 inspectionAngles=System.IO.File.ReadAllLines(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location),"inspect-rotor.txt")).Select(x=>float.Parse(x,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
 ReadDrawTrack();instance=this;patch=new Harmony(Info.Metadata.GUID);
 patch.Patch(AccessTools.Method(typeof(EFT.UI.ItemSpecificationPanel),"RecreateAttributeBars"),transpiler:new HarmonyMethod(typeof(SpinUp),nameof(FeedStats)));
 patch.Patch(AccessTools.Method(typeof(EFT.InventoryLogic.StackSlot),"Add",new[]{typeof(EFT.InventoryLogic.Item),typeof(bool)}),prefix:new HarmonyMethod(typeof(SpinUp),nameof(PackRounds)));
 patch.Patch(AccessTools.Method(typeof(ItemIconCreator),"PoseModelByPivot"),prefix:new HarmonyMethod(typeof(SpinUp),nameof(HideIconTool)),postfix:new HarmonyMethod(typeof(SpinUp),nameof(FitArgesIcon)));
 RegisterLaserCommands();
 patch.Patch(AccessTools.Method(typeof(ItemIconCreator),"GetItemIcon"),prefix:new HarmonyMethod(typeof(SpinUp),nameof(IconRequest)));
 patch.Patch(AccessTools.Method(typeof(EFT.UI.WeaponModding.WeaponPreview),"PositionGameObject"),prefix:new HarmonyMethod(typeof(SpinUp),nameof(PositionPreview)));
 patch.Patch(AccessTools.Method(typeof(RendererExtensions),"PlaceInClosestPosition"),prefix:new HarmonyMethod(typeof(SpinUp),nameof(CameraFit)));
 patch.Patch(AccessTools.Method(typeof(Player.FirearmController),"SetTriggerPressed",new[]{typeof(bool)}),
 prefix:new HarmonyMethod(typeof(SpinUp),nameof(Filter)));
 patch.Patch(AccessTools.Method(typeof(EFT.InventoryLogic.Slot),"Add",new[]{typeof(EFT.InventoryLogic.Item),typeof(bool),typeof(bool)}),prefix:new HarmonyMethod(typeof(SpinUp),nameof(InsertFeed)));
 patch.Patch(AccessTools.Method(typeof(EFT.InventoryLogic.Slot),"RemoveItem",new[]{typeof(bool)}),prefix:new HarmonyMethod(typeof(SpinUp),nameof(RemoveFeed)));
 patch.Patch(AccessTools.Method(typeof(EFT.InventoryLogic.ItemController),"LoadMagazine",new[]{typeof(EFT.InventoryLogic.Ammo),typeof(EFT.InventoryLogic.Magazine),typeof(int),typeof(bool)}),prefix:new HarmonyMethod(typeof(SpinUp),nameof(LoadFeed)));
 patch.Patch(AccessTools.Method(typeof(Player.FirearmController),"UpdateHipInaccuracy"),postfix:new HarmonyMethod(typeof(SpinUp),nameof(SteadyHipFire)));
 patch.Patch(AccessTools.Method(typeof(Player.FirearmController),"InitiateShot"),prefix:new HarmonyMethod(typeof(SpinUp),nameof(ArgesShot)));
 patch.Patch(AccessTools.Method(typeof(Stamina),"Process"),prefix:new HarmonyMethod(typeof(SpinUp),nameof(SlowRecovery)));
 patch.Patch(AccessTools.Method(typeof(Player.FirearmController),"Shot"),transpiler:new HarmonyMethod(typeof(SpinUp),nameof(RepeatableSpread)));
 patch.Patch(AccessTools.Method(typeof(Player),"GetCurveValue",new[]{typeof(int)}),postfix:new HarmonyMethod(typeof(SpinUp),nameof(ThirdPersonArms)));
 patch.Patch(AccessTools.Method(typeof(EFT.UI.PlayerModelLoader),"CreateWeapon"),prefix:new HarmonyMethod(typeof(SpinUp),nameof(InventoryPortraitStart)),postfix:new HarmonyMethod(typeof(SpinUp),nameof(InventoryPortraitReady)));
 foreach(var command in new[]{"CheckAmmo","CheckChamber","CanStartReload"})
  patch.Patch(AccessTools.Method(typeof(Player.FirearmController),command,System.Type.EmptyTypes),prefix:new HarmonyMethod(typeof(SpinUp),nameof(AllowCheck)));
 patch.Patch(AccessTools.Method(typeof(Player.FirearmController),"SetAim",new[]{typeof(bool)}),prefix:new HarmonyMethod(typeof(SpinUp),nameof(Aim)));
 patch.Patch(AccessTools.Method(typeof(Player.FirearmController),"ExamineWeapon",System.Type.EmptyTypes),postfix:new HarmonyMethod(typeof(SpinUp),nameof(Examine)));
 }
 static bool Filter(Player.FirearmController __instance,bool __0){
 if(__instance.Item?.TemplateId!=WeaponId)return true;
 if(__0 && instance.looks.ContainsKey(__instance))return false;
 var gates=instance.gates;
 if(!__0){if(gates.TryGetValue(__instance,out var old)){old.Held=false;old.ReleasedToGame=false;}return true;}
 var g=PrepareSpin(__instance);
 if(g==null)return false;
 g.Held=true;
 return g.ReleasedToGame;
 }

 static bool MountedWeapon(EFT.InventoryLogic.Item item){
  return item?.TemplateId==WeaponId && item.Parent?.Container is EFT.InventoryLogic.Slot slot && slot.ParentItem is EFT.InventoryLogic.InventoryEquipment;
 }
 static bool MountedFeed(EFT.InventoryLogic.Item item){
  return item?.TemplateId=="686a13500000000000000002" && MountedWeapon(item.Parent?.Container?.ParentItem);
 }
 static bool InsertFeed(EFT.InventoryLogic.Slot __instance,bool simulate, ref Diz.LanguageExtensions.OperationResult<EFT.InventoryLogic.ContainerAddResult> __result){
  if(!simulate || __instance.ID!="mod_magazine" || !MountedWeapon(__instance.ParentItem))return true;
  __result=new EFT.InventoryLogic.NotRaidModdableError(__instance.ParentItem);return false;
 }
 static bool RemoveFeed(EFT.InventoryLogic.Slot __instance,bool simulate, ref Diz.LanguageExtensions.OperationResult<EFT.InventoryLogic.ContainerRemoveResult> __result){
  if(!simulate || __instance.ID!="mod_magazine" || !MountedWeapon(__instance.ParentItem))return true;
  __result=new EFT.InventoryLogic.NotRaidModdableError(__instance.ParentItem);return false;
 }
 static bool LoadFeed(EFT.InventoryLogic.Magazine __1,ref System.Threading.Tasks.Task<Comfort.Common.IResult> __result){
  if(!MountedFeed(__1))return true;
  __result=System.Threading.Tasks.Task.FromResult<Comfort.Common.IResult>(new Comfort.Common.FailedResult("Cant StartReload",0));return false;
 }

 static void SteadyHipFire(Player.FirearmController __instance){
  if(__instance.Item?.TemplateId!=WeaponId)return;
  var player=Traverse.Create(__instance).Field("_player").GetValue<Player>();
  if(player==null || player.IsAI || player.ProceduralWeaponAnimation?.Breath==null)return;
  __instance.HipInaccuracy*=.65f;
  player.ProceduralWeaponAnimation.Breath.HipPenalty=__instance.HipInaccuracy;
 }

 static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Player.FirearmController,Burst> bursts=new System.Runtime.CompilerServices.ConditionalWeakTable<Player.FirearmController,Burst>();
 sealed class Burst {public int Index;public float Last=-10;}
 static void ArgesShot(Player.FirearmController __instance){
  if(__instance.Item?.TemplateId!=WeaponId)return;
  var owner=Traverse.Create(__instance).Field("_player").GetValue<Player>();
  if(!owner || !owner.IsYourPlayer)return;
  var stamina=owner.Physical.HandsStamina;
  stamina.Current=Mathf.Max(0,stamina.Current-(float)stamina.TotalCapacity*.01f);
  stamina.DisableRestoration=Mathf.Max(stamina.DisableRestoration,Time.time+3f);
  instance.recovering=stamina;instance.recoveryUntil=Time.time+18f;

 }
 static Vector3 SpreadSample(Player.FirearmController controller){
  if(controller.Item?.TemplateId!=WeaponId)return UnityEngine.Random.insideUnitSphere;
  var burst=bursts.GetOrCreateValue(controller);
  if(Time.time-burst.Last>.18f)burst.Index=0;
  burst.Last=Time.time;
  int n=burst.Index++%64;
  return SpreadAt(controller,n);
 }
 static Vector3 SpreadAt(Player.FirearmController controller,int n){
  float angle=n*2.39996323f;
  float radius=Mathf.Sqrt(((n*37)%64+.5f)/64f);
  var forward=controller.WeaponDirection.normalized;
  var right=Vector3.Cross(Vector3.up,forward).normalized;
  return radius*(right*Mathf.Cos(angle)+Vector3.Cross(forward,right)*Mathf.Sin(angle));
 }
 static IEnumerable<CodeInstruction> RepeatableSpread(IEnumerable<CodeInstruction> instructions){
  var getter=AccessTools.PropertyGetter(typeof(UnityEngine.Random),"insideUnitSphere");
  foreach(var op in instructions){
   if(!op.Calls(getter)){yield return op;continue;}
   var load=new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0);load.labels.AddRange(op.labels);
   yield return load;yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call,AccessTools.Method(typeof(SpinUp),nameof(SpreadSample)));
  }
 }
 Stamina recovering;float recoveryUntil;
 static void SlowRecovery(Stamina __instance,float dt){
  if(instance.recovering!=__instance || Time.time>=instance.recoveryUntil || Time.time<=__instance.DisableRestoration || __instance.Current>=(float)__instance.TotalCapacity)return;
  float gain=(float)__instance.SelfRestoration*__instance.Multiplier*dt;
  __instance.Current-=Mathf.Min(__instance.Current,Mathf.Max(0,gain*.5f));
 }


 static void InventoryPortraitStart(EFT.InventoryLogic.Item weapon,ref bool animateWeapon,out bool __state){
  __state=weapon?.TemplateId==WeaponId && Comfort.Common.Singleton<GameWorld>.Instantiated && Comfort.Common.Singleton<GameWorld>.Instance.MainPlayer?.IsInventoryOpened==true;
  if(__state)animateWeapon=false;
 }
 static void InventoryPortraitReady(EFT.UI.PlayerModelLoader __instance,bool __state){
  if(!__state || __instance._weaponAnimator==null)return;
  var animator=__instance._weaponAnimator;
  animator.SetBool("Inventory",true);
  animator.Play("OPEN INVENTORY",animator.GetLayerIndex("Hands"),0f);
 }
 static void ThirdPersonArms(Player __instance,int hash,ref float __result){
  if(hash!=PlayerAnimator.WEAPON_ROOT_3RD && hash!=PlayerAnimator.LEFT_HAND_WEIGHT && hash!=PlayerAnimator.RIGHT_HAND_WEIGHT)return;
  if(__instance.PointOfView!=EPointOfView.ThirdPerson || !(__instance.HandsController is Player.FirearmController c) || c.Item?.TemplateId!=WeaponId)return;
  if(__instance.MovementContext?.StationaryWeapon!=null || __instance.LeftHandInteractionTarget!=null)return;
  // EFT uses (1 - hand weight) to blend onto the animated weapon hand markers.
  __result=0f;
 }
 static bool AllowCheck(Player.FirearmController __instance){
  return __instance.Item?.TemplateId!=WeaponId;
 }
 static bool Aim(Player.FirearmController __instance,bool __0){
  if(__instance.Item?.TemplateId!=WeaponId)return true;
  if(!__0){
   if(instance.gates.TryGetValue(__instance,out var existing))existing.PreSpin=false;
  }else if(Input.GetMouseButton(1)){
   var g=PrepareSpin(__instance);
   if(g!=null)g.PreSpin=true;
  }
  return false;
 }
 static Gate PrepareSpin(Player.FirearmController __instance){
 if(instance.looks.ContainsKey(__instance)||__instance.IsInReloadOperation()||__instance.IsInRemoveOperation()||__instance.IsInSpawnOperation())return null;
 var gates=instance.gates;
 if(!gates.TryGetValue(__instance,out var g)||g.Item!=__instance.Item.Id){
 var owner=Traverse.Create(__instance).Field("_player").GetValue<Player>();
 if(!owner)return null;
 var root=__instance.HandsHierarchy?.Self;
 var rotor=root?root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="ArgesRotor"):null;
 var bank=root?root.GetComponentInParent<WeaponSoundPlayer>():null;
 gates[__instance]=g=new Gate{Owner=owner,Item=__instance.Item.Id,Rotor=rotor,Motor=rotor?rotor.GetComponent<AudioSource>():null,Clicks=bank?.AdditionalSounds.FirstOrDefault(s=>s.EventName=="ArgesSpinStart")?.SoundClips};}

 if(!g.Held&&!g.PreSpin&&g.Motor&&!g.Motor.isPlaying){
  g.MotorStart=Time.unscaledTime;g.Motor.volume=0;g.Motor.pitch=.55f;
  g.Motor.Play();
  if(g.Clicks!=null&&g.Clicks.Length>0)g.Motor.PlayOneShot(g.Clicks[UnityEngine.Random.Range(0,g.Clicks.Length)]);
 }
 return g;
 }
 void Update(){
 foreach(var pair in gates.ToArray()){
 var c=pair.Key;var g=pair.Value;
 if(!g.Owner||g.Owner.HandsController!=c||c.Item?.Id!=g.Item){if(g.Motor)g.Motor.Stop();gates.Remove(c);continue;}
 if(c.IsInReloadOperation()||c.IsInRemoveOperation()){g.Held=false;g.PreSpin=false;g.ReleasedToGame=false;}
 if(g.PreSpin&&(!Application.isFocused||!Input.GetMouseButton(1)))g.PreSpin=false;
 bool charging=g.Held||g.PreSpin;
 g.Charge=Mathf.MoveTowards(g.Charge,charging?1:0,Time.deltaTime*(charging?1:2)/ReadyTime);
 if(g.Held && g.ReleasedToGame && c.Item is EFT.InventoryLogic.Weapon empty && empty.ChamberAmmoCount==0 && empty.GetCurrentMagazineCount()==0){
   c.SetTriggerPressed(false);g.Held=true;g.ReleasedToGame=false;
 }
 if(g.Rotor)g.Rotor.Rotate(Vector3.down,1660f*g.Charge*Time.deltaTime,Space.Self);
 if(g.Motor){
  g.Motor.volume=.65f*Mathf.SmoothStep(0,1,(Time.unscaledTime-g.MotorStart)/.08f);
  g.Motor.pitch=Mathf.Lerp(.55f,1f,g.Charge);
 }
 if(g.Held&&!g.ReleasedToGame&&g.Charge>=1 && c.Item is EFT.InventoryLogic.Weapon w && (w.ChamberAmmoCount>0 || w.GetCurrentMagazineCount()>0)){g.ReleasedToGame=true;c.SetTriggerPressed(true);}
 if(!charging&&g.Charge<=0){if(g.Motor)g.Motor.Stop();gates.Remove(c);}
 }}
 void OnDestroy(){ClearLaser();releaseLaserAsset?.Invoke();releaseLaserAsset=null;ClearDrawTool();patch?.UnpatchSelf();gates.Clear();instance=null;}
}}

namespace Hawks.Arges {
 public sealed class ArgesPreviewRestore : MonoBehaviour {
  internal readonly List<MeshRenderer> Hidden=new List<MeshRenderer>();
  readonly Dictionary<MeshRenderer,Material[]> originals=new Dictionary<MeshRenderer,Material[]>();
  readonly Dictionary<Material,Material> copies=new Dictionary<Material,Material>();
  internal void ApplyTone(GameObject root){
   foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)){
    if(originals.ContainsKey(renderer) || renderer.name=="EquipTool")continue;
    var materials=renderer.sharedMaterials;
    var toned=(Material[])materials.Clone();bool changed=false;
    for(int i=0;i<materials.Length;i++){
     var material=materials[i];if(!material || !material.HasProperty("_Color"))continue;
     if(!copies.TryGetValue(material,out var copy)){
      copy=new Material(material);var color=material.color;copy.color=new Color(color.r*.78f,color.g*.78f,color.b*.78f,color.a);
      if(copy.HasProperty("_Glossiness"))copy.SetFloat("_Glossiness",material.GetFloat("_Glossiness")*.7f);
      copies.Add(material,copy);
     }
     toned[i]=copy;changed=true;
    }
    if(changed){originals.Add(renderer,materials);renderer.sharedMaterials=toned;}
   }
  }
  void OnDisable(){foreach(var entry in originals)if(entry.Key)entry.Key.sharedMaterials=entry.Value;originals.Clear();
   foreach(var copy in copies.Values)if(copy)Destroy(copy);copies.Clear();
   foreach(var mesh in Hidden)if(mesh)mesh.enabled=true;Hidden.Clear();}
 }
}