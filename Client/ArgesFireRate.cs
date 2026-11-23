using BepInEx.Configuration;
using EFT.InventoryLogic;
using HarmonyLib;

namespace Hawks.Arges {
public sealed partial class SpinUp {
 ConfigEntry<int> configuredFireRate;
 WeaponTemplate argesTemplate;

 void RegisterFireRate(){
  configuredFireRate=Config.Bind("Weapon","Fire rate (RPM)",1337,
   new ConfigDescription("Automatic fire rate for the M135 Arges. Takes effect on the next burst.",
    new AcceptableValueRange<int>(60,6000)));
  configuredFireRate.SettingChanged+=(sender,args)=>ApplyFireRate();
  var constructor=AccessTools.Constructor(typeof(Weapon),new[]{typeof(string),typeof(WeaponTemplate)});
  patch.Patch(constructor,postfix:new HarmonyMethod(typeof(SpinUp),nameof(OnWeaponCreated)));
 }

 static void OnWeaponCreated(Weapon __instance){
  if(__instance.TemplateId!=WeaponId)return;
  instance.argesTemplate=__instance.GetTemplate<WeaponTemplate>();
  instance.ApplyFireRate();
 }

 void ApplyFireRate(){
  if(argesTemplate!=null)argesTemplate.bFirerate=configuredFireRate.Value;
 }
}}