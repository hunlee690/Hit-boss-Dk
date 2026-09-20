using UnityEngine;

public class BattelRoyalmanager : ModeManagerBase
{
    public static BattelRoyalmanager Instance { get; private set; }


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