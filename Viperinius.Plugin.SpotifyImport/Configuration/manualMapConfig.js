const classEditCell = 'editCell';
const classViewCell = 'viewCell';
const attrTrackId = 'item-raw';

const dummyText = 'replace-me';

const jfTrackIdRegexes = [
    new RegExp("^https?://[^/]+/Items/(?<id>[0-9a-fA-F]+)"), // url from "copy stream uri"
    new RegExp("^https?://.+/\\?#/details\\?id=(?<id>[0-9a-fA-F]+)"), // url from web view
    new RegExp("^\\s*(?<id>[0-9a-fA-F]+)\\s*$"), // raw id
];
function parseJfTrackId(raw) {
    for (const regex of jfTrackIdRegexes) {
        const { id } = regex.exec(raw)?.groups ?? {};
        if (id) {
            return id;
        }
    }

    return null;
}

function onClickEditJellyfinTrack(event) {
    const target = event.currentTarget ?? event.target;
    target.parentNode.classList.add('hide');

    const td = target.parentNode.parentNode;
    td.querySelector('.' + classEditCell).classList.remove('hide');
}

function onClickCancelEditJellyfinTrack(event) {
    const target = event.currentTarget ?? event.target;
    const editCell = target.parentNode;
    const td = editCell.parentNode;
    const viewCell = td.querySelector('.' + classViewCell);
    
    editCell.classList.add('hide');
    // restore content
    const raw = viewCell.querySelector('a').getAttribute(attrTrackId);
    editCell.querySelector('span').innerText = raw;

    viewCell.classList.remove('hide');
}

function onClickFinishEditJellyfinTrack(event) {
    const target = event.currentTarget ?? event.target;
    const editCell = target.parentNode;
    const td = editCell.parentNode;
    const viewCell = td.querySelector('.' + classViewCell);

    const newRaw = parseJfTrackId(editCell.querySelector('span').innerText);
    if (!newRaw) {
        Dashboard.alert('Failed to parse a track ID for the given Jellyfin track');
        return;
    }

    editCell.classList.add('hide');
    viewCell.classList.remove('hide');

    getJfTrackNameById(newRaw).then(function (result) {
        const aElem = viewCell.querySelector('a');
        aElem.setAttribute(attrTrackId, newRaw);

        let url = aElem.getAttribute('href');
        url = url.replace(/id=([a-fA-F0-9])+/gm, `id=${newRaw}`);
        aElem.setAttribute('href', url);

        aElem.innerText = result;
    })
    .catch(function (error) {
        console.error(error);
        Dashboard.alert('Given Jellyfin track could not be found');
    });
}

async function getJfTrackNameById(id) {
    return ApiClient.getItem(ApiClient.getCurrentUserId(), id).then(result => {
        return result['Name'];
    });
}

//#region table generation

function createJfTrackCell(trackId, trackName) {
    const isEmpty = !trackId || !trackName;

    const td = document.createElement('td');
    td.classList.add('detailTableBodyCell');

    const viewCell = document.createElement('div');
    viewCell.classList.add(classViewCell);
    if (isEmpty) {
        viewCell.classList.add('hide');
    }
    td.appendChild(viewCell);

    const linkBtn = document.createElement('a');
    linkBtn.setAttribute('is', 'emby-linkbutton');
    linkBtn.classList.add('button-link');
    linkBtn.href = `#/details?id=${trackId || 0}&serverId=${ApiClient.serverId()}`;
    linkBtn.setAttribute('item-raw', isEmpty ? dummyText : trackId);
    linkBtn.innerText = isEmpty ? dummyText : trackName;
    viewCell.appendChild(linkBtn);

    const editBtn = document.createElement('button');
    editBtn.classList.add('paper-icon-button-light');
    editBtn.type = 'button';
    editBtn.innerHTML = '<span class="material-icons edit"></span>';
    editBtn.onclick = onClickEditJellyfinTrack;
    viewCell.appendChild(editBtn);
    
    const editCell = document.createElement('div');
    editCell.classList.add(classEditCell);
    if (!isEmpty) {
        editCell.classList.add('hide');
    }
    td.appendChild(editCell);

    const rawSpan = document.createElement('span');
    rawSpan.contentEditable = 'true';
    rawSpan.innerText = isEmpty ? dummyText : trackId;
    editCell.appendChild(rawSpan);

    const okBtn = document.createElement('button');
    okBtn.classList.add('paper-icon-button-light');
    okBtn.type = 'button';
    okBtn.innerHTML = '<span class="material-icons done"></span>';
    okBtn.onclick = onClickFinishEditJellyfinTrack;
    editCell.appendChild(okBtn);

    const cancelBtn = document.createElement('button');
    cancelBtn.classList.add('paper-icon-button-light');
    cancelBtn.type = 'button';
    cancelBtn.innerHTML = '<span class="material-icons close"></span>';
    cancelBtn.onclick = onClickCancelEditJellyfinTrack;
    editCell.appendChild(cancelBtn);

    return td;
}

