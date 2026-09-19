using UnityEngine;

public class PlayerNameTag : MonoBehaviour
{
    Camera mainCamera;


    void Start()
    {
        FindCamera();
    }


    void LateUpdate()
    {
        if (mainCamera == null)
        {
            FindCamera();

            if (mainCamera == null)
                return;
        }


        Vector3 direction =
            transform.position -
            mainCamera.transform.position;


        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(direction);
        }
    }


    void FindCamera()
    {
        mainCamera = Camera.main;
    }
}