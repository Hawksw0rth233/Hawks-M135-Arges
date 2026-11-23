using System.Collections.Generic;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace Hawks.Arges {
public sealed partial class SpinUp {
 Camera poseCamera;
 Matrix4x4 undoRenderedGrip;
 static readonly Quaternion GripRotation=Quaternion.Euler(-11f,0f,12f);
 static readonly Vector3 GripTranslation=new Vector3(0,.10f,.15f);
 readonly List<Transform> gripRoots=new List<Transform>(5);
 readonly List<Pose> originalGrip=new List<Pose>(5);

 static Matrix4x4 GripPose(Transform camera, Vector3 pivot){
  var turn=camera.rotation*GripRotation*Quaternion.Inverse(camera.rotation);
  return Matrix4x4.TRS(pivot+camera.rotation*GripTranslation-turn*pivot,turn,Vector3.one);
 }
 void AddGripRoot(Transform node,Transform camera){
  if(!node || node==camera || camera.IsChildOf(node))return;
  for(int i=gripRoots.Count-1;i>=0;i--){
   if(node==gripRoots[i] || node.IsChildOf(gripRoots[i]))return;
   if(gripRoots[i].IsChildOf(node))gripRoots.RemoveAt(i);
  }
  gripRoots.Add(node);
 }
 void ApplyGrip(Camera camera){
  if(poseCamera || camera!=Camera.main || !Singleton<GameWorld>.Instantiated)return;
  var player=Singleton<GameWorld>.Instance.MainPlayer;
  if(!player || player.PointOfView!=EPointOfView.FirstPerson ||
   !(player.HandsController is Player.FirearmController controller) || controller.Item?.TemplateId!=WeaponId)return;
  var weapon=player.ProceduralWeaponAnimation?.HandsContainer?.WeaponRoot;
  var bones=player.PlayerBones;
  if(!weapon || !bones.RightPalm)return;
  var basis=camera.transform;
  var pose=GripPose(basis,bones.RightPalm.position);
  AddGripRoot(weapon,basis);
  if(bones.Upperarms!=null)foreach(var arm in bones.Upperarms)AddGripRoot(arm,basis);
  AddGripRoot(bones.LeftPalm,basis);AddGripRoot(bones.RightPalm,basis);
  undoRenderedGrip=pose.inverse;
  poseCamera=camera;
  foreach(var root in gripRoots){
   originalGrip.Add(new Pose(root.localPosition,root.localRotation));
   root.SetPositionAndRotation(pose.MultiplyPoint3x4(root.position),pose.rotation*root.rotation);
  }
 }
 void RestoreGrip(){
  for(int i=0;i<originalGrip.Count;i++)if(gripRoots[i]){
   gripRoots[i].localPosition=originalGrip[i].position;
   gripRoots[i].localRotation=originalGrip[i].rotation;
  }
  gripRoots.Clear();originalGrip.Clear();poseCamera=null;
 }
 void AfterGrip(Camera camera){if(poseCamera==camera)RestoreGrip();}
 void OnEnable(){Camera.onPreCull+=ApplyGrip;Camera.onPostRender+=AfterGrip;}
 void OnDisable(){Camera.onPreCull-=ApplyGrip;Camera.onPostRender-=AfterGrip;RestoreGrip();}
}}
