/**
 * SCORM 1.2 API Wrapper - Enterprise VR Training Bridge
 * Handles all communication with the LMS via SCORM 1.2 API.
 */
var ScormAPI = (function () {
    "use strict";

    var _api = null;
    var _initialized = false;

    /**
     * Searches up the window hierarchy to find the SCORM API object.
     */
    function _findAPI(win) {
        var attempts = 0;
        var maxAttempts = 500;
        while ((!win.API) && (win.parent) && (win.parent !== win) && (attempts < maxAttempts)) {
            attempts++;
            win = win.parent;
        }
        return win.API || null;
    }

    /**
     * Locates the SCORM API from opener or parent windows.
     */
    function _getAPI() {
        var api = null;
        if (window.opener && typeof window.opener !== "undefined") {
            api = _findAPI(window.opener);
        }
        if (!api) {
            api = _findAPI(window);
        }
        return api;
    }

    return {
        /**
         * Initializes the SCORM session with the LMS.
         * @returns {boolean} True if initialization succeeded.
         */
        initialize: function () {
            _api = _getAPI();
            if (!_api) {
                console.warn("SCORM API not found. Running in standalone mode.");
                return false;
            }
            var result = _api.LMSInitialize("");
            _initialized = (result === "true" || result === true);
            if (_initialized) {
                console.log("SCORM session initialized.");
            }
            return _initialized;
        },

        /**
         * Gets learner name from LMS.
         * @returns {string} Learner full name.
         */
        getLearnerName: function () {
            if (!_api) return "Unknown Learner";
            return _api.LMSGetValue("cmi.core.student_name") || "Unknown Learner";
        },

        /**
         * Gets learner ID from LMS.
         * @returns {string} Learner ID.
         */
        getLearnerID: function () {
            if (!_api) return "unknown";
            return _api.LMSGetValue("cmi.core.student_id") || "unknown";
        },

        /**
         * Sets the lesson status in the LMS.
         * @param {string} status - One of: "passed", "failed", "completed", "incomplete", "browsed", "not attempted"
         */
        setStatus: function (status) {
            if (!_api) return;
            _api.LMSSetValue("cmi.core.lesson_status", status);
            _api.LMSCommit("");
        },

        /**
         * Sets the score in the LMS.
         * @param {number} raw - Raw score value.
         * @param {number} min - Minimum possible score.
         * @param {number} max - Maximum possible score.
         */
        setScore: function (raw, min, max) {
            if (!_api) return;
            _api.LMSSetValue("cmi.core.score.raw", String(raw));
            _api.LMSSetValue("cmi.core.score.min", String(min));
            _api.LMSSetValue("cmi.core.score.max", String(max));
            _api.LMSCommit("");
        },

        /**
         * Commits all pending data and terminates the SCORM session.
         */
        finish: function () {
            if (!_api) return;
            _api.LMSCommit("");
            _api.LMSFinish("");
            _initialized = false;
            console.log("SCORM session finished.");
        },

        /**
         * @returns {boolean} Whether SCORM is connected to an LMS.
         */
        isConnected: function () {
            return _initialized;
        }
    };
})();