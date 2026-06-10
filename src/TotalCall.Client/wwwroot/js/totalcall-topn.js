(function () {
    var shortcutEntry = null;
    var reorderPositions = null;
    var reorderAnimations = [];
    var drawerDrags = {};

    // --- mobile bottom-sheet drag-to-dismiss (athlete context drawer + category switcher) ---
    function detachDrawerDrag(sheetSelector) {
        var keys = sheetSelector ? [sheetSelector] : Object.keys(drawerDrags);
        keys.forEach(function (key) {
            var drag = drawerDrags[key];
            if (!drag) {
                return;
            }

            drag.handle.removeEventListener("pointerdown", drag.onDown);
            window.removeEventListener("pointermove", drag.onMove);
            window.removeEventListener("pointerup", drag.onUp);
            window.removeEventListener("pointercancel", drag.onUp);

            // Clear any inline transform so the class/animation-based transition stays in control.
            drag.sheet.style.transition = "";
            drag.sheet.style.transform = "";
            delete drawerDrags[key];
        });
    }

    function attachDrawerDrag(dotnetRef, sheetSelector, handleSelector, closeMethod) {
        detachDrawerDrag(sheetSelector);

        var sheet = document.querySelector(sheetSelector);
        if (!sheet) {
            return;
        }

        var handle = sheet.querySelector(handleSelector);
        // The handle is only rendered/visible in the mobile bottom-sheet layout.
        if (!handle || window.getComputedStyle(handle).display === "none") {
            return;
        }

        var startY = 0;
        var offsetY = 0;
        var height = 0;
        var dragging = false;

        function onDown(event) {
            dragging = true;
            startY = event.clientY;
            offsetY = 0;
            height = sheet.getBoundingClientRect().height;
            sheet.style.transition = "none";
            if (handle.setPointerCapture) {
                handle.setPointerCapture(event.pointerId);
            }
            event.preventDefault();
        }

        function onMove(event) {
            if (!dragging) {
                return;
            }

            var delta = event.clientY - startY;
            // Follow the finger downward; resist upward drags so the sheet feels anchored.
            offsetY = delta > 0 ? delta : delta * 0.2;
            sheet.style.transform = "translateY(" + offsetY + "px)";
        }

        function onUp() {
            if (!dragging) {
                return;
            }

            dragging = false;
            var threshold = Math.min(140, height * 0.3);
            var shouldClose = offsetY > threshold;

            // Restore the transition (0.2s) so the sheet animates from here.
            sheet.style.transition = "";

            if (shouldClose && dotnetRef) {
                // Keep the dragged offset; detachDrawerDrag runs after the close re-render and
                // clears the inline transform, animating the sheet the rest of the way out.
                dotnetRef.invokeMethodAsync(closeMethod).catch(function () {});
            } else {
                // Snap back up to the open position.
                sheet.style.transform = "";
            }
        }

        handle.addEventListener("pointerdown", onDown);
        window.addEventListener("pointermove", onMove);
        window.addEventListener("pointerup", onUp);
        window.addEventListener("pointercancel", onUp);

        drawerDrags[sheetSelector] = {
            sheet: sheet,
            handle: handle,
            onDown: onDown,
            onMove: onMove,
            onUp: onUp
        };
    }

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
        animateReorder: animateReorder,
        attachDrawerDrag: attachDrawerDrag,
        detachDrawerDrag: detachDrawerDrag
    };
})();
