// Citation chips: hover / click to preview chunk text; only one popover open at a time.
(function () {
    "use strict";

    var openWrap = null;
    var hideTimer = null;

    function clearHideTimer() {
        if (hideTimer) {
            window.clearTimeout(hideTimer);
            hideTimer = null;
        }
    }

    function closePopover(wrap) {
        if (!wrap) { return; }
        var pop = wrap.querySelector(".citation-popover");
        var btn = wrap.querySelector(".citation-chip");
        if (pop) { pop.hidden = true; }
        if (btn) { btn.setAttribute("aria-expanded", "false"); btn.classList.remove("active"); }
        if (openWrap === wrap) { openWrap = null; }
    }

    function closeAll() {
        document.querySelectorAll(".citation-chip-wrap").forEach(closePopover);
    }

    function openPopover(wrap) {
        clearHideTimer();
        if (openWrap && openWrap !== wrap) { closePopover(openWrap); }
        var pop = wrap.querySelector(".citation-popover");
        var btn = wrap.querySelector(".citation-chip");
        if (!pop || !btn) { return; }
        pop.hidden = false;
        btn.setAttribute("aria-expanded", "true");
        btn.classList.add("active");
        openWrap = wrap;
    }

    function scheduleClose(wrap) {
        clearHideTimer();
        hideTimer = window.setTimeout(function () {
            if (openWrap === wrap && !wrap.classList.contains("is-pinned")) {
                closePopover(wrap);
            }
        }, 180);
    }

    document.addEventListener("mouseover", function (event) {
        var wrap = event.target.closest && event.target.closest(".citation-chip-wrap");
        if (!wrap) { return; }
        openPopover(wrap);
    });

    document.addEventListener("mouseout", function (event) {
        var wrap = event.target.closest && event.target.closest(".citation-chip-wrap");
        if (!wrap) { return; }
        var related = event.relatedTarget;
        if (related && wrap.contains(related)) { return; }
        if (wrap.classList.contains("is-pinned")) { return; }
        scheduleClose(wrap);
    });

    document.addEventListener("click", function (event) {
        var closeBtn = event.target.closest && event.target.closest(".citation-popover__close");
        if (closeBtn) {
            var wrapClose = closeBtn.closest(".citation-chip-wrap");
            if (wrapClose) {
                wrapClose.classList.remove("is-pinned");
                closePopover(wrapClose);
            }
            return;
        }

        var chip = event.target.closest && event.target.closest(".citation-chip");
        if (chip) {
            event.preventDefault();
            var wrap = chip.closest(".citation-chip-wrap");
            if (!wrap) { return; }
            if (wrap.classList.contains("is-pinned") && openWrap === wrap) {
                wrap.classList.remove("is-pinned");
                closePopover(wrap);
            } else {
                closeAll();
                wrap.classList.add("is-pinned");
                openPopover(wrap);
            }
            return;
        }

        if (!event.target.closest(".citation-chip-wrap")) {
            document.querySelectorAll(".citation-chip-wrap.is-pinned").forEach(function (w) {
                w.classList.remove("is-pinned");
            });
            closeAll();
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            document.querySelectorAll(".citation-chip-wrap.is-pinned").forEach(function (w) {
                w.classList.remove("is-pinned");
            });
            closeAll();
        }
    });
})();
