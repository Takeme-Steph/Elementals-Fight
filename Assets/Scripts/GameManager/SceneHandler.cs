using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneHandler : MonoBehaviour
{
    // Scene data
    public GameObject ground; // Reference the ground
    private Vector3 _envRightEdge; // Reference the right edge of the play envirenment.
    private Vector3 _envLeftEdge; // Reference left edge of the play environment.
    public float _safeZoneRightX; // The playable right edge(x) of the game enironment.
    public float _safeZoneLeftX; // The playable left edge(x) of the game environment.
    public float _bufferX; // Difference between the environment edge and the playable edge.
    public float _resetBuffer; // amount to substract/ add when player reaches edge of the screen.
    public Collider groundCollider; // reference the environments ground collider
    
    // Flags
    public bool _isGameOver; // track when game is over


    void OnEnable()
    {
        // Initialize environment variables
        ground = GameObject.Find("Environment/Ground");
        // Get the ground's collider and its edges
        if(ground.TryGetComponent<Collider>(out groundCollider))
        {
            _envRightEdge = groundCollider.bounds.max;
            _envLeftEdge = groundCollider.bounds.min;
        } 
        
    }
    // Start is called before the first frame update
    void Start()
    {
        // Return if there is no ground in the scene
        if(!ground)
        {
            Debug.Log("Scene has no game object named Ground");
        }
        // Initialize variables
        _bufferX = 10.0f;
        _resetBuffer = 0.1f;
        _safeZoneRightX = _envRightEdge.x - _bufferX;
        _safeZoneLeftX = _envLeftEdge.x + _bufferX;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void GameOver()
    {

    }

    public void MatchOver()
    {

    }
}
