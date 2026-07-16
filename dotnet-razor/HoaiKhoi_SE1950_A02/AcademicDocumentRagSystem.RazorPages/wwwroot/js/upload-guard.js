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
    var processingPanel = form.querySelector("[data-upload-processing]");
    var content = form.querySelector("[data-upload-content]");
    var processingFileName = form.querySelector("[data-upload-file-name]");
    var processingIndicator = form.querySelector("[data-processing-indicator]");
    var processingTitle = form.querySelector("[data-processing-title]");
    var processingNote = form.querySelector("[data-processing-note]");
    var formErrors = form.querySelector("[data-upload-errors]");
    var defaultLabel = submitBtn ? (submitBtn.getAttribute("data-default-label") || submitBtn.textContent.trim()) : "";
    var submitting = false;
    var stepTimers = [];
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
        form.setAttribute("aria-busy", active ? "true" : "false");
        form.classList.toggle("is-processing", active);

        if (content) {
            if (active) { content.setAttribute("inert", ""); }
            else { content.removeAttribute("inert"); }
        }
        if (processingPanel) {
            processingPanel.setAttribute("aria-hidden", active ? "false" : "true");
        }
        if (submitBtn) {
            submitBtn.disabled = active;
            submitBtn.textContent = active ? "Đang xử lý…" : defaultLabel;
        }

        if (active) {
            form.classList.remove("is-complete", "is-failed");
            if (processingIndicator) { processingIndicator.removeAttribute("data-status"); }
            if (processingTitle) { processingTitle.textContent = "Đang xử lý tài liệu"; }
            if (processingNote) { processingNote.textContent = "Thời gian xử lý phụ thuộc kích thước và định dạng file."; }
            if (formErrors) {
                formErrors.textContent = "";
                formErrors.classList.remove("validation-summary-errors");
                formErrors.classList.add("validation-summary-valid");
            }
            var selectedFile = fileInput && fileInput.files && fileInput.files[0];
            if (processingFileName) {
                processingFileName.textContent = selectedFile
                    ? selectedFile.name + " đang được chuẩn bị để index."
                    : "Vui lòng giữ trang này mở.";
            }
            startStepAnimation();
        } else {
            resetStepAnimation();
        }
    }

    function finishProcessing(status, message) {
        resetStepAnimation();
        var steps = Array.prototype.slice.call(form.querySelectorAll("[data-upload-step]"));
        steps.forEach(function (step) {
            step.classList.remove("is-active", "is-failed");
            step.classList.add("is-complete");
        });

        if (status === "success") {
            form.classList.add("is-complete");
            if (processingTitle) { processingTitle.textContent = "Index thành công"; }
        } else {
            form.classList.add("is-failed");
            var indexStep = form.querySelector('[data-upload-step="index"]');
            if (indexStep) {
                indexStep.classList.remove("is-complete");
                indexStep.classList.add("is-failed");
            }
            if (processingTitle) { processingTitle.textContent = "Index thất bại"; }
        }

        if (processingFileName) { processingFileName.textContent = message; }
        if (processingNote) { processingNote.textContent = "Đang mở chi tiết pipeline…"; }
        if (processingIndicator) { processingIndicator.setAttribute("data-status", status); }
    }

    function showServerError(html) {
        var parsed = new DOMParser().parseFromString(html, "text/html");
        var parsedSummary = parsed.querySelector("[data-upload-errors], .validation-summary-errors");
        var message = parsedSummary ? parsedSummary.textContent.trim() : "Không thể upload tài liệu. Vui lòng thử lại.";

        setSubmitting(false);
        if (formErrors) {
            formErrors.textContent = message;
            formErrors.classList.remove("validation-summary-valid");
            formErrors.classList.add("validation-summary-errors");
            formErrors.removeAttribute("hidden");
            formErrors.scrollIntoView({ behavior: "smooth", block: "center" });
        }
    }

    function detailsUrlWithResult(url, result) {
        var separator = url.indexOf("?") >= 0 ? "&" : "?";
        return url + separator + "indexResult=" + result + "#chunk-preview";
    }

    function handleUploadResponse(response) {
        return response.text().then(function (html) {
            var responseUrl = new URL(response.url, window.location.origin);
            if (responseUrl.pathname.toLowerCase() === "/login") {
                window.location.assign(response.url);
                return;
            }

            var parsed = new DOMParser().parseFromString(html, "text/html");
            var resultRoot = parsed.querySelector("[data-index-result]");
            var statusRoot = parsed.querySelector("[data-highlight-details-url]");
            var detailsUrl = statusRoot ? statusRoot.getAttribute("data-highlight-details-url") : "";
            var indexStatus = resultRoot ? resultRoot.getAttribute("data-result-status") : "";
            var resultMessage = resultRoot ? resultRoot.querySelector("[data-result-message]") : null;

            if (!detailsUrl || (indexStatus !== "Indexed" && indexStatus !== "Failed")) {
                showServerError(html);
                return;
            }

            var succeeded = indexStatus === "Indexed";
            var result = succeeded ? "success" : "failed";
            var fallbackMessage = succeeded
                ? "Tài liệu đã được chunk và index thành công."
                : "Không thể hoàn tất index tài liệu.";
            finishProcessing(result, resultMessage ? resultMessage.textContent.trim() : fallbackMessage);

            window.setTimeout(function () {
                window.location.assign(detailsUrlWithResult(detailsUrl, result));
            }, 1400);
        });
    }

    function setActiveStep(name) {
        var steps = Array.prototype.slice.call(form.querySelectorAll("[data-upload-step]"));
        var targetIndex = steps.findIndex(function (step) {
            return step.getAttribute("data-upload-step") === name;
        });
        steps.forEach(function (step, index) {
            step.classList.toggle("is-complete", index < targetIndex);
            step.classList.toggle("is-active", index === targetIndex);
        });
    }

    function resetStepAnimation() {
        stepTimers.forEach(window.clearTimeout);
        stepTimers = [];
        setActiveStep("upload");
    }

    function startStepAnimation() {
        resetStepAnimation();
        stepTimers.push(window.setTimeout(function () { setActiveStep("chunk"); }, 1200));
        stepTimers.push(window.setTimeout(function () { setActiveStep("index"); }, 3200));
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

        if (typeof window.fetch !== "function" || typeof window.FormData !== "function") {
            setSubmitting(true);
            return;
        }

        event.preventDefault();
        var payload = new FormData(form);
        setSubmitting(true);

        window.fetch(form.action || window.location.href, {
            method: "POST",
            body: payload,
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        })
            .then(handleUploadResponse)
            .catch(function () {
                setSubmitting(false);
                if (formErrors) {
                    formErrors.textContent = "Mất kết nối trong khi xử lý tài liệu. Vui lòng kiểm tra dịch vụ và thử lại.";
                    formErrors.classList.remove("validation-summary-valid");
                    formErrors.classList.add("validation-summary-errors");
                    formErrors.scrollIntoView({ behavior: "smooth", block: "center" });
                }
            });
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
