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

    window.totalCallActions = {
        copyText: copyText,
        downloadTextFile: downloadTextFile
    };
})();
