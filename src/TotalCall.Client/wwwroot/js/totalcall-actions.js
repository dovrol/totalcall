(function () {
    function copyText(text) {
        if (navigator.clipboard && window.isSecureContext) {
            return navigator.clipboard.writeText(text)
                .then(function () { return true; })
                .catch(function () { return execCommandCopy(text); });
        }

        return Promise.resolve(execCommandCopy(text));
    }

    function execCommandCopy(text) {
        var textarea = document.createElement("textarea");
        textarea.value = text;
        textarea.setAttribute("readonly", "");
        textarea.style.position = "fixed";
        textarea.style.top = "-9999px";
        document.body.appendChild(textarea);
        textarea.select();

        var succeeded = false;
        try {
            succeeded = document.execCommand("copy");
        } catch (err) {
            succeeded = false;
        } finally {
            document.body.removeChild(textarea);
        }

        return succeeded;
    }

    function downloadTextFile(fileName, content, contentType) {
        var blob = new Blob([content], { type: contentType || "text/plain;charset=utf-8" });
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");

        link.href = url;
        link.download = fileName;
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        window.setTimeout(function () {
            URL.revokeObjectURL(url);
        }, 0);
    }

    function getLocalStorageKeys(prefix) {
        var keys = [];

        for (var index = 0; index < localStorage.length; index += 1) {
            var key = localStorage.key(index);

            if (key && key.indexOf(prefix) === 0) {
                keys.push(key);
            }
        }

        return keys;
    }

    function trapFocus(root) {
        if (!root) {
            return;
        }

        var focusableSelector = [
            "a[href]",
            "button:not([disabled])",
            "textarea:not([disabled])",
            "input:not([disabled])",
            "select:not([disabled])",
            "[tabindex]:not([tabindex='-1'])"
        ].join(",");

        function getFocusable() {
            return Array.prototype.slice.call(root.querySelectorAll(focusableSelector))
                .filter(function (element) {
                    return element.offsetWidth > 0 ||
                        element.offsetHeight > 0 ||
                        element === document.activeElement;
                });
        }

        function onKeyDown(event) {
            if (event.key !== "Tab") {
                return;
            }

            var focusable = getFocusable();
            if (focusable.length === 0) {
                event.preventDefault();
                root.focus();
                return;
            }

            var first = focusable[0];
            var last = focusable[focusable.length - 1];

            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }

        root.addEventListener("keydown", onKeyDown);

        window.setTimeout(function () {
            var autofocus = root.querySelector("[data-autofocus]");
            var focusable = getFocusable();
            (autofocus || focusable[0] || root).focus();
        }, 0);
    }

    window.totalCallActions = {
        copyText: copyText,
        downloadTextFile: downloadTextFile,
        getLocalStorageKeys: getLocalStorageKeys,
        trapFocus: trapFocus
    };
})();
