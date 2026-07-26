using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Reacts to SceneHandler's round/match events - keeps the round-win
// indicator text and Game Over screen in sync without either of them
// needing to poll SceneHandler every frame.
public class MatchUIManager : MonoBehaviour
{
    [SerializeField] private SceneHandler sceneHandler;

    [Header("Round win indicator")]
    [SerializeField] private TextMeshProUGUI playerRoundWinsText;
    [SerializeField] private TextMeshProUGUI oppRoundWinsText;

    [Header("Round transition (announcement + countdown)")]
    [SerializeField] private GameObject roundTransitionPanel;
    [SerializeField] private TextMeshProUGUI roundWinnerText;
    [SerializeField] private TextMeshProUGUI roundCountdownText;

    [Header("Game Over screen")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button quitButton;

    private void OnEnable()
    {
        sceneHandler.RoundWinsChanged += OnRoundWinsChanged;
        sceneHandler.GameEnded += OnGameEnded;
        sceneHandler.RoundEnded += OnRoundEnded;
        sceneHandler.RoundCountdownTick += OnRoundCountdownTick;
        sceneHandler.RoundTransitionEnded += OnRoundTransitionEnded;
    }

    private void OnDisable()
    {
        sceneHandler.RoundWinsChanged -= OnRoundWinsChanged;
        sceneHandler.GameEnded -= OnGameEnded;
        sceneHandler.RoundEnded -= OnRoundEnded;
        sceneHandler.RoundCountdownTick -= OnRoundCountdownTick;
        sceneHandler.RoundTransitionEnded -= OnRoundTransitionEnded;
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (roundTransitionPanel != null) roundTransitionPanel.SetActive(false);
        if (playAgainButton != null) playAgainButton.onClick.AddListener(PlayAgain);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        // Initialize the indicator to 0-0 at match start.
        OnRoundWinsChanged(0, 0);
    }

    private void OnRoundWinsChanged(int mainWins, int oppWins)
    {
        if (playerRoundWinsText != null) playerRoundWinsText.text = mainWins.ToString();
        if (oppRoundWinsText != null) oppRoundWinsText.text = oppWins.ToString();
    }

    private void OnRoundEnded(bool mainPlayerWonRound)
    {
        if (roundWinnerText != null)
            roundWinnerText.text = mainPlayerWonRound ? "You Won the Round!" : "Opponent Won the Round!";
        if (roundTransitionPanel != null) roundTransitionPanel.SetActive(true);
    }

    private void OnRoundCountdownTick(int secondsRemaining)
    {
        if (roundCountdownText != null) roundCountdownText.text = secondsRemaining.ToString();
    }

    private void OnRoundTransitionEnded()
    {
        if (roundTransitionPanel != null) roundTransitionPanel.SetActive(false);
    }

    private void OnGameEnded(bool mainPlayerWon)
    {
        if (gameOverText != null) gameOverText.text = mainPlayerWon ? "YOU WIN!" : "YOU LOSE!";
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    private void PlayAgain()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        sceneHandler.PlayAgain();
    }

    private void QuitGame()
    {
        // Only actually quits in a real build, not in the Editor - expected.
        Application.Quit();
    }
}
