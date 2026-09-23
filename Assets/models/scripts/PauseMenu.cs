using HitBoss.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel, resultScreen;
    public Button resumeButton, quitMatchButton;
    public string playerTag = "Player", cameraTag = "MainCamera", mainMenuScene = "main menu";
    PlayerController playerController;
    CoopPlayerController coop;
    PlayerCamera playerCamera;
    CombatController combat;
    ThrowableInventory inventory;
    NetworkPlayer onlinePlayer;
    bool paused, quitting, movementEnabled, coopEnabled, combatEnabled, inventoryEnabled, resultWasOpen;
    float previousTimeScale = 1;
    public bool IsPaused => paused;

    void Awake()
    {
        if (pausePanel == gameObject) { Debug.LogError("PausePanel cannot be the PauseMenu object."); return; }
        if (pausePanel != null) pausePanel.SetActive(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(ResumeMatch);
        if (quitMatchButton != null) quitMatchButton.onClick.AddListener(QuitMatch);
    }

    void Update()
    {
        if (!quitting && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        { if (paused) ResumeMatch(); else PauseMatch(); }
    }

    void FindPlayer()
    {
        onlinePlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();
        if (RoomManager.Instance?.Room != null)
        {
            playerController = onlinePlayer != null ? onlinePlayer.movement : null;
            playerCamera = onlinePlayer != null ? onlinePlayer.playerCamera : null;
        }
        else
        {
            var player = GameObject.FindGameObjectWithTag(playerTag);
            playerController = player != null ? player.GetComponentInChildren<PlayerController>(true) : null;
            playerCamera = playerController != null ? playerController.transform.root.GetComponentInChildren<PlayerCamera>(true) : null;
        }
        coop = playerController != null ? playerController.GetComponent<CoopPlayerController>() : null;
        combat = playerController != null ? playerController.GetComponent<CombatController>() : null;
        inventory = playerController != null ? playerController.GetComponent<ThrowableInventory>() : null;
    }

    public void PauseMatch()
    {
        if (paused || quitting) return;
        FindPlayer();
        if (RoomManager.Instance?.Room != null && onlinePlayer == null) return;
        paused = true;
        resultWasOpen = resultScreen != null && resultScreen.activeSelf;
        if (resultScreen != null) resultScreen.SetActive(false);
        if (pausePanel != null) { pausePanel.SetActive(true); pausePanel.transform.SetAsLastSibling(); }

        if (onlinePlayer != null) onlinePlayer.SetPaused(true);
        else
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            movementEnabled = playerController != null && playerController.enabled;
            coopEnabled = coop != null && coop.enabled;
            combatEnabled = combat != null && combat.enabled;
            inventoryEnabled = inventory != null && inventory.enabled;
            if (movementEnabled) { playerController.StopImmediately(); playerController.enabled = false; }
            if (coopEnabled) { coop.StopImmediately(); coop.enabled = false; }
            if (combat != null) combat.enabled = false;
            if (inventory != null) inventory.enabled = false;
            if (playerCamera != null) playerCamera.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeMatch()
    {
        if (!paused || quitting) return;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (resultScreen != null && resultWasOpen) resultScreen.SetActive(true);

        if (onlinePlayer != null) onlinePlayer.SetPaused(false);
        else
        {
            Time.timeScale = previousTimeScale;
            if (playerController != null) playerController.enabled = movementEnabled;
            if (coop != null) coop.enabled = coopEnabled;
            if (combat != null) combat.enabled = combatEnabled;
            if (inventory != null) inventory.enabled = inventoryEnabled;
            if (playerCamera != null) playerCamera.enabled = true;
        }

        paused = false;
        resultWasOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public async void QuitMatch()
    {
        if (quitting) return;
        quitting = true;
        if (onlinePlayer == null) Time.timeScale = previousTimeScale;

        if (RoomManager.Instance?.Room != null)
        {
            await RoomManager.Instance.LeaveAsync();
            if (this != null) quitting = false;
            return;
        }

        FindPlayer();

        if (playerController != null)
        {
            var rig = playerController.transform.root.gameObject;
            rig.SetActive(false);
            Destroy(rig);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuScene);
    }

    void OnDestroy()
    {
        if (paused && onlinePlayer == null) Time.timeScale = previousTimeScale;
    }

}