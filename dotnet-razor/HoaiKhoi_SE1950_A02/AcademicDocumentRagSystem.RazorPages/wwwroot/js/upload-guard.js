// Prevent double-submit on teacher upload. Re-enable when a new file is chosen.
(function () {
    "use strict";

    var form = document.querySelector("[data-upload-form]");
    if (!form) { return; }

    var submitBtn = form.querySelector("[data-upload-submit]");
    var fileInput = form.querySelector('input[type="file"]');
    var defaultLabel = submitBtn ? (submitBtn.getAttribute("data-default-label") || submitBtn.textContent.trim()) : "";
    var submitting = false;

    function setSubmitting(active) {
        submitting = active;
        if (!submitBtn) { return; }
        submitBtn.disabled = active;
        submitBtn.textContent = active ? "Đang upload…" : defaultLabel;
    }

    function isFormValid() {
        if (window.jQuery) {
            var $form = window.jQuery(form);
            if ($form.length && $form.data("validator")) {
                return $form.valid();
            }
        }
        return typeof form.checkValidity !== "function" || form.checkValidity();
    }

    form.addEventListener("submit", function (event) {
        if (submitting) {
            event.preventDefault();
            return;
        }
        if (!isFormValid()) {
            return;
        }
        setSubmitting(true);
    });

    if (fileInput) {
        fileInput.addEventListener("change", function () {
            // New file selected → allow a fresh upload (do not stay locked on the old attempt).
            setSubmitting(false);
        });
    }

    // Back/forward cache or cancelled navigation should never leave the button stuck.
    window.addEventListener("pageshow", function () {
        setSubmitting(false);
    });
})();
