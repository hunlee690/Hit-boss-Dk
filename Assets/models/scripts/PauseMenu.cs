using HitBoss.Multiplayer;
using HitBoss.Multiplayer.Skate;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    public Button resumeButton, quitMatchButton;
    public string mainMenuScene = "main menu";
    NetworkPlayer player;
    bool paused, quitting;
    public bool IsPaused => paused;
    bool MatchFinished => player != null && player.GetComponent<SkateCombat>()?.Finished.Value == true;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() { SceneManager.sceneLoaded -= EnsurePause; SceneManager.sceneLoaded += EnsurePause; }
    static void EnsurePause(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "main menu") return;
        var existing = FindFirstObjectByType<PauseMenu>();
        if (existing != null) { existing.enabled = true; return; }
        new GameObject("Pause menu").AddComponent<PauseMenu>();
    }
    void Awake()
    {
        if (pausePanel == gameObject) pausePanel = null;
        if (pausePanel != null)
        {
            var canvas = pausePanel.GetComponent<Canvas>();
            if (canvas == null) canvas = pausePanel.AddComponent<Canvas>();
            var parentCanvas = pausePanel.transform.parent != null ? pausePanel.transform.parent.GetComponentInParent<Canvas>() : null;
            if (parentCanvas != null) canvas.sortingLayerID = parentCanvas.sortingLayerID;
            canvas.overrideSorting = true; canvas.sortingOrder = 32760;
            if (pausePanel.GetComponent<GraphicRaycaster>() == null) pausePanel.AddComponent<GraphicRaycaster>();
            pausePanel.SetActive(false);
        }
        if (resumeButton != null) resumeButton.onClick.AddListener(ResumeMatch);
        if (quitMatchButton != null) quitMatchButton.onClick.AddListener(QuitMatch);
    }
    void Update()
    {
        if (!quitting && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        { if (paused) ResumeMatch(); else PauseMatch(); }
    }
    public void PauseMatch()
    {
        if (paused || quitting) return;
        player = NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();
        paused = true;
        if (pausePanel != null) { pausePanel.transform.SetAsLastSibling(); pausePanel.SetActive(true); }
        if (player != null) player.SetPaused(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        var selected = MatchFinished ? quitMatchButton : resumeButton;
        if (selected != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selected.gameObject);
    }
    public void ResumeMatch()
    {
        if (!paused || quitting) return;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (player != null) player.SetPaused(false);
        paused = false;
        Cursor.lockState = MatchFinished ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = MatchFinished;
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
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        SceneManager.LoadScene(mainMenuScene);
    }
    void OnGUI()
    {
        if (!paused || pausePanel != null) return;
        var box = new Rect(Screen.width / 2f - 150, Screen.height / 2f - 100, 300, 200);
        GUI.Box(box, "Paused");
        if (GUI.Button(new Rect(box.x + 30, box.y + 50, 240, 50), "Resume")) ResumeMatch();
        if (GUI.Button(new Rect(box.x + 30, box.y + 115, 240, 50), "Leave match")) QuitMatch();
    }
}
