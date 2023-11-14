using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class LoadCharacter : MonoBehaviour
{
    public GameObject[] charPrefabs;
    private int selectedCharacter = 0; // have a default selected character index

    // Start is called before the first frame update
    void Start()
    {
        // Load up the selected player character
        selectedCharacter = PlayerPrefs.GetInt("selectedCharacter"); // get the selected char index
        GameObject prefab = charPrefabs[selectedCharacter];
        // instantiate an instance of the selected character
        GameObject player = Instantiate(prefab, prefab.transform.position, prefab.transform.rotation);
        player.SetActive(true); // Activate character instance
        player.tag = "Player"; // Tag the instanciated character as a player.

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
