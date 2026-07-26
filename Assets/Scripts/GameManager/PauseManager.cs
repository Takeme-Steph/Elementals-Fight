using UnityEngine;
using UnityEngine.UI;

// Owns the actual pause behaviour that InputReader.PauseEvent/ResumeEvent
// were already wiring up to nothing. Freezes gameplay via Time.timeScale,
// switches the shared InputReader over to its UI action map (so Resume
// still works while Ground actions are disabled), and shows/hides a simple
// overlay menu.
public class PauseManager : MonoBehaviour
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitButton;

    private void OnEnable()
    {
        inputReader.PauseEvent += Pause;
        inputReader.ResumeEvent += Resume;
    }

    private void OnDisable()
    {
        inputReader.PauseEvent -= Pause;
        inputReader.ResumeEvent -= Resume;
    }

    private void Start()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
    }

    public void Pause()
    {
        Time.timeScale = 0f;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        inputReader.EnableUIInput();
    }

    public void Resume()
    {
        Time.timeScale = 1f;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        inputReader.EnableGroundInput();
    }

    public void QuitGame()
    {
        // Only actually quits in a real build - the Editor doesn't have a
        // process to terminate, this is expected, not a bug.
        Application.Quit();
    }
}
