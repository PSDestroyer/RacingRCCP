from pathlib import Path
import re,json,csv,math,sys
import numpy as np
root=Path(__file__).resolve().parents[3]
p=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parent
if (p/'RuntimeWaypoints.json').exists():
 points=np.array(json.loads((p/'RuntimeWaypoints.json').read_text()));n=len(points)
else:
 s=(root/'Assets/Formula/Scenes/Formula_Circuit_01.unity').read_text()
 parts=re.split(r'^--- !u!(\d+) &(\d+)[^\n]*\n',s,flags=re.M)
 blocks={int(parts[i+1]):(int(parts[i]),parts[i+2]) for i in range(1,len(parts),3)}
 def fid(body,key):return int(re.search(r'^  '+key+r': \{fileID: (\d+)',body,re.M).group(1))
 def vec(body,key):return [float(x) for x in re.search(r'^  '+key+r': \{([^}]+)',body,re.M).group(1).replace(' ','').split(',') for x in [x.split(':')[1]]]
 trans={fid(b,'m_GameObject'):(i,b) for i,(typ,b) in blocks.items() if typ==4 and 'm_LocalPosition:' in b}
 cache={}
 def world(i):
  if i==0:return np.eye(4)
  if i in cache:return cache[i]
  b=blocks[i][1];x,y,z,w=vec(b,'m_LocalRotation');sx,sy,sz=vec(b,'m_LocalScale')
  r=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
  m=np.eye(4);m[:3,:3]=r@np.diag([sx,sy,sz]);m[:3,3]=vec(b,'m_LocalPosition');cache[i]=world(fid(b,'m_Father'))@m;return cache[i]
 ids=[int(x) for x in re.findall(r'^  - \{fileID: (\d+)\}',blocks[330244572][1],re.M)]
 points=np.array([world(trans[fid(blocks[i][1],'m_GameObject')][0])[:3,3] for i in ids]);n=len(points)
angles=[]
for i in range(n):
 vals=[]
 for k in range(4):
  j=(i+k)%n;a=points[j]-points[(j-1)%n];b=points[(j+1)%n]-points[j];a[1]=b[1]=0
  vals.append(math.degrees(math.acos(np.clip(np.dot(a,b)/(np.linalg.norm(a)*np.linalg.norm(b)),-1,1))))
 angles.append(max(vals))
(p/'RoadGeometry.json').write_text(json.dumps({'points':points.tolist(),'cornerAngles':angles}))
rows=[]
with (p/'Telemetry.csv').open() as f:
 for row in csv.DictReader(f):
  try:r={k:float(v) for k,v in row.items()};assert len(r)>=14
  except:continue
  rows.append(r)
metrics=[]
for i in range(4):
 rr=[r for r in rows if r['racer']==i];straight=[];flips=[];windows=0;sign=0;last=-10;reversals=0
 for r in rr:
  j=int(r['checkpoint'])%n;r['roadAngle']=angles[j]
  a=points[(j-1)%n][[0,2]];b=points[j][[0,2]];v=b-a;q=np.array([r['x'],r['z']])-a
  r['signedLateral']=(v[0]*q[1]-v[1]*q[0])/np.linalg.norm(v)
  if r['speed']>=60 and r['roadAngle']<8:
   straight.append(r)
   if r['time']-last>.3:flips=[];sign=0
   if abs(r['steer'])>.12:
    new=math.copysign(1,r['steer'])
    if sign and sign!=new:flips.append(r['time']);reversals+=1
    sign=new
   flips=[t for t in flips if r['time']-t<=2]
   if len(flips)>=4:windows+=1;flips=[]
   last=r['time']
 metrics.append({'racer':i,'samples':len(rr),'straightSamples':len(straight),'wobbleWindows':windows,'steeringReversals':reversals,'straightLateralRmsMeters':float(np.sqrt(np.mean([r['signedLateral']**2 for r in straight]))) if straight else None,'maxGapMeters':max([r['gapToPlayer'] for r in rr],default=0),'recoveries':max([r['recoveries'] for r in rr],default=0)})
(p/'IndependentAnalysis.json').write_text(json.dumps({'definition':'Road geometry ahead, independent of car heading; speed >=60 km/h; four steering reversals above 0.12 amplitude within two seconds','metrics':metrics},indent=2))
print(json.dumps(metrics,indent=2))
