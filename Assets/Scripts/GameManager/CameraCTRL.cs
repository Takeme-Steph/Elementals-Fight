using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraCTRL : MonoBehaviour
{
    private GameObject mainPlayer; // Reference the player.
    private float _xMin, _yMin; // Left bounds of the camera.
    private float _xMax, _yMax; // Right bounds of the camera.
    public float _yOffset; // Offset the camera Y position.
    public float _minDistance; // Minimum distance(z) of camera from players.
    private Transform[] playerTransforms; // Reference the location of all characters in the scene

    // Start is called before the first frame update
    void Start()
    {   
        //Initialize variables
        _yOffset = 2f;
        _minDistance = 3.5f;
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player"); // Get all characters in the scene
        int _allPlayersLen = allPlayers.Length; // Reference all players array lenght.
        
        // Initialize character references
        // Check if there are players in the scene
        if(_allPlayersLen > 0)
        {
            playerTransforms = new Transform[_allPlayersLen];
            PlayerManager[] playerManagers = new PlayerManager[_allPlayersLen];

            // Loop through all characters, get thier character controllers and identify he main player
            for (int i = 0; i < _allPlayersLen; i++)
            {
                playerTransforms[i] = allPlayers[i].transform; // Store char transforms
                // Store char controllers
                // Check if players have controllers
                if(allPlayers[i].TryGetComponent<PlayerManager>(out PlayerManager playerManager))
                {
                    playerManagers[i] = playerManager;
                    // If main player then store the refrence for future use
                    if(playerManagers[i].isCTRLPlayer)
                    {
                        mainPlayer = allPlayers[i];
                    }
                }
                // Handle if the player has no character controller script attached
                else
                {
                    Debug.Log(allPlayers[i].name + "has no character controller script");
                }  
            }
        }
        // Return if no players in the scene
        else
        {
            Debug.Log("No players in the scene");
        }
    }

    void LateUpdate()
    {   
        // Check if there are characters in the scene
        if(playerTransforms.Length > 0)
        {
            // Have the camera center the main player character
            _xMin = _xMax = mainPlayer.transform.position.x;
            _yMin = _yMax = mainPlayer.transform.position.y;

            for (int i = 1; i < playerTransforms.Length; i++)
            {
                _xMin = Mathf.Min(_xMin, playerTransforms[i].position.x);
                _xMax = Mathf.Max(_xMax, playerTransforms[i].position.x);
                _yMin = Mathf.Min(_yMin, playerTransforms[i].position.y);
                _yMax = Mathf.Max(_yMax, playerTransforms[i].position.y);
            }

            float _xMiddle = (_xMin + _xMax) / 2;
            float _yMiddle = (_yMin + _yMax) / 2;
            float _distance = _xMax - _xMin;
    
            _distance = Mathf.Max(_distance, _minDistance);

            // Update camera position
            transform.position = new Vector3(_xMiddle, _yMiddle + _yOffset ,
                -_distance + mainPlayer.transform.position.z);
        }
        // Return if no players in the scene
        else
        {
            Debug.Log("No players in the scene");
            return;
        }
    }
}

