using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using EFT;
public static class ArgesPd2Fire {
 const string dir="Assets/HawksArgesNative/";
 static void Set(string name, params string[] ids) {
  var bank=AssetDatabase.LoadAssetAtPath<SoundBank>(dir+name+".asset");
  var clips=ids.Select(id=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/HawksArges/Audio/pd2-vulcan-"+id+".wav")).ToArray();
  if(!bank || clips.Any(c=>!c || c.length<=0))throw new Exception(name);
  foreach(var env in bank.Environments)foreach(var range in env.Clips)range.Clips=clips;
  var so=new SerializedObject(bank);so.FindProperty("_clipLiength").floatValue=clips[0].length;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(bank);
 }
 public static void Run(){
  Set("arges_shot","auto-a","auto-b");
  Set("arges_tail","release-a","release-b");
  var path=dir+"weapon_m135_arges_container.prefab";
  var prefab=PrefabUtility.LoadPrefabContents(path);
  try {
   var player=prefab.GetComponent<WeaponSoundPlayer>();
   player.Non_auto=false;
   player.Body.CustomLength=16f*2048f/44100f;
   EditorUtility.SetDirty(player.Body);
   PrefabUtility.SaveAsPrefabAsset(prefab,path);
  } finally { PrefabUtility.UnloadPrefabContents(prefab); }
  AssetDatabase.SaveAssets();
  const string output="bundles";Directory.CreateDirectory(output);
  const string file="weapon_m135_arges.bundle";
  var builds=new[]{new AssetBundleBuild{assetBundleName=file,assetNames=new[]{dir+"weapon_m135_arges_container.prefab"}}};
  if(!BuildPipeline.BuildAssetBundles(output,builds,BuildAssetBundleOptions.StrictMode|BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("build");
  var bundle=AssetBundle.LoadFromFile(Path.Combine(output,file));var obj=bundle.LoadAllAssets<GameObject>().First(g=>g.GetComponent<WeaponSoundPlayer>());
  var sp=obj.GetComponent<WeaponSoundPlayer>(); if(sp.Non_auto || sp.Body!=sp.Doublet || sp.Body.CustomLength<=0)throw new Exception("automatic playback settings");
  foreach(var bank in new[]{sp.Body,sp.Tail}){
   string prefix=bank==sp.Body?"pd2-vulcan-auto-":"pd2-vulcan-release-";
   foreach(var env in bank.Environments)foreach(var range in env.Clips)
    if(!range.Clips.Select(c=>c.name).SequenceEqual(new[]{prefix+"a",prefix+"b"}) || range.Clips.Any(c=>c.length<.98f))throw new Exception("incomplete "+prefix);
  }
  Debug.Log("ARGES_PD2_CORE_VERIFIED "+string.Join(",",sp.Body.Environments[0].Clips[0].Clips.Select(c=>c.name)));
 }
}
