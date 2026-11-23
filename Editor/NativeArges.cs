using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using EFT;
using EFT.AssetsManager;
using Object=UnityEngine.Object;
public static class NativeArges {
 const string Out="bundles",Folder="Assets/HawksArgesNative/",Source="Assets/HawksArges/";
 static bool FeedDecal(Vector3[] v,int a,int b,int c){
  // The two disconnected decals below the receiver are on the removable feed box.
  return (v[a].z+v[b].z+v[c].z)/3f < -.00268f;
 }
 static int[] Decals(Mesh m,int sub,bool onFeed){
  var tris=m.GetTriangles(sub);var v=m.vertices;
  return Enumerable.Range(0,tris.Length/3).Where(i=>FeedDecal(v,tris[i*3],tris[i*3+1],tris[i*3+2])==onFeed)
   .SelectMany(i=>new[]{tris[i*3],tris[i*3+1],tris[i*3+2]}).ToArray();
 }
 static GameObject Save(GameObject obj,string name){var prefab=PrefabUtility.SaveAsPrefabAsset(obj,Folder+name+".prefab");Object.DestroyImmediate(obj);return prefab;}
 static GameObject Load(string n)=>AssetDatabase.LoadAssetAtPath<GameObject>(Source+n+".prefab");
 static Transform Bone(GameObject o,string name)=>o.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
 public static Vector3[] toolPositions=new Vector3[57];public static Quaternion[] toolRotations=new Quaternion[57];
 static Vector3 feedLocal;static Quaternion feedRotation;
 static GameObject Rig(GameObject source,string name){
 var o=Object.Instantiate(source);o.name=name;
 foreach(var r in o.GetComponentsInChildren<Renderer>(true))Object.DestroyImmediate(r);
 foreach(var m in o.GetComponentsInChildren<MeshFilter>(true))Object.DestroyImmediate(m);
 foreach(var group in o.GetComponentsInChildren<LODGroup>(true))Object.DestroyImmediate(group);
 var weapon=Bone(o,"weapon");
 var model=Object.Instantiate(Load("Arges"),weapon,false);model.name="M135";
 model.transform.localRotation=Quaternion.LookRotation(Vector3.down,Vector3.forward)*Quaternion.Inverse(Quaternion.LookRotation(Vector3.left,Vector3.up));
 // The annotated side view spans x=73..464 and y=29..224, including the loaded feed.
 // Project its marked centre onto the outward surface of the receiver.
 var bodyMesh=Bone(model,"Body_Standard").GetComponent<MeshFilter>();
 var profile=model.GetComponentsInChildren<MeshFilter>().SelectMany(filter=>
  filter.sharedMesh.vertices.Select(v=>model.transform.InverseTransformPoint(filter.transform.TransformPoint(v)))).ToArray();
 var xMark=Mathf.Lerp(profile.Max(v=>v.x),profile.Min(v=>v.x),(240f-73)/(464-73));
 var yMark=Mathf.Lerp(profile.Max(v=>v.y),profile.Min(v=>v.y),(86f-29)/(224-29));
 var cast=bodyMesh.gameObject.AddComponent<MeshCollider>();cast.sharedMesh=bodyMesh.sharedMesh;
 var right=model.transform.forward;var ray=new Ray(model.transform.TransformPoint(new Vector3(xMark,yMark,1)), -right);
 Physics.SyncTransforms();
 if(!cast.Raycast(ray,out var hit,2))throw new Exception("Annotated shell opening missed body geometry");
 var port=Bone(o,"shellport");port.SetParent(weapon,true);
 var downRight=model.transform.TransformDirection(Quaternion.AngleAxis(-15,Vector3.right)*Vector3.down);
 port.SetPositionAndRotation(hit.point+right*.002f,Quaternion.LookRotation(downRight,model.transform.TransformDirection(Vector3.left)));
 Debug.Log("ARGES shell centre="+model.transform.InverseTransformPoint(port.position)+" direction="+model.transform.InverseTransformDirection(port.forward));
 Object.DestroyImmediate(cast);
 var tool=new GameObject("EquipTool").transform;tool.SetParent(model.transform,false);
 var toolObject=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source+"EquipTool.fbx"),tool,false);
 var skin=toolObject.GetComponentInChildren<SkinnedMeshRenderer>(true);if(!skin||skin.sharedMesh.blendShapeCount!=57)throw new Exception("Equip prop frames missing");
 var tm=AssetDatabase.LoadAssetAtPath<Material>(Folder+"EquipTool.mat");if(!tm){tm=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(tm,Folder+"EquipTool.mat");}tm.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"tool-albedo.png");var inputMesh=skin.sharedMesh;var sourceVerts=inputMesh.vertices;
 var basis=sourceVerts.Select(v=>tool.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
 int a=0,b=Enumerable.Range(1,basis.Length-1).OrderByDescending(i=>(basis[i]-basis[a]).sqrMagnitude).First();
 int c=Enumerable.Range(0,basis.Length).OrderByDescending(i=>Vector3.Cross(basis[b]-basis[a],basis[i]-basis[a]).sqrMagnitude).First();
 Quaternion Frame(Vector3[] v)=>Quaternion.LookRotation(v[b]-v[a],v[c]-v[a]);
 var frame=Frame(basis);var deltas=new Vector3[basis.Length];
 float worst=0;
 for(int k=0;k<57;k++){inputMesh.GetBlendShapeFrameVertices(k,0,deltas,null,null);
 var posed=sourceVerts.Select((v,i)=>tool.InverseTransformPoint(skin.transform.TransformPoint(v+deltas[i]))).ToArray();
 var rot=Frame(posed)*Quaternion.Inverse(frame);var pos=posed[a]-rot*basis[a];toolPositions[k]=pos;toolRotations[k]=rot;
 for(int i=0;i<basis.Length;i++)worst=Mathf.Max(worst,Vector3.Distance(pos+rot*basis[i],posed[i]));}
 Debug.Log("DRAW TOOL rigid residual metres "+worst);if(worst>.003f)throw new Exception("Non-rigid equip tool");
 var meshTool=Object.Instantiate(inputMesh);meshTool.ClearBlendShapes();var pivot=meshTool.bounds.center;var toolBounds=new Bounds(basis[0],Vector3.zero);foreach(var v in basis)toolBounds.Encapsulate(v);pivot=toolBounds.center;for(int k=0;k<57;k++){toolPositions[k]=model.transform.localPosition+model.transform.localRotation*(toolPositions[k]+toolRotations[k]*pivot);toolRotations[k]=model.transform.localRotation*toolRotations[k];}meshTool.vertices=basis.Select(v=>v-pivot).ToArray();meshTool.RecalculateNormals();meshTool.RecalculateBounds();
 AssetDatabase.CreateAsset(meshTool,Folder+name+"-tool.asset");Object.DestroyImmediate(toolObject);tool.gameObject.AddComponent<MeshFilter>().sharedMesh=meshTool;tool.gameObject.AddComponent<MeshRenderer>().sharedMaterial=tm;// Animate an existing weapon skeleton channel. EFT need not bind a custom
 // renderer, or promote a zero-size prop to a visible first-person object.
 var toolDriver=Bone(o,"weapon_bolt");
 if(toolDriver.parent!=weapon || toolDriver.GetComponentsInChildren<Renderer>(true).Length!=0)throw new Exception("Equip tool driver already occupied");
 tool.SetParent(toolDriver,false);
 tool.localPosition=Vector3.zero;tool.localRotation=Quaternion.identity;tool.localScale=Vector3.one;
 tool.gameObject.layer=Bone(model,"Body_Standard").gameObject.layer;
 toolDriver.localScale=Vector3.one*.00001f;
 if(name=="arges_simple")Object.DestroyImmediate(tool.gameObject);
 var mesh=model.GetComponentsInChildren<MeshFilter>().Single(x=>x.name.Contains("Barrel"));var vertices=mesh.sharedMesh.vertices.Select(v=>weapon.InverseTransformPoint(mesh.transform.TransformPoint(v))).ToArray();


 var end=vertices.Min(v=>v.y);var tip=vertices.Where(v=>v.y<end+.005f).ToArray();var mid=tip.Aggregate(Vector3.zero,(v,x)=>v+x)/tip.Length;
 var fireport=weapon.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="fireport");if(!fireport){fireport=new GameObject("fireport").transform;fireport.SetParent(weapon,false);}fireport.localPosition=mid;
 var rotor=new GameObject("ArgesRotor").transform;rotor.SetParent(weapon,false);rotor.localPosition=mid;var fireRotor=new GameObject("ArgesFireRotor").transform;fireRotor.SetParent(rotor,false);mesh.transform.SetParent(fireRotor,true);
 var motor=rotor.gameObject.AddComponent<AudioSource>();motor.playOnAwake=false;motor.loop=true;motor.spatialBlend=1;motor.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Source+"Audio/698153320.wem.wav");
 foreach(var t in o.GetComponentsInChildren<Transform>(true).Where(x=>x.name=="fireport"||x.name.StartsWith("smokeport")))t.position=weapon.TransformPoint(mid);
 // The detachable magazine brings its own geometry through mod_magazine.
 foreach(var r in model.GetComponentsInChildren<MeshRenderer>()){
 var materials=r.sharedMaterials;var keep=Enumerable.Range(0,materials.Length).Where(i=>materials[i].name!="Mag"&&materials[i].name!="AmmoBelt").ToArray();if(keep.Length==materials.Length)continue;
 var filter=r.GetComponent<MeshFilter>();var part=Object.Instantiate(filter.sharedMesh);var tris=keep.Select(i=>materials[i].name=="MM_M135_Stickers"?Decals(part,i,false):part.GetTriangles(i)).ToArray();part.subMeshCount=keep.Length;for(int i=0;i<keep.Length;i++)part.SetTriangles(tris[i],i);
 var path=Folder+name+"-body.asset";AssetDatabase.CreateAsset(part,path);filter.sharedMesh=part;r.sharedMaterials=keep.Select(i=>materials[i]).ToArray();}
 var slot=Bone(o,"mod_magazine");
 // Give the game a proper socket at the geometric center of the detachable box.
 var rawBody=Bone(Load("Arges"),"Body_Standard");var rawMesh=rawBody.GetComponent<MeshFilter>().sharedMesh;
 var sub=Array.FindIndex(rawBody.GetComponent<MeshRenderer>().sharedMaterials,m=>m.name=="Mag");
 var verts=rawMesh.vertices;var ids=rawMesh.GetTriangles(sub);
 var bounds=new Bounds(rawBody.TransformPoint(verts[ids[0]]),Vector3.zero);foreach(var id in ids)bounds.Encapsulate(rawBody.TransformPoint(verts[id]));
 slot.SetParent(model.transform,false);slot.localPosition=bounds.center;slot.localRotation=Quaternion.identity;slot.localScale=Vector3.one;
 feedLocal=-bounds.center;feedRotation=Quaternion.identity;
 return Save(o,name);}
 static SoundBank Bank(SoundBank template,string name,string[] ids){
 var b=Object.Instantiate(template);var clips=ids.Select(id=>AssetDatabase.LoadAssetAtPath<AudioClip>(Source+"Audio/"+id+(id=="arges-shot-tail-short"?".wav":".wem.wav"))).ToArray();
 if(clips.Any(x=>!x))throw new Exception("Missing audio "+name);
 foreach(var env in b.Environments)foreach(var range in env.Clips)range.Clips=clips;
 var so=new SerializedObject(b);so.FindProperty("_clipLiength").floatValue=clips[0].length;so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(b,Folder+name+".asset");return b;}
 public static void Build(){
 string path=AssetDatabase.FindAssets("weapon_zis_ppsh41_762x25_container t:Prefab").Select(AssetDatabase.GUIDToAssetPath).First(x=>x.EndsWith("_container.prefab"));
 var o=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));var wp=o.GetComponent<WeaponPrefab>();
 wp._weaponObject=Rig(AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace("_container.prefab",".generated.prefab")),"arges_full");wp._weaponObjectSimple=Rig(AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace("_container.prefab","_simple.generated.prefab")),"arges_simple");
 // EFT rotates the cartridge by +90 degrees around local X before applying its local impulse.
 var extraction=o.GetComponent<ShellExtractionData>();var shot=extraction.ShotSettings;
 shot.ShellsForceXRange=Vector2.zero;shot.ShellsForceYRange=new Vector2(2,2);
 shot.ShellsForceZRange=Vector2.zero;shot.ShellForceMultiplier=0;extraction.ShotSettings=shot;
 var sp=o.GetComponent<WeaponSoundPlayer>();sp.Non_auto=true;sp.Body=Bank(sp.Body,"arges_shot",new[]{"1030816981","276528188","316098162","351295528","704695388"});sp.Tail=Bank(sp.Tail,"arges_tail",new[]{"arges-shot-tail-short"});sp.Doublet=sp.Body;
 foreach(var m in sp.AdditionalSounds)Debug.Log("NATIVE ACTION "+m.EventName);
 Debug.Log("NATIVE CLIPS "+string.Join(",",wp._originalAnimatorController.animationClips.Select(c=>c.name).Distinct()));
 ArgesMotionBake.Run(wp);wp._fastAnimatorControllerBinaryData=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"ArgesStates.bytes");var simple=new AnimatorOverrideController(wp._originalAnimatorController);AssetDatabase.CreateAsset(simple,Folder+"ArgesSimple.overrideController");wp._animatorSimple=simple;wp._animatorSpirit=simple;var clicks=new[]{"171486","374244581","397811854","482803504","610798379","964743721"};
 sp.AdditionalSounds.Add(new BaseSoundPlayer.SoundElement{EventName="ArgesSpinStart",SoundClips=clicks.Select(id=>AssetDatabase.LoadAssetAtPath<AudioClip>(Source+"Audio/"+id+(id=="arges-shot-tail-short"?".wav":".wem.wav"))).ToArray()});
 var weaponView=o.GetComponent<PreviewPivot>();var displayModel=Bone(wp._weaponObjectSimple,"M135");var visible=displayModel.GetComponentsInChildren<MeshRenderer>(true).Where(x=>x.enabled && x.name!="EquipTool").Select(x=>x.bounds).ToArray();var wb=visible[0];foreach(var v in visible)wb.Encapsulate(v);wb.Encapsulate(Bone(wp._weaponObjectSimple,"ArgesFireRotor").GetComponentInChildren<MeshRenderer>().bounds);
 weaponView.pivotPosition=wp._weaponObjectSimple.transform.InverseTransformPoint(wb.center);weaponView.scale=Vector3.one;weaponView.SpawnPosition=Vector3.zero;weaponView.Icon.rotation=Quaternion.Euler(6,-16,0)*Quaternion.Inverse(displayModel.rotation);weaponView.Icon.rotationEuler=weaponView.Icon.rotation.eulerAngles;weaponView.Icon.orthographic=true;weaponView.Icon.hasOffset=false;
 var prefab=Save(o,"weapon_m135_arges_container");var feed=new GameObject("mag_m135_arges");feed.AddComponent<MagazineInHandsVisualController>();
 // SlotView.InsertItem defaults to +90 degrees on X for attachments without ModPlacer.
 var placement=feed.AddComponent<ModPlacer>();placement.ModRotation=Vector3.zero;placement.ModPosition=Vector3.zero;placement.ModScale=Vector3.one;
 var visual=Object.Instantiate(Load("Feed"),feed.transform,false);
 var body=Bone(Load("Arges"),"Body_Standard");
 visual.transform.localPosition=feedLocal+feedRotation*body.localPosition;visual.transform.localRotation=feedRotation*body.localRotation;visual.transform.localScale=body.localScale;
 var filter=visual.GetComponent<MeshFilter>();var mats=visual.GetComponent<MeshRenderer>().sharedMaterials;
 var renderer=body.GetComponent<MeshRenderer>();var original=body.GetComponent<MeshFilter>().sharedMesh;
 var decal=Array.FindIndex(renderer.sharedMaterials,m=>m.name=="MM_M135_Stickers");
 var faces=Decals(original,decal,true);if(faces.Length!=66)throw new Exception("Expected 22 feed decal triangles");
 var panel=new GameObject("Feed box decals");panel.transform.SetParent(visual.transform,false);
 var panelMesh=Object.Instantiate(original);panelMesh.subMeshCount=1;panelMesh.SetTriangles(faces,0);var dv=panelMesh.vertices;var db=new Bounds(dv[faces[0]],Vector3.zero);foreach(var id in faces)db.Encapsulate(dv[id]);panelMesh.bounds=db;
 AssetDatabase.CreateAsset(panelMesh,Folder+"feed-decals.asset");panel.AddComponent<MeshFilter>().sharedMesh=panelMesh;panel.AddComponent<MeshRenderer>().sharedMaterial=renderer.sharedMaterials[decal];
 var ix=Array.FindIndex(mats,m=>m.name=="Mag");var mesh=filter.sharedMesh;var verts=mesh.vertices;var points=mesh.GetTriangles(ix).Select(i=>visual.transform.TransformPoint(verts[i])).ToArray();
 var box=new Bounds(points[0],Vector3.zero);foreach(var point in points)box.Encapsulate(point);
 // The PAYDAY belt already includes its visible cartridges. Keep the two native
 // pooled cartridges inside the feed box, with consecutive one-based slot IDs.
 for(int id=2;id>=1;id--){var bullet=new GameObject("patron_"+id).transform;bullet.SetParent(feed.transform,false);bullet.position=box.center+Vector3.up*(id==1?.004f:-.004f);}
 var preview=feed.AddComponent<PreviewPivot>();preview.Icon=new PreviewPivot.IconSettings();preview.ApplyDefaultSettings();preview.pivotPosition=box.center;preview.Icon.hasOffset=false;preview.Icon.rotation=Quaternion.Euler(-9,116,4)*Quaternion.Inverse(feedRotation);preview.Icon.rotationEuler=preview.Icon.rotation.eulerAngles;preview.Icon.orthographic=true;Save(feed,"mag_m135_arges");
 AssetDatabase.SaveAssets();
 var builds=new[]{new AssetBundleBuild{assetBundleName="weapon_m135_arges.bundle",assetNames=new[]{AssetDatabase.GetAssetPath(prefab)}},new AssetBundleBuild{assetBundleName="mag_m135_arges.bundle",assetNames=new[]{Folder+"mag_m135_arges.prefab"}}};
 if(!BuildPipeline.BuildAssetBundles(Out,builds,BuildAssetBundleOptions.StrictMode|BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Native build failed");
 }
}
