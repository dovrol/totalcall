(function () {
    var shortcutEntry = null;

    function detachShortcuts() {
        if (!shortcutEntry) {
            return;
        }

        window.removeEventListener("keydown", shortcutEntry.onKeyDown, true);
        shortcutEntry = null;
    }

    function attachShortcuts(dotnetRef) {
        detachShortcuts();

        if (!dotnetRef) {
            return;
        }

        function onKeyDown(event) {
            if (event.defaultPrevented ||
                event.altKey ||
                (!event.metaKey && !event.ctrlKey) ||
                String(event.key).toLowerCase() !== "k") {
                return;
            }

            event.preventDefault();
            event.stopPropagation();

            dotnetRef.invokeMethodAsync("ToggleCommandPalette").catch(function () {
                // The workspace may have been disposed while the event was queued.
            });
        }

        window.addEventListener("keydown", onKeyDown, true);
        shortcutEntry = { onKeyDown: onKeyDown };
    }

    window.totalCallTopN = {
        attachShortcuts: attachShortcuts,
        detachShortcuts: detachShortcuts
    };
})();
