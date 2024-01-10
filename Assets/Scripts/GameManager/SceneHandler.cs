using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneHandler : MonoBehaviour
{
    public GameObject ground;
    public Collider groundCollider;
    private Vector3 envRightEdge;
    private Vector3 envLeftEdge;
    private float bufferX = 10.0f;
    public float resetBuffer = 0.1f;
    public float safeZoneRightX;
    public float safeZoneLeftX;
    private Transform[] playerTransforms;
    public GameObject mainPlayer;
    public bool isGameOver;
    private PlayerManager[] playerManagers;

    void OnEnable()
    {
        InitializeEnvironment();
    }

    void Start()
    {
        if (ground == null)
        {
            Debug.LogError("Scene has no game object named Ground");
            return;
        }

        InitializeVariables();
        GetPlayers();
    }

    void Update()
    {
        // Add your update logic here if needed
    }

    private void InitializeEnvironment()
    {
        ground = GameObject.Find("Environment/Ground");

        if (ground.TryGetComponent<Collider>(out groundCollider))
        {
            envRightEdge = groundCollider.bounds.max;
            envLeftEdge = groundCollider.bounds.min;
        }

        // Set the safe zones
        safeZoneRightX = envRightEdge.x - bufferX;
        safeZoneLeftX = envLeftEdge.x + bufferX;
    }

    private void InitializeVariables()
    {
        bufferX = 10.0f;
        resetBuffer = 0.1f;
        safeZoneRightX = envRightEdge.x - bufferX;
        safeZoneLeftX = envLeftEdge.x + bufferX;
    }

    private void GameOver()
    {
        // Your game over logic goes here
    }

    public void MatchOver()
    {
        // Your match over logic goes here
    }

    private void GetPlayers()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");

        if (allPlayers.Length > 0)
        {
            InitializePlayerArrays(allPlayers);
            mainPlayer = IdentifyMainPlayer();

            if (mainPlayer == null)
            {
                Debug.LogError("No main player in the scene");
            }
        }
        else
        {
            Debug.LogError("No players in the scene");
        }
    }


    private (Transform[], PlayerManager[]) InitializePlayerArrays(GameObject[] allPlayers)
    {
        int numPlayers = allPlayers.Length;
        playerTransforms = new Transform[numPlayers];
        playerManagers = new PlayerManager[numPlayers];

        for (int i = 0; i < numPlayers; i++)
        {
            playerTransforms[i] = allPlayers[i].transform;

            if (!allPlayers[i].TryGetComponent<PlayerManager>(out PlayerManager playerManager))
            {
                HandleMissingPlayerManager(allPlayers[i]);
                continue;
            }

            // Store the reference to PlayerManager for future use
            playerManagers[i] = playerManager;
        }

        return (playerTransforms, playerManagers);
    }


    private GameObject IdentifyMainPlayer()
    {
        for (int i = 0; i < playerManagers.Length; i++)
        {
            if (playerManagers[i].isCTRLPlayer)
            {
                return playerTransforms[i].gameObject;
            }
        }

        Debug.LogError("No main player identified");
        return null; // or handle the absence of the main player as needed
    }
    
    public GameObject GetMainPlayer()
    {
        return mainPlayer;
    }

    private void HandleMissingPlayerManager(GameObject playerGameObject)
    {
        Debug.LogError(playerGameObject.name + " has no PlayerManager script attached");
    }
}
