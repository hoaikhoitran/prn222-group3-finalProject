// Persist sidebar collapse across dashboard pages.
// Labels clip with the right edge; icons stay on a fixed column (no end-of-animation snap).
(function () {
    "use strict";

    var KEY = "edurag.sidebarCollapsed";
    var transitionTimer = null;

    function isCollapsed() {
        try { return localStorage.getItem(KEY) === "1"; } catch (e) { return false; }
    }

    function syncToggleUi(collapsed) {
        document.querySelectorAll("[data-sidebar-toggle]").forEach(function (btn) {
            btn.setAttribute("aria-expanded", collapsed ? "false" : "true");
            btn.title = collapsed ? "Mở rộng thanh bên" : "Thu gọn thanh bên";
            btn.blur();
        });
    }

    function setCollapsed(collapsed, animate) {
        try { localStorage.setItem(KEY, collapsed ? "1" : "0"); } catch (e) { /* ignore */ }
        var root = document.documentElement;
        if (transitionTimer) {
            window.clearTimeout(transitionTimer);
            transitionTimer = null;
        }
        root.classList.remove("sidebar-collapsing", "sidebar-expanding");
        if (animate) {
            root.classList.add(collapsed ? "sidebar-collapsing" : "sidebar-expanding");
        }
        root.classList.toggle("sidebar-collapsed", collapsed);
        root.classList.remove("sidebar-collapsed-settled");
        syncToggleUi(collapsed);

        if (animate) {
            transitionTimer = window.setTimeout(function () {
                root.classList.remove("sidebar-collapsing", "sidebar-expanding");
                transitionTimer = null;
            }, 320);
        }
    }

    function lockHoverReveal(slot) {
        if (!slot) { return; }
        slot.classList.add("sidebar-hover-lock");
        var unlock = function () {
            slot.classList.remove("sidebar-hover-lock");
            slot.removeEventListener("mouseleave", unlock);
        };
        slot.addEventListener("mouseleave", unlock);
    }

    setCollapsed(isCollapsed(), false);

    document.addEventListener("click", function (event) {
        var logout = event.target.closest && event.target.closest("[data-logout-confirm]");
        if (logout) {
            if (!window.confirm("Bạn có chắc muốn đăng xuất?")) {
                event.preventDefault();
            }
            return;
        }

        var btn = event.target.closest && event.target.closest("[data-sidebar-toggle]");
        if (!btn) { return; }
        event.preventDefault();

        var willCollapse = !document.documentElement.classList.contains("sidebar-collapsed");
        setCollapsed(willCollapse, true);

        if (willCollapse) {
            lockHoverReveal(btn.closest(".sidebar-brand-slot"));
        }
    });
})();
