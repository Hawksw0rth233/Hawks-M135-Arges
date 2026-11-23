using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ArgesCarryEffects {
 public static void BindBarrel(GameObject rig){
  var nodes=rig.GetComponentsInChildren<Transform>(true);
  var barrel=nodes.Single(n=>n.name=="Barrel_Standard");
  var fx=nodes.Single(n=>n.name=="ArgesMuzzleFX");
  var vertices=barrel.GetComponent<MeshFilter>().sharedMesh.vertices;
  var tip=vertices.Min(v=>v.x);
  var rim=vertices.Where(v=>v.x<=tip+.000005f).ToArray();
  var center=rim.Aggregate(Vector3.zero,(sum,v)=>sum+v)/rim.Length;
  fx.SetParent(barrel,true);
  fx.position=barrel.TransformPoint(center);
  fx.rotation=Quaternion.FromToRotation(fx.TransformDirection(Vector3.down),barrel.TransformDirection(Vector3.left))*fx.rotation;
  // EFT resolves the named fireport recursively below WeaponRoot.
  var fireport=nodes.Single(n=>n.name=="fireport");
  fireport.SetParent(barrel,true);
  fireport.SetPositionAndRotation(fx.position,fx.rotation);
  Debug.Log("ARGES FX barrel centre "+center.ToString("F6")+" rim vertices "+rim.Length);
 }
 public static void BindPatrol(string controllerPath){
  var states=AssetDatabase.LoadAllAssetsAtPath(controllerPath).OfType<AnimatorState>().ToArray();
  var folder=System.IO.Path.GetDirectoryName(controllerPath).Replace('\\','/')+"/";
  void CopyPose(string patrol,string inventory){
   var state=states.Single(s=>s.name==patrol);
   var reference=states.Single(s=>s.name==inventory);
   var clip=UnityEngine.Object.Instantiate((AnimationClip)reference.motion);
   clip.name=state.motion.name;
   var path=folder+"carry-"+patrol.Replace(' ','-')+".anim";
   var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
   if(saved){EditorUtility.CopySerialized(clip,saved);UnityEngine.Object.DestroyImmediate(clip);}
   else {AssetDatabase.CreateAsset(clip,path);saved=clip;}
   state.motion=saved;state.speed=reference.speed;EditorUtility.SetDirty(state);
  }
  CopyPose("PATROL IN","OPEN INVENTORY");
  CopyPose("PATROL","IN INVENTORY");
  CopyPose("PATROL OUT","CLOSE INVENTORY");
  AssetDatabase.SaveAssets();
 }
}
