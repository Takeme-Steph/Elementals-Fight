using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSelection : MonoBehaviour
{
    public GameObject[] characters;
    public GameObject[] opponents;

    private int _selectedPlayer = 0;
    private int _selectedOpponent = 1;

    private const int CharacterSelectScene = 0;
    private const int FightScene = 1;

    private const string SelectedCharacterKey = "selectedCharacter";
    private const string SelectedOpponentKey = "selectedOpponent";

    private void Start()
    {
        InitializeSelection();
    }

    public void NextPlayer()
    {
        SelectNext(ref _selectedPlayer, characters);
    }

    public void PreviousPlayer()
    {
        SelectPrevious(ref _selectedPlayer, characters);
    }

    public void NextOpponent()
    {
        SelectNext(ref _selectedOpponent, opponents);
    }

    public void PreviousOpponent()
    {
        SelectPrevious(ref _selectedOpponent, opponents);
    }

    public void StartGame()
    {
        if (!IsSelectionValid(characters, _selectedPlayer) || !IsSelectionValid(opponents, _selectedOpponent))
        {
            Debug.LogWarning("Unable to start the fight because the current selection is invalid.");
            return;
        }

        PlayerPrefs.SetInt(SelectedCharacterKey, _selectedPlayer);
        PlayerPrefs.SetInt(SelectedOpponentKey, _selectedOpponent);
        PlayerPrefs.Save();
        SceneManager.LoadScene(FightScene, LoadSceneMode.Single);
    }

    public void ReturnToSelection()
    {
        SceneManager.LoadScene(CharacterSelectScene, LoadSceneMode.Single);
    }

    private void InitializeSelection()
    {
        _selectedPlayer = GetValidSelection(PlayerPrefs.GetInt(SelectedCharacterKey, _selectedPlayer), characters);
        _selectedOpponent = GetValidSelection(PlayerPrefs.GetInt(SelectedOpponentKey, _selectedOpponent), opponents);

        ShowSelection(characters, _selectedPlayer);
        ShowSelection(opponents, _selectedOpponent);
    }

    private void SelectNext(ref int currentIndex, GameObject[] options)
    {
        if (!IsSelectionValid(options, currentIndex))
        {
            return;
        }

        SetActiveOption(options, currentIndex, false);
        currentIndex = (currentIndex + 1) % options.Length;
        SetActiveOption(options, currentIndex, true);
    }

    private void SelectPrevious(ref int currentIndex, GameObject[] options)
    {
        if (!IsSelectionValid(options, currentIndex))
        {
            return;
        }

        SetActiveOption(options, currentIndex, false);
        currentIndex = (currentIndex - 1 + options.Length) % options.Length;
        SetActiveOption(options, currentIndex, true);
    }

    private void ShowSelection(GameObject[] options, int selectedIndex)
    {
        if (!IsSelectionValid(options, selectedIndex))
        {
            return;
        }

        for (int i = 0; i < options.Length; i++)
        {
            SetActiveOption(options, i, i == selectedIndex);
        }
    }

    private bool IsSelectionValid(GameObject[] options, int index)
    {
        return options != null && options.Length > 0 && index >= 0 && index < options.Length;
    }

    private int GetValidSelection(int storedIndex, GameObject[] options)
    {
        if (options == null || options.Length == 0)
        {
            return 0;
        }

        return Mathf.Clamp(storedIndex, 0, options.Length - 1);
    }

    private void SetActiveOption(GameObject[] options, int index, bool isActive)
    {
        if (options == null || index < 0 || index >= options.Length || options[index] == null)
        {
            return;
        }

        options[index].SetActive(isActive);
    }
}
