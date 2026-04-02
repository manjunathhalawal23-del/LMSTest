/**
 * SCORM 1.2 API wrapper for VR Training.
 * Finds the LMS API, reads learner info, and commits scores.
 */
var ScormDriver = (function () {
    "use strict";

    var _api = null;
    var _initialized = false;

    /** Scan parent/opener frames to find the SCORM 1.2 API object. */
    function _findAPI(win) {
        var attempts = 0;
        while (win && !win.API && attempts < 10) {
            if (win.parent && win.parent !== win) {
                win = win.parent;
            } else if (win.opener) {
                win = win.opener;
            } else {
                break;
            }
            attempts++;
        }
        return win && win.API ? win.API : null;
    }

    function initialize() {
        _api = _findAPI(window);
        if (!_api) {
            console.warn("[ScormDriver] SCORM API not found. Running outside LMS?");
            return false;
        }
        var result = _api.LMSInitialize("");
        _initialized = (result === "true" || result === true);
        if (_initialized) {
            _api.LMSSetValue("cmi.core.lesson_status", "incomplete");
            _api.LMSCommit("");
        }
        return _initialized;
    }

    function getValue(key) {
        if (!_api) return "";
        return _api.LMSGetValue(key) || "";
    }

    function setValue(key, value) {
        if (!_api) return;
        _api.LMSSetValue(key, String(value));
    }

    function commit() {
        if (!_api) return;
        _api.LMSCommit("");
    }

    function finish() {
        if (!_api) return;
        _api.LMSFinish("");
        _initialized = false;
    }

    /** Read learner identity from the LMS. */
    function getLearnerInfo() {
        return {
            learnerName: getValue("cmi.core.student_name") || "Unknown Learner",
            learnerID: getValue("cmi.core.student_id") || "unknown"
        };
    }

    /** Write final score and status back to the LMS. */
    function setScore(raw, min, max, passingScore) {
        setValue("cmi.core.score.raw", raw);
        setValue("cmi.core.score.min", min);
        setValue("cmi.core.score.max", max);

        var status = (raw >= passingScore) ? "passed" : "failed";
        setValue("cmi.core.lesson_status", status);
        commit();
    }

    /** Write session time in SCORM 1.2 format (HHHH:MM:SS). */
    function setSessionTime(durationStr) {
        // durationStr expected as "HH:MM:SS"
        setValue("cmi.core.session_time", durationStr);
        commit();
    }

    return {
        initialize: initialize,
        getLearnerInfo: getLearnerInfo,
        setScore: setScore,
        setSessionTime: setSessionTime,
        commit: commit,
        finish: finish,
        isAvailable: function () { return _api !== null; }
    };
})();