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

This plugin enables you to import playlists from Spotify to your Jellyfin server automatically. It provides a scheduled task that queries a given list of Spotify playlists and tries to recreate them as best as possible in Jellyfin.

The playlist will be created with the same name, description and image as configured in Spotify. Any matching songs that exist on your Jellyfin server will be added to this new playlist or to an already existing playlist with this name.

If desired, the plugin can create a JSON file per playlist containing a list of any missing tracks that are part of the Spotify playlist but are not present on your server.

This is still work in progress, see [below](#to-do) for more details what might be coming.


## Installation

Link to the repository manifest to get the plugin to show up in your catalogue:
```
https://raw.githubusercontent.com/Viperinius/jellyfin-plugins/master/manifest.json
```

[See the official documentation for install instructions](https://jellyfin.org/docs/general/server/plugins/index.html#installing).

## How to

### Prerequisites

To connect to Spotify, you need to be authenticated. In order to do this, the plugin needs to know a `Client ID` and will ask you for authorisation (needed for reading private or collaborative playlists).

This procedure needs a little bit of setup from your end (basically the same as described in the official [docs](https://developer.spotify.com/documentation/web-api/concepts/apps)):
1. Head over to the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard) and sign in
2. Create an app (name and description do not matter really, pick whatever you want)
3. Copy the Client ID, you will need it in a second when configuring the plugin
4. Go to the `Settings` page
5. There, add a redirect URL in `Redirect URIs`. This URL is called after you grant the plugin read access to your playlists and must be the following value:\
   `https://<YOUR JELLYFIN IP OR DOMAIN>/Viperinius.Plugin.SpotifyImport/SpotifyAuthCallback`, e.g. `http://localhost:8096/Viperinius.Plugin.SpotifyImport/SpotifyAuthCallback`\
   The IP or domain must match the one you are using when configuring the plugin
6. Save the settings

### Get started

After installing the plugin, visit its configuration page and add your Spotify Client ID (save afterwards) and click on `Authorize`. You will be redirected to Spotify to grant access. The plugin requests access to these scopes:
- Read private playlists
- Read collaborative playlists

When the authorisation is done, you can continue with the plugin configuration page.

Go to the section `Playlist Configuration` and click on `Add new playlist`. This creates a new row with three fields:
- `Spotify ID`: Paste the identifier of the Spotify playlists that you want to import in here.
- `Target Name`: Jellyfin playlist name. Keep this empty if the original name from Spotify should be used.
- `Target User`: If you want to set another user as the playlist owner, select them here.

Following "Spotify ID" formats work:
- The raw ID, e.g. `4cOdK2wGLETKBW3PvgPWqT`
- The Spotify URI, e.g. `spotify:playlist:4cOdK2wGLETKBW3PvgPWqT`
- The full Spotify playlist URL, e.g. `https://open.spotify.com/playlist/4cOdK2wGLETKBW3PvgPWqT`

When done, save these settings and you're set. The plugin will do its thing periodically (default: daily at 03:00).
If you want to change this or want to let it run immediately, head to the scheduled tasks page and look for the task `Import Spotify playlists`.

### Track match tweaking

By default, the plugin will accept a Jellyfin track as equal to a Spotify track if these conditions are met:
- Same track name
- Same album name
- Jellyfin album artists contain Spotify album artist
- Jellyfin artists contain Spotify artist

If you experience issues with tracks not matching even if they exist, you can "relax" these settings:

1. `Match Type` determines how strict the individual comparison is (e.g. if case differences are ignored)
2. `Enable * comparison` fully enables or disables the comparison of the respective condition

## To do

A few things that are not implemented yet:

- Allow (optional) synchronisation by also removing items that are not present in the Spotify playlist
