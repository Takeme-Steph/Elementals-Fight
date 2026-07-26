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

    [Header("Game Over screen")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button quitButton;

    private void OnEnable()
    {
        sceneHandler.RoundWinsChanged += OnRoundWinsChanged;
        sceneHandler.GameEnded += OnGameEnded;
    }

    private void OnDisable()
    {
        sceneHandler.RoundWinsChanged -= OnRoundWinsChanged;
        sceneHandler.GameEnded -= OnGameEnded;
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
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
