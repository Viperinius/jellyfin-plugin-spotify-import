#!/usr/bin/env python3
import math
import os
import struct
import subprocess
import wave

def _create_sine(f: float = 440.0, ampl: float = 0.5, sample_rate: int = 44100, length_ms: int = 500):
    ampl = max(min(ampl, 1.0), 0.0)
    sample_count = sample_rate / 1000.0 * length_ms
    for ii in range(int(sample_count)):
        yield ampl * math.sin(2 * math.pi * f * (ii / sample_rate))

def _gen_wav(path: str, length_ms: int, sample_rate: int):
    channel_count = 1
    sample_width = 2

    data = list(_create_sine(sample_rate=sample_rate, length_ms=length_ms))
    frame_count = len(data)

    with wave.open(path, 'w') as f:
        f.setparams((channel_count, sample_width, sample_rate, frame_count, 'NONE', 'not compressed'))

        for d in data:
            f.writeframes(struct.pack('h', int(d * 32767.0)))

def _convert_wav_to_mp3(ffmpeg_path: str, wav_path: str, mp3_path: str, sample_rate: int):
    real_ffmpeg = os.path.realpath(ffmpeg_path)
    real_wav = os.path.realpath(wav_path)
    real_mp3 = os.path.realpath(mp3_path)
    cmd = [real_ffmpeg, '-i', real_wav, '-vn', '-ar', str(sample_rate), '-ac', '1', '-b:a', '192k', real_mp3]
    print(cmd)
    return subprocess.run(cmd).returncode

def gen_dummy_mp3(ffmpeg_path: str, mp3_path: str):
    if os.path.exists(mp3_path):
        return

    sample_rate = 44100
    ms = 3 * 1000

    tmp_wav_path = os.path.join(os.path.dirname(os.path.realpath(mp3_path)), '__tmp.wav')
    print(tmp_wav_path)
    _gen_wav(tmp_wav_path, ms, sample_rate)
    if _convert_wav_to_mp3(ffmpeg_path, tmp_wav_path, mp3_path, sample_rate) != 0:
        print('failed to convert wav to mp3')
        return
    os.remove(tmp_wav_path)
