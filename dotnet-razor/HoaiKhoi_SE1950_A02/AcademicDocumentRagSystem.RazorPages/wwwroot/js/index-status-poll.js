// Poll teacher index-status rows until none remain Processing/Pending.
(function () {
    "use strict";

    var root = document.querySelector("[data-index-status]");
    if (!root) { return; }

    var statusesUrl = root.getAttribute("data-statuses-url");
    if (!statusesUrl) { return; }

    var INTERVAL_MS = 2500;
    var timer = null;

    function statusMeta(status) {
        if (status === "Indexed") {
            return { label: "Đã index", className: "status-badge-indexed" };
        }
        if (status === "Failed") {
            return { label: "Lỗi", className: "status-badge-failed" };
        }
        return { label: "Đang xử lý", className: "status-badge-processing" };
    }

    function hasProcessing(items) {
        return items.some(function (d) {
            return d.indexStatus !== "Indexed" && d.indexStatus !== "Failed";
        });
    }

    function currentRows() {
        return Array.prototype.map.call(root.querySelectorAll("[data-document-id]"), function (row) {
            return { indexStatus: row.getAttribute("data-index-status") || "" };
        });
    }

    function updateStats(stats) {
        var statIndexed = root.querySelector("[data-stat-indexed]");
        var statProcessing = root.querySelector("[data-stat-processing]");
        var statChunks = root.querySelector("[data-stat-chunks]");
        var statReady = root.querySelector("[data-stat-ready]");
        if (statIndexed) { statIndexed.textContent = String(stats.indexed); }
        if (statProcessing) { statProcessing.textContent = String(stats.processing); }
        if (statChunks) { statChunks.textContent = Number(stats.totalChunks).toLocaleString("en-US"); }
        if (statReady) {
            statReady.textContent = stats.indexed + "/" + stats.total + " đã sẵn sàng Q&A";
        }
    }

    function updateRow(doc) {
        var row = root.querySelector('[data-document-id="' + doc.documentId + '"]');
        if (!row) { return; }

        var meta = statusMeta(doc.indexStatus);
        var badgeHost = row.querySelector("[data-index-badge]");
        if (badgeHost) {
            badgeHost.innerHTML = "";
            var badge = document.createElement("span");
            badge.className = meta.className;
            badge.textContent = meta.label;
            badgeHost.appendChild(badge);
            if (doc.indexError) {
                var err = document.createElement("div");
                err.className = "small text-danger";
                err.textContent = doc.indexError;
                badgeHost.appendChild(err);
            }
        }

        var chunksCell = row.querySelector("[data-total-chunks]");
        if (chunksCell) {
            chunksCell.textContent = String(doc.totalChunks || 0);
        }

        row.setAttribute("data-index-status", doc.indexStatus || "");
    }

    function stop() {
        if (timer) {
            clearInterval(timer);
            timer = null;
        }
    }

    function poll() {
        fetch(statusesUrl, {
            headers: { "Accept": "application/json" },
            credentials: "same-origin"
        })
            .then(function (res) {
                if (!res.ok) { throw new Error("status " + res.status); }
                return res.json();
            })
            .then(function (payload) {
                var items = payload.documents || [];
                items.forEach(updateRow);
                if (payload.stats) { updateStats(payload.stats); }
                if (!hasProcessing(items)) { stop(); }
            })
            .catch(function () {
                // Keep trying while the page is open; transient network blips are fine.
            });
    }

    function start() {
        if (timer) { return; }
        poll();
        timer = setInterval(poll, INTERVAL_MS);
    }

    window.EduRagIndexStatusPoll = { start: start, stop: stop };

    if (hasProcessing(currentRows())) {
        start();
    }
})();
