(function () {
    'use strict';
    const recentKey = 'xtreampro-recent';
    const guideHours = 3;
    const deviceId = 'xtreampro-' + (localStorage.getItem('_deviceId2') || 'browser');
    const $ = (id) => document.getElementById(id);
    const state = { channels: [], visible: [], category: 'All', selected: -1, session: null, hls: null, digits: '', digitTimer: null, ticket: 0, playTimer: null, tuning: Promise.resolve() };

    function server() {
        let saved = null;
        try {
            saved = JSON.parse(localStorage.getItem('jellyfin_credentials') || '{}');
        } catch (e) {
            saved = {};
        }

        return (saved.Servers || []).find((s) => s.AccessToken && s.UserId) || null;
    }

    const session = server();
    if (!session) {
        location.href = '/web/';
        return;
    }

    function authorization() {
        return 'MediaBrowser Client="Xtream Pro", Device="Browser", DeviceId="' + deviceId + '", Version="1.0", Token="' + session.AccessToken + '"';
    }

    async function api(path, options) {
        const response = await fetch(path, Object.assign({ headers: { 'Authorization': authorization(), 'Content-Type': 'application/json' } }, options));
        if (!response.ok) {
            throw new Error(response.status + ' ' + path);
        }

        return response.status === 204 ? null : response.json();
    }

    function el(tag, props, ...children) {
        const node = document.createElement(tag);
        Object.entries(props || {}).forEach(([key, value]) => {
            if (key in node) {
                node[key] = value;
            } else {
                node.setAttribute(key, value);
            }
        });
        children.flat().forEach((child) => node.append(child));
        return node;
    }

    function status(text) {
        $('status').textContent = text;
    }

    function recent() {
        try {
            return JSON.parse(localStorage.getItem(recentKey) || '[]');
        } catch (e) {
            return [];
        }
    }

    function remember(id) {
        try {
            localStorage.setItem(recentKey, JSON.stringify([id, ...recent().filter((r) => r !== id)].slice(0, 20)));
        } catch (e) {
            // Private windows may refuse storage; recent channels are a convenience only.
        }
    }

    // Jellyfin stores tags split on "|", so "UK | News" comes back as two tags.
    function group(channel) {
        return (channel.Tags || []).join(' | ') || 'Other';
    }

    function renderCategories() {
        const term = $('category-search').value.trim().toLowerCase();
        const groups = ['All', 'Favourites', 'Recent', ...new Set(state.channels.map(group))]
            .filter((name) => !term || name.toLowerCase().includes(term));
        $('categories').replaceChildren(...groups.map((name) => {
            const button = el('button', { type: 'button', textContent: name });
            button.setAttribute('aria-current', String(name === state.category));
            button.addEventListener('click', () => {
                state.category = name;
                $('search').value = '';
                renderCategories();
                renderChannels();
            });
            return el('li', {}, button);
        }));
    }

    function filtered() {
        // A channel search covers every channel, whatever category is selected.
        const term = $('search').value.trim().toLowerCase();
        if (term) {
            return state.channels.filter((c) => c.Name.toLowerCase().includes(term) || c.ChannelNumber === term);
        }

        let list = state.channels;
        if (state.category === 'Favourites') {
            list = list.filter((c) => c.UserData && c.UserData.IsFavorite);
        } else if (state.category === 'Recent') {
            const order = recent();
            list = order.map((id) => state.channels.find((c) => c.Id === id)).filter(Boolean);
        } else if (state.category !== 'All') {
            list = list.filter((c) => group(c) === state.category);
        }

        return list;
    }

    function logo(channel) {
        if (!channel.ImageTags || !channel.ImageTags.Primary) {
            return el('span', {});
        }

        const image = el('img', { src: '/Items/' + channel.Id + '/Images/Primary?maxHeight=64&tag=' + channel.ImageTags.Primary, alt: '' });
        image.addEventListener('error', () => { image.style.visibility = 'hidden'; });
        return image;
    }

    function renderChannels() {
        state.visible = filtered();
        const current = state.channels[state.selected];
        $('channels').replaceChildren(...state.visible.map((channel) => {
            const item = el('li', { role: 'option', id: 'ch-' + channel.Id },
                el('span', { className: 'num', textContent: channel.ChannelNumber || '' }),
                logo(channel),
                el('span', {}, channel.Name, el('span', { className: 'now', textContent: channel.CurrentProgram ? channel.CurrentProgram.Name : '' })));
            item.setAttribute('aria-selected', String(current === channel));
            item.addEventListener('click', () => select(state.channels.indexOf(channel)));
            return item;
        }));
        renderGuide();
    }

    function closeSession(previous) {
        // Both start at once with keepalive, so they still complete when the page is closing and the provider connection is freed.
        return Promise.all([
            api('/Videos/ActiveEncodings?deviceId=' + encodeURIComponent(deviceId) + '&playSessionId=' + encodeURIComponent(previous.playSessionId), { method: 'DELETE', keepalive: true }),
            api('/LiveStreams/Close?liveStreamId=' + encodeURIComponent(previous.liveStreamId), { method: 'POST', keepalive: true })
        ].map((p) => p.catch(() => {})));
    }

    async function stopPreview() {
        if (state.hls) {
            state.hls.destroy();
            state.hls = null;
        }

        $('video').removeAttribute('src');
        const previous = state.session;
        state.session = null;
        if (previous) {
            await closeSession(previous);
        }
    }

    // The channel info updates at once; tuning waits for a pause and runs one at a time, so fast zapping never holds extra provider connections.
    function schedulePlay(channel) {
        const ticket = ++state.ticket;
        clearTimeout(state.playTimer);
        state.playTimer = setTimeout(() => {
            state.tuning = state.tuning.then(() => play(channel, ticket));
        }, 400);
    }

    async function play(channel, ticket) {
        if (ticket !== state.ticket) {
            return;
        }

        await stopPreview();
        status('Tuning ' + channel.Name + '...');
        const profile = {
            MaxStreamingBitrate: 20000000,
            DirectPlayProfiles: [],
            TranscodingProfiles: [{ Container: 'ts', Type: 'Video', AudioCodec: 'aac', VideoCodec: 'h264', Context: 'Streaming', Protocol: 'hls', BreakOnNonKeyFrames: true }]
        };
        try {
            const info = await api('/Items/' + channel.Id + '/PlaybackInfo?userId=' + session.UserId + '&autoOpenLiveStream=true&isPlayback=true', {
                method: 'POST',
                body: JSON.stringify({ DeviceProfile: profile, DeviceId: deviceId })
            });
            const source = info.MediaSources[0];
            const opened = { liveStreamId: source.LiveStreamId, playSessionId: info.PlaySessionId };
            if (ticket !== state.ticket) {
                // A newer channel change started while this one was opening.
                await closeSession(opened);
                return;
            }

            state.session = opened;
            const url = source.TranscodingUrl + (source.TranscodingUrl.includes('api_key') ? '' : '&api_key=' + session.AccessToken);
            const video = $('video');
            if (window.Hls && window.Hls.isSupported()) {
                state.hls = new window.Hls();
                state.hls.loadSource(url);
                state.hls.attachMedia(video);
            } else {
                video.src = url;
            }

            video.play().catch(() => {});
            status('Playing ' + channel.Name);
        } catch (e) {
            status('Could not play ' + channel.Name + ': ' + e.message);
        }
    }

    async function showNowNext(channel) {
        $('channel-name').textContent = (channel.ChannelNumber ? channel.ChannelNumber + ' ' : '') + channel.Name;
        const favourite = $('favourite');
        favourite.hidden = false;
        favourite.setAttribute('aria-pressed', String(!!(channel.UserData && channel.UserData.IsFavorite)));
        favourite.textContent = channel.UserData && channel.UserData.IsFavorite ? 'Remove favourite' : 'Add favourite';
        const items = await api('/LiveTv/Programs?userId=' + session.UserId + '&channelIds=' + channel.Id + '&minEndDate=' + new Date().toISOString() + '&sortBy=StartDate&limit=2')
            .then((programmes) => programmes.Items, () => []);
        // A slower reply for a channel the user has already zapped past must not overwrite the current one.
        if (state.channels[state.selected] !== channel) {
            return;
        }

        $('now').textContent = items[0] ? items[0].Name : '-';
        $('next').textContent = items[1] ? items[1].Name : '-';
    }

    function select(index) {
        const channel = state.channels[index];
        if (!channel) {
            return;
        }

        state.selected = index;
        remember(channel.Id);
        $('channels').querySelectorAll('[role="option"]').forEach((o) => o.setAttribute('aria-selected', String(o.id === 'ch-' + channel.Id)));
        const item = $('ch-' + channel.Id);
        if (item) {
            $('channels').setAttribute('aria-activedescendant', item.id);
            item.scrollIntoView({ block: 'nearest' });
        }

        showNowNext(channel);
        schedulePlay(channel);
    }

    function zap(delta) {
        if (state.visible.length === 0) {
            return;
        }

        const position = state.visible.indexOf(state.channels[state.selected]);
        const next = state.visible[(position + delta + state.visible.length) % state.visible.length];
        select(state.channels.indexOf(next));
    }

    function jump(number) {
        const index = state.channels.findIndex((c) => c.ChannelNumber === number);
        if (index >= 0) {
            select(index);
        } else {
            status('No channel ' + number);
        }
    }

    async function toggleFavourite() {
        const channel = state.channels[state.selected];
        if (!channel) {
            return;
        }

        const favourite = !(channel.UserData && channel.UserData.IsFavorite);
        const data = await api('/UserFavoriteItems/' + channel.Id + '?userId=' + session.UserId, { method: favourite ? 'POST' : 'DELETE' });
        channel.UserData = Object.assign(channel.UserData || {}, data || { IsFavorite: favourite });
        showNowNext(channel);
        if (state.category === 'Favourites') {
            renderChannels();
        }
    }

    async function renderGuide() {
        const channels = state.visible.slice(0, 50);
        const start = new Date();
        const end = new Date(start.getTime() + guideHours * 3600000);
        if (channels.length === 0) {
            $('guide').replaceChildren();
            return;
        }

        let programmes = [];
        try {
            const result = await api('/LiveTv/Programs?userId=' + session.UserId + '&channelIds=' + channels.map((c) => c.Id).join(',') + '&minEndDate=' + start.toISOString() + '&maxStartDate=' + end.toISOString() + '&sortBy=StartDate');
            programmes = result.Items;
        } catch (e) {
            status('Could not load the guide.');
        }

        const span = end - start;
        $('guide').replaceChildren(...channels.map((channel) => el('div', { className: 'guide-row', role: 'row' },
            el('span', { role: 'rowheader', textContent: channel.Name }),
            el('div', { className: 'guide-track', role: 'gridcell' }, programmes.filter((p) => p.ChannelId === channel.Id).map((p) => {
                const from = Math.max(new Date(p.StartDate) - start, 0);
                const to = Math.min(new Date(p.EndDate) - start, span);
                return el('span', { textContent: p.Name, title: p.Name, style: 'left:' + (100 * from / span) + '%;width:' + (100 * (to - from) / span) + '%' });
            })))));
    }

    document.addEventListener('keydown', (e) => {
        if (e.target === $('search') || e.target === $('category-search')) {
            return;
        }

        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault();
            zap(e.key === 'ArrowDown' ? 1 : -1);
        } else if (/^[0-9]$/.test(e.key)) {
            state.digits += e.key;
            status('Channel ' + state.digits);
            clearTimeout(state.digitTimer);
            state.digitTimer = setTimeout(() => {
                jump(state.digits);
                state.digits = '';
            }, 1200);
        } else if (e.key === 'f' || e.key === 'F') {
            toggleFavourite();
        }
    });
    $('search').addEventListener('input', renderChannels);
    $('category-search').addEventListener('input', renderCategories);
    $('favourite').addEventListener('click', toggleFavourite);
    window.addEventListener('pagehide', stopPreview);
    window.addEventListener('beforeunload', stopPreview);

    api('/LiveTv/Channels?userId=' + session.UserId + '&addCurrentProgram=true&enableUserData=true&fields=Tags')
        .then((result) => {
            state.channels = result.Items;
            status(state.channels.length + ' channels');
            renderCategories();
            renderChannels();
        })
        .catch((e) => status('Could not load channels: ' + e.message));
})();
