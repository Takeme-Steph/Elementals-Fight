using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraCTRL : MonoBehaviour
{
    private SceneHandler sceneHandler; // Reference to the scene handler script
    private GameObject player; // Reference the player
    private float _xMin; // Left bound of the camera
    private float _xMax; // Right bounds of the camera

    // Start is called before the first frame update
    void Start()
    {
        sceneHandler = GameObject.Find("GameManager").GetComponent<SceneHandler>(); // Initialize scene handler
        player = GameObject.FindGameObjectsWithTag("Player")[0];
        _xMax = sceneHandler._safeZoneRightX - sceneHandler._camBufferX;
        _xMin = sceneHandler._safeZoneLeftX + sceneHandler._camBufferX;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // Update the camera osition to follow the player with bounds
        float _x = Mathf.Clamp(player.transform.position.x, _xMin, _xMax);
        transform.position = new Vector3(_x, transform.position.y, transform.position.z);
    }

}

