from pathlib import Path
import wave,subprocess,random
import numpy as np
import argparse
parser=argparse.ArgumentParser(description="Render the Vulcan fire banks from extracted game audio.")
parser.add_argument("root",type=Path,help="Directory containing the five decoded shot WAVs and full/ with streamed WEMs")
parser.add_argument("audio",type=Path,help="Unity project Assets/HawksArges/Audio directory")
parser.add_argument("decoder",help="Path to vgmstream-cli")
args=parser.parse_args()
root,audio,vg=args.root,args.audio,args.decoder
def read(path):
 with wave.open(str(path),"rb") as f:
  rate=f.getframerate();ch=f.getnchannels();a=np.frombuffer(f.readframes(f.getnframes()),dtype="<i2").reshape(-1,ch)/32768.
 if ch==1:a=np.repeat(a,2,axis=1)
 return a,rate
def convert(a,n):
 return np.stack([np.interp(np.arange(n)*len(a)/n,np.arange(len(a)),c) for c in a.T],axis=1)
def stream(id):
 p=next((root/"full").rglob(str(id)+".wem"));dest=root/(str(id)+"-full.wav")
 subprocess.run([vg,"-i","-o",str(dest),str(p)],check=True,stdout=subprocess.DEVNULL)
 a,rate=read(dest);return convert(a,round(len(a)*44100/rate))
def save(p,a):
 with wave.open(str(p),"wb") as f:
  f.setparams((2,2,44100,0,"NONE",""));f.writeframes(np.rint(np.clip(a,-1,.99996)*32768).astype("<i2").tobytes())
core=[read(root/(id+".wav"))[0] for id in ("230308582","250440446","654771294","798331966","813916260")]
def fire(id):
 bed=stream(id);beats=round(len(bed)/len(core[0]));rng=random.Random(id);seq=[]
 for i in range(beats):
  seq.append(rng.choice([j for j in range(5) if j not in seq[-2:]]))
 dry=convert(np.concatenate([core[j] for j in seq]),len(bed))
 return dry+bed
def tail(id):
 echo=stream(id)*10**(-5/20);stop=stream(528456961)*10**(-1/20)
 a=np.zeros((max(len(echo),len(stop)),2));a[:len(echo)]+=echo;a[:len(stop)]+=stop
 return a
parts=[fire(28055691),fire(177245922),tail(92317016),tail(238938844)]
gain=.96/max(abs(a).max() for a in parts)
for name,a in zip(["auto-a","auto-b","release-a","release-b"],parts):
 save(audio/("pd2-vulcan-"+name+".wav"),a*gain)
# Native fire starts both Wwise playback branches together. Hold across two seams, then release.
save(root/"Vulcan-full-cycle.wav",np.concatenate([parts[0],parts[0],parts[0][:16384],parts[2]])*gain)
print("samples",[len(a) for a in parts],"shared gain",gain)
