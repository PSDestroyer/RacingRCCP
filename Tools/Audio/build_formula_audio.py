"""Reproducible original support sounds. Requires numpy; no downloaded audio.

Engine recordings and legacy impacts remain RCCP assets. These synthesized
rolling, abrasion, UI and music layers are replaceable through SoundBank.asset.
"""
from pathlib import Path
import json, math, uuid, wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Formula/Audio/Designed'
OUT.mkdir(parents=True, exist_ok=True)
SR = 48000
rng = np.random.default_rng(20271026)
manifest = []

def write(name, data, purpose, peak=.66):
    data = np.asarray(data, dtype=float)
    data -= np.mean(data, axis=0)
    data *= peak / max(.001, np.max(np.abs(data)))
    with wave.open(str(OUT / (name + '.wav')), 'wb') as f:
        f.setparams((1 if data.ndim == 1 else data.shape[1], 2, SR, 0, 'NONE', 'not compressed'))
        f.writeframes((data * 32767).astype('<i2').tobytes())
    manifest.append(dict(name=name, seconds=round(len(data)/SR, 3), purpose=purpose, peak=peak,
                         source='Original deterministic synthesis; not a vehicle recording'))

def noise(seconds, low, high, seed):
    n = round(seconds*SR)
    random = np.random.default_rng(seed)
    freqs = np.fft.rfftfreq(n, 1/SR)
    spectral = np.fft.rfft(random.normal(size=n))
    shaping = (1 - np.exp(-(freqs/max(1, low))**4)) * np.exp(-(freqs/high)**2)
    result = np.fft.irfft(spectral*shaping, n)
    return result / max(.001, np.std(result))

for i, (name, low, high, rough) in enumerate([
    ('RollAsphalt', 40, 850, .08), ('RollKerb', 55, 1800, .3),
    ('RollGrass', 120, 2600, .16), ('RollGravel', 80, 4200, .6),
    ('RollWet', 300, 5000, .1), ('ScrapeAsphalt', 150, 5200, .35),
    ('ScrapeMetal', 650, 6800, .22), ('GarageRoom', 35, 500, .025),
    ('RainOpenAir', 250, 7000, .06), ('RollSnow', 70, 1700, .25)]):
    t = np.arange(4*SR)/SR
    signal = noise(4, low, high, 60+i)
    # Periodic modulation preserves loop seams; gameplay supplies speed/load.
    signal *= 1 + rough*np.sin(2*np.pi*7.25*t)*np.sin(2*np.pi*11.5*t)
    if name == 'ScrapeMetal': signal += .15*np.sin(2*np.pi*1570*t) + .1*np.sin(2*np.pi*2430*t)
    if name == 'GarageRoom': signal += .16*np.sin(2*np.pi*80*t)
    write(name, signal, 'Loop; gain and pitch controlled by contact telemetry', .5)

for i in range(4):
    t = np.arange(int(.24*SR))/SR
    signal = noise(.24, 55, 2400, 300+i)*np.exp(-t*32)
    signal += .75*np.sin(2*np.pi*(90+i*12)*t)*np.exp(-t*22)
    signal *= np.minimum(1, t/.0015)*np.minimum(1, (.24-t)/.012)
    write('LightImpact'+str(i+1), signal, 'Short composite/chassis contact variation', .6)
    metal = signal*.6 + (np.sin(2*np.pi*(720+i*83)*t) + .25*np.sin(2*np.pi*(1810+i*96)*t))*np.exp(-t*20)*np.minimum(1,t/.0015)
    metal[-600:]*=np.linspace(1,0,600)
    write('MetalImpact'+str(i+1),metal,'Short metal barrier contact variation',.65)
    carbon=noise(.24,180,4800,400+i)*np.exp(-t*40)*np.minimum(1,t/.0015)
    carbon+=.5*np.sin(2*np.pi*(150+i*20)*t)*np.exp(-t*30)
    carbon[-600:]*=np.linspace(1,0,600)
    write('BodyImpact'+str(i+1),carbon,'Composite panel contact variation',.65)

tones = {
    'Back': ([620,430], .13), 'Error': ([220,185], .21),
    'Purchase': ([440,660,880], .32), 'Equip': ([510,765], .2),
    'Countdown': ([760], .11), 'Go': ([1040,1560], .24),
    'LapInvalid': ([460,330], .27), 'WrongWay': ([310,430,310], .38),
    'FinalLap': ([520,780,1040], .34)
}
for name,(notes,seconds) in tones.items():
    data=np.zeros(round(seconds*SR))
    for i,freq in enumerate(notes):
        start=int(i*len(data)/len(notes)*.7)
        t=np.arange(len(data)-start)/SR
        envelope=(1-np.exp(-t*550))*np.exp(-t/(seconds*.22))
        signal=(np.sin(2*np.pi*freq*t)+.12*np.sin(2*np.pi*freq*2*t))*envelope
        data[start:]+=signal
    data[-int(.015*SR):]*=np.linspace(1,0,int(.015*SR))
    write(name,data,'Short motorsport UI/race cue',.5 if name in ('WrongWay','Error') else .6)

