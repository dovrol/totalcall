(function () {
    var shortcutEntry = null;
    var reorderPositions = null;
    var reorderAnimations = [];

    function visibleReorderRows() {
        return Array.prototype.filter.call(
            document.querySelectorAll("[data-topn-athlete-id]"),
            function (element) {
                return element.offsetParent !== null;
            });
    }

    function cancelReorderAnimations() {
        reorderAnimations.forEach(function (animation) {
            animation.cancel();
        });
        reorderAnimations = [];
    }

    function captureReorderPositions() {
        cancelReorderAnimations();
        reorderPositions = {};

        visibleReorderRows().forEach(function (element) {
            reorderPositions[element.dataset.topnAthleteId] = element.getBoundingClientRect();
        });
    }

    function animateReorder() {
        var previousPositions = reorderPositions;
        reorderPositions = null;

        if (!previousPositions ||
            window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            return;
        }

        window.requestAnimationFrame(function () {
            visibleReorderRows().forEach(function (element) {
                var previous = previousPositions[element.dataset.topnAthleteId];
                if (!previous) {
                    return;
                }

                var current = element.getBoundingClientRect();
                var offsetX = previous.left - current.left;
                var offsetY = previous.top - current.top;
                if (Math.abs(offsetX) < 0.5 && Math.abs(offsetY) < 0.5) {
                    return;
                }

                var duration = Math.min(480, 280 + Math.abs(offsetY) * 0.65);
                var animation = element.animate(
                    [
                        { transform: "translate(" + offsetX + "px, " + offsetY + "px)" },
                        { transform: "translate(0, 0)" }
                    ],
                    {
                        duration: duration,
                        easing: "cubic-bezier(0.22, 1, 0.36, 1)"
                    });

                reorderAnimations.push(animation);
                animation.addEventListener("finish", function () {
                    reorderAnimations = reorderAnimations.filter(function (entry) {
                        return entry !== animation;
                    });
                }, { once: true });
            });
        });
    }

    function detachShortcuts() {
        if (!shortcutEntry) {
            return;
        }

        window.removeEventListener("keydown", shortcutEntry.onKeyDown, true);
        document.removeEventListener("pointerdown", shortcutEntry.onPointerDown, true);
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

        function onPointerDown(event) {
            var openPopover = document.querySelector(".topn-rowgroup--popover-open .topn-fillwrap");
            if (!openPopover || openPopover.contains(event.target)) {
                return;
            }

            dotnetRef.invokeMethodAsync("CloseFillPopover").catch(function () {
                // The workspace may have been disposed while the event was queued.
            });
        }

        window.addEventListener("keydown", onKeyDown, true);
        document.addEventListener("pointerdown", onPointerDown, true);
        shortcutEntry = { onKeyDown: onKeyDown, onPointerDown: onPointerDown };
    }

    window.totalCallTopN = {
        attachShortcuts: attachShortcuts,
        detachShortcuts: detachShortcuts,
        captureReorderPositions: captureReorderPositions,
        animateReorder: animateReorder
    };
})();
