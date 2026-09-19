using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    public GameObject pausePanel;
    public Button resumeButton;
    public Button quitMatchButton;

    [Header("Tags")]
    public string playerTag = "Player";
    public string cameraTag = "MainCamera";

    [Header("Quit")]
    public string mainMenuScene = "MainMenu";


    PlayerController playerController;
    PlayerCamera playerCamera;

    bool paused;


    void Awake()
    {
        // IMPORTANT:
        // PausePanel must NOT be this GameObject.
        if (pausePanel == gameObject)
        {
            Debug.LogError(
                "PausePanel cannot be the same object as PauseMenu script!"
            );

            return;
        }


        if (pausePanel != null)
            pausePanel.SetActive(false);


        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeMatch);


        if (quitMatchButton != null)
            quitMatchButton.onClick.AddListener(QuitMatch);


        Debug.Log("Pause Menu READY");
    }


    void Update()
    {
        if (Keyboard.current == null)
            return;


        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("ESC PRESSED");


            if (paused)
                ResumeMatch();
            else
                PauseMatch();
        }
    }


    // =====================================================
    // PAUSE
    // =====================================================

    public void PauseMatch()
    {
        paused = true;


        if (pausePanel != null)
            pausePanel.SetActive(true);


        FindPlayer();


        if (playerController != null)
        {
            playerController.StopImmediately();
            playerController.enabled = false;
        }


        if (playerCamera != null)
            playerCamera.enabled = false;


        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;


        Debug.Log("LOCAL PLAYER PAUSED");
    }


    // =====================================================
    // RESUME
    // =====================================================

    public void ResumeMatch()
    {
        paused = false;


        if (pausePanel != null)
            pausePanel.SetActive(false);


        FindPlayer();


        if (playerController != null)
            playerController.enabled = true;


        if (playerCamera != null)
            playerCamera.enabled = true;


        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }


    // =====================================================
    // FIND PLAYER + CAMERA
    // =====================================================

    void FindPlayer()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag(
                playerTag
            );


        if (player != null)
        {
            playerController =
                player.GetComponent<PlayerController>();


            if (playerController == null)
            {
                playerController =
                    player.GetComponentInChildren<PlayerController>(
                        true
                    );
            }
        }


        GameObject cameraObject =
            GameObject.FindGameObjectWithTag(
                cameraTag
            );


        if (cameraObject != null)
        {
            playerCamera =
                cameraObject.GetComponent<PlayerCamera>();


            if (playerCamera == null)
            {
                playerCamera =
                    cameraObject.GetComponentInParent<PlayerCamera>();
            }
        }
    }


    // =====================================================
    // QUIT
    // =====================================================

    public void QuitMatch()
    {
        FindPlayer();


        if (playerController != null)
        {
            GameObject setup =
                playerController.transform.root.gameObject;


            Destroy(setup);
        }


        SceneManager.LoadScene(
            mainMenuScene
        );
    }
}