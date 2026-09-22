using UnityEngine;

public class CoopModeManager : ModeManagerBase
{
    public static CoopModeManager Instance { get; private set; }


    void Awake()
    {
        Instance = this;
    }


    void Start()
    {
        EnterCoopMode();
    }


    // =====================================================
    // CO-OP MODE
    // =====================================================

    public void EnterCoopMode()
    {
        ApplyModeScripts();

        Debug.Log("Co-op Mode activated.");
    }
}