using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

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


    if (playerController != null)
        playerController.enabled = true;

    if (playerCamera != null)
        playerCamera.enabled = true;


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
