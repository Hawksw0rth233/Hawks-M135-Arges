using System;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using UnityEngine;

namespace Hawks.Arges {
public sealed partial class SpinUp {
 // Centre of the 258 muzzle-rim vertices in Barrel_Standard mesh coordinates.
 static readonly Vector3 MuzzleInMesh=new Vector3(-.006694087f,.000000046335f,-.001458927f);
 Transform muzzleRig, barrelMesh;
 Func<RaycastHit,bool> ignoreMuzzleOwner;
 static void AlignVisualMuzzle(Player.FirearmController __instance,ref Vector3 position,ref Vector3 direction){
  if(__instance.Item?.TemplateId!=WeaponId || !Singleton<GameWorld>.Instantiated)return;
  var owner=Singleton<GameWorld>.Instance.MainPlayer;
  if(!owner || owner.HandsController!=__instance || owner.PointOfView!=EPointOfView.FirstPerson)return;
  var rig=__instance.HandsHierarchy?.Self;
  if(!rig)return;
  if(instance.muzzleRig!=rig || !instance.barrelMesh){
   var barrel=rig.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m=>m.name=="Barrel_Standard");
   if(!barrel)return;
   instance.muzzleRig=rig;instance.barrelMesh=barrel.transform;
   instance.ignoreMuzzleOwner=h=>owner.HasBodyPartCollider(h.collider)||h.collider.transform.IsChildOf(owner.Transform.Original);
  }
  var mesh=instance.barrelMesh;
  var muzzle=mesh.TransformPoint(MuzzleInMesh);
  var rear=mesh.TransformPoint(new Vector3(-.00272284f,MuzzleInMesh.y,MuzzleInMesh.z));
  var camera=instance.poseCamera?instance.poseCamera:Camera.main;
  if(instance.poseCamera && instance.gripRoots.Exists(root=>mesh.IsChildOf(root))){
   muzzle=instance.undoRenderedGrip.MultiplyPoint3x4(muzzle);
   rear=instance.undoRenderedGrip.MultiplyPoint3x4(rear);
  }
  if(camera){
   var offset=camera.transform.rotation*GripTranslation;
   muzzle+=offset;
   rear+=offset;
  }
  var axis=muzzle-rear;
  if(axis.sqrMagnitude<1e-8f)return;
  direction=axis.normalized;
  position=muzzle;
  // When the rendered barrel straddles a target, start on the near side.
  var mask=BallisticsCalculatorConstants.HitMask;
  if(Shot.LinecastInBothSides(rear,muzzle,out var obstruction,out _,mask,mask,instance.ignoreMuzzleOwner))
   position=obstruction.point-direction*.002f;
 }
}}
