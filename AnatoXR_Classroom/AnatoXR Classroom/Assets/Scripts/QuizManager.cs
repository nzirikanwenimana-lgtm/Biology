using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages interactive anatomy quizzes and educational assessments (Build Book Phase 9).
/// Generates multiple-choice and identification questions dynamically from the central AnatomyDatabase.
/// </summary>
public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance { get; private set; }

    [System.Serializable]
    public enum QuestionType
    {
        MultipleChoice_Function,
        MultipleChoice_System,
        MultipleChoice_Location,
        PointAndSelect
    }

    [System.Serializable]
    public class QuizQuestion
    {
        public string prompt;
        public string correctPartName;
        public string correctSystem;
        public string[] choices; // 4 choices
        public int correctChoiceIndex;
        public QuestionType type;
        public string explanation;
    }

    [Header("Anatomy Database")]
    [Tooltip("Reference to the AnatomyDatabase asset. Auto-loaded from Resources if empty.")]
    [SerializeField] private AnatomyDatabase anatomyDatabase;

    [Header("Quiz Settings")]
    [Range(3, 20)]
    [SerializeField] private int totalQuestionsPerSession = 5;

    [Tooltip("Optional filter to quiz only on a specific system (e.g. Skeletal System). Leave empty for all.")]
    [SerializeField] private string systemFilter = "";

    [Header("Events")]
    public UnityEvent<QuizQuestion, int, int> OnQuestionPresented; // Question, currentIndex (1-based), total
    public UnityEvent<bool, string> OnAnswerEvaluated;             // isCorrect, feedbackMessage
    public UnityEvent<int, int, float> OnQuizCompleted;            // score, total, percentage

    private List<QuizQuestion> _generatedQuestions = new List<QuizQuestion>();
    private int _currentQuestionIndex = 0;
    private int _score = 0;
    private bool _isQuestionActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (anatomyDatabase == null)
        {
            anatomyDatabase = Resources.Load<AnatomyDatabase>("AnatomyDatabase");
        }
    }

    /// <summary>
    /// Starts a new quiz session with newly generated questions from the database.
    /// </summary>
    public void StartQuiz()
    {
        if (anatomyDatabase == null)
        {
            anatomyDatabase = Resources.Load<AnatomyDatabase>("AnatomyDatabase");
        }

        if (anatomyDatabase == null || anatomyDatabase.entries.Count < 4)
        {
            Debug.LogWarning("[QuizManager] AnatomyDatabase is missing or has fewer than 4 entries. Cannot generate quiz.");
            return;
        }

        GenerateQuestions();
        _currentQuestionIndex = 0;
        _score = 0;

        PresentCurrentQuestion();
    }

    private void GenerateQuestions()
    {
        _generatedQuestions.Clear();

        List<AnatomyEntry> eligible = new List<AnatomyEntry>();
        foreach (var entry in anatomyDatabase.entries)
        {
            if (string.IsNullOrWhiteSpace(entry.partName)) continue;
            if (!string.IsNullOrEmpty(systemFilter) && !string.Equals(entry.systemName, systemFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            // Prefer entries that have at least some descriptive content
            if (!string.IsNullOrWhiteSpace(entry.function) || !string.IsNullOrWhiteSpace(entry.description))
            {
                eligible.Add(entry);
            }
        }

        if (eligible.Count < 4)
        {
            // Fallback to all entries
            eligible = new List<AnatomyEntry>(anatomyDatabase.entries);
        }

        // Shuffle eligible entries
        ShuffleList(eligible);

        int count = Mathf.Min(totalQuestionsPerSession, eligible.Count);
        for (int i = 0; i < count; i++)
        {
            AnatomyEntry target = eligible[i];
            QuizQuestion q = CreateMultipleChoiceQuestion(target, eligible);
            _generatedQuestions.Add(q);
        }
    }

    private QuizQuestion CreateMultipleChoiceQuestion(AnatomyEntry target, List<AnatomyEntry> pool)
    {
        string formattedName = AnatomyNamingUtils.FormatDisplayName(target.partName);
        QuizQuestion q = new QuizQuestion
        {
            correctPartName = target.partName,
            correctSystem = target.systemName,
            type = QuestionType.MultipleChoice_Function,
            choices = new string[4]
        };

        // Determine question prompt based on available data
        if (!string.IsNullOrWhiteSpace(target.function))
        {
            q.prompt = $"Which anatomical structure performs this function:\n\"{target.function}\"?";
            q.explanation = $"{formattedName} is part of the {target.systemName}.";
        }
        else if (!string.IsNullOrWhiteSpace(target.description))
        {
            q.prompt = $"Identify the structure described below:\n\"{target.description}\"";
            q.explanation = $"{formattedName} belongs to {target.systemName}.";
        }
        else
        {
            q.prompt = $"Which system does '{formattedName}' belong to?";
            q.explanation = $"{formattedName} is part of the {target.systemName}.";
        }

        // Correct answer slot (0 to 3)
        int correctIdx = UnityEngine.Random.Range(0, 4);
        q.correctChoiceIndex = correctIdx;
        q.choices[correctIdx] = formattedName;

        // Pick 3 distinct wrong choices
        HashSet<string> picked = new HashSet<string> { target.partName, formattedName };
        int slot = 0;
        int safety = 0;
        while (slot < 4 && safety < 100)
        {
            safety++;
            if (slot == correctIdx)
            {
                slot++;
                continue;
            }

            AnatomyEntry candidate = pool[UnityEngine.Random.Range(0, pool.Count)];
            string candidateName = AnatomyNamingUtils.FormatDisplayName(candidate.partName);
            if (!picked.Contains(candidate.partName) && !picked.Contains(candidateName))
            {
                picked.Add(candidate.partName);
                picked.Add(candidateName);
                q.choices[slot] = candidateName;
                slot++;
            }
        }

        // Fill any empty slots if pool was small
        for (int j = 0; j < 4; j++)
        {
            if (string.IsNullOrEmpty(q.choices[j]))
                q.choices[j] = $"Anatomy Part {j + 1}";
        }

        return q;
    }

    private void PresentCurrentQuestion()
    {
        if (_currentQuestionIndex >= _generatedQuestions.Count)
        {
            // Quiz Finished
            _isQuestionActive = false;
            float pct = _generatedQuestions.Count > 0 ? ((float)_score / _generatedQuestions.Count) * 100f : 0f;
            OnQuizCompleted?.Invoke(_score, _generatedQuestions.Count, pct);
            Debug.Log($"[QuizManager] Quiz complete! Score: {_score}/{_generatedQuestions.Count} ({pct:F0}%)");
            return;
        }

        _isQuestionActive = true;
        QuizQuestion current = _generatedQuestions[_currentQuestionIndex];
        OnQuestionPresented?.Invoke(current, _currentQuestionIndex + 1, _generatedQuestions.Count);
    }

    /// <summary>
    /// Evaluates an answer choice selected by the user.
    /// </summary>
    public void SubmitChoice(int choiceIndex)
    {
        if (!_isQuestionActive) return;
        _isQuestionActive = false;

        QuizQuestion current = _generatedQuestions[_currentQuestionIndex];
        bool isCorrect = (choiceIndex == current.correctChoiceIndex);

        if (isCorrect)
        {
            _score++;
        }

        string feedback = isCorrect 
            ? "✓ Correct!" 
            : $"✗ Incorrect. The correct answer was: {current.choices[current.correctChoiceIndex]}.\n{current.explanation}";

        OnAnswerEvaluated?.Invoke(isCorrect, feedback);
    }

    /// <summary>
    /// Evaluates a 3D anatomy mesh selection during a Point-and-Select question or exploration.
    /// </summary>
    public void OnAnatomyMeshSelected(string selectedPartName)
    {
        if (!_isQuestionActive) return;
        QuizQuestion current = _generatedQuestions[_currentQuestionIndex];

        bool isCorrect = string.Equals(current.correctPartName, selectedPartName, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(AnatomyNamingUtils.CleanPartName(current.correctPartName), 
                                       AnatomyNamingUtils.CleanPartName(selectedPartName), StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            _score++;
        }

        string feedback = isCorrect 
            ? "✓ Correct structure selected!" 
            : $"✗ That was {AnatomyNamingUtils.FormatDisplayName(selectedPartName)}. Target: {AnatomyNamingUtils.FormatDisplayName(current.correctPartName)}";

        OnAnswerEvaluated?.Invoke(isCorrect, feedback);
    }

    /// <summary>
    /// Advances to the next question in the quiz.
    /// </summary>
    public void NextQuestion()
    {
        _currentQuestionIndex++;
        PresentCurrentQuestion();
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rnd = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[rnd]) = (list[rnd], list[i]);
        }
    }

    public int CurrentScore => _score;
    public int TotalQuestions => _generatedQuestions.Count;
    public bool IsActive => _isQuestionActive;
}
