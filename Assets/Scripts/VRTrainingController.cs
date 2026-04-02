using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Example controller showing how to integrate LaunchCodeManager
/// with your existing VR training and scoring system.
/// </summary>
public class VRTrainingController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LaunchCodeManager launchCodeManager;
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject codeEntryPanel;
    [SerializeField] private GameObject trainingPanel;
    [SerializeField] private GameObject resultsPanel;

    [Header("Training Config")]
    [SerializeField] private int passingScore = 70;
    [SerializeField] private int maxScore = 100;

    private float _trainingStartTime;
    private bool _trainingActive;

    private void Start()
    {
        // Subscribe to events
        launchCodeManager.OnSessionValidated += OnSessionValidated;
        launchCodeManager.OnValidationFailed += OnValidationFailed;
        launchCodeManager.OnResultsSubmitted += OnResultsSubmitted;
        launchCodeManager.OnSubmissionFailed += OnSubmissionFailed;

        ShowCodeEntry();
    }

    /// <summary>
    /// Called by the "Submit Code" button in the VR UI.
    /// </summary>
    public void OnSubmitCode()
    {
        string code = codeInputField.text;
        statusText.text = "Validating...";
        launchCodeManager.ValidateCode(code);
    }

    private void OnSessionValidated(LaunchCodeManager.SessionInfo session)
    {
        statusText.text = "Welcome, " + session.learnerName + "!";
        StartTraining();
    }

    private void OnValidationFailed(string error)
    {
        statusText.text = "Invalid code. Please try again.";
    }

    /// <summary>
    /// Begins the VR training session.
    /// </summary>
    private void StartTraining()
    {
        _trainingStartTime = Time.time;
        _trainingActive = true;
        codeEntryPanel.SetActive(false);
        trainingPanel.SetActive(true);
    }

    /// <summary>
    /// Call this method when your training/scoring system determines
    /// that the learner has completed all training activities.
    /// </summary>
    /// <param name="finalScore">The learner's final computed score.</param>
    public void OnTrainingComplete(int finalScore)
    {
        _trainingActive = false;
        float duration = Time.time - _trainingStartTime;

        bool passed = finalScore >= passingScore;
        string details = passed
            ? "Learner successfully completed all safety modules."
            : "Learner did not meet the minimum passing score.";

        launchCodeManager.SubmitResults(finalScore, maxScore, passingScore, duration, details);
        statusText.text = "Submitting results...";
    }

    private void OnResultsSubmitted()
    {
        trainingPanel.SetActive(false);
        resultsPanel.SetActive(true);
        statusText.text = "Results recorded! You may remove your headset.";
    }

    private void OnSubmissionFailed(string error)
    {
        statusText.text = "Failed to submit results. Retrying...";
        // Implement retry logic as needed
    }

    private void ShowCodeEntry()
    {
        codeEntryPanel.SetActive(true);
        trainingPanel.SetActive(false);
        resultsPanel.SetActive(false);
    }
}