using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ArgesMotionBake {
 const string Folder="Assets/HawksArgesNative/";
 static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
 struct Pose {
  public Vector3 p; public Quaternion q;
  public Pose(Transform t){p=t.localPosition;q=t.localRotation;}
  public void Put(Transform t){t.localPosition=p;t.localRotation=q;}
 }
 static Quaternion Palm(Transform wrist,Transform middle,Transform index,Transform pinky)=>
  Quaternion.LookRotation(middle.position-wrist.position,index.position-pinky.position);
 public static void Run(WeaponPrefab wp){
  var src=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HawksArges/ArgesMotion.prefab"));
  var rig=Object.Instantiate(wp._weaponObject);
  try {
   var template=wp._originalAnimatorController;
   var source=AssetDatabase.LoadAllAssetsAtPath("Assets/HawksArges/ArgesMotion.fbx").OfType<AnimationClip>().ToDictionary(c=>c.name);
   var idle=template.animationClips.First(c=>c.name=="ppsh41_idle_armed");
   
   idle.SampleAnimation(rig,0);source["Idle"].SampleAnimation(src,0);
   var mount=Find(src,"Mount_RightHandWeapon");var gun=Find(rig,"weapon");var model=Find(rig,"M135");
   var srcRight=Find(src,"RightArm");var srcLeft=Find(src,"LeftArm");
   var dstBase=(Find(rig,"Base HumanRUpperarm").position+Find(rig,"Base HumanLUpperarm").position)*.5f;
   var srcBase=(srcLeft.position+srcRight.position)*.5f;
   var forward=mount.TransformDirection(Vector3.left);
   var up=Vector3.Cross(forward,srcRight.position-srcLeft.position);
   var align=Quaternion.Inverse(Quaternion.LookRotation(forward,up));
   Vector3 Point(Transform t)=>dstBase+align*(t.position-srcBase);
   var shoulderDirection=(Point(srcRight)-Point(srcLeft)).normalized;
   bool inspectRightArm=false;var calibrateArm=new List<Action>();var contactMarkers=new List<Action>();var targets=new List<Transform>();var apply=new List<Action>();
   void Write(Transform t,Action action){targets.Add(t);apply.Add(action);}
   Write(gun,()=>gun.SetPositionAndRotation(Point(mount),align*mount.rotation*Quaternion.Inverse(model.localRotation)));
   foreach(string side in new[]{"Left","Right"}){
    string prefix="Base Human"+side[0];var hand=Find(src,side+"Hand");var wrist=Find(rig,prefix+"Palm");
    var middle=Find(src,side+"HandMiddle1");var index=Find(src,side+"HandIndex1");var pinky=Find(src,side+"HandPinky1");
    var wristFix=Quaternion.Inverse(Palm(wrist,Find(rig,prefix+"Digit31"),Find(rig,prefix+"Digit21"),Find(rig,prefix+"Digit51")))*wrist.rotation;
    var sh=Find(rig,prefix+"Upperarm");var el=Find(rig,prefix+"Forearm1");
    var nativeBend=Vector3.Cross(el.position-sh.position,wrist.position-el.position).normalized;
    var ssh=Find(src,side+"Arm");var sel=Find(src,side+"ForeArm");
    void Segment(Transform t,Transform end,Transform a,Transform b){
     var axis=t.InverseTransformDirection(end.position-t.position);
     var nativeFrame=Quaternion.LookRotation(end.position-t.position,nativeBend);
     var bendFrameOffset=Quaternion.Inverse(nativeFrame)*t.rotation;
     Quaternion sourceToTarget=Quaternion.identity;
     if(side=="Right")calibrateArm.Add(()=>sourceToTarget=Quaternion.Inverse(align*a.rotation)*t.rotation);
     Write(t,()=>{
      t.position=Point(a);
      // Direction alone leaves the roll of the arm undetermined during inspection.
      Quaternion basis=inspectRightArm && side=="Right"?align*a.rotation*sourceToTarget:t.rotation;
      var direction=Point(b)-Point(a);
      var bend=Vector3.Cross(Point(sel)-Point(ssh),Point(hand)-Point(sel));
      t.rotation=inspectRightArm && side=="Right" && bend.sqrMagnitude>1e-7f
       ? Quaternion.LookRotation(direction,bend)*bendFrameOffset
       : Quaternion.FromToRotation(basis*axis,direction)*basis;
     });
    }
    Segment(sh,el,ssh,sel);Segment(el,wrist,sel,hand);
    if(side=="Right"){
     // PD3 twist helpers are siblings, while the EFT forearm helpers form a chain.
     var previous=Find(src,"RightForeArm");
     for(int j=2;j<=3;j++){
      var helper=Find(rig,prefix+"Forearm"+j);
      var sourceHelper=Find(src,"RightForeArmTwist"+(4-j));
      var parentHelper=previous;previous=sourceHelper;
      var reference=Quaternion.Inverse(parentHelper.rotation)*sourceHelper.rotation;
      var nativeLocal=helper.localRotation;
      var localAxis=helper.InverseTransformDirection(wrist.position-helper.position).normalized;
      Write(helper,()=>{
       if(!inspectRightArm)return;
       var delta=Quaternion.Inverse(reference)*Quaternion.Inverse(parentHelper.rotation)*sourceHelper.rotation;
       float twist=2f*Mathf.Atan2(delta.z,delta.w)*Mathf.Rad2Deg;
       helper.localRotation=nativeLocal*Quaternion.AngleAxis(Mathf.DeltaAngle(0,twist),localAxis);
      });
     }
    }
    Write(wrist,()=>wrist.SetPositionAndRotation(Point(hand),align*Palm(hand,middle,index,pinky)*wristFix));
    foreach(string suffix in new[]{"_hand_marker","_IK_marker"}){
     var marker=Find(rig,"weapon_"+side[0]+suffix);
     var loc=wrist.InverseTransformPoint(marker.position);var rot=Quaternion.Inverse(wrist.rotation)*marker.rotation;
     Action placeMarker=()=>marker.SetPositionAndRotation(wrist.TransformPoint(loc),wrist.rotation*rot);
     Write(marker,placeMarker);contactMarkers.Add(placeMarker);
    }
    int finger=0;foreach(string name in new[]{"Thumb","Index","Middle","Ring","Pinky"}){
     finger++;for(int j=1;j<=3;j++){var t=Find(rig,prefix+"Digit"+finger+j);
      var a=Find(src,side+"Hand"+name+j);var b=Find(src,side+"Hand"+name+(j+1));
      Write(t,()=>{t.position=Point(a);t.rotation=Quaternion.FromToRotation(t.TransformDirection(Vector3.left),Point(b)-Point(a))*t.rotation;});
     }
    }
   }
   var rigNodes=rig.GetComponentsInChildren<Transform>(true);var rest=rigNodes.Select(t=>new Pose(t)).ToArray();
   var sourceNodes=src.GetComponentsInChildren<Transform>(true);var neutral=sourceNodes.Select(t=>new Pose(t)).ToArray();
   var jointPaths=targets.Select(t=>AnimationUtility.CalculateTransformPath(t,rig.transform)).ToArray();
   var idleGun=align*mount.rotation*Quaternion.Inverse(model.localRotation);
   // Local correction measured from the frame-zero Blender weapon Z channel edit.
   var editWorld=new Quaternion(0.0000005066395f,-0.0000004768371f,-0.798633039f,0.601818323f);
   var editLocal=Quaternion.Inverse(idleGun)*editWorld*idleGun;
   void Sample(){for(int n=0;n<rest.Length;n++)rest[n].Put(rigNodes[n]);foreach(var set in apply)set();gun.rotation=gun.rotation*editLocal;
    // The four contact markers are children of the weapon. Restore their wrist-space pose after rotating it.
    foreach(var placeMarker in contactMarkers)placeMarker();}
   Sample();foreach(var calibrate in calibrateArm)calibrate();var holding=targets.Select(t=>new Pose(t)).ToArray();
   var actions=new Dictionary<string,string>{{"idle_armed","Idle"},{"out_to_idle","Equip"},{"idle_to_out","Unequip"},{"look","Inspect"},{"fire","Loop"},{"fire_dry","Idle"},{"idle_disarmed","Idle"}};
   var replacements=new Dictionary<string,AnimationClip>();
   foreach(var old in template.animationClips.Distinct()){
    if(!actions.TryGetValue(old.name.Replace("ppsh41_",""),out var action))continue;
    inspectRightArm=action=="Inspect";var input=source[action];var clip=Object.Instantiate(idle);clip.name=old.name;
    // Use the same static base for every state, including untargeted camera and twist bones.
    foreach(var bind in AnimationUtility.GetCurveBindings(clip)){
     if((bind.path.EndsWith("/mod_magazine") || bind.path.EndsWith("/shellport")) && bind.type==typeof(Transform)){ AnimationUtility.SetEditorCurve(clip,bind,null); continue; }
     var original=AnimationUtility.GetEditorCurve(clip,bind);
     AnimationUtility.SetEditorCurve(clip,bind,AnimationCurve.Constant(0,input.length,original.Evaluate(0)));
    }
    var curves=targets.Select(t=>Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray()).ToArray();
    input.SampleAnimation(src,0);var first=sourceNodes.Select(t=>new Pose(t)).ToArray();
    float len=action=="Idle"?idle.length:input.length;int frames=Mathf.Max(1,Mathf.RoundToInt(len*30));
    for(int i=0;i<=frames;i++){
     float u=(float)i/frames,time=u*len;input.SampleAnimation(src,u*input.length);
     if(action=="Loop")for(int n=0;n<sourceNodes.Length;n++){
      var t=sourceNodes[n];t.localPosition=neutral[n].p+t.localPosition-first[n].p;
      t.localRotation=neutral[n].q*Quaternion.Inverse(first[n].q)*t.localRotation;}
     if(action=="Unequip")for(int n=0;n<sourceNodes.Length;n++)neutral[n].Put(sourceNodes[n]);
     Sample();
     if(action=="Unequip"){
      var drop=Vector3.down*.72f*Mathf.SmoothStep(0,1,Mathf.Clamp01(u/.91f));
      gun.position+=drop;
      foreach(var side in new[]{"L","R"})Find(rig,"Base Human"+side+"Upperarm").position+=drop;
     }
     float weight=action=="Loop" ? .4f : 1f;

     if(action=="Inspect")weight=Mathf.SmoothStep(0,1,Mathf.Min(time,len-time)/.15f);
     for(int n=0;n<targets.Count;n++){
      var t=targets[n];var v=Vector3.Lerp(holding[n].p,t.localPosition,weight);var q=Quaternion.Slerp(holding[n].q,t.localRotation,weight);
      for(int j=0;j<7;j++)curves[n][j].AddKey(time,j<3?v[j]:q[j-3]);
     }
    }
    for(int n=0;n<targets.Count;n++)for(int j=0;j<7;j++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(jointPaths[n],typeof(Transform),j<3?"m_LocalPosition."+"xyz"[j]:"m_LocalRotation."+"xyzw"[j-3]),curves[n][j]);
    if(action=="Inspect"){
     var sourceBarrel=Find(src,"Barrel");var rotor=Find(rig,"ArgesFireRotor");var path=AnimationUtility.CalculateTransformPath(rotor,rig.transform);
     var angles=new List<string>();float total=0,previous=0;var spin=Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray();input.SampleAnimation(src,0);var initial=sourceBarrel.localRotation;
     for(int i=0;i<=frames;i++){float t=i*len/frames;input.SampleAnimation(src,t);var delta=Quaternion.Inverse(initial)*sourceBarrel.localRotation;delta.ToAngleAxis(out float angle,out var axis);
      var signed=angle*(Mathf.Abs(axis.x)>Mathf.Abs(axis.y)&&Mathf.Abs(axis.x)>Mathf.Abs(axis.z)?Mathf.Sign(axis.x):Mathf.Abs(axis.y)>Mathf.Abs(axis.z)?Mathf.Sign(axis.y):Mathf.Sign(axis.z));
      total+=Mathf.DeltaAngle(previous,signed);previous=signed;angles.Add(total.ToString("R",System.Globalization.CultureInfo.InvariantCulture));var q=Quaternion.AngleAxis(signed,Vector3.up);for(int j=0;j<4;j++)spin[j].AddKey(t,q[j]);}
     File.WriteAllLines("Client/inspect-rotor.txt",angles);
    }
    var toolDriver=Find(rig,"weapon_bolt");
    var driverPath=AnimationUtility.CalculateTransformPath(toolDriver,rig.transform);
    foreach(char axis in "xyz") AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(driverPath,typeof(Transform),"m_LocalScale."+axis),AnimationCurve.Constant(0,len,0));

    if(action=="Equip"){
     var track=File.ReadAllLines("Editor/equip-hand-track.txt");
     var channels=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
     var palm=Find(rig,"Base HumanRPalm");var middle=Find(rig,"Base HumanRDigit31");
     var index=Find(rig,"Base HumanRDigit21");var pinky=Find(rig,"Base HumanRDigit51");
     for(int k=0;k<track.Length;k++){
      float time=k/30f;clip.SampleAnimation(rig,time);
      var x=(middle.position-palm.position).normalized;
      var z=Vector3.Cross(x,index.position-pinky.position).normalized;var y=Vector3.Cross(z,x);
      var row=track[k].Split(' ').Select(n=>float.Parse(n,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
      Vector3 Mark(int n)=>palm.position+x*row[n]+y*row[n+1]+z*row[n+2];
      var center=Mark(0);var pos=gun.InverseTransformPoint(center);
      var rotation=Quaternion.Inverse(gun.rotation)*Quaternion.LookRotation(Mark(3)-center,Mark(6)-center);
      float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,11,k));
      pos=Vector3.Lerp(NativeArges.toolPositions[k],pos,blend);rotation=Quaternion.Slerp(NativeArges.toolRotations[k],rotation,blend);
      for(int ch=0;ch<7;ch++)channels[ch].AddKey(time,ch<3?pos[ch]:rotation[ch-3]);
     }
     for(int ch=0;ch<7;ch++){
      var property=ch<3?"m_LocalPosition."+"xyz"[ch]:"m_LocalRotation."+"xyzw"[ch-3];
      AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(driverPath,typeof(Transform),property),channels[ch]);
     }
     var show=new AnimationCurve(new Keyframe(0,1),new Keyframe(Mathf.Min(len-.001f,46/30f),0),new Keyframe(len,0));
     for(int k=0;k<show.length;k++)AnimationUtility.SetKeyRightTangentMode(show,k,AnimationUtility.TangentMode.Constant);
     foreach(char axis in "xyz") AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(driverPath,typeof(Transform),"m_LocalScale."+axis),show);

     var angleRows=File.ReadAllLines("Editor/equip-barrel-rest.txt");
     var rotorPath=AnimationUtility.CalculateTransformPath(Find(rig,"ArgesFireRotor"),rig.transform);
     var spinCurves=Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray();
     for(int k=0;k<angleRows.Length;k++){
      float angle=float.Parse(angleRows[k],System.Globalization.CultureInfo.InvariantCulture);
      var turn=Quaternion.AngleAxis(angle,Vector3.up);
      for(int j=0;j<4;j++)spinCurves[j].AddKey(k/30f,turn[j]);
     }
     for(int j=0;j<4;j++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(rotorPath,typeof(Transform),"m_LocalRotation."+"xyzw"[j]),spinCurves[j]);
    }
    var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=len;settings.loopTime=action=="Loop"||action=="Idle";AnimationUtility.SetAnimationClipSettings(clip,settings);
    clip.EnsureQuaternionContinuity();
    // Resample just the two shoulder translations and their elbow compensation against the finished clip.
    var shoulderNodes=new[]{"L","R"}.SelectMany(s=>new[]{Find(rig,"Base Human"+s+"Upperarm"),Find(rig,"Base Human"+s+"Forearm1")}).ToArray();
    var positionCurves=shoulderNodes.Select(t=>new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()}).ToArray();
    int steps=Mathf.CeilToInt(len*120);
    for(int i=0;i<=steps;i++){
     float time=Mathf.Min(i/120f,len);clip.SampleAnimation(rig,time);
     for(int side=0;side<2;side++){
      var shoulder=shoulderNodes[side*2];var elbow=shoulderNodes[side*2+1];var saved=elbow.position;
      shoulder.position+=shoulderDirection*(side==0?-.02f:.02f);elbow.position=saved;
     }
     for(int n=0;n<4;n++)for(int axis=0;axis<3;axis++)positionCurves[n][axis].AddKey(time,shoulderNodes[n].localPosition[axis]);
    }
    for(int n=0;n<4;n++)for(int axis=0;axis<3;axis++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(shoulderNodes[n],rig.transform),typeof(Transform),"m_LocalPosition."+"xyz"[axis]),positionCurves[n][axis]);
    if(action=="Inspect"){
     // EFT GatherIK feeds this marker to the player IK solver after animation playback.
     var goal=Find(rig,"Bend_Goal_Right");var wrist=Find(rig,"Base HumanRPalm");
     var shoulder=shoulderNodes[2];var elbow=shoulderNodes[3];
     var baseLocal=goal.localPosition;var curvesGoal=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
     for(int i=0;i<=steps;i++){
      float t=Mathf.Min(i/120f,len);goal.localPosition=baseLocal;clip.SampleAnimation(rig,t);
      var line=wrist.position-shoulder.position;
      var outward=Vector3.ProjectOnPlane(elbow.position-shoulder.position,line);
      var pole=elbow.position+outward.normalized*.15f;
      float mix=Mathf.SmoothStep(0,1,Mathf.Min(t,len-t)/.15f);
      var local=Vector3.Lerp(goal.localPosition,goal.parent.InverseTransformPoint(pole),mix);
      for(int j=0;j<3;j++)curvesGoal[j].AddKey(t,local[j]);
     }
     for(int j=0;j<3;j++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(goal,rig.transform),typeof(Transform),"m_LocalPosition."+"xyz"[j]),curvesGoal[j]);
    }
    AssetDatabase.CreateAsset(clip,Folder+old.name+".anim");replacements.Add(old.name,clip);
   }
   var pathOut=Folder+"Arges.controller";File.Copy(AssetDatabase.GetAssetPath(template),pathOut,true);AssetDatabase.ImportAsset(pathOut);
   wp._originalAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(pathOut);
   ArgesNativeEvents.Bind((UnityEditor.Animations.AnimatorController)wp._originalAnimatorController,wp.GetComponent<WeaponSoundPlayer>(),replacements);
   // Preserve the native inventory states, durations and gameplay event timing.
   var inventoryStates=AssetDatabase.LoadAllAssetsAtPath(pathOut).OfType<UnityEditor.Animations.AnimatorState>();
   var inspect=replacements["ppsh41_look"];float stop=0f,best=-10;
   for(int frame=8;frame<=inspect.length*15;frame++){
    float t=frame/30f;inspect.SampleAnimation(rig,t);
    var direction=model.TransformDirection(Vector3.left).normalized;
    float horizontal=Mathf.Abs(direction.x);
    if(horizontal>best+.001f){best=horizontal;stop=t;}
   }
   foreach(var state in inventoryStates){
    bool opening=state.name=="OPEN INVENTORY",closing=state.name=="CLOSE INVENTORY";
    if(!opening && !closing && state.name!="IN INVENTORY")continue;
    var previous=(AnimationClip)state.motion;
    var segment=new AnimationClip{name=previous.name,frameRate=60};
    float duration=previous.length;int samples=Mathf.CeilToInt(duration*60);
    foreach(var binding in AnimationUtility.GetCurveBindings(inspect)){
     var sourceCurve=AnimationUtility.GetEditorCurve(inspect,binding);var curve=new AnimationCurve();
     for(int i=0;i<=samples;i++){
      float u=(float)i/samples;float phase=opening?u:closing?1-u:1;
      curve.AddKey(duration*u,sourceCurve.Evaluate(phase*stop));
     }
     AnimationUtility.SetEditorCurve(segment,binding,curve);
    }
    segment.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(segment);
    settings.loopTime=!opening&&!closing;settings.stopTime=duration;AnimationUtility.SetAnimationClipSettings(segment,settings);
    AssetDatabase.CreateAsset(segment,Folder+"inventory-"+previous.name+".anim");state.motion=segment;EditorUtility.SetDirty(state);
    Debug.Log("ARGES INVENTORY "+state.name+" length="+duration+" stop="+stop+" horiz="+best);
   }
   AssetDatabase.SaveAssets();
   ArgesCarryEffects.BindPatrol(pathOut);
  }finally{Object.DestroyImmediate(rig);Object.DestroyImmediate(src);}
 }
}
