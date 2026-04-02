/**
 * SCORM Launch Page - Enterprise VR Training Bridge
 * 
 * Flow:
 * 1. SCORM page opens in LMS → generates 6-digit code
 * 2. Learner enters code in Quest VR app
 * 3. VR app validates code, runs training, posts results
 * 4. This page polls for results and commits them to LMS
 */
(function () {
    "use strict";

    // =====================================================================
    // CONFIGURATION - Change this to your deployed middleware URL
    // =====================================================================
    var CONFIG = {
        API_BASE_URL: "https://your-middleware.azurewebsites.net/api",
        POLL_INTERVAL_MS: 5000,
        SESSION_EXPIRY_MINUTES: 60,
        QR_SIZE: 200
    };

    var _sessionCode = "";
    var _pollTimer = null;
    var _learnerName = "";
    var _learnerID = "";

    // =====================================================================
    // INITIALIZATION
    // =====================================================================

    /**
     * Entry point - called when the page loads.
     */
    function init() {
        ScormAPI.initialize();
        _learnerName = ScormAPI.getLearnerName();
        _learnerID = ScormAPI.getLearnerID();
        ScormAPI.setStatus("incomplete");

        document.getElementById("learner-name").textContent = _learnerName;

        createSession();
    }

    // =====================================================================
    // SESSION MANAGEMENT
    // =====================================================================

    /**
     * Creates a new training session via the middleware API.
     * Generates a 6-digit code and QR code for the VR app.
     */
    function createSession() {
        showPhase("loading");

        var payload = {
            learnerName: _learnerName,
            learnerID: _learnerID,
            courseTitle: "VR Safety Training",
            expiryMinutes: CONFIG.SESSION_EXPIRY_MINUTES
        };

        fetch(CONFIG.API_BASE_URL + "/session/create", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        })
        .then(function (response) {
            if (!response.ok) throw new Error("Failed to create session");
            return response.json();
        })
        .then(function (data) {
            _sessionCode = data.code;
            displayLaunchCode(_sessionCode);
            generateQRCode(_sessionCode);
            showPhase("launch");
            startPolling();
        })
        .catch(function (err) {
            console.error("Session creation failed:", err);
            // Fallback: generate code client-side for POC/demo
            _sessionCode = generateOfflineCode();
            displayLaunchCode(_sessionCode);
            generateQRCode(_sessionCode);
            showPhase("launch");
            startPolling();
        });
    }

    /**
     * Generates a random 6-digit code for offline/demo mode.
     * @returns {string} A 6-digit numeric string.
     */
    function generateOfflineCode() {
        return String(Math.floor(100000 + Math.random() * 900000));
    }

    /**
     * Displays the 6-digit launch code on screen.
     * @param {string} code - The 6-digit code.
     */
    function displayLaunchCode(code) {
        var display = document.getElementById("launch-code");
        display.textContent = code.substring(0, 3) + " " + code.substring(3);
    }

    /**
     * Generates a QR code containing the launch code.
     * Uses a simple Google Charts API for POC. Replace with a library for production.
     * @param {string} code - The 6-digit code.
     */
    function generateQRCode(code) {
        var qrContainer = document.getElementById("qr-code");
        var qrUrl = "https://api.qrserver.com/v1/create-qr-code/?size=" +
                    CONFIG.QR_SIZE + "x" + CONFIG.QR_SIZE +
                    "&data=" + encodeURIComponent(code);
        qrContainer.innerHTML = '<img src="' + qrUrl + '" alt="QR Code" width="' +
                                CONFIG.QR_SIZE + '" height="' + CONFIG.QR_SIZE + '"/>';
    }

    // =====================================================================
    // POLLING FOR RESULTS
    // =====================================================================

    /**
     * Starts polling the middleware for training completion results.
     */
    function startPolling() {
        if (_pollTimer) clearInterval(_pollTimer);

        _pollTimer = setInterval(function () {
            pollForResults();
        }, CONFIG.POLL_INTERVAL_MS);
    }

    /**
     * Polls the middleware API to check if the VR training is complete.
     */
    function pollForResults() {
        fetch(CONFIG.API_BASE_URL + "/session/" + _sessionCode + "/result", {
            method: "GET",
            headers: { "Content-Type": "application/json" }
        })
        .then(function (response) {
            if (!response.ok) return null;
            return response.json();
        })
        .then(function (data) {
            if (data && data.completed) {
                clearInterval(_pollTimer);
                onTrainingCompleted(data);
            }
        })
        .catch(function (err) {
            console.warn("Polling error (will retry):", err);
        });
    }

    // =====================================================================
    // COMPLETION HANDLING
    // =====================================================================

    /**
     * Called when the VR app reports training completion.
     * Commits all results to the LMS via SCORM and shows the results page.
     * @param {object} data - Result data from the middleware.
     */
    function onTrainingCompleted(data) {
        // Commit to SCORM
        var score = data.scoreRaw || 0;
        var maxScore = data.scoreMax || 100;
        var minScore = data.scoreMin || 0;
        var passed = score >= (data.passingScore || 70);

        ScormAPI.setScore(score, minScore, maxScore);
        ScormAPI.setStatus(passed ? "passed" : "failed");

        // Display results
        document.getElementById("result-score").textContent = score + " / " + maxScore;
        document.getElementById("result-status").textContent = passed ? "PASSED" : "FAILED";
        document.getElementById("result-status").className = "status-badge " + (passed ? "passed" : "failed");
        document.getElementById("result-duration").textContent = data.duration || "N/A";
        document.getElementById("result-details").textContent = data.details || "";

        showPhase("results");

        // Finish SCORM session after a brief delay to ensure LMS captures the data
        setTimeout(function () {
            ScormAPI.finish();
        }, 2000);
    }

    // =====================================================================
    // UI HELPERS
    // =====================================================================

    /**
     * Shows the specified phase panel and hides all others.
     * @param {string} phase - One of "loading", "launch", "results"
     */
    function showPhase(phase) {
        var phases = ["loading", "launch", "results"];
        phases.forEach(function (p) {
            var el = document.getElementById("phase-" + p);
            if (el) el.style.display = (p === phase) ? "block" : "none";
        });
    }

    // =====================================================================
    // BOOT
    // =====================================================================
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();