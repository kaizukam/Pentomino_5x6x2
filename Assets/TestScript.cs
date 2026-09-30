using UnityEngine;

public class TestScript : MonoBehaviour
{
    [SerializeField] 
    private string message = "Hello, Unity from VSCode!";

    [SerializeField] 
    private float rotateSpeed = 50.0f;

    void Start()
    {
        Debug.Log(message);
    }

    void Update()
    {
        transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space key was pressed!");
        }
    }
}