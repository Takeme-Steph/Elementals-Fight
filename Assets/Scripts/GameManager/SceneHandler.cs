using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneHandler : MonoBehaviour
{
    // Scene data
    private GameObject ground; // Reference the ground
    private Vector3 _envRightEdge; // Reference the right edge of the play envirenment.
    private Vector3 _envLeftEdge; // Reference left edge of the play environment.
    public float _safeZoneRightX; // The playable right edge(x) of the game enironment.
    public float _safeZoneLeftX; // The playable left edge(x) of the game environment.
    public float _bufferX; // Difference between the environment edge and the playale edge.
    public float _resetBuffer; // amount to substract/ add when player reaches edge of the screen.
    public float _groundY; // reference the ground y position
    public float _camBufferX; // Buffer for the camera position when at the edge of the screen.

    // Player data
    public float _playerMoveSpeed; //this is players movement speed.
    public float _playerJumpForce; //this is players jump force.
    

    // Flags
    public bool _isGameOver; // track when game is over


    // Start is called before the first frame update
    void Start()
    {
        // Initialize environment variables
        ground = GameObject.Find("Environment/Ground");
        _groundY = ground.transform.position.y;
        _envRightEdge = ground.GetComponent<Collider>().bounds.max;
        _envLeftEdge = ground.GetComponent<Collider>().bounds.min;
        _playerJumpForce = 7;
        _playerMoveSpeed = 10;
        _camBufferX = 5.0f;
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
