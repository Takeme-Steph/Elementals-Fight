using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraCTRL : MonoBehaviour
{
    private GameObject mainPlayer; // Reference the player.
    private float _xMin, _yMin; // Left bounds of the camera.
    private float _xMax, _yMax; // Right bounds of the camera.
    public float _yOffset; // Offset the camera Y position.
    private float _minDistance; // Minimum distance(z) of camera from players.
    private float _maxDistance;
    private Transform[] playerTransforms; // Reference the location of all characters in the scene

    private SceneHandler _sceneHandler; // Reference the scene handler script

    // Start is called before the first frame update
    void Start()
    {   
        GameObject.Find("GameManager").TryGetComponent<SceneHandler>(out _sceneHandler);
        playerTransforms = _sceneHandler.GetPlayers();
        mainPlayer = _sceneHandler.GetMainPlayer();


        //Initialize variables
        _yOffset = 2f;
        _minDistance = 3.5f;
        _maxDistance = 7f;
       
        
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
            _distance = Mathf.Min(_distance, _maxDistance);

            // Check if the calculated distance exceeds the max distance
            if (_distance >= _maxDistance)
            {
                // Use the main player's position to set the camera position
                transform.position = new Vector3(mainPlayer.transform.position.x, mainPlayer.transform.position.y + _yOffset, -_maxDistance + mainPlayer.transform.position.z);
            }
            else
            {
                // Update camera position based on the calculated distance
                transform.position = new Vector3(_xMiddle, _yMiddle + _yOffset, -_distance + mainPlayer.transform.position.z);
            }

        }
        // Return if no players in the scene
        else
        {
            Debug.Log("No players in the scene");
            return;
        }
    }
}

