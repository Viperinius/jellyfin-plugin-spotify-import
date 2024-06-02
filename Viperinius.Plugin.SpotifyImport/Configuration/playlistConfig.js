const apiQueryOpts = {};
const users = [];

function getPlaylistIdElementHtml(id) {
    let value = id;
    if (!id) {
        value = '';
    }

    return `<td class="detailTableBodyCell cellPlaylistId" contenteditable>${value}</td>`
}

function getNameElementHtml(name) {
    let value = name;
    if (!name) {
        value = '';
    }

    return `<td class="detailTableBodyCell cellPlaylistName" contenteditable>${value}</td>`
}

function getUserSelectHtml(selectedUser) {
    let userOptionsHtml = '';
    users.forEach(user => {
        userOptionsHtml += `<option value="${user.id}" ${user.id === selectedUser ? 'selected' : ''}>${user.name}</option>`;
    });

    return `<td class="detailTableBodyCell cellPlaylistUser"><select class="emby-select-withcolor emby-select" is="emby-select">${userOptionsHtml}</select></td>`;
}

function getRecreateFromScratchHtml(alwaysFromScratch) {
    return `<td class="detailTableBodyCell cellPlaylistFromScratch">
        <label class="emby-checkbox-label">
            <input type="checkbox" is="emby-checkbox" ${alwaysFromScratch ? 'checked' : ''}/>
            <span></span>
        </label>
    </td>`;
}

function getOnlyOwnHtml(onlyOwn) {
    return `<td class="detailTableBodyCell cellPlaylistOnlyOwn">
        <label class="emby-checkbox-label">
            <input type="checkbox" is="emby-checkbox" ${onlyOwn ? 'checked' : ''}/>
            <span></span>
        </label>
    </td>`;
}

function createPlaylistRowHtml(playlistId, name, user, alwaysFromScratch) {
    const row = `<tr class="detailTableBodyRow detailTableBodyRow-shaded">
        ${getPlaylistIdElementHtml(playlistId)}
        ${getNameElementHtml(name)}
        ${getUserSelectHtml(user)}
        ${getRecreateFromScratchHtml(alwaysFromScratch)}
        <td>
            <button class="paper-icon-button-light" type="button" onclick="this.closest('tr').remove()">
                <span class="material-icons delete"></span>
            </button>
        </td>
    </tr>`;

    return row;
}

function createUserRowHtml(spotifyUser, jellyfinUser, onlyOwn) {
    const row = `<tr class="detailTableBodyRow detailTableBodyRow-shaded">
        ${getPlaylistIdElementHtml(spotifyUser)}
        ${getUserSelectHtml(jellyfinUser)}
        ${getOnlyOwnHtml(onlyOwn)}
        <td>
            <button class="paper-icon-button-light" type="button" onclick="this.closest('tr').remove()">
                <span class="material-icons delete"></span>
            </button>
        </td>
    </tr>`;

    return row;
}



function loadPlaylistTable(page, config) {
    const tableBody = page.querySelector('#playlistTable > tbody');
    if (tableBody && config && config.Playlists) {
        let rowsHtml = '';

        config.Playlists.forEach(pl => {
            const user = users.find(u => u.name === pl.UserName);
            const userId = user?.id || '';
            rowsHtml += createPlaylistRowHtml(pl.Id, pl.Name, userId, pl.RecreateFromScratch);
        });

        tableBody.innerHTML = rowsHtml;
    }

    const addBtn = page.querySelector('#addPlaylistId');
    if (addBtn) {
        addBtn.addEventListener('click', function () {
            const tableBody = page.querySelector('#playlistTable > tbody');
            if (tableBody) {
                tableBody.insertAdjacentHTML('beforeend', createPlaylistRowHtml());
            }
        });
    }
}

function loadUsersTable(page, config) {
    const tableBody = page.querySelector('#userlistTable > tbody');
    if (tableBody && config && config.Users) {
        let rowsHtml = '';

        config.Users.forEach(user => {
            const spotifyUser = user.Id;
            const jellyfinUser = users.find(u => u.name === user.UserName);
            const jellyfinUserId = jellyfinUser?.id || '';
            const onlyOwn = user.OnlyOwnPlaylists;

            rowsHtml += createUserRowHtml(spotifyUser, jellyfinUserId, onlyOwn);
        });

        tableBody.innerHTML = rowsHtml;
    }

    const addBtn = page.querySelector('#addUser');
    addBtn.addEventListener('click', function () {
        const tableBody = page.querySelector('#userlistTable > tbody');
        if (tableBody) {
            tableBody.insertAdjacentHTML('beforeend', createUserRowHtml());
        }
    });
}

