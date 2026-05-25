(function () {
    var registry = new Map();
    var mobileMql = window.matchMedia("(max-width: 720px)");
    var MIN_WIDTH = 320;
    var MIN_HEIGHT = 240;
    var EDGE_MARGIN = 8;

    function isMobile() {
        return mobileMql.matches;
    }

    function clamp(value, min, max) {
        if (max < min) {
            return min;
        }

        if (value < min) {
            return min;
        }

        if (value > max) {
            return max;
        }

        return value;
    }

    function applyPosition(root, x, y) {
        if (!root) {
            return;
        }

        root.style.left = x + "px";
        root.style.top = y + "px";
    }

    function applySize(root, width, height) {
        if (!root) {
            return;
        }

        root.style.width = width + "px";
        root.style.height = height + "px";
        root.style.maxHeight = height + "px";
    }

    function viewportBounds(root) {
        var rect = root.getBoundingClientRect();
        var maxX = Math.max(EDGE_MARGIN, window.innerWidth - rect.width - EDGE_MARGIN);
        var maxY = Math.max(EDGE_MARGIN, window.innerHeight - rect.height - EDGE_MARGIN);
        return { minX: EDGE_MARGIN, maxX: maxX, minY: EDGE_MARGIN, maxY: maxY };
    }

    function attach(windowId, handle, root, resizeHandle, dotnetRef) {
        if (!handle || !root || !dotnetRef) {
            return;
        }

        detach(windowId);

        var dragState = {
            dragging: false,
            pointerId: null,
            offsetX: 0,
            offsetY: 0,
            lastX: 0,
            lastY: 0
        };

        var resizeState = {
            resizing: false,
            pointerId: null,
            startX: 0,
            startY: 0,
            startWidth: 0,
            startHeight: 0,
            startLeft: 0,
            startTop: 0,
            lastWidth: 0,
            lastHeight: 0
        };

        function onPointerDown(event) {
            if (isMobile()) {
                return;
            }

            if (event.button !== 0 && event.pointerType !== "touch" && event.pointerType !== "pen") {
                return;
            }

            // Ignore drag start when the user clicks a window control (close, minimize, etc.).
            var control = event.target.closest("[data-window-control]");
            if (control) {
                return;
            }

            var rect = root.getBoundingClientRect();
            dragState.dragging = true;
            dragState.pointerId = event.pointerId;
            dragState.offsetX = event.clientX - rect.left;
            dragState.offsetY = event.clientY - rect.top;
            dragState.lastX = rect.left;
            dragState.lastY = rect.top;

            try {
                handle.setPointerCapture(event.pointerId);
            } catch (e) {
                // setPointerCapture can throw on some browsers; safe to ignore.
            }

            handle.classList.add("is-dragging");
            root.classList.add("is-dragging");
            event.preventDefault();
        }

        function onPointerMove(event) {
            if (!dragState.dragging || event.pointerId !== dragState.pointerId) {
                return;
            }

            var bounds = viewportBounds(root);
            var nextX = clamp(event.clientX - dragState.offsetX, bounds.minX, bounds.maxX);
            var nextY = clamp(event.clientY - dragState.offsetY, bounds.minY, bounds.maxY);

            dragState.lastX = nextX;
            dragState.lastY = nextY;
            applyPosition(root, nextX, nextY);
        }

        function endDrag(event) {
            if (!dragState.dragging) {
                return;
            }

            if (event && event.pointerId !== dragState.pointerId) {
                return;
            }

            dragState.dragging = false;

            try {
                if (dragState.pointerId !== null) {
                    handle.releasePointerCapture(dragState.pointerId);
                }
            } catch (e) {
                // Ignore release errors.
            }

            handle.classList.remove("is-dragging");
            root.classList.remove("is-dragging");

            try {
                dotnetRef.invokeMethodAsync("OnDragEnd", dragState.lastX, dragState.lastY);
            } catch (e) {
                // Reference may have been disposed; nothing to do.
            }
        }

        function onResizePointerDown(event) {
            if (isMobile()) {
                return;
            }

            if (event.button !== 0 && event.pointerType !== "touch" && event.pointerType !== "pen") {
                return;
            }

            var rect = root.getBoundingClientRect();
            resizeState.resizing = true;
            resizeState.pointerId = event.pointerId;
            resizeState.startX = event.clientX;
            resizeState.startY = event.clientY;
            resizeState.startWidth = rect.width;
            resizeState.startHeight = rect.height;
            resizeState.startLeft = rect.left;
            resizeState.startTop = rect.top;
            resizeState.lastWidth = rect.width;
            resizeState.lastHeight = rect.height;

            try {
                resizeHandle.setPointerCapture(event.pointerId);
            } catch (e) {
                // Safe to ignore.
            }

            resizeHandle.classList.add("is-resizing");
            root.classList.add("is-resizing");
            event.preventDefault();
            event.stopPropagation();
        }

        function onResizePointerMove(event) {
            if (!resizeState.resizing || event.pointerId !== resizeState.pointerId) {
                return;
            }

            var deltaX = event.clientX - resizeState.startX;
            var deltaY = event.clientY - resizeState.startY;

            var maxWidth = Math.max(MIN_WIDTH, window.innerWidth - resizeState.startLeft - EDGE_MARGIN);
            var maxHeight = Math.max(MIN_HEIGHT, window.innerHeight - resizeState.startTop - EDGE_MARGIN);

            var nextWidth = clamp(resizeState.startWidth + deltaX, MIN_WIDTH, maxWidth);
            var nextHeight = clamp(resizeState.startHeight + deltaY, MIN_HEIGHT, maxHeight);

            resizeState.lastWidth = nextWidth;
            resizeState.lastHeight = nextHeight;
            applySize(root, nextWidth, nextHeight);
        }

        function endResize(event) {
            if (!resizeState.resizing) {
                return;
            }

            if (event && event.pointerId !== resizeState.pointerId) {
                return;
            }

            resizeState.resizing = false;

            try {
                if (resizeState.pointerId !== null && resizeHandle) {
                    resizeHandle.releasePointerCapture(resizeState.pointerId);
                }
            } catch (e) {
                // Ignore release errors.
            }

            if (resizeHandle) {
                resizeHandle.classList.remove("is-resizing");
            }
            root.classList.remove("is-resizing");

            try {
                dotnetRef.invokeMethodAsync("OnResizeEnd", resizeState.lastWidth, resizeState.lastHeight);
            } catch (e) {
                // Reference may have been disposed.
            }
        }

        function onPointerDownRoot() {
            try {
                dotnetRef.invokeMethodAsync("OnActivate");
            } catch (e) {
                // Reference may have been disposed.
            }
        }

        handle.addEventListener("pointerdown", onPointerDown);
        handle.addEventListener("pointermove", onPointerMove);
        handle.addEventListener("pointerup", endDrag);
        handle.addEventListener("pointercancel", endDrag);
        root.addEventListener("pointerdown", onPointerDownRoot, true);

        if (resizeHandle) {
            resizeHandle.addEventListener("pointerdown", onResizePointerDown);
            resizeHandle.addEventListener("pointermove", onResizePointerMove);
            resizeHandle.addEventListener("pointerup", endResize);
            resizeHandle.addEventListener("pointercancel", endResize);
        }

        registry.set(windowId, {
            handle: handle,
            root: root,
            resizeHandle: resizeHandle,
            onPointerDown: onPointerDown,
            onPointerMove: onPointerMove,
            endDrag: endDrag,
            onResizePointerDown: onResizePointerDown,
            onResizePointerMove: onResizePointerMove,
            endResize: endResize,
            onPointerDownRoot: onPointerDownRoot
        });
    }

    function detach(windowId) {
        var entry = registry.get(windowId);
        if (!entry) {
            return;
        }

        if (entry.handle) {
            entry.handle.removeEventListener("pointerdown", entry.onPointerDown);
            entry.handle.removeEventListener("pointermove", entry.onPointerMove);
            entry.handle.removeEventListener("pointerup", entry.endDrag);
            entry.handle.removeEventListener("pointercancel", entry.endDrag);
        }

        if (entry.root) {
            entry.root.removeEventListener("pointerdown", entry.onPointerDownRoot, true);
        }

        if (entry.resizeHandle) {
            entry.resizeHandle.removeEventListener("pointerdown", entry.onResizePointerDown);
            entry.resizeHandle.removeEventListener("pointermove", entry.onResizePointerMove);
            entry.resizeHandle.removeEventListener("pointerup", entry.endResize);
            entry.resizeHandle.removeEventListener("pointercancel", entry.endResize);
        }

        registry.delete(windowId);
    }

    window.totalCallWindows = {
        attach: attach,
        detach: detach,
        isMobile: isMobile
    };
})();
