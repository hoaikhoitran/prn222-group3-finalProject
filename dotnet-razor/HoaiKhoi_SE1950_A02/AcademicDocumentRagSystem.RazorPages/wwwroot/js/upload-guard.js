// Prevent double-submit + FE duplicate title/chapter (case-sensitive) on teacher upload.
(function () {
    "use strict";

    var form = document.querySelector("[data-upload-form]");
    if (!form) { return; }

    var submitBtn = form.querySelector("[data-upload-submit]");
    var fileInput = form.querySelector('input[type="file"]');
    var courseSelect = form.querySelector("[data-upload-course]");
    var titleInput = form.querySelector("[data-upload-title]");
    var chapterInput = form.querySelector("[data-upload-chapter]");
    var titleError = form.querySelector("[data-upload-title-error]");
    var chapterError = form.querySelector("[data-upload-chapter-error]");
    var defaultLabel = submitBtn ? (submitBtn.getAttribute("data-default-label") || submitBtn.textContent.trim()) : "";
    var submitting = false;
    var duplicateTitleMessage = "Tiêu đề này đã tồn tại với chương đang chọn trong môn học.";
    var duplicateChapterMessage = "Chương này đã tồn tại với tiêu đề đang chọn trong môn học.";

    var existingKeys = [];
    try {
        var keysEl = document.getElementById("upload-existing-keys");
        existingKeys = JSON.parse(keysEl ? keysEl.textContent : "[]");
        if (!Array.isArray(existingKeys)) { existingKeys = []; }
    } catch (e) {
        existingKeys = [];
    }

    function setSubmitting(active) {
        submitting = active;
        if (!submitBtn) { return; }
        submitBtn.disabled = active;
        submitBtn.textContent = active ? "Đang upload…" : defaultLabel;
    }

    function clearDuplicateErrors() {
        if (titleError) { titleError.textContent = ""; }
        if (chapterError) { chapterError.textContent = ""; }
        if (titleInput) { titleInput.classList.remove("is-invalid"); }
        if (chapterInput) { chapterInput.classList.remove("is-invalid"); }
    }

    function showDuplicateErrors() {
        if (titleError) { titleError.textContent = duplicateTitleMessage; }
        if (chapterError) { chapterError.textContent = duplicateChapterMessage; }
        if (titleInput) { titleInput.classList.add("is-invalid"); }
        if (chapterInput) { chapterInput.classList.add("is-invalid"); }
    }

    // Case-sensitive exact title + chapter match within the selected course.
    function isDuplicateTitleChapter() {
        if (!titleInput || !courseSelect) { return false; }

        var courseId = parseInt(courseSelect.value, 10);
        var title = titleInput.value || "";
        var chapter = chapterInput ? (chapterInput.value || "") : "";

        if (!title || isNaN(courseId)) { return false; }

        return existingKeys.some(function (item) {
            return item.courseId === courseId
                && item.title === title
                && (item.chapter || "") === chapter;
        });
    }

    function validateTitleChapter() {
        clearDuplicateErrors();
        if (isDuplicateTitleChapter()) {
            showDuplicateErrors();
            return false;
        }
        return true;
    }

    function isFormValid() {
        if (!validateTitleChapter()) {
            return false;
        }
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
            event.preventDefault();
            return;
        }
        setSubmitting(true);
    });

    [titleInput, chapterInput, courseSelect].forEach(function (el) {
        if (!el) { return; }
        el.addEventListener("input", validateTitleChapter);
        el.addEventListener("change", validateTitleChapter);
    });

    if (fileInput) {
        fileInput.addEventListener("change", function () {
            setSubmitting(false);
        });
    }

    window.addEventListener("pageshow", function () {
        setSubmitting(false);
    });
})();
