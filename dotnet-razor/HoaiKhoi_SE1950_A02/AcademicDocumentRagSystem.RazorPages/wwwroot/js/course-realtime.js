// Real-time course cards for teachers (MVC).
// Subscribes to CourseHub at /hubs/courses; refreshes the card grid when admin CRUD succeeds.
(function () {
    "use strict";

    var listEl = document.getElementById("course-list");
    if (!listEl) {
        return;
    }

    if (typeof signalR === "undefined") {
        console.warn("SignalR client not loaded; live course updates are disabled.");
        return;
    }

    var refreshUrl = listEl.getAttribute("data-refresh-url");
    var indicator = document.getElementById("live-indicator");

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/courses")
        .withAutomaticReconnect()
        .build();

    function flash() {
        if (!indicator) {
            return;
        }
        indicator.classList.add("live-indicator--active");
        setTimeout(function () {
            indicator.classList.remove("live-indicator--active");
        }, 1500);
    }

    function refreshCourseList() {
        fetch(refreshUrl, {
            headers: { "X-Requested-With": "XMLHttpRequest" },
            credentials: "same-origin"
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Refresh failed with status " + response.status);
                }
                return response.text();
            })
            .then(function (html) {
                listEl.innerHTML = html;
                flash();
            })
            .catch(function (err) {
                console.error("Could not refresh course list:", err);
            });
    }

    connection.on("CoursesChanged", refreshCourseList);

    connection.start().catch(function (err) {
        console.error("Could not connect to CourseHub:", err);
    });
})();
