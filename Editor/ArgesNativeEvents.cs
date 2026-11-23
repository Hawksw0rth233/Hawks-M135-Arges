using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using AnimationEventSystem;

public static class ArgesNativeEvents {
 const string Root="Assets/HawksArgesNative/";
 public static void Bind(AnimatorController controller,WeaponSoundPlayer player,Dictionary<string,AnimationClip> replacements){
  var actions=new Dictionary<string,string>{{"ppsh41_out_to_idle","Equip"},{"ppsh41_look","Inspect"}};
  var cueTable=Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,Cue[]>>(File.ReadAllText("E:/SPT Mod Workspace/PAYDAY3 Minigun/SPT-build/anim-audio.json"));
  var states=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).OfType<AnimatorState>().ToArray();
  var baseEvents=states.SelectMany(s=>s.behaviours).OfType<AnimationEventsStateBehaviour>().First(x=>x.EventsData).EventsData;
  var events=UnityEngine.Object.Instantiate(baseEvents);AssetDatabase.CreateAsset(events,Root+"Events.asset");
  foreach(var state in states)foreach(var b in state.behaviours.OfType<AnimationEventsStateBehaviour>()){b.EventsData=events;EditorUtility.SetDirty(b);}
  player.AdditionalSounds.Clear();player.MainSounds=Array.Empty<BaseSoundPlayer.SoundElement>();
  var data=new SerializedObject(events);var lists=data.FindProperty("_stateHashToEventsCollection");var prefix="Snd";
  foreach(var pair in replacements){
   var old=pair.Key;var clip=pair.Value;
   foreach(var state in states.Where(x=>(x.motion is AnimationClip c && (c==clip||c.name==old)) || (old=="ppsh41_out_to_idle" && x.name=="OUT TO IDLE ARM") || (old=="ppsh41_idle_to_out" && x.name=="FAST IDLE TO OUT"))){
    state.motion=clip;EditorUtility.SetDirty(state);
    if(!actions.TryGetValue(old,out var act))continue;
    var beh=state.behaviours.OfType<AnimationEventsStateBehaviour>().FirstOrDefault();
    if(!beh){beh=state.AddStateMachineBehaviour<AnimationEventsStateBehaviour>();beh.FullName="Hands."+state.name;beh.FullNameHash=Animator.StringToHash(beh.FullName);beh.EventsData=events;beh.EventsListId=lists.arraySize;lists.arraySize++;lists.GetArrayElementAtIndex(beh.EventsListId).FindPropertyRelative("_animationEvents").ClearArray();}
    var items=lists.GetArrayElementAtIndex(beh.EventsListId).FindPropertyRelative("_animationEvents");
    for(int i=items.arraySize-1;i>=0;i--)if(items.GetArrayElementAtIndex(i).FindPropertyRelative("_functionName").stringValue=="Sound")items.DeleteArrayElementAtIndex(i);
    int index=0;foreach(var cue in cueTable[act]){
     string key="Arges"+act+index++;var media=cue.media.Select(n=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/HawksArges/Audio/"+n)).ToArray();
     if(media.Any(x=>!x))throw new Exception("Missing "+key);
     if(!player.AdditionalSounds.Any(s=>s.EventName==prefix+key))player.AdditionalSounds.Add(new BaseSoundPlayer.SoundElement{EventName=prefix+key,SoundClips=media});
     int pos=0;while(pos<items.arraySize&&items.GetArrayElementAtIndex(pos).FindPropertyRelative("_time").floatValue<cue.seconds/clip.length)pos++;
     items.InsertArrayElementAtIndex(pos);var e=items.GetArrayElementAtIndex(pos);
     e.FindPropertyRelative("_functionName").stringValue="Sound";e.FindPropertyRelative("_functionNameHash").intValue=1554795451;e.FindPropertyRelative("Enabled").boolValue=true;
     e.FindPropertyRelative("_time").floatValue=cue.seconds/clip.length;e.FindPropertyRelative("EventConditions").ClearArray();
     var p=e.FindPropertyRelative("Parameter");p.FindPropertyRelative("StringParam").stringValue=key;p.FindPropertyRelative("ParamType").intValue=3;
    }EditorUtility.SetDirty(beh);
   }
  }
  data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
 }
 public sealed class Cue {public float seconds;public string[] media;}
}
