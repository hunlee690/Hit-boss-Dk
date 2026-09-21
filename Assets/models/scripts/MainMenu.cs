using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-50)]
public class MainMenu : MonoBehaviour
{
    [System.Serializable]
    public class GameMode
    {
        public string modeName;
        public Button button;
        public string sceneName;
    }

    [Header("Menus")]
    public GameObject mainMenu;
    public GameObject customizeMenu;
    public GameObject gameModeMenu;

    [Header("Main Menu Buttons")]
    public Button customizeButton;
    public Button playButton;

    [Header("Back Buttons")]
    public Button customizeBackButton;
    public Button gameModeBackButton;

    [Header("Game Modes")]
    public GameMode[] gameModes;
    public bool onlinePlay;
    public Button onlineModeButton;
    public void ToggleOnlinePlay()
    {
        onlinePlay = !onlinePlay;
        if (onlineModeButton != null) onlineModeButton.GetComponentInChildren<TMPro.TMP_Text>().text = onlinePlay ? "ONLINE MATCH  (switch to solo)" : "SOLO PLAY  (switch to online)";
    }

    [Header("Player")]
    public GameObject playerSetup;
    public PlayerController playerController;
    public PlayerCamera playerCamera;

    [Header("Customization")]
    public PlayerCustomizationManager customizationManager;

    [Header("Model Rotation")]
    public Transform playerModel;
    public float rotateSpeed = 0.25f;


    void Awake()
    {
        SetPlayerGameplay(false);
        if (playerSetup != null)
        {
            playerSetup.transform.SetParent(null);
            DontDestroyOnLoad(playerSetup);
        }
    }


    void Start()
{
    if (customizationManager != null)
        customizationManager.InitializeCustomization();


    if (playerController != null)
        playerController.enabled = false;

    if (playerCamera != null)
        playerCamera.enabled = false;


    if (customizeButton != null)
        customizeButton.onClick.AddListener(OpenCustomization);

    if (customizeBackButton != null)
        customizeBackButton.onClick.AddListener(OpenMainMenu);

    if (playButton != null)
        playButton.onClick.AddListener(OpenGameModes);

    if (gameModeBackButton != null)
        gameModeBackButton.onClick.AddListener(OpenMainMenu);


    for (int i = 0; i < gameModes.Length; i++)
    {
        int index = i;

        if (gameModes[index].button != null)
        {
            gameModes[index].button.onClick.AddListener(
                () => LoadGameMode(index)
            );
        }
    }


    OpenMainMenu();
    if (onlineModeButton != null) onlineModeButton.onClick.AddListener(ToggleOnlinePlay);
}

    void Update()
    {
        // Keep cursor usable whenever we're in the menu scene
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RotatePlayer();
    }


    // =========================================================
    // MENU CONTROL
    // =========================================================

    public void OpenMainMenu()
    {
        SetMenu(mainMenu);
    }


    public void OpenCustomization()
    {
        SetMenu(customizeMenu);

        if (customizationManager != null)
            customizationManager.ShowHeadAccessories();
    }


    public void OpenGameModes()
    {
        SetMenu(gameModeMenu);
    }


    void SetMenu(GameObject menu)
    {
        if (mainMenu != null)
            mainMenu.SetActive(menu == mainMenu);

        if (customizeMenu != null)
            customizeMenu.SetActive(menu == customizeMenu);

        if (gameModeMenu != null)
            gameModeMenu.SetActive(menu == gameModeMenu);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    // =========================================================
    // GAME MODES
    // =========================================================

    void SetPlayerGameplay(bool playing)
    {
        if (playing)
        {
            // Scene loading completes next frame; silence the menu before enabling the player.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            }
        }
        if (playerController != null)
        {
            playerController.enabled = playing;
            var body = playerController.GetComponent<Rigidbody>();
            if (body != null) { if (playing) body.isKinematic = false; body.useGravity = playing; }
            var coop = playerController.GetComponent<CoopPlayerController>();
            if (coop != null) coop.enabled = false;
            var combat = playerController.GetComponent<CombatController>();
            if (combat != null) combat.enabled = playing;
            var inventory = playerController.GetComponent<ThrowableInventory>();
            if (inventory != null) inventory.enabled = playing;
        }
        if (playerCamera != null)
        {
            playerCamera.enabled = playing;
            var camera = playerCamera.GetComponent<Camera>();
            if (camera != null) camera.enabled = playing;
            var listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = playing;
        }
    }

    void LoadGameMode(int index)
{
    if (HitBoss.Multiplayer.RoomManager.Instance?.Room != null)
    {
        HitBoss.Multiplayer.RoomManager.Instance.SetStatus("Leave your private room before starting solo play.");
        FindFirstObjectByType<HitBoss.Multiplayer.RoomMenuController>()?.Open();
        return;
    }

    if (index < 0 || index >= gameModes.Length)
        return;


    string sceneName =
        gameModes[index].sceneName;

    if (onlinePlay)
    {
        FindFirstObjectByType<HitBoss.Multiplayer.RoomMenuController>()?.FindOnlineMode(sceneName);
        return;
    }


    if (string.IsNullOrEmpty(sceneName))
    {
        Debug.LogWarning(
            "No scene assigned to " +
            gameModes[index].modeName
        );

        return;
    }


    // IMPORTANT:
    // Push current customization into PlayerController
    // before entering gameplay.
    if (customizationManager != null)
    {
        customizationManager.ApplyEquippedItemsToPlayer();
    }


    SetPlayerGameplay(true);


    Cursor.lockState =
        CursorLockMode.Locked;

    Cursor.visible = false;


    SceneManager.LoadScene(
        sceneName
    );
}

    // =========================================================
    // PLAYER MODEL ROTATION
    // =========================================================

    void RotatePlayer()
    {
        if (playerModel == null || Mouse.current == null)
            return;

        if (!Mouse.current.leftButton.isPressed)
            return;

        // Don't rotate model while clicking UI
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;


        float mouseX = Mouse.current.delta.ReadValue().x;

        playerModel.Rotate(
            Vector3.up,
            -mouseX * rotateSpeed,
            Space.World
        );
    }
}
