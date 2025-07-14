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
            if (config.EnableVerboseLogging) {
                document.querySelector('#dbgSection').classList.remove('hide');
            }

            const redirectUrl = new URL(ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/SpotifyAuthCallback'));
            if (redirectUrl.hostname == 'localhost') {
                // spotify forbids "localhost" in redirect URIs, only IP allowed
                redirectUrl.hostname = '127.0.0.1';
            }

            document.querySelector('#SpotifyAuthRedirectUri').innerText = redirectUrl.href;
            if (config.SpotifyAuthToken && 'CreatedAt' in config.SpotifyAuthToken) {
                document.querySelector('#authSpotifyAlreadyDesc').classList.remove('hide');
                document.querySelector('#authSpotifyCreatedAt').innerText = config.SpotifyAuthToken['CreatedAt'].split('T')[0];
            }

            document.querySelector('#EnableVerboseLogging').checked = config.EnableVerboseLogging;
            document.querySelector('#ShowCompletenessInformation').checked = config.ShowCompletenessInformation;
            document.querySelector('#SpotifyClientId').value = config.SpotifyClientId;
            document.querySelector('#SpotifyCookie').value = config.SpotifyCookie;
            // Note: SpotifyOAuthTokenJson removed - textarea will be empty by default

            document.querySelector('#GenerateMissingTrackLists').checked = config.GenerateMissingTrackLists;
            document.querySelector('#MissingTrackListsDateFormat').value = config.MissingTrackListsDateFormat;
            document.querySelector('#KeepMissingTrackLists').checked = config.KeepMissingTrackLists;
            if (config.GenerateMissingTrackLists && config.MissingTrackListPaths && config.MissingTrackListPaths.length) {
                let missingTracksHtml = '';
                missingTracksHtml += '<div class="paperList">';
                missingTracksHtml += config.MissingTrackListPaths.map(function (path) {
                    const fileName = path.split('\\').pop().split('/').pop();
                    const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/MissingTracksFile', {
                        name: fileName,
                        'api_key': apiQueryOpts.api_key
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
                    const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/MissingTracksFile');
                    ApiClient.fetch({
                        url: apiUrl,
                        type: 'DELETE'
                    }, true).then(function (res) {
                        if (!res || !res.ok) {
                            throw "invalid response";
                        }
                        document.querySelector('.missingTracks').innerHTML = 'None';
                    }).catch(function (error) {
                        console.error(error);
                    });
                });
            }

            document.querySelector('#ItemMatchLevel').value = config.ItemMatchLevel;
            document.querySelector('#FuzzyMaxDiff').value = config.MaxFuzzyCharDifference;
            mapItemMatchCriteriaToCheckboxes(config);
            document.querySelector('#UseLegacyMatching').checked = config.UseLegacyMatching;

            ApiClient.getJSON(ApiClient.getUrl('Users'), apiQueryOpts).then(function (result) {
                users.length = 0;
                result.forEach(user => {
                    users.push({
                        name: user['Name'],
                        id: user['Id'],
                        isAdmin: user['Policy']['IsAdministrator']
                    })
                });
                users.sort((a, b) => b.isAdmin - a.isAdmin);

                loadPlaylistTable(view, config);
                loadUsersTable(view, config);

                Dashboard.hideLoadingMsg();
            });
        });
    });

    document.querySelector('#SpotifyImportConfigForm').addEventListener('submit', function (e) {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(SpotifyImportConfig.pluginUniqueId).then(function (config) {
            config.EnableVerboseLogging = document.querySelector('#EnableVerboseLogging').checked;
            config.ShowCompletenessInformation = document.querySelector('#ShowCompletenessInformation').checked;
            config.SpotifyClientId = document.querySelector('#SpotifyClientId').value;
            config.SpotifyCookie = document.querySelector('#SpotifyCookie').value;
            // Note: SpotifyOAuthTokenJson removed - not saved to config anymore

            config.Playlists = [];
            const playlists = getPlaylistTableData(view) || [];

            playlists.forEach(pl => {
                // match a given spotify id with or without a prepended url or uri part, ignore url params
                const match = /^(https?:\/\/open\.spotify\.com\/playlist\/|spotify:playlist:)?([a-zA-Z0-9]+)(\?si=.*)?$/gm.exec(pl.Id);
                if (match !== null && match.length > 2) {
                    pl.Id = match[2];
                    config.Playlists.push(pl);
                }
            });

            config.Users = [];
            const users = getUsersTableData(view) || [];

            users.forEach(user => {
                // match a given spotify id with or without a prepended url or uri part, ignore url params
                const match = /^(https?:\/\/open\.spotify\.com\/user\/|spotify:user:)?([^/?\s]+)(\?si=.*)?$/gm.exec(user.Id);
                if (match !== null && match.length > 2) {
                    user.Id = match[2];
                    config.Users.push(user);
                }
            });

            config.GenerateMissingTrackLists = document.querySelector('#GenerateMissingTrackLists').checked;
            config.MissingTrackListsDateFormat = document.querySelector('#MissingTrackListsDateFormat').value;
            config.KeepMissingTrackLists = document.querySelector('#KeepMissingTrackLists').checked;
            config.ItemMatchLevel = document.querySelector('#ItemMatchLevel').value;
            config.MaxFuzzyCharDifference = document.querySelector('#FuzzyMaxDiff').value;
            config.ItemMatchCriteriaRaw = getItemMatchCriteriaFromCheckboxes();
            if (config.ItemMatchCriteriaRaw == 0) {
                Dashboard.alert('Could not save settings, please select at least one track match criterium.');
                Dashboard.hideLoadingMsg();
                return;
            }
            config.UseLegacyMatching = document.querySelector('#UseLegacyMatching').checked;

            ApiClient.updatePluginConfiguration(SpotifyImportConfig.pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });

        e.preventDefault();
        return false;
    });

    document.querySelector('#authSpotify').addEventListener('click', function () {
        const redirectBaseUrl = new URL(ApiClient._serverAddress);
        if (redirectBaseUrl.hostname == 'localhost') {
            // spotify forbids "localhost" in redirect URIs, only IP allowed
            redirectBaseUrl.hostname = '127.0.0.1';
        }

        const fullAuthUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/SpotifyAuth', {
            'baseUrl': redirectBaseUrl.href.replace(/\/$/g, '')
        });

        ApiClient.fetch({
            url: fullAuthUrl,
            type: 'POST',
            dataType: 'json',
            headers: {
                accept: 'application/json'
            }
        }, true).then(function (json) {
            if (!json || !('login_req_uri' in json)) {
                throw "invalid json response";
            }

            window.open(json['login_req_uri'], '_self');
        }).catch(function (error) {
            console.error(error);
        });
    });

    const dbgDumpMetaBtn = document.querySelector('#dbgDumpMeta');
    dbgDumpMetaBtn.addEventListener('click', function () {
        const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/Debug/DumpMetadata');

        dbgDumpMetaBtn.disabled = true;

        ApiClient.fetch({
            url: apiUrl,
            type: 'POST'
        }, true).then(function (res) {
            dbgDumpMetaBtn.disabled = false;
            if (!res || !res.ok) {
                throw "invalid response";
            }
            console.log('dump done');
        }).catch(function (error) {
            console.error(error);
        });
    });

    const dbgDumpRefsBtn = document.querySelector('#dbgDumpRefs');
    dbgDumpRefsBtn.addEventListener('click', function () {
        const name = document.querySelector('#dbgDumpRefsTrackName').value;
        const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/Debug/DumpTrackRefs', {
            name: name
        });

        dbgDumpRefsBtn.disabled = true;

        ApiClient.fetch({
            url: apiUrl,
            type: 'POST'
        }, true).then(function (res) {
            dbgDumpRefsBtn.disabled = false;
            if (!res || !res.ok) {
                throw "invalid response";
            }
            console.log('dump done');
        }).catch(function (error) {
            console.error(error);
        });
    });

    // Get Current Token JSON button
    const getCurrentTokenBtn = document.querySelector('#getCurrentToken');
    getCurrentTokenBtn.addEventListener('click', function () {
        const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/SpotifyAuthToken');
        
        getCurrentTokenBtn.disabled = true;

        ApiClient.fetch({
            url: apiUrl,
            type: 'GET',
            headers: {
                accept: 'application/json'
            }
        }, true).then(function (res) {
            getCurrentTokenBtn.disabled = false;
            
            // Handle both Response object and already parsed JSON
            if (res && typeof res === 'object' && 'ok' in res) {
                // It's a Response object
                if (!res.ok) {
                    throw "invalid response";
                }
                return res.json();
            } else {
                // It's already parsed JSON
                return res;
            }
        }).then(function (data) {
            if (data && data.tokenJson) {
                document.querySelector('#SpotifyOAuthTokenJson').value = data.tokenJson;
                Dashboard.alert('Current token loaded successfully!');
            } else {
                document.querySelector('#SpotifyOAuthTokenJson').value = '';
                Dashboard.alert('No current token found.');
            }
        }).catch(function (error) {
            console.error(error);
            Dashboard.alert('Failed to get current token: ' + error);
        });
    });

    // Set Token from JSON button
    const setCurrentTokenBtn = document.querySelector('#setCurrentToken');
    setCurrentTokenBtn.addEventListener('click', function () {
        const tokenJson = document.querySelector('#SpotifyOAuthTokenJson').value;
        
        if (!tokenJson || tokenJson.trim() === '') {
            Dashboard.alert('Please enter a valid JSON token first.');
            return;
        }

        const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/SpotifyAuthToken');
        
        setCurrentTokenBtn.disabled = true;

        ApiClient.fetch({
            url: apiUrl,
            type: 'POST',
            headers: {
                'Content-Type': 'application/json',
                accept: 'application/json'
            },
            data: JSON.stringify({ tokenJson: tokenJson })
        }, true).then(function (res) {
            setCurrentTokenBtn.disabled = false;
            
            // Handle both Response object and already parsed JSON
            if (res && typeof res === 'object' && 'ok' in res) {
                // It's a Response object
                if (!res.ok) {
                    return res.json().then(function(errorData) {
                        throw errorData.title || errorData.message || "Invalid response";
                    }).catch(function(jsonError) {
                        // If parsing error response fails, use the status text
                        throw res.statusText || "Invalid response";
                    });
                }
                return res.json();
            } else {
                // It's already parsed JSON - check for success/error indicators
                if (res && res.success) {
                    return res;
                } else if (res && (res.title || res.message)) {
                    throw res.title || res.message || "Unknown error";
                } else {
                    return res;
                }
            }
        }).then(function (data) {
            if (data && data.success) {
                Dashboard.alert('Token set successfully! The token has been parsed and stored in the configuration.');
            } else {
                throw data.message || "Unknown error";
            }
        }).catch(function (error) {
            console.error(error);
            Dashboard.alert('Failed to set token: ' + error);
        });
    });
}