function getPlaylistTableData(page) {
    const tableRows = page.querySelectorAll('#playlistTable > tbody > tr');
    if (tableRows) {
        const playlistData = [...tableRows].map(r => {
            const spotifyId = r.querySelector('td.cellPlaylistId').innerText.trim();
            const renameTo = r.querySelector('td.cellPlaylistName').innerText.trim();
            const userSelect = r.querySelector('td.cellPlaylistUser > select');
            const jellyfinUser = userSelect.options[userSelect.selectedIndex].text.trim();
            const recreateFromScratch = r.querySelector('td.cellPlaylistFromScratch > * input').checked;

            return {
                Id: spotifyId,
                Name: renameTo,
                UserName: jellyfinUser,
                RecreateFromScratch : recreateFromScratch,
            };
        });

        return playlistData;
    }

    return [];
}

function getUsersTableData(page) {
    const tableRows = page.querySelectorAll('#userlistTable > tbody > tr');
    if (!tableRows) {
        return [];
    }

    const userData = [...tableRows].map(r => {
        const spotifyUser = r.querySelector('td.cellPlaylistId').innerText.trim();
        const userSelect = r.querySelector('td.cellPlaylistUser > select');
        const jellyfinUser = userSelect.options[userSelect.selectedIndex].text.trim();
        const onlyOwn = r.querySelector('td.cellPlaylistOnlyOwn > * input').checked;

        return {
            Id: spotifyUser,
            UserName: jellyfinUser,
            OnlyOwnPlaylists: onlyOwn
        };

    });

    return userData;
}


function mapItemMatchCriteriaToCheckboxes(config) {
    document.querySelector('#ItemMatchCriteriaTrack').checked = (config.ItemMatchCriteriaRaw & (1 << 0)) > 0;
    document.querySelector('#ItemMatchCriteriaAlbum').checked = (config.ItemMatchCriteriaRaw & (1 << 1)) > 0;
    document.querySelector('#ItemMatchCriteriaArtist').checked = (config.ItemMatchCriteriaRaw & (1 << 2)) > 0;
    document.querySelector('#ItemMatchCriteriaAlbumArtist').checked = (config.ItemMatchCriteriaRaw & (1 << 3)) > 0;
}

function getItemMatchCriteriaFromCheckboxes() {
    let result = 0;
    if (document.querySelector('#ItemMatchCriteriaTrack').checked) result |= 1 << 0;
    if (document.querySelector('#ItemMatchCriteriaAlbum').checked) result |= 1 << 1;
    if (document.querySelector('#ItemMatchCriteriaArtist').checked) result |= 1 << 2;
    if (document.querySelector('#ItemMatchCriteriaAlbumArtist').checked) result |= 1 << 3;

    return result;
}

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

            document.querySelector('#SpotifyAuthRedirectUri').innerText = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/SpotifyAuthCallback');
            if (config.SpotifyAuthToken && 'CreatedAt' in config.SpotifyAuthToken) {
                document.querySelector('#authSpotifyAlreadyDesc').classList.remove('hide');
                document.querySelector('#authSpotifyCreatedAt').innerText = config.SpotifyAuthToken['CreatedAt'].split('T')[0];
            }

            document.querySelector('#EnableVerboseLogging').checked = config.EnableVerboseLogging;
            document.querySelector('#ShowProgressIndicator').checked = config.ShowProgressIndicator;
            document.querySelector('#SpotifyClientId').value = config.SpotifyClientId;

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
                    const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/MissingTracksFile', {
                        'api_key': apiQueryOpts.api_key
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
            config.ShowProgressIndicator = document.querySelector('#ShowProgressIndicator').checked;
            config.SpotifyClientId = document.querySelector('#SpotifyClientId').value;

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
        const fullAuthUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/SpotifyAuth', {
            'baseUrl': ApiClient._serverAddress,
            'api_key': apiQueryOpts.api_key
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

    const dbgDumpMetaBtn = document.querySelector('#dbgDumpMeta');
    dbgDumpMetaBtn.addEventListener('click', function () {
        const apiUrl = ApiClient.getUrl(SpotifyImportConfig.pluginApiBaseUrl + '/Debug/DumpMetadata', {
            'api_key': apiQueryOpts.api_key
        });

        dbgDumpMetaBtn.disabled = true;

        fetch(apiUrl, { method: 'POST' }).then(function (res) {
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
            name: name,
            'api_key': apiQueryOpts.api_key
        });

        dbgDumpRefsBtn.disabled = true;

        fetch(apiUrl, { method: 'POST' }).then(function (res) {
            dbgDumpRefsBtn.disabled = false;
            if (!res || !res.ok) {
                throw "invalid response";
            }
            console.log('dump done');
        }).catch(function (error) {
            console.error(error);
        });
    });
}
