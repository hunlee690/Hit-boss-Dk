using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using HitBoss.Multiplayer;
using TMPro;

[DefaultExecutionOrder(-50)]
public class MainMenu : MonoBehaviour
{
    [System.Serializable]
    public class GameMode { public string modeName; public Button button; public string sceneName; }
    public GameObject mainMenu, customizeMenu, gameModeMenu, shopMenu, gamePassMenu, spinMenu, settingsMenu;
    public Button customizeButton, playButton, customizeBackButton, gameModeBackButton;
    public Button shopButton, coinShopButton, gemShopButton, gamePassButton, dailySpinButton, gemSpinButton, settingsButton;
    public Button shopBackButton, gamePassBackButton, spinBackButton, settingsBackButton;
    public Button[] shopCategoryButtons;
    public TMP_Text shopStatus, spinTitle;
    public Slider masterVolume, musicVolume, effectsVolume;
    public Toggle fullscreenToggle;
    public Button qualityButton;
    public TMP_Text qualityLabel;
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
        Bind(shopButton, OpenShop); Bind(coinShopButton, OpenShop); Bind(gemShopButton, OpenShop);
        Bind(gamePassButton, OpenGamePass); Bind(dailySpinButton, () => OpenSpin("DAILY SPIN"));
        Bind(gemSpinButton, () => OpenSpin("FREE GEM SPIN")); Bind(settingsButton, OpenSettings);
        Bind(shopBackButton, OpenMainMenu); Bind(gamePassBackButton, OpenMainMenu);
        Bind(spinBackButton, OpenMainMenu); Bind(settingsBackButton, OpenMainMenu);
        if (shopCategoryButtons != null)
            for (int i = 0; i < shopCategoryButtons.Length; i++)
            {
                int index = i; Bind(shopCategoryButtons[i], () => SelectShopSection(index));
            }
        SetupSettings();
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
    public void OpenShop() { SetMenu(shopMenu); SelectShopSection(3); }
    public void OpenGamePass() => SetMenu(gamePassMenu);
    public void OpenSpin(string title) { if (spinTitle != null) spinTitle.text = title; SetMenu(spinMenu); }
    public void OpenSettings() => SetMenu(settingsMenu);
    static void Bind(Button button, UnityEngine.Events.UnityAction action) { if (button != null) button.onClick.AddListener(action); }
    void SetMenu(GameObject menu)
    {
        if (mainMenu != null) mainMenu.SetActive(menu == mainMenu);
        if (customizeMenu != null) customizeMenu.SetActive(menu == customizeMenu);
        if (gameModeMenu != null) gameModeMenu.SetActive(menu == gameModeMenu);
        if (shopMenu != null) shopMenu.SetActive(menu == shopMenu);
        if (gamePassMenu != null) gamePassMenu.SetActive(menu == gamePassMenu);
        if (spinMenu != null) spinMenu.SetActive(menu == spinMenu);
        if (settingsMenu != null) settingsMenu.SetActive(menu == settingsMenu);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    void SelectShopSection(int index)
    {
        if (index == 0) { OpenSpin("GEM SPIN"); return; }
        if (index == 1) { OpenSpin("COIN SPIN"); return; }
        if (shopStatus != null) shopStatus.text = index == 2 ? "CURRENCY SHOP\nComing later" : "ITEM SHOP\nComing later";
    }
    void SetupSettings()
    {
        float master = PlayerPrefs.GetFloat("settings.master", 1);
        float music = PlayerPrefs.GetFloat("settings.music", .8f);
        float effects = PlayerPrefs.GetFloat("settings.effects", 1);
        if (masterVolume != null) { masterVolume.SetValueWithoutNotify(master); masterVolume.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat("settings.master", v); }); }
        if (musicVolume != null) { musicVolume.SetValueWithoutNotify(music); musicVolume.onValueChanged.AddListener(v => PlayerPrefs.SetFloat("settings.music", v)); }
        if (effectsVolume != null) { effectsVolume.SetValueWithoutNotify(effects); effectsVolume.onValueChanged.AddListener(v => PlayerPrefs.SetFloat("settings.effects", v)); }
        AudioListener.volume = master;
        if (fullscreenToggle != null) { fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen); fullscreenToggle.onValueChanged.AddListener(v => Screen.fullScreen = v); }
        if (qualityButton != null)
        {
            RefreshQualityLabel();
            qualityButton.onClick.AddListener(() =>
            {
                QualitySettings.SetQualityLevel((QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length, true);
                RefreshQualityLabel();
            });
        }
    }
    void RefreshQualityLabel()
    {
        if (qualityLabel != null && QualitySettings.names.Length > 0)
            qualityLabel.text = QualitySettings.names[QualitySettings.GetQualityLevel()];
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
