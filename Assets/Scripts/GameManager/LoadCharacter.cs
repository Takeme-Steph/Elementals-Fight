using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class LoadCharacter : MonoBehaviour
{
    [SerializeField] private CharacterRoster roster; // Single source of spawn order once wired in-scene; charPrefabs[] is a fallback until then
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

    // Resolves a roster index to a playable prefab. Prefers the CharacterRoster asset so
    // adding a fighter is a data change; falls back to charPrefabs[] (with a loud warning)
    // until a scene edit assigns the roster on this GameManager - see the migration task
    // in TASKS.md. Remove the fallback once every scene has the roster wired.
    private GameObject ResolvePrefab(int index, bool isMainPlayer)
    {
        // CharacterSelect owns the canonical roster today. When the player has arrived
        // through its normal confirm flow, use the exact selected definition instead of
        // silently dropping back to this scene's legacy, hand-ordered prefab array.
        CharacterDefinition selectedDefinition;
        bool hasMatchDefinition = isMainPlayer
            ? MatchSelection.TryGetPlayer(out selectedDefinition)
            : MatchSelection.TryGetOpponent(out selectedDefinition);
        if (hasMatchDefinition)
        {
            return selectedDefinition.PlayablePrefab;
        }

        if (roster != null)
        {
            CharacterDefinition definition = roster.Get(index);
            return definition != null ? definition.PlayablePrefab : null;
        }

        Debug.LogWarning("LoadCharacter: no CharacterRoster assigned - falling back to charPrefabs[]. Assign Assets/Data/Roster/CharacterRoster.asset on this GameManager.");
        return charPrefabs[index];
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
        GameObject prefab = ResolvePrefab(_selectedCharacter, true);
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

        PlayerAutoPilot autoPilot = player.GetComponent<PlayerAutoPilot>();
        if (autoPilot != null)
        {
            autoPilot.enabled = false; // Prevent the fight AI from also driving the human-controlled character
        }
        else
        {
            Debug.LogError(player.name + " has no PlayerAutoPilot script");
        }
        
        // Get player controller and log an error message of not found
        if(!player.TryGetComponent<PlayerManager>(out PlayerManager playerManager))
        {
            Debug.Log(player.name + "has no character manager script");
        }

        playerManager.isCTRLPlayer = true;
        playerManager.roundWins = 0;
    }

    void SpawnOpponent()
    {
        // Get the selected opponent character data
        _selectedOpponent = PlayerPrefs.GetInt("selectedOpponent"); // get the selected char index
        GameObject prefab = ResolvePrefab(_selectedOpponent, false); // Get selected character
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

        PlayerController playerController = opponent.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.enabled = false; // Prevent human input from also driving the AI-controlled opponent
        }
        else
        {
            Debug.LogError(opponent.name + " has no PlayerController script");
        }

        // Get player controller and log an error message of not found
        if(!opponent.TryGetComponent<PlayerManager>(out PlayerManager playerManager))
        {
            Debug.Log(opponent.name + "has no character controller script");
        }
        
        playerManager.isCTRLPlayer = false;
        playerManager.roundWins = 0;
    }
}
