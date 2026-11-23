using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Diz.Resources;
using Comfort.Common;
using EFT;

using EFT.InventoryLogic;
using UnityEngine;

namespace Hawks.Arges {
public sealed partial class SpinUp {
 // M135 model space. Centre of the forward circular end cap of the offset motor.
 static readonly Vector3 MotorFace = new Vector3(-.252f,-.20025f,.10125f);
 Material beamOnly;
 Player.FirearmController laserController;
 Transform laserBody, laserRoot;
 LaserBeam nativeBeam, beamPrefab;
 Action releaseLaserAsset;
 bool loadingBeam;
 float retryAt;

 const string LaserKey="assets/content/items/mods/tactical/tactical_tt_dlp_tactical_precision_laser_sight.bundle";
 sealed class SingleMode : ILightComponentTemplate {public int ModesCount=>1;}
 static readonly SingleMode mode=new SingleMode();
 static readonly ConditionalWeakTable<Weapon,LightComponent> lights=new ConditionalWeakTable<Weapon,LightComponent>();
 static LightComponent LightFor(Weapon weapon)=>lights.GetValue(weapon,w=>new LightComponent(w,mode));

 static void BeginToggle(Player.FirearmController __instance,ref bool animated,out (bool active,bool animated) __state){
  __state=default;
  if(__instance.Item is Weapon w && w.TemplateId==WeaponId){
   __state=(LightFor(w).IsActive,animated);
   animated=false;
  }
 }
 static void EndToggle(Player.FirearmController __instance,(bool active,bool animated) __state,bool __result){
  if(!__result || !(__instance.Item is Weapon w) || w.TemplateId!=WeaponId || LightFor(w).IsActive==__state.active)return;
  var player=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance.MainPlayer:null;
  if(player && player.HandsController==__instance && __state.animated)player.PlayTacticalSound();
  if(player && player.HandsController==__instance)
   player.ProceduralWeaponAnimation?.HandsContainer?.HandsRotation?.AddAcceleration(new Vector3(.6f,-.2f,.25f));
 }
 void RegisterLaserCommands(){
  patch.Patch(AccessTools.Method(typeof(Player.FirearmController),"SetLightsState"),prefix:new HarmonyMethod(typeof(SpinUp),nameof(BeginToggle)),postfix:new HarmonyMethod(typeof(SpinUp),nameof(EndToggle)));
  patch.Patch(AccessTools.Method(typeof(FirearmHandsInputTranslator),"GetCurrentLightStates"),postfix:new HarmonyMethod(typeof(SpinUp),nameof(IncludeLaserState)));
  patch.Patch(AccessTools.Method(typeof(Player.FirearmController),"GetCurrentLightStatus"),postfix:new HarmonyMethod(typeof(SpinUp),nameof(IncludeLaserComponent)));
  patch.Patch(AccessTools.Method(typeof(Player.FirearmController),"GetAllLightMods"),postfix:new HarmonyMethod(typeof(SpinUp),nameof(IncludeLaserMod)));
 }
 static void IncludeLaserState(FirearmHandsInputTranslator __instance,ref LightsState[] __result){
  var w=__instance._controller?.Item;
  if(w?.TemplateId!=WeaponId)return;
  Array.Resize(ref __result,__result.Length+1);
  __result[__result.Length-1]=LightFor(w).GetLightState();
 }
 static void IncludeLaserComponent(Player.FirearmController __instance,Dictionary<string,LightComponent> __result){
  if(__instance.Item is Weapon w && w.TemplateId==WeaponId)__result[w.Id]=LightFor(w);
 }
 static void IncludeLaserMod(Player.FirearmController __instance,List<LightComponent> __result){
  if(__instance.Item is Weapon w && w.TemplateId==WeaponId)__result.Add(LightFor(w));
 }
 async void LoadNativeBeam(){
  if(loadingBeam || Time.unscaledTime<retryAt || !Singleton<IEasyAssets>.Instantiated)return;
  loadingBeam=true;
  try{
   var assets=Singleton<IEasyAssets>.Instance;
   var token=assets.Retain(new[]{LaserKey});releaseLaserAsset=()=>token.Release();
   await EasyAssetsExtensions.LoadBundles(token);
   if(!this)return;
   var device=assets.GetAsset<GameObject>(LaserKey);
   beamPrefab=device.GetComponentsInChildren<LaserBeam>(true).Single(b=>b.transform.parent.name=="VIS_000");
  }catch(Exception e){
   releaseLaserAsset?.Invoke();releaseLaserAsset=null;retryAt=Time.unscaledTime+10;
   if(this)Logger.LogError(e);
  }finally{loadingBeam=false;}
 }
 void ClearLaser(){
  if(laserRoot){laserRoot.gameObject.SetActive(false);Destroy(laserRoot.gameObject);}
  laserRoot=null;laserBody=null;laserController=null;nativeBeam=null;
  if(beamOnly)Destroy(beamOnly);beamOnly=null;
 }
 bool BindLaser(Player player,Player.FirearmController controller){
  var rig=controller.HandsHierarchy?.Self;
  if(!rig)return false;
  if(laserController==controller && laserRoot && laserBody && laserBody.IsChildOf(rig))return true;
  if(!beamPrefab){LoadNativeBeam();return false;}
  ClearLaser();
  laserBody=rig.GetComponentsInChildren<Transform>().FirstOrDefault(node=>node.name=="M135");
  if(!laserBody)return false;
  laserRoot=Instantiate(beamPrefab.transform.parent,laserBody,false);
  nativeBeam=laserRoot.GetComponentInChildren<LaserBeam>(true);
  // Only the beam draw call uses this material; native point and surface light keep donor settings.
  beamOnly=new Material(nativeBeam.BeamMaterial);
  beamOnly.SetFloat("_Factor",beamOnly.GetFloat("_Factor")*4f);
  nativeBeam.BeamMaterial=beamOnly;
  nativeBeam.SetOwner(true);
  var axis=laserBody.TransformVector(Vector3.left).normalized;
  laserRoot.rotation=Quaternion.FromToRotation(nativeBeam.transform.forward,axis)*laserRoot.rotation;
  laserRoot.position+=laserBody.TransformPoint(MotorFace)-nativeBeam.transform.position;
  laserController=controller;
  return true;
 }
 void UpdateLaser(){
  var player=Singleton<GameWorld>.Instantiated?Singleton<GameWorld>.Instance.MainPlayer:null;
  if(!player || !(player.HandsController is Player.FirearmController c) ||
     !(c.Item is Weapon w) || w.TemplateId!=WeaponId){
   if(laserRoot)ClearLaser();
   return;
  }
  if(!LightFor(w).IsActive){
   if(laserRoot)laserRoot.gameObject.SetActive(false);
   return;
  }
  if(!BindLaser(player,c))return;
  laserRoot.gameObject.SetActive(!player.IsInventoryOpened && laserBody.gameObject.activeInHierarchy);
 }
}}