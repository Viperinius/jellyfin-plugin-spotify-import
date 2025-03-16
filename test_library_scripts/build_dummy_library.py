#!/usr/bin/env python3
import glob
import json
import mutagen.easyid3 as easyid3
import mutagen.mp3 as mp3
import os
import re
import shutil
import tempfile
from dummy_mp3 import gen_dummy_mp3

class faker:
    def __init__(self, library_files, source_file) -> None:
        self.dummy_path = source_file
        self.libs_json_paths = library_files
        self.current_lib_index = 0

    def sanitise_name(self, input):
        return re.sub(r'[^\w_\-() ]', '', input)

    def exec(self, base_lib_path, track):
        album_path = os.path.join(base_lib_path, self.sanitise_name(track['ArtistNames'][0]), self.sanitise_name(track['AlbumName'])).strip()
        os.makedirs(album_path, exist_ok=True)
        track_path = os.path.join(album_path, self.sanitise_name(track['Name']) + '.mp3')
        shutil.copyfile(self.dummy_path, track_path)

        mp3_file = mp3.MP3(track_path, ID3=easyid3.EasyID3)
        mp3_file['title'] = [track['Name']]
        mp3_file['album'] = [track['AlbumName']]
        mp3_file['albumartist'] = track['AlbumArtistNames']
        mp3_file['artist'] = track['ArtistNames']
        mp3_file.save()

    def next_json(self, base_lib_path):
        if self.current_lib_index >= len(self.libs_json_paths):
            return False
        
        json_content = None
        with open(self.libs_json_paths[self.current_lib_index], 'r', encoding='utf-8') as f:
            json_content = json.load(f)
        if json_content is None:
            return False
        
        for track in json_content:
            self.exec(base_lib_path, track)

        self.current_lib_index += 1
        return True

if __name__ == '__main__':
    this_path = os.path.dirname(os.path.abspath(__file__))
    ffmpeg_path = f'{this_path}/../jellyfin_srv_/runtime_10.10.3/ffmpeg.exe'
    sample_mp3_path = f'{this_path}/sample.mp3'

    jf_library_path = r'Z:\dev\jellyfin_test_libs\gen_music_1'
    missing_tracks_path = os.path.join(tempfile.gettempdir(), 'jfplugin_spotify_import')

    # create dummy if not present
    gen_dummy_mp3(ffmpeg_path, sample_mp3_path)

    # go through missing track lists and create fake files for their entries
    if True:
        faky = faker(glob.glob(f'{missing_tracks_path}/*.json'), sample_mp3_path)
        cont = True
        while cont:
            cont = faky.next_json(jf_library_path)

    ### OR: manual mode, add specific items: ###
    if False:
        faky = faker([], sample_mp3_path)
        faky.exec(jf_library_path, {
            "Name": "You Make My Dreams (Come True)",
            "AlbumName": "Voices",
            "AlbumArtistNames": ["Daryl Hall & John Oates"],
            "ArtistNames": ["Daryl Hall & John Oates"]
        })
