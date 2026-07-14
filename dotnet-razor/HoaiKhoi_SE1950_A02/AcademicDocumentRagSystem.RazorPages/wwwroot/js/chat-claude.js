// Claude-like chat UX: optimistic user bubble, cleared input, typing
// indicator, then swap in the server-rendered thread. Uses the existing
// Razor Pages "Ask" handler via fetch — no backend changes.
(function () {
    "use strict";

    var form = document.querySelector("form[data-chat-ask]");
    if (!form) { return; }

    var input = form.querySelector("input[name='AskForm.Question']");
    var sendBtn = form.querySelector("button[type='submit']");
    var thread = document.querySelector(".chat-main__thread");
    var messages = document.querySelector(".chat-main__messages");
    if (!input || !thread || !messages) { return; }

    function scrollToBottom(smooth) {
        messages.scrollTo({ top: messages.scrollHeight, behavior: smooth ? "smooth" : "auto" });
    }

    function removeEmptyState() {
        var empty = thread.querySelector("p.text-muted-edurag.text-center");
        if (empty) { empty.remove(); }
    }

    function appendUserBubble(text) {
        var row = document.createElement("div");
        row.className = "d-flex justify-content-end mb-3 chat-msg-in";
        var bubble = document.createElement("div");
        bubble.className = "chat-bubble-user";
        bubble.textContent = text;
        row.appendChild(bubble);
        thread.appendChild(row);
        return row;
    }

    function appendTypingIndicator() {
        var row = document.createElement("div");
        row.className = "d-flex gap-3 mb-4 chat-typing-row chat-msg-in";
        row.innerHTML =
            '<div class="chat-avatar">' +
            '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M4 19.5v-15A2.5 2.5 0 0 1 6.5 2H20v20H6.5a2.5 2.5 0 0 1 0-5H20"/></svg>' +
            '</div>' +
            '<div class="chat-bubble-assistant chat-typing" aria-label="Đang trả lời">' +
            '<span class="chat-typing__dot"></span><span class="chat-typing__dot"></span><span class="chat-typing__dot"></span>' +
            '</div>';
        thread.appendChild(row);
        return row;
    }

    function appendErrorBubble(message) {
        var row = document.createElement("div");
        row.className = "d-flex gap-3 mb-4";
        var bubble = document.createElement("div");
        bubble.className = "chat-bubble-assistant";
        bubble.style.borderColor = "#f3c6c6";
        bubble.style.background = "#fdf3f3";
        bubble.textContent = message;
        row.appendChild(bubble);
        thread.appendChild(row);
    }

    var pending = false;

    function setBusy(busy) {
        pending = busy;
        if (sendBtn) { sendBtn.disabled = busy; }
    }

    function replaceIfBoth(doc, selector) {
        var fresh = doc.querySelector(selector);
        var current = document.querySelector(selector);
        if (fresh && current) { current.replaceWith(fresh); }
    }

    form.addEventListener("submit", function (event) {
        event.preventDefault();
        if (pending) { return; }

        var question = (input.value || "").trim();
        if (!question) { return; }

        // Capture form data BEFORE clearing the input.
        input.value = question;
        var formData = new FormData(form);

        removeEmptyState();
        appendUserBubble(question);
        var typing = appendTypingIndicator();
        input.value = "";
        setBusy(true);
        scrollToBottom(true);

        fetch(form.action || window.location.href, {
            method: "POST",
            body: formData,
            headers: { "X-Requested-With": "XMLHttpRequest" },
            credentials: "same-origin"
        }).then(function (response) {
            if (!response.ok) {
                throw new Error("HTTP " + response.status);
            }
            return response.text().then(function (html) {
                return { html: html, url: response.url };
            });
        }).then(function (result) {
            var doc = new DOMParser().parseFromString(result.html, "text/html");
            var freshThread = doc.querySelector(".chat-main__thread");
            if (!freshThread) {
                throw new Error("Không đọc được phản hồi từ máy chủ.");
            }

            thread.innerHTML = freshThread.innerHTML;

            // Session id can change when the server creates a new chat session.
            var freshSession = doc.querySelector("input[name='AskForm.ChatSessionId']");
            var currentSession = form.querySelector("input[name='AskForm.ChatSessionId']");
            if (freshSession && currentSession) { currentSession.value = freshSession.value; }

            // Keep sidebars (recent sessions / source docs) in sync.
            replaceIfBoth(doc, ".student-sidebar__body");
            replaceIfBoth(doc, ".chat-sources");

            // Surface server-side errors (e.g. RAG service down) shown as alerts.
            var alert = doc.querySelector(".chat-main .alert-danger");
            if (alert) { appendErrorBubble(alert.textContent.trim()); }

            if (result.url) {
                window.history.replaceState(null, "", result.url);
            }
        }).catch(function (err) {
            appendErrorBubble("Không gửi được câu hỏi: " + err.message + ". Vui lòng thử lại.");
            if (!input.value) { input.value = question; }
        }).finally(function () {
            if (typing && typing.parentNode) { typing.remove(); }
            setBusy(false);
            input.focus();
            scrollToBottom(true);
        });
    });

    // Land at the latest message like Claude does.
    scrollToBottom(false);
    input.focus();
})();
