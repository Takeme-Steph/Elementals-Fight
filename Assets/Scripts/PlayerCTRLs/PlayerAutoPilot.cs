using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAutoPilot : MonoBehaviour
{
    private SceneHandler sceneHandler;
    
    void Start()
    {
       GameObject gameManager = GameObject.Find("GameManager");

        if (gameManager != null && gameManager.TryGetComponent<SceneHandler>(out sceneHandler))
        {
            // SceneHandler found, continue with initialization
        }
        else
        {
            Debug.LogError("No SceneHandler script found in the scene. The game will not run correctly.");
        }
    }

    void Update()
    {
        if (sceneHandler != null)
        {
            GameObject mainPlayer = sceneHandler.GetMainPlayer();

            if (mainPlayer != null)
            {
                // Calculate the distance between this object and the main player
                //float distance = Vector3.Distance(transform.position, mainPlayer.transform.position);

            }
            else
            {

            }
        }
    }
}
