using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test helper: simulates VR training completion with a hardcoded score.
/// Attach to a button in the TrainingPanel for local testing.
/// </summary>
public class TestCompleteButton : MonoBehaviour
{
    [SerializeField] private VRTrainingController trainingController;
    [SerializeField] private int testScore = 85;
    [SerializeField] Button completeButton;

    private void Start()
    {
        completeButton.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        Debug.Log($"[Test] Simulating training complete with score: {testScore}");
        trainingController.OnTrainingComplete(testScore);
    }
}