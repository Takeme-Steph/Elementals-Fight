using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SceneHandler : MonoBehaviour
{
    // Fixed: PlayerController used to look this up via
    // GameObject.Find("GameManager").TryGetComponent<SceneHandler>() in its
    // own Start(). That's a name-based lookup racing against script execution
    // order - if a player's Start() ran before this object was fully ready
    // (or briefly inactive during scene setup), the lookup silently failed
    // and left sceneHandler null forever, crashing GroundCheck() every
    // FixedUpdate. Awake() is guaranteed to run before any other object's
    // Start(), so a singleton set here is reliable regardless of timing.
    public static SceneHandler Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }
    public GameObject ground { get; private set; }
    public Collider groundCollider;
    private Vector3 envRightEdge;
    private Vector3 envLeftEdge;
    private float bufferX = 10.0f;
    public float resetBuffer = 0.1f;
    public float safeZoneRightX;
    public float safeZoneLeftX;
    private Transform[] playerTransforms;
    public GameObject mainPlayer;
    public GameObject oppPlayer;
    public bool isGameOver;
    public bool activeMatch;
    private PlayerManager[] playerManagers;
    private PlayerManager mainPlayerManager;
    private PlayerManager oppPlayerManager;
    private PlayerStateMachine mainPlayerStateMachine;
    private PlayerStateMachine oppPlayerStateMachine;

    [SerializeField] private int roundsToWinMatch = 3;
    private Vector3 mainPlayerStartPos;
    private Vector3 oppPlayerStartPos;

    // Fired whenever either player's round-win count changes, so UI can
    // reflect it without polling. (mainWins, oppWins)
    public event System.Action<int, int> RoundWinsChanged;

    // Fired once a player has won enough rounds to win the whole match.
    // (mainPlayerWon)
    public event System.Action<bool> GameEnded;

    public LayerMask groundLayerMask;

    public Slider playerHealthBar;
    public Slider oppHealthBar;

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
        FindPlayers();
        SubscribeToHealthEvents();
        activeMatch = true;
    }

    // Fixed: this used to call UpdateHealthBars() unconditionally every single
    // frame, which did a fresh TryGetComponent<PlayerManager>() lookup on both
    // players every frame too - continuous polling and repeated redundant
    // component lookups for a value that only actually changes occasionally
    // (on a hit). Now the bars update only when PlayerManager.HealthChanged
    // actually fires - see SubscribeToHealthEvents() and OnMainPlayerHealthChanged/
    // OnOppPlayerHealthChanged below.
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

    // Called by a losing player's PlayerManager when its health hits zero.
    // Ends the current round, awards it to the other player, and either
    // starts a fresh round or ends the whole match if that's enough round
    // wins.
    public void RoundOver(PlayerManager loser)
    {
        if (isGameOver) return; // match already decided, ignore stray calls

        PlayerManager winner = (loser == mainPlayerManager) ? oppPlayerManager : mainPlayerManager;
        winner.roundWins++;

        RoundWinsChanged?.Invoke(mainPlayerManager.roundWins, oppPlayerManager.roundWins);

        if (winner.roundWins >= roundsToWinMatch)
        {
            EndMatch(winner);
        }
        else
        {
            StartNewRound();
        }
    }

    private void StartNewRound()
    {
        activeMatch = false; // brief freeze while resetting, avoids stray input/hits mid-reset

        mainPlayerManager.ResetHealth();
        oppPlayerManager.ResetHealth();

        if (mainPlayerStateMachine != null) mainPlayerStateMachine.ForceIdle();
        if (oppPlayerStateMachine != null) oppPlayerStateMachine.ForceIdle();

        mainPlayer.transform.position = mainPlayerStartPos;
        oppPlayer.transform.position = oppPlayerStartPos;

        activeMatch = true;
    }

    private void EndMatch(PlayerManager winner)
    {
        isGameOver = true;
        activeMatch = false;
        GameEnded?.Invoke(winner == mainPlayerManager);
    }

    // Called by the Game Over screen's "Play Again" button - resets round
    // wins for a fresh match and starts the first round of it.
    public void PlayAgain()
    {
        mainPlayerManager.roundWins = 0;
        oppPlayerManager.roundWins = 0;
        RoundWinsChanged?.Invoke(0, 0);

        isGameOver = false;
        StartNewRound();
    }

    private void FindPlayers()
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");

        if (allPlayers.Length > 0)
        {
            InitializePlayerArrays(allPlayers);
            mainPlayer = IdentifyMainPlayer();
            oppPlayer = IdentifyOppPlayer();

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

    private GameObject IdentifyOppPlayer()
    {
        for (int i = 0; i < playerManagers.Length; i++)
        {
            if (!playerManagers[i].isCTRLPlayer)
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

    // Given one player, returns the other - used for facing and AI targeting.
    public GameObject GetOpponentOf(GameObject self)
    {
        if (self == mainPlayer) return oppPlayer;
        if (self == oppPlayer) return mainPlayer;
        return null;
    }

    public Transform[] GetPlayers()
    {
        return playerTransforms;
    }

    private void HandleMissingPlayerManager(GameObject playerGameObject)
    {
        Debug.LogError(playerGameObject.name + " has no PlayerManager script attached");
    }

    private void SubscribeToHealthEvents()
    {
        if (mainPlayer == null || oppPlayer == null) return;

        mainPlayer.TryGetComponent(out mainPlayerManager);
        oppPlayer.TryGetComponent(out oppPlayerManager);
        mainPlayer.TryGetComponent(out mainPlayerStateMachine);
        oppPlayer.TryGetComponent(out oppPlayerStateMachine);

        // Cache each player's scene-placed position as where they'll be put
        // back at the start of every subsequent round.
        mainPlayerStartPos = mainPlayer.transform.position;
        oppPlayerStartPos = oppPlayer.transform.position;

        if (mainPlayerManager != null)
        {
            mainPlayerManager.HealthChanged += OnMainPlayerHealthChanged;
            // Initialize the bar to the correct starting fill immediately.
            OnMainPlayerHealthChanged(mainPlayerManager.playerHealth, mainPlayerManager.playerMaxHealth);
        }

        if (oppPlayerManager != null)
        {
            oppPlayerManager.HealthChanged += OnOppPlayerHealthChanged;
            OnOppPlayerHealthChanged(oppPlayerManager.playerHealth, oppPlayerManager.playerMaxHealth);
        }
    }

    private void OnMainPlayerHealthChanged(float current, float max)
    {
        if (playerHealthBar != null) playerHealthBar.value = current / max;
    }

    private void OnOppPlayerHealthChanged(float current, float max)
    {
        if (oppHealthBar != null) oppHealthBar.value = current / max;
    }

    private void OnDestroy()
    {
        if (mainPlayerManager != null) mainPlayerManager.HealthChanged -= OnMainPlayerHealthChanged;
        if (oppPlayerManager != null) oppPlayerManager.HealthChanged -= OnOppPlayerHealthChanged;
    }
}
