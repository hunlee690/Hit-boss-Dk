using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using HitBoss.Multiplayer;

[DefaultExecutionOrder(-50)]
public class MainMenu : MonoBehaviour
{
    [System.Serializable]
    public class GameMode { public string modeName; public Button button; public string sceneName; }
    public GameObject mainMenu, customizeMenu, gameModeMenu;
    public Button customizeButton, playButton, customizeBackButton, gameModeBackButton;
    public GameMode[] gameModes;
    public GameObject playerSetup;
    public PlayerController playerController;
    public PlayerCamera playerCamera;
    public PlayerCustomizationManager customizationManager;
    public Transform playerModel;
    public float rotateSpeed = .25f;

    void Awake()
    {
        if (playerController != null)
        {
            playerController.enabled = false;
            var body = playerController.GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
            var coop = playerController.GetComponent<CoopPlayerController>();
            if (coop != null) coop.enabled = false;
        }
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
            var camera = playerCamera.GetComponent<Camera>(); if (camera != null) camera.enabled = false;
            var listener = playerCamera.GetComponent<AudioListener>(); if (listener != null) listener.enabled = false;
        }
        if (playerSetup != null) { playerSetup.transform.SetParent(null); DontDestroyOnLoad(playerSetup); }
    }
    void Start()
    {
        if (customizationManager != null) customizationManager.InitializeCustomization();
        if (customizeButton != null) customizeButton.onClick.AddListener(OpenCustomization);
        if (customizeBackButton != null) customizeBackButton.onClick.AddListener(OpenMainMenu);
        if (playButton != null) playButton.onClick.AddListener(OpenGameModes);
        if (gameModeBackButton != null) gameModeBackButton.onClick.AddListener(OpenMainMenu);
        for (int i = 0; i < gameModes.Length; i++)
        {
            int index = i;
            if (gameModes[i].button != null) gameModes[i].button.onClick.AddListener(() => LoadGameMode(index));
        }
        OpenMainMenu();
    }
    public void OpenMainMenu() => SetMenu(mainMenu);
    public void OpenCustomization() { SetMenu(customizeMenu); if (customizationManager != null) customizationManager.ShowHeadAccessories(); }
    public void OpenGameModes() => SetMenu(gameModeMenu);
    void SetMenu(GameObject menu)
    {
        if (mainMenu != null) mainMenu.SetActive(menu == mainMenu);
        if (customizeMenu != null) customizeMenu.SetActive(menu == customizeMenu);
        if (gameModeMenu != null) gameModeMenu.SetActive(menu == gameModeMenu);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    void LoadGameMode(int index)
    {
        if (index < 0 || index >= gameModes.Length) return;
        var rooms = FindFirstObjectByType<RoomMenuController>();
        if (RoomManager.Instance?.Room != null)
        {
            RoomManager.Instance.SetStatus("Leave your current room before finding another match."); rooms?.Open(); return;
        }
        rooms?.FindOnlineMode(gameModes[index].sceneName);
    }
    void Update()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (playerModel == null || Mouse.current == null || !Mouse.current.leftButton.isPressed) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        playerModel.Rotate(Vector3.up, -Mouse.current.delta.ReadValue().x * rotateSpeed, Space.World);
    }
}
