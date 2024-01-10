using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class LoadCharacter : MonoBehaviour
{
    public GameObject[] charPrefabs; // Reference playable characters
    private int _selectedCharacter = 0; // have a default selected character index
    private int _selectedOpponent = 1;
    private SceneHandler sceneHandler; // Reference to the scene handler script
    private float _spawnX; // Player spawn x location
    private float _spawnY; // Player spawn y location
    private float _spawnZ; // Player spawn z location
    


    // Start is called before the first frame update
    void Start()
    {
        // Get scene handler script and throw an error message if not found
        if(!TryGetComponent<SceneHandler>(out sceneHandler))
        {
            Debug.Log("No scene handler script found in scene. Game will not run");
        }
        GameObject _ground = sceneHandler.ground; // Reference the environment ground

        // Initialize variables
        _spawnX = _ground.transform.position.x; // Set player spawn x
        _spawnY = _ground.transform.position.y + sceneHandler.groundCollider.bounds.max.y
                    + sceneHandler.resetBuffer; // Set player spwan y
        _spawnZ = _ground.transform.position.z; // Set player spawn z

        
        
        SpawnPlayer();
        SpawnOpponent();

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void SpawnPlayer()
    {
        Vector3 _playerSpawnLocation = new Vector3(_spawnX, _spawnY,_spawnZ); // Set spawn location of the player
        // Get the selected player character data
        _selectedCharacter = PlayerPrefs.GetInt("selectedCharacter"); 
        GameObject prefab = charPrefabs[_selectedCharacter];
        // instantiate an instance of the selected player character
        GameObject player = Instantiate(prefab, _playerSpawnLocation, prefab.transform.rotation);
        player.SetActive(true); 
        player.tag = "Player";
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.enabled = true; // Enable the PlayerController component
        }
        else
        {
            Debug.LogError(player.name + " has no PlayerController script");
        }
        
        // Get player controller and log an error message of not found
        if(!player.TryGetComponent<PlayerManager>(out PlayerManager playerManager))
        {
            Debug.Log(player.name + "has no character manager script");
        }

        playerManager.isCTRLPlayer = true;
    }

    void SpawnOpponent()
    {
        // Get the selected opponent character data
        _selectedOpponent = PlayerPrefs.GetInt("selectedOpponent"); // get the selected char index
        GameObject prefab = charPrefabs[_selectedOpponent]; // Get selected character
        Vector3 _playerSpawnLocation = new Vector3(_spawnX + 5, _spawnY,_spawnZ); // Set spawn location of the player
        
        // instantiate an instance of the selected opponent character
        GameObject opponent = Instantiate(prefab, _playerSpawnLocation, Quaternion.Euler(0,-90,0));
        opponent.SetActive(true); // set character instance to active
        opponent.tag = "Player"; // Tag the instanciated character as a opponent.
        PlayerAutoPilot autoPilot = opponent.GetComponent<PlayerAutoPilot>();
        if (autoPilot != null)
        {
            autoPilot.enabled = true; // Enable the PlayerAutoPilot component
        }
        else
        {
            Debug.LogError(opponent.name + " has no PlayerAutoPilot script");
        }

        // Get player controller and log an error message of not found
        if(!opponent.TryGetComponent<PlayerManager>(out PlayerManager playerManager))
        {
            Debug.Log(opponent.name + "has no character controller script");
        }
        
        playerManager.isCTRLPlayer = false;
    }
}
