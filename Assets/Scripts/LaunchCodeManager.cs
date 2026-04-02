using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Manages launch code validation and result submission to the middleware API.
/// Attach to a persistent GameObject in your Unity VR scene.
/// </summary>
public class LaunchCodeManager : MonoBehaviour
{
    [Header("Middleware Configuration")]
    //[SerializeField] private string apiBaseUrl = "https://your-middleware.azurewebsites.net/api";
    [SerializeField] private string apiBaseUrl = "http://localhost:5080/api";

    [Header("Events")]
    public Action<SessionInfo> OnSessionValidated;
    public Action<string> OnValidationFailed;
    public Action OnResultsSubmitted;
    public Action<string> OnSubmissionFailed;

    /// <summary>
    /// Session information returned after code validation.
    /// </summary>
    [Serializable]
    public class SessionInfo
    {
        public string learnerName;
        public string learnerID;
        public string courseTitle;
        public string expiresUtc;
    }

    /// <summary>
    /// Payload sent to the middleware when training is complete.
    /// </summary>
    [Serializable]
    public class CompletionPayload
    {
        public string code;
        public int scoreRaw;
        public int scoreMin;
        public int scoreMax;
        public int passingScore;
        public string duration;
        public string details;
    }

    private string _activeCode;

    /// <summary>
    /// Validates a 6-digit launch code against the middleware API.
    /// Call this when the learner submits the code from the VR keyboard.
    /// </summary>
    /// <param name="code">The 6-digit launch code entered by the learner.</param>
    public void ValidateCode(string code)
    {
        code = code.Replace(" ", "").Trim();
        if (code.Length != 6)
        {
            OnValidationFailed?.Invoke("Code must be 6 digits.");
            return;
        }
        _activeCode = code;
        StartCoroutine(ValidateCodeCoroutine(code));
    }

    /// <summary>
    /// Submits training results to the middleware API.
    /// Call this when the VR training session is complete.
    /// </summary>
    /// <param name="scoreRaw">The learner's raw score.</param>
    /// <param name="scoreMax">Maximum possible score.</param>
    /// <param name="passingScore">Minimum score to pass.</param>
    /// <param name="durationSeconds">Training duration in seconds.</param>
    /// <param name="details">Optional details or summary text.</param>
    public void SubmitResults(int scoreRaw, int scoreMax, int passingScore,
                              float durationSeconds, string details = "")
    {
        if (string.IsNullOrEmpty(_activeCode))
        {
            OnSubmissionFailed?.Invoke("No active session. Validate a code first.");
            return;
        }

        var payload = new CompletionPayload
        {
            code = _activeCode,
            scoreRaw = scoreRaw,
            scoreMin = 0,
            scoreMax = scoreMax,
            passingScore = passingScore,
            duration = FormatDuration(durationSeconds),
            details = details
        };

        StartCoroutine(SubmitResultsCoroutine(payload));
    }

    private IEnumerator ValidateCodeCoroutine(string code)
    {
        string url = apiBaseUrl + "/session/" + code;
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                SessionInfo session = JsonUtility.FromJson<SessionInfo>(request.downloadHandler.text);
                Debug.Log($"[LaunchCode] Session validated for {session.learnerName}");
                OnSessionValidated?.Invoke(session);
            }
            else
            {
                string error = request.downloadHandler != null ? request.downloadHandler.text : request.error;
                Debug.Log($"[LaunchCode] Validation failed: {error}");
                OnValidationFailed?.Invoke(error);
            }
        }
    }

    private IEnumerator SubmitResultsCoroutine(CompletionPayload payload)
    {
        string url = apiBaseUrl + "/session/complete";
        string json = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[LaunchCode] Results submitted successfully.");
                OnResultsSubmitted?.Invoke();
            }
            else
            {
                string error = request.downloadHandler != null ? request.downloadHandler.text : request.error;
                Debug.LogWarning($"[LaunchCode] Submission failed: {error}");
                OnSubmissionFailed?.Invoke(error);
            }
        }
    }

    private string FormatDuration(float totalSeconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(totalSeconds);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", ts.Hours, ts.Minutes, ts.Seconds);
    }
}