async function createJfTrackCellWithName(trackId) {
    return getJfTrackNameById(trackId || 'x').then(trackName => {
        return createJfTrackCell(trackId, trackName);
    });
}

function createProviderTextCell(text) {
    const td = document.createElement('td');
    td.classList.add('detailTableBodyCell');
    td.contentEditable = 'true';
    td.innerText = text || '';

    return td;
}

function createProviderListCell(list) {
    const td = document.createElement('td');
    td.classList.add('detailTableBodyCell');
    td.contentEditable = 'true';
    td.innerText = list?.join(', ') || '';

    return td;
}

async function createRow(trackId, providerTrackName, providerAlbumName, providerAlbumArtists, providerArtists) {
    const tr = document.createElement('tr');
    tr.classList.add('detailTableBodyRow', 'detailTableBodyRow-shaded');

    if (trackId) {
        tr.appendChild(await createJfTrackCellWithName(trackId));
    }
    else {
        tr.appendChild(createJfTrackCell());
    }
    tr.appendChild(createProviderTextCell(providerTrackName));
    tr.appendChild(createProviderTextCell(providerAlbumName));
    tr.appendChild(createProviderListCell(providerAlbumArtists));
    tr.appendChild(createProviderListCell(providerArtists));

    const deleteCell = document.createElement('td');
    deleteCell.innerHTML = `<button class="paper-icon-button-light" type="button" onclick="this.closest('tr').remove()">
        <span class="material-icons delete"></span>
    </button>`;
    tr.appendChild(deleteCell);

    return tr;
}

async function loadTableRows(page, map) {
    const body = page.querySelector('#mapTable > tbody');
    if (body && map) {
        const children = await Promise.all(map.map(async entry => {
            return await createRow(
                entry.Jellyfin.Track,
                entry.Provider.Name,
                entry.Provider.AlbumName,
                entry.Provider.AlbumArtistNames,
                entry.Provider.ArtistNames);
        }));
        body.replaceChildren(...children);
    }
}

//#endregion

function getTableData(page) {
    const tableRows = page.querySelectorAll('#mapTable > tbody > tr');
    if (tableRows) {
        return [...tableRows].flatMap(r => {
            const tds = r.querySelectorAll('td');
            if (tds.length === 6) {
                const trackId = tds[0].querySelector('.viewCell > a').getAttribute(attrTrackId).trim();
                if (!trackId || trackId === dummyText) {
                    return [];
                }

                return {
                    Jellyfin: {
                        Track: trackId,
                    },
                    Provider: {
                        Name: tds[1].innerText.trim(),
                        AlbumName: tds[2].innerText.trim(),
                        AlbumArtistNames: tds[3].innerText.trim().split(',').flatMap(s => s.trim().length == 0 ? [] : s.trim()),
                        ArtistNames: tds[4].innerText.trim().split(',').flatMap(s => s.trim().length == 0 ? [] : s.trim()),
                    },
                };
            }

            return [];
        });
    }

    return [];
}

export default function (view) {
    view.dispatchEvent(new CustomEvent('create'));

    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();

        const mapUrl = ApiClient.getUrl('Viperinius.Plugin.SpotifyImport/ManualTrackMap');
        ApiClient.getJSON(mapUrl, { 'api_key': ApiClient.accessToken() }).then(async result => {
            await loadTableRows(view, result);
            Dashboard.hideLoadingMsg();
        }).catch(response => {
            if (response.status == 404) {
                // map is currently empty / not existing, simply continue
                Dashboard.hideLoadingMsg();
            }
            else {
                throw response;
            }
        });
    });

    const addRowBtn = view.querySelector('#addRowBtn');
    if (addRowBtn) {
        addRowBtn.addEventListener('click', async function () {
            const tableBody = view.querySelector('#mapTable > tbody');
            if (tableBody) {
                tableBody.appendChild(await createRow());
            }
        });
    }

    document.querySelector('#SpotifyImportMapPage').addEventListener('submit', event => {
        Dashboard.showLoadingMsg();
        const entries = getTableData(view);
        ApiClient.ajax({
            url: ApiClient.getUrl('Viperinius.Plugin.SpotifyImport/ManualTrackMap'),
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(entries),
        }, true).then(res => {
            Dashboard.processPluginConfigurationUpdateResult();
        }).catch(error => {
            console.error(error);
            Dashboard.hideLoadingMsg();
            Dashboard.alert('Failed to save');
        });

        event.preventDefault();
    });
}