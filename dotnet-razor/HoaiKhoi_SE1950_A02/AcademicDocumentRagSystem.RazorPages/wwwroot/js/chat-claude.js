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
            '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z"/><path d="M20 3v4M22 5h-4M4 17v2M5 18H3"/></svg>' +
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