# Original energetic Formula instrumental; rendered offline into the same asset.
from build_formula_music import build_track
write('PaddockTheme', build_track(SR), 'Original 128 BPM Formula electronic instrumental; streamed music source', .83)
(OUT/'Provenance.json').write_text(json.dumps(manifest,indent=2)+'\n')

# Mixer YAML uses the same native attenuation-only structure as existing assets.
# Stable deterministic internal parameter ids allow repeat authoring safely.
def guid(label):return uuid.uuid5(uuid.NAMESPACE_URL,'formula-game/audio/'+label).hex
groups=['Master','Vehicle','Effects','MusicLevel','Player Engine','Opponents','Tyres','Contacts','World','UI','Music']
children={'Master':['Vehicle','Effects','MusicLevel'],'Vehicle':['Player Engine','Opponents','Tyres','Contacts'],'Effects':['World','UI'],'MusicLevel':['Music']}
ids={g:24300002+i for i,g in enumerate(groups)}
gains={'Master':-4,'Vehicle':0,'Effects':0,'MusicLevel':0,'Player Engine':-1,'Opponents':-6,'Tyres':-3,'Contacts':0,'World':-4,'UI':0,'Music':-3}
snaps=['Menu','Race','Pause','Results']
parts=['%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n']
def block(kind,id,name,body):
    parts.append(f'--- !u!{kind} &{id}\n{name}:\n  m_ObjectHideFlags: {0 if kind == 241 else 3}\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n'+body)
snaprefs=''.join(f'  - {{fileID: {24500006+i}}}\n' for i in range(len(snaps)))
params=''.join(f'  - guid: {guid(g+"volume")}\n    name: {p}\n' for g,p in [('Vehicle','VehicleVolume'),('Effects','EffectsVolume'),('MusicLevel','MusicVolume')])
block(241,24100000,'AudioMixerController',f'  m_Name: Formula Mixer\n  m_OutputGroup: {{fileID: 0}}\n  m_MasterGroup: {{fileID: 24300002}}\n  m_Snapshots:\n{snaprefs}  m_StartSnapshot: {{fileID: 24500006}}\n  m_SuspendThreshold: -80\n  m_EnableSuspend: 1\n  m_UpdateMode: 1\n  m_ExposedParameters:\n{params}  m_AudioMixerGroupViews:\n  - guids:\n'+''.join(f'    - {guid(g)}\n' for g in groups)+'    name: View\n  m_CurrentViewIndex: 0\n  m_TargetSnapshot: {fileID: 24500006}\n')
for i,g in enumerate(groups):
    ch=children.get(g,[])
    cs='  m_Children:\n'+''.join(f'  - {{fileID: {ids[c]}}}\n' for c in ch) if ch else '  m_Children: []\n'
    block(243,ids[g],'AudioMixerGroupController',f'  m_Name: {g}\n  m_AudioMixer: {{fileID: 24100000}}\n  m_GroupID: {guid(g)}\n{cs}  m_Volume: {guid(g+"volume")}\n  m_Pitch: {guid(g+"pitch")}\n  m_Send: '+ '0'*32+f'\n  m_Effects:\n  - {{fileID: {24400004+i}}}\n  m_UserColorIndex: 0\n  m_Mute: 0\n  m_Solo: 0\n  m_BypassEffects: 0\n')
    block(244,24400004+i,'AudioMixerEffectController',f'  m_Name: \n  m_EffectID: {guid(g+"attenuation")}\n  m_EffectName: Attenuation\n  m_MixLevel: {guid(g+"mixlevel")}\n  m_Parameters: []\n  m_SendTarget: {{fileID: 0}}\n  m_EnableWetMix: 0\n  m_Bypass: 0\n')
for i,s in enumerate(snaps):
    vals=gains.copy()
    if s=='Menu':
        for g in ('Player Engine','Opponents','Tyres','Contacts'):vals[g]=-80
    if s=='Results':
        vals['Player Engine']=-12;vals['Opponents']=-20;vals['Tyres']=-18;vals['Contacts']=-12
    block(245,24500006+i,'AudioMixerSnapshotController',f'  m_Name: {s}\n  m_AudioMixer: {{fileID: 24100000}}\n  m_SnapshotID: {guid(s+"snapshot")}\n  m_FloatValues:\n'+''.join(f'    {guid(g+"volume")}: {v}\n' for g,v in vals.items())+'  m_TransitionOverrides: {}\n')
path=ROOT/'Assets/Formula/Audio/Formula Mixer.mixer'
if not path.exists():path.write_text(''.join(parts))
print(json.dumps({'clips':len(manifest),'seconds':sum(m['seconds'] for m in manifest),'mixer':str(path)}))
