using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSelection : MonoBehaviour
{
    public GameObject[] characters; // reference characters to be picked from
    private int selectedCharacter = 0; // store selected character index
    
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

    // Deactivate current selected character and activate next character
    public void NextCharacter()
    {
        characters[selectedCharacter].SetActive(false); // Deactivate currently selected character
        // Update selected character to the next charater,and run in a loop
        selectedCharacter = (selectedCharacter+1) % characters.Length; 
        characters[selectedCharacter].SetActive(true); // Active newly selected character
    }

    // Deactivate current selected character and activate previous character
    public void PreviousCharacter()
    {
        characters[selectedCharacter].SetActive(false); // Deactivate currently selected character
        // Update selected character to the previous charater,and run in a loop
        selectedCharacter --;
        if(selectedCharacter < 0)
        {
            selectedCharacter += characters.Length;
        }
        characters[selectedCharacter].SetActive(true); // Active newly selected character
    }

    // Load the next (fight) scene
    public void StartGame()
    {
        PlayerPrefs.SetInt("selectedCharacter",selectedCharacter); // Store the selected character index 
        SceneManager.LoadScene(FightScene, LoadSceneMode.Single); // Load the next scene and close current scene
    }
}
