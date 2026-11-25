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
    PlayerCamera playerCamera;
    NetworkPlayer onlinePlayer;
    bool paused, quitting, resultWasOpen;
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

    void LateUpdate()
    {
        if (!paused) return;
        if (resultScreen != null && resultScreen.activeSelf) resultScreen.SetActive(false);
        if (pausePanel != null)
        {
            if (!pausePanel.activeSelf) pausePanel.SetActive(true);
            pausePanel.transform.SetAsLastSibling();
        }
    }

    void FindPlayer()
    {
        onlinePlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();
        playerController = onlinePlayer != null ? onlinePlayer.movement : null;
        playerCamera = onlinePlayer != null ? onlinePlayer.playerCamera : null;
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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeMatch()
    {
        if (!paused || quitting) return;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (resultScreen != null && resultWasOpen) resultScreen.SetActive(true);

        if (onlinePlayer != null) onlinePlayer.SetPaused(false);

        paused = false;
        Cursor.lockState = resultWasOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = resultWasOpen;
        resultWasOpen = false;
    }

    public async void QuitMatch()
    {
        if (quitting) return;
        quitting = true;

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



}
