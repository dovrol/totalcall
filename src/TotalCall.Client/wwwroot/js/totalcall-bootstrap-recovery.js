(function () {
    "use strict";

    var errorUi = document.getElementById("blazor-error-ui");
    var repairButton = errorUi && errorUi.querySelector("[data-repair-cache]");

    if (!errorUi || !repairButton) {
        return;
    }

    var storedCulture = "pl-PL";

    try {
        storedCulture = localStorage.getItem("totalcall:culture") ||
            localStorage.getItem("totalcall:language") ||
            storedCulture;
    } catch {
        // Recovery must remain available when browser storage is disabled.
    }

    var currentUrl = new URL(window.location.href);
    if (currentUrl.searchParams.has("_tc_refresh")) {
        currentUrl.searchParams.delete("_tc_refresh");
        window.history.replaceState(null, "", currentUrl.toString());
    }

    var isEnglish = storedCulture.toLowerCase().startsWith("en");
    var copy = isEnglish
        ? {
            title: "The app encountered a problem",
            description: "Refresh the page. If the problem returns after an update or rebuild, repair the app cache.",
            repair: "Repair cache and refresh",
            repairing: "Repairing...",
            reload: "Refresh",
            dismiss: "Dismiss"
        }
        : {
            title: "Aplikacja napotkała problem",
            description: "Odśwież stronę. Jeśli problem wraca po aktualizacji lub przebudowie, użyj naprawy cache.",
            repair: "Napraw cache i odśwież",
            repairing: "Naprawianie...",
            reload: "Odśwież",
            dismiss: "Zamknij"
        };

    errorUi.querySelector("[data-error-title]").textContent = copy.title;
    errorUi.querySelector("[data-error-description]").textContent = copy.description;
    errorUi.querySelector("[data-reload-label]").textContent = copy.reload;
    errorUi.querySelector("[data-dismiss-label]").setAttribute("aria-label", copy.dismiss);
    repairButton.textContent = copy.repair;

    repairButton.addEventListener("click", async function () {
        repairButton.disabled = true;
        repairButton.textContent = copy.repairing;

        try {
            if ("caches" in window) {
                var cacheNames = await window.caches.keys();
                await Promise.all(cacheNames.map(function (name) {
                    return window.caches.delete(name);
                }));
            }

            if ("serviceWorker" in navigator) {
                var registrations = await navigator.serviceWorker.getRegistrations();
                await Promise.all(registrations.map(function (registration) {
                    return registration.unregister();
                }));
            }
        } finally {
            var url = new URL(window.location.href);
            url.searchParams.set("_tc_refresh", Date.now().toString());
            window.location.replace(url.toString());
        }
    });
})();
