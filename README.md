<h1 align="center">Jellyfin Spotify Import Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.org/">Jellyfin Project</a></h3>

<div align="center">
<img alt="Logo" src="viperinius-plugin-spotifyimport.png" />
<br>
<br>
<a href="https://github.com/Viperinius/jellyfin-plugin-spotify-import">
<img alt="GPL-3.0 license" src="https://img.shields.io/github/license/Viperinius/jellyfin-plugin-spotify-import" />
</a>
<a href="https://github.com/Viperinius/jellyfin-plugin-spotify-import/releases">
<img alt="Current release" src="https://img.shields.io/github/release/Viperinius/jellyfin-plugin-spotify-import" />
</a>
</div>

## About


## Installation

Link to the repository manifest to get the plugin to show up in your catalogue:
```
https://raw.githubusercontent.com/Viperinius/jellyfin-plugins/master/manifest.json
```

[See the official documentation for install instructions](https://jellyfin.org/docs/general/server/plugins/index.html#installing).

## How to


## To do

A few things that are not implemented yet:

- Use PKCE / code authorisation instead of Client ID / Secret to avoid storing the secret in clear text
- Expand configuration to allow more customisation than just pasting in Spotify IDs
    - Set name of the Jellyfin playlist
    - Specify target user that owns the Jellyfin playlist
