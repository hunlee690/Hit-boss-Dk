using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using HitBoss.Multiplayer;
using HitBoss.Items;
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
    public ShopManager itemShop;
    public SpinManager spinManager;
    public Slider masterVolume, musicVolume, effectsVolume;
    public Toggle fullscreenToggle;
    public Button qualityButton;
    public TMP_Text qualityLabel;
    public GameMode[] gameModes;
    public GameObject playerSetup;
    public PlayerController playerController;
    public PlayerCamera playerCamera;
    public PlayerCustomizationManager customizationManager;
    public RoomMenuController roomMenuController;
    public Transform playerModel;
    public float rotateSpeed = .25f;

    GameObject mapMenu;
    TMP_Text mapMenuTitle;
    readonly List<Button> mapButtons = new List<Button>();
    MultiplayerMap[] shownMaps = System.Array.Empty<MultiplayerMap>();
    int pendingMode = -1;

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
        BuildMapMenu();
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
                int index = i;
                Bind(shopCategoryButtons[i], () => SelectShopSection(index));
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
    public void OpenSpin(string title) { SetMenu(spinMenu); if (spinTitle != null) spinTitle.text = title; spinManager?.Open(title); }
    public void OpenSpin(int spinIndex) { SetMenu(spinMenu); spinManager?.OpenSpin(spinIndex); }
    public void OpenSettings() => SetMenu(settingsMenu);
    public void HideAllMenus() => SetMenu(null, false);

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    void SetMenu(GameObject menu, bool closeRoomUI = true)
    {
        if (closeRoomUI && roomMenuController != null) roomMenuController.HideRoomUI();
        if (mainMenu != null) mainMenu.SetActive(menu == mainMenu);
        if (customizeMenu != null) customizeMenu.SetActive(menu == customizeMenu);
        if (gameModeMenu != null) gameModeMenu.SetActive(menu == gameModeMenu);
        if (shopMenu != null) shopMenu.SetActive(menu == shopMenu);
        if (gamePassMenu != null) gamePassMenu.SetActive(menu == gamePassMenu);
        if (spinMenu != null) spinMenu.SetActive(menu == spinMenu);
        if (settingsMenu != null) settingsMenu.SetActive(menu == settingsMenu);
        if (mapMenu != null) mapMenu.SetActive(menu == mapMenu);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void SelectShopSection(int index)
    {
        if (index == 0) { itemShop?.OpenSpins(ShopCurrency.Gems); return; }
        if (index == 1) { itemShop?.OpenSpins(ShopCurrency.Coins); return; }
        if (index == 3) { itemShop?.Open(); return; }
        itemShop?.Hide();
        if (shopStatus != null) shopStatus.text = "CURRENCY SHOP\nComing later";
    }

    void SetupSettings()
    {
        float master = PlayerPrefs.GetFloat("settings.master", 1);
        float music = PlayerPrefs.GetFloat("settings.music", .8f);
        float effects = PlayerPrefs.GetFloat("settings.effects", 1);

        if (masterVolume != null)
        {
            masterVolume.SetValueWithoutNotify(master);
            masterVolume.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat("settings.master", v); });
        }

        if (musicVolume != null)
        {
            musicVolume.SetValueWithoutNotify(music);
            musicVolume.onValueChanged.AddListener(v => PlayerPrefs.SetFloat("settings.music", v));
        }

        if (effectsVolume != null)
        {
            effectsVolume.SetValueWithoutNotify(effects);
            effectsVolume.onValueChanged.AddListener(v => PlayerPrefs.SetFloat("settings.effects", v));
        }

        AudioListener.volume = master;

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            fullscreenToggle.onValueChanged.AddListener(v => Screen.fullScreen = v);
        }

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

        pendingMode = index;
        var manager = RoomManager.Instance;
        int roomMode = manager == null
            ? -1
            : System.Array.FindIndex(manager.modes,
                mode => mode != null && mode.sceneName == gameModes[index].sceneName);

        shownMaps = roomMode >= 0
            ? manager.MapsForMode(roomMode)
            : System.Array.Empty<MultiplayerMap>();

        RefreshMapMenu();
        SetMenu(mapMenu);
    }

    void PlayMap(MultiplayerMap map)
    {
        if (map == null || pendingMode < 0 || pendingMode >= gameModes.Length)
            return;

        var rooms = roomMenuController != null
            ? roomMenuController
            : FindFirstObjectByType<RoomMenuController>();

        if (RoomManager.Instance?.Room != null)
        {
            RoomManager.Instance.SetStatus("Leave your current room before finding another match.");
            rooms?.Open();
            return;
        }

        if (rooms != null)
            rooms.FindOnlineMode(gameModes[pendingMode].sceneName, map.mapId);
        else
            RoomManager.Instance?.SetStatus("Matchmaking menu is not ready yet.");
    }

    void BuildMapMenu()
    {
        if (gameModeMenu == null || mapMenu != null)
            return;

        mapMenu = Instantiate(gameModeMenu, gameModeMenu.transform.parent);
        mapMenu.name = "Map Menu";
        mapMenu.transform.SetSiblingIndex(gameModeMenu.transform.GetSiblingIndex() + 1);

        Transform panel = mapMenu.transform.Find("Panel");
        if (panel == null)
            panel = mapMenu.transform;

        mapButtons.AddRange(panel.GetComponentsInChildren<Button>(true));
        if (mapButtons.Count == 0)
            return;

        int mapCapacity = RoomManager.Instance != null && RoomManager.Instance.maps != null
            ? Mathf.Max(1, RoomManager.Instance.maps.Length)
            : 5;

        while (mapButtons.Count < mapCapacity)
        {
            Button clone = Instantiate(mapButtons[0], mapButtons[0].transform.parent);
            clone.name = "Map " + (mapButtons.Count + 1);
            mapButtons.Add(clone);
        }

        for (int i = 0; i < mapButtons.Count; i++)
        {
            bool used = i < mapCapacity;
            mapButtons[i].gameObject.SetActive(used);
            if (!used) continue;

            mapButtons[i].onClick.RemoveAllListeners();
            RectTransform rect = mapButtons[i].transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(260, 145);
                int columns = 3;
                int row = i / columns;
                int firstIndex = row * columns;
                int rowCount = Mathf.Min(columns, mapCapacity - firstIndex);
                float x = (i - firstIndex - (rowCount - 1) * .5f) * 300f;
                float y = row == 0 ? 92f : -92f;
                rect.anchoredPosition = new Vector2(x, y);
            }
        }

        Button back = mapMenu.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => !button.transform.IsChildOf(panel));
        if (back != null)
        {
            back.onClick.RemoveAllListeners();
            back.onClick.AddListener(OpenGameModes);
        }

        TMP_Text source = mapButtons[0].GetComponentInChildren<TMP_Text>(true);
        if (source != null)
        {
            mapMenuTitle = Instantiate(source, mapMenu.transform);
            mapMenuTitle.name = "Map Menu Title";
            mapMenuTitle.text = "SELECT MAP";
            mapMenuTitle.alignment = TextAlignmentOptions.Center;
            mapMenuTitle.fontSize = 30;
            RectTransform titleRect = mapMenuTitle.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(.5f, .5f);
            titleRect.pivot = new Vector2(.5f, .5f);
            titleRect.anchoredPosition = new Vector2(0, 225);
            titleRect.sizeDelta = new Vector2(620, 55);
        }

        mapMenu.SetActive(false);
    }

    void RefreshMapMenu()
    {
        if (mapMenu == null)
            BuildMapMenu();

        if (mapMenuTitle != null)
        {
            string mode = pendingMode >= 0 && pendingMode < gameModes.Length
                ? gameModes[pendingMode].modeName
                : "MODE";
            mapMenuTitle.text = shownMaps.Length == 0
                ? "NO MAPS AVAILABLE FOR " + mode.ToUpperInvariant()
                : "SELECT MAP • " + mode.ToUpperInvariant();
        }

        for (int i = 0; i < mapButtons.Count; i++)
        {
            bool visible = i < shownMaps.Length;
            mapButtons[i].gameObject.SetActive(visible);
            if (!visible) continue;

            MultiplayerMap map = shownMaps[i];
            TMP_Text label = mapButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = map.displayName.ToUpperInvariant();

            Image image = mapButtons[i].GetComponent<Image>();
            if (image != null && map.preview != null)
                image.sprite = map.preview;

            mapButtons[i].onClick.RemoveAllListeners();
            mapButtons[i].onClick.AddListener(() => PlayMap(map));
        }
    }

    void Update()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerModel == null || Mouse.current == null || !Mouse.current.leftButton.isPressed) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        playerModel.Rotate(Vector3.up, -Mouse.current.delta.ReadValue().x * rotateSpeed, Space.World);
    }
}