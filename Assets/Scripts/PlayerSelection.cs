using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSelection : MonoBehaviour
{
    public GameObject[] characters; // reference characters to be picked from
    public GameObject[] opponents; // reference characters to be picked from
    private int _selectedPlayer = 0; // store selected character index
    private int _selectedOpponent = 1; // store selected opponent index
    
    // ToDO Update to use an enum
    private readonly int FightScene = 1; // store fight scene index
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Deactivate current selected player and activate next player
    public void NextPlayer()
    {
        characters[_selectedPlayer].SetActive(false); // Deactivate currently selected character
        // Update selected character to the next charater,and run in a loop
        _selectedPlayer = (_selectedPlayer+1) % characters.Length; 
        characters[_selectedPlayer].SetActive(true); // Active newly selected character
    }

    // Deactivate current selected player and activate previous player
    public void PreviousPlayer()
    {
        characters[_selectedPlayer].SetActive(false); // Deactivate currently selected character
        // Update selected character to the previous charater,and run in a loop
        _selectedPlayer --;
        if(_selectedPlayer < 0)
        {
            _selectedPlayer += characters.Length;
        }
        characters[_selectedPlayer].SetActive(true); // Active newly selected character
    }

 // Deactivate current selected player and activate next player
    public void NextOpponent()
    {
        opponents[_selectedOpponent].SetActive(false); // Deactivate currently selected character
        // Update selected character to the next charater,and run in a loop
        _selectedOpponent = (_selectedOpponent+1) % characters.Length; 
        opponents[_selectedOpponent].SetActive(true); // Active newly selected character
    }

    // Deactivate current selected player and activate previous player
    public void PreviousOpponent()
    {
        opponents[_selectedOpponent].SetActive(false); // Deactivate currently selected character
        // Update selected character to the previous charater,and run in a loop
        _selectedOpponent --;
        if(_selectedOpponent < 0)
        {
            _selectedOpponent += characters.Length;
        }
        opponents[_selectedOpponent].SetActive(true); // Active newly selected character
    }

    // Load the next (fight) scene
    public void StartGame()
    {
        PlayerPrefs.SetInt("selectedCharacter",_selectedPlayer); // Store the selected character index 
        PlayerPrefs.SetInt("selectedOpponent",_selectedOpponent); // Store the selected character index 
        SceneManager.LoadScene(FightScene, LoadSceneMode.Single); // Load the next scene and close current scene
    }
}
