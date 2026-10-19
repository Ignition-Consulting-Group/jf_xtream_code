(function () {
    'use strict';
    const linkId = 'xtreampro-livetv-link';
    let found = false;

    function addLink() {
        const menu = document.querySelector('.mainDrawer-scrollContainer');
        if (!menu) {
            return;
        }

        found = true;
        if (!document.getElementById(linkId)) {
            const link = document.createElement('a');
            link.id = linkId;
            link.className = 'navMenuOption lnkMediaFolder';
            link.href = '/XtreamPro/Web/';
            link.textContent = 'Live TV+';
            menu.appendChild(link);
        }
    }

    // jellyfin-web rebuilds its drawer as you navigate, so the link is put back whenever it goes missing.
    new MutationObserver(addLink).observe(document.documentElement, { childList: true, subtree: true });
    addLink();
    setTimeout(function () {
        if (!found) {
            console.warn('Xtream Pro: the web client menu was not found, so the Live TV+ link was not added. The page is still at /XtreamPro/Web/.');
        }
    }, 60000);
})();
