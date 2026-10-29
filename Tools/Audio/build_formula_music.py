"""Original 128 BPM instrumental, arranged for a Formula racing game.

No downloaded samples. All voices are rendered offline; the player streams one
stereo clip through the existing music source. Requires numpy.
"""
from pathlib import Path
import json
import math
import wave
import numpy as np

BPM = 128
BARS = 64


def build_track(sample_rate=48000):
    beat = 60 / BPM
    count = round(BARS * 4 * beat * sample_rate)
    rhythm = np.zeros((count, 2), dtype=np.float32)
    instruments = np.zeros_like(rhythm)
    random = np.random.default_rng(30102026)

    def add(bus, signal, start, gain=1, pan=0):
        signal = np.asarray(signal, dtype=np.float32)
        start = round(start * sample_rate) % count
        for channel, level in enumerate((math.sqrt((1-pan)/2), math.sqrt((1+pan)/2))):
            first = min(len(signal), count-start)
            bus[start:start+first, channel] += signal[:first] * gain * level
            if first < len(signal):
                bus[:len(signal)-first, channel] += signal[first:] * gain * level

    def frequency(note):
        return 440 * 2 ** ((note-69)/12)

    def synth(note, seconds, voice):
        t = np.arange(round(seconds * sample_rate)) / sample_rate
        f = frequency(note)
        if voice == 'bass':
            signal = np.sin(2*np.pi*f*t) + .32*np.sin(2*np.pi*f*2*t) + .14*np.sin(2*np.pi*f*3*t)
            signal = np.tanh(signal*1.4) * np.minimum(1, t/.006) * np.minimum(1, (seconds-t)/.045)
        elif voice == 'pad':
            signal = np.sin(2*np.pi*f*t) + .18*np.sin(2*np.pi*f*2*t)
            signal *= (1-np.exp(-t*9)) * np.minimum(1, (seconds-t)/.45)
        else:
            signal = np.zeros_like(t)
            for harmonic in range(1, 7):
                signal += np.sin(2*np.pi*f*harmonic*t) / harmonic**1.65
            signal *= np.minimum(1, t/.004) * np.exp(-t*(5 if voice == 'arp' else 2.5))
            signal *= np.minimum(1, (seconds-t)/.07)
        return signal

    def noise(seconds, low, high):
        n = round(seconds * sample_rate)
        raw = random.normal(size=n)
        freq = np.fft.rfftfreq(n, 1/sample_rate)
        mask = (1-np.exp(-(freq/low)**4)) * np.exp(-(freq/high)**4)
        filtered = np.fft.irfft(np.fft.rfft(raw)*mask, n)
        return filtered / max(.001, np.std(filtered))

    t = np.arange(round(.4*sample_rate))/sample_rate
    kick = np.sin(2*np.pi*(49*t + 112*.018*(1-np.exp(-t/.018)))) * np.exp(-t*13)
    kick += noise(.4, 1800, 6500) * np.exp(-t*150) * .045
    kick *= np.minimum(1, t/.001) * np.minimum(1, (.4-t)/.012)
    t = np.arange(round(.22*sample_rate))/sample_rate
    clap = noise(.22, 850, 8500) * (np.exp(-t*30)+.55*np.exp(-np.maximum(0,t-.013)*40)*(t>.013))
    clap *= np.minimum(1, t/.001) * np.minimum(1, (.22-t)/.018)
    t = np.arange(round(.08*sample_rate))/sample_rate
    hat = noise(.08, 5000, 12500)*np.exp(-t*65)*np.minimum(1,t/.0008)
    t = np.arange(round(.25*sample_rate))/sample_rate
    open_hat = noise(.25, 4200, 11500)*np.exp(-t*18)*np.minimum(1,t/.001)*np.minimum(1,(.25-t)/.025)
    roots = [33, 29, 36, 31]
    chords = [[57,60,64], [53,57,60], [55,60,64], [55,59,62]]
    motif = [76, 79, 81, 84, 83, 79, 76, 74]
    for bar in range(BARS):
        start = bar*4*beat
        # Intro/groove, first lift, breathing section, then the full final lift.
        breakdown = 32 <= bar < 40
        lift = 16 <= bar < 32 or 44 <= bar < 60
        intro_outro = bar < 8 or bar >= 60
        chord_index = (bar//2)%4
        root = roots[chord_index]
        if not breakdown:
            for b in range(4):
                add(rhythm, kick, start+b*beat, .66)
                if b%2: add(rhythm, clap, start+b*beat, .10, -.08 if b==1 else .08)
                add(rhythm, hat, start+(b+.5)*beat, .045, .22 if b%2 else -.22)
                if lift: add(rhythm, open_hat, start+(b+.5)*beat, .026, -.28)
            if bar%8 == 7:
                for n in range(4): add(rhythm, clap, start+(3+n/4)*beat, .035+n*.008, .15)
        else:
            for b in [0, 2]: add(rhythm, hat, start+(b+.5)*beat, .018, .25)
        bass_gain = .14 if breakdown else .27
        for b in range(4):
            add(instruments, synth(root+(12 if b==3 and bar%2 else 0), beat*.68, 'bass'), start+(b+.5)*beat, bass_gain)
            if lift and b in (1,3): add(instruments, synth(root, beat*.22, 'bass'), start+(b+.25)*beat, .12)
        for n, note in enumerate(chords[chord_index]):
            add(instruments, synth(note, 4*beat+.45, 'pad'), start, .035 if breakdown else .021, (n-1)*.4)
        if not intro_outro:
            for step in range(8):
                note = chords[chord_index][(step+bar)%3] + 12
                add(instruments, synth(note, .38, 'arp'), start+step*beat*.5, .05 if lift else .028, -.48 if step%2 else .48)
        if lift:
            for b in range(4):
                note = motif[(bar%2)*4+b]
                phrase = synth(note, beat*.82, 'lead')
                add(instruments, phrase, start+(b+.125)*beat, .075, -.12)
                add(instruments, phrase, start+(b+.875)*beat, .018, .45)
        if bar in (15, 43):
            seconds = 4*beat
            t = np.arange(round(seconds*sample_rate))/sample_rate
            rise = noise(seconds, 900, 10000)*(t/seconds)**2*np.minimum(1,(seconds-t)/.03)
            add(rhythm, rise, start, .025)

    phase = (np.arange(count)/sample_rate) % beat
    # Offline sidechain leaves room for the kick; no runtime DSP is required.
    # Periodic with matching value/slope at the beat boundary: a sawtooth
    # release envelope would click against sustained voices on every kick.
    sidechain = .58+.42*(1-np.exp(-(np.sin(np.pi*phase/beat)/.42)**2))
    instruments *= sidechain[:,None]
    wet = np.zeros_like(instruments)
    for beats, gain in [(0.75,.09),(1.5,.045),(2.25,.025)]:
        wet += np.roll(instruments[:,::-1],round(beats*beat*sample_rate),axis=0)*gain
    result = np.tanh((rhythm+instruments+wet)*1.15)
    result -= result.mean(axis=0)
    result *= .83/max(.001,float(np.max(np.abs(result))))
    return result.astype(np.float32)


def main():
    root = Path(__file__).resolve().parents[2]
    target = root/'Assets/Formula/Audio/Designed/PaddockTheme.wav'
    signal = build_track()
    with wave.open(str(target),'wb') as output:
        output.setparams((2,2,48000,0,'NONE','not compressed'))
        output.writeframes((signal*32767).astype('<i2').tobytes())
    metadata = dict(name='PaddockTheme',title='Grid Pulse',bpm=BPM,bars=BARS,seconds=len(signal)/48000,
        purpose='Original energetic electronic instrumental for Formula menus and quiet race accompaniment',
        source='Deterministic offline synthesis; no external recordings or samples',peak=float(np.max(np.abs(signal))),
        rms=float(np.sqrt(np.mean(signal**2))),runtime='One streamed stereo music voice on the existing source; no additional AudioSource objects or runtime synthesis')
    provenance = target.with_name('Provenance.json')
    rows = json.loads(provenance.read_text())
    rows = [metadata if row.get('name')=='PaddockTheme' else row for row in rows]
    provenance.write_text(json.dumps(rows,indent=2)+'\n')
    report = root/'Docs/AI/FormulaPolish/Music.json';report.parent.mkdir(parents=True,exist_ok=True)
    report.write_text(json.dumps(metadata,indent=2)+'\n')
    print(json.dumps(metadata,indent=2))


if __name__ == '__main__':
    main()
