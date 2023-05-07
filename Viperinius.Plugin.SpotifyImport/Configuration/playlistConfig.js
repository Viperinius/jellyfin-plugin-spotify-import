const apiQueryOpts = {};

export default function (view) {
    view.dispatchEvent(new CustomEvent('create'));

    var SpotifyImportConfig = {
        pluginUniqueId: 'F03D0ADB-289F-4986-BD6F-2468025249B3',
        pluginApiBaseUrl: 'Viperinius.Plugin.SpotifyImport'
    };

    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();

        apiQueryOpts.UserId = Dashboard.getCurrentUserId();
        apiQueryOpts.api_key = ApiClient.accessToken();

        ApiClient.getPluginConfiguration(SpotifyImportConfig.pluginUniqueId).then(function (config) {
            document.querySelector('#SpotifyAuthRedirectUri').innerText = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/SpotifyAuthCallback');
            if (config.SpotifyAuthToken && 'CreatedAt' in config.SpotifyAuthToken) {
                document.querySelector('#authSpotifyAlreadyDesc').classList.remove('hide');
                document.querySelector('#authSpotifyCreatedAt').innerText = config.SpotifyAuthToken['CreatedAt'].split('T')[0];
            }

            document.querySelector('#EnableVerboseLogging').checked = config.EnableVerboseLogging;
            document.querySelector('#SpotifyClientId').value = config.SpotifyClientId;

            if (config.PlaylistIds) {
                document.querySelector('#SpotifyPlaylists').value = config.PlaylistIds.join('\n');
            }

            document.querySelector('#GenerateMissingTrackLists').checked = config.GenerateMissingTrackLists;
            document.querySelector('#MissingTrackListsDateFormat').value = config.MissingTrackListsDateFormat;
            if (config.GenerateMissingTrackLists && config.MissingTrackListPaths && config.MissingTrackListPaths.length) {
                let missingTracksHtml = '';
                missingTracksHtml += '<div class="paperList">';
                missingTracksHtml += config.MissingTrackListPaths.map(function(path) {
                    const fileName = path.split('\\').pop().split('/').pop();
                    const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/MissingTracksFile', {
                        name: fileName,
                        'api_key': ApiClient.accessToken()
                    });
                    let pathHtml = '';
                    pathHtml += '<a is="emby-linkbutton" href="' + apiUrl + '" target="_blank" class="listItem listItem-border" style="color:inherit;">';
                    pathHtml += '<div class="listItemBody two-line">';
                    pathHtml += "<h3 class='listItemBodyText' dir='ltr' style='text-align: left'>" + fileName + '</h3>';
                    pathHtml += '<div class="listItemBodyText secondary"></div>';
                    pathHtml += '</div>';
                    pathHtml += '</a>';
                    return pathHtml;
                }).join('');
                missingTracksHtml += '</div>';
                document.querySelector('.missingTracks').innerHTML = missingTracksHtml;

                const clearFilesButton = document.querySelector('#removeMissingTracksFiles');
                clearFilesButton.classList.remove('hide');
                clearFilesButton.addEventListener('click', function () {
                    const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/MissingTracksFile', {
                        'api_key': ApiClient.accessToken()
                    });
                    fetch(apiUrl, { method: 'DELETE' }).then(function (res) {
                        if (!res || !res.ok) {
                            throw "invalid response";
                        }
                        document.querySelector('.missingTracks').innerHTML = 'None';
                    }).catch(function (error) {
                        console.error(error);
                    });
                });
            }

            Dashboard.hideLoadingMsg();
        });

        

    });

    document.querySelector('#SpotifyImportConfigForm').addEventListener('submit', function(e) {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(SpotifyImportConfig.pluginUniqueId).then(function (config) {
            config.EnableVerboseLogging = document.querySelector('#EnableVerboseLogging').checked;
            config.SpotifyClientId = document.querySelector('#SpotifyClientId').value;

            let match;
            config.PlaylistIds = [];

            // match a given spotify id with or without a prepended url or uri part
            const playlistIdRegex = /^(https?:\/\/open.spotify.com\/playlist\/|spotify:playlist:)?([a-zA-Z0-9]+)$/gm;

            while ((match = playlistIdRegex.exec(document.querySelector('#SpotifyPlaylists').value)) !== null) {
                if (match.index === playlistIdRegex.lastIndex) {
                    playlistIdRegex.lastIndex++;
                }

                if (match.length > 2) {
                    config.PlaylistIds.push(match[2]);
                }
            }

            config.GenerateMissingTrackLists = document.querySelector('#GenerateMissingTrackLists').checked;
            config.MissingTrackListsDateFormat = document.querySelector('#MissingTrackListsDateFormat').value;

            ApiClient.updatePluginConfiguration(SpotifyImportConfig.pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });

        e.preventDefault();
        return false;
    });

    document.querySelector('#authSpotify').addEventListener('click', function () {
        const fullAuthUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/SpotifyAuth', {
            'baseUrl': ApiClient._serverAddress,
            'api_key': ApiClient.accessToken()
        });

        fetch(fullAuthUrl, { method: 'POST' }).then(function (res) {
            if (!res || !res.ok) {
                throw "invalid response";
            }

            res.json().then(function (json) {
                if (!json || !('login_req_uri' in json)) {
                    throw "invalid json response";
                }

                window.open(json['login_req_uri'], '_self');
            });
        }).catch(function (error) {
            console.error(error);
        });
    });
}