using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// World-Space VR UI controller for the anatomy quiz (Build Book Phase 9).
/// Presents questions, choice buttons, feedback, score tally, and next/restart controls.
/// </summary>
public class QuizUI : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] private GameObject quizPanel;

    [Header("Question Header")]
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text questionPromptText;

    [Header("Choice Buttons (4 Choices)")]
    [SerializeField] private Button[] choiceButtons;
    [SerializeField] private TMP_Text[] choiceButtonTexts;

    [Header("Feedback Display")]
    [SerializeField] private GameObject feedbackGroup;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button nextButton;

    [Header("Results Screen")]
    [SerializeField] private GameObject resultsGroup;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private Button restartButton;

    [Header("Color Palette")]
    [SerializeField] private Color defaultButtonColor = new Color(0.2f, 0.25f, 0.35f, 1f);
    [SerializeField] private Color correctColor = new Color(0.1f, 0.7f, 0.3f, 1f);
    [SerializeField] private Color incorrectColor = new Color(0.85f, 0.2f, 0.2f, 1f);

    private QuizManager _manager;

    private void Awake()
    {
        _manager = FindFirstObjectByType<QuizManager>();

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextClicked);

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);

        // Bind 4 choice button clicks
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(index));
            }
        }
    }

    private void Start()
    {
        if (_manager != null)
        {
            _manager.OnQuestionPresented.AddListener(DisplayQuestion);
            _manager.OnAnswerEvaluated.AddListener(DisplayFeedback);
            _manager.OnQuizCompleted.AddListener(DisplayResults);
        }
    }

    private void OnDestroy()
    {
        if (_manager != null)
        {
            _manager.OnQuestionPresented.RemoveListener(DisplayQuestion);
            _manager.OnAnswerEvaluated.RemoveListener(DisplayFeedback);
            _manager.OnQuizCompleted.RemoveListener(DisplayResults);
        }
    }

    public void OpenQuiz()
    {
        if (quizPanel != null) quizPanel.SetActive(true);
        if (resultsGroup != null) resultsGroup.SetActive(false);

        if (_manager != null)
        {
            _manager.StartQuiz();
        }
    }

    public void CloseQuiz()
    {
        if (quizPanel != null) quizPanel.SetActive(false);
    }

    private void DisplayQuestion(QuizManager.QuizQuestion question, int currentIndex, int totalQuestions)
    {
        if (quizPanel != null) quizPanel.SetActive(true);
        if (resultsGroup != null) resultsGroup.SetActive(false);
        if (feedbackGroup != null) feedbackGroup.SetActive(false);

        if (progressText != null)
            progressText.text = $"Question {currentIndex} of {totalQuestions}";

        if (questionPromptText != null)
            questionPromptText.text = question.prompt;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
            {
                choiceButtons[i].gameObject.SetActive(true);
                choiceButtons[i].interactable = true;

                // Reset button color
                var img = choiceButtons[i].GetComponent<Image>();
                if (img != null) img.color = defaultButtonColor;

                if (choiceButtonTexts != null && i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                {
                    choiceButtonTexts[i].text = (question.choices != null && i < question.choices.Length)
                        ? question.choices[i]
                        : "";
                }
            }
        }
    }

    private void OnChoiceSelected(int index)
    {
        if (_manager != null)
        {
            _manager.SubmitChoice(index);
        }
    }

    private void DisplayFeedback(bool isCorrect, string message)
    {
        // Disable choice buttons after answering
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].interactable = false;
        }

        if (feedbackGroup != null)
            feedbackGroup.SetActive(true);

        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = isCorrect ? correctColor : incorrectColor;
        }
    }

    private void DisplayResults(int score, int total, float percentage)
    {
        if (feedbackGroup != null) feedbackGroup.SetActive(false);
        if (resultsGroup != null) resultsGroup.SetActive(true);

        // Hide choice buttons on results screen
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(false);
        }

        if (questionPromptText != null)
            questionPromptText.text = "Quiz Finished!";

        if (finalScoreText != null)
        {
            string grade = percentage >= 80f ? "Outstanding! Mastery achieved." :
                           percentage >= 60f ? "Good job! Review incorrect parts to improve." :
                                              "Keep practicing! Study the systems and retry.";

            finalScoreText.text = $"<b>Final Score: {score} / {total}</b> ({percentage:F0}%)\n\n{grade}";
        }
    }

    private void OnNextClicked()
    {
        if (_manager != null)
            _manager.NextQuestion();
    }

    private void OnRestartClicked()
    {
        OpenQuiz();
    }
}
