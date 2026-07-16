// Shared branded confirmation dialog for links with data-confirm or data-logout-confirm.
(function () {
    "use strict";

    var root = document.querySelector("[data-confirm-dialog]");
    if (!root) { return; }

    var panel = root.querySelector(".edurag-confirm__panel");
    var title = root.querySelector("[data-confirm-title]");
    var message = root.querySelector("[data-confirm-message]");
    var accept = root.querySelector("[data-confirm-accept]");
    var pendingTrigger = null;
    var previousFocus = null;
    var closeTimer = null;

    function read(trigger, name, fallback) {
        return trigger.getAttribute(name) || fallback;
    }

    function open(trigger) {
        if (closeTimer) {
            window.clearTimeout(closeTimer);
            closeTimer = null;
        }

        pendingTrigger = trigger;
        previousFocus = document.activeElement;
        title.textContent = read(trigger, "data-confirm-title", "Xác nhận đăng xuất");
        message.textContent = read(trigger, "data-confirm-message", "Bạn có chắc muốn đăng xuất khỏi EduRAG?");
        accept.textContent = read(trigger, "data-confirm-label", "Đăng xuất");
        root.removeAttribute("hidden");
        document.body.classList.add("confirm-dialog-open");

        window.requestAnimationFrame(function () {
            root.classList.add("is-open");
            accept.focus();
        });
    }

    function close() {
        root.classList.remove("is-open");
        document.body.classList.remove("confirm-dialog-open");
        pendingTrigger = null;

        closeTimer = window.setTimeout(function () {
            root.setAttribute("hidden", "");
            closeTimer = null;
            if (previousFocus && typeof previousFocus.focus === "function") {
                previousFocus.focus();
            }
            previousFocus = null;
        }, 180);
    }

    document.addEventListener("click", function (event) {
        var trigger = event.target.closest && event.target.closest("[data-confirm], [data-logout-confirm]");
        if (trigger) {
            event.preventDefault();
            open(trigger);
            return;
        }

        if (event.target.closest && event.target.closest("[data-confirm-cancel]")) {
            close();
        }
    });

    accept.addEventListener("click", function () {
        if (!pendingTrigger) { return; }
        var destination = pendingTrigger.getAttribute("href");
        if (destination) {
            window.location.assign(destination);
        }
    });

    document.addEventListener("keydown", function (event) {
        if (root.hasAttribute("hidden")) { return; }
        if (event.key === "Escape") {
            event.preventDefault();
            close();
            return;
        }
        if (event.key === "Tab") {
            var cancel = root.querySelector("button[data-confirm-cancel]");
            if (event.shiftKey && document.activeElement === cancel) {
                event.preventDefault();
                accept.focus();
            } else if (!event.shiftKey && document.activeElement === accept) {
                event.preventDefault();
                cancel.focus();
            }
        }
    });
})();
