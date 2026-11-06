using UnityEngine;

public class FingerSensor : MonoBehaviour
{
    [Header("Finger Settings")]
    public string fingerName;       // e.g. Thumb, Index...
    public string vibrationSignal;  // Command sent to the Arduino (A, B, C, D, E)

    private void OnTriggerEnter(Collider other)
    {
        // Called when the fingertip enters an object tagged 'Touchable'
        if (other.CompareTag("Touchable"))
        {
            if (other.transform.parent != null) return; // Skip objects that are already held
            // An object with a parent is currently grabbed and follows the hand.
            // In that case, do not recolor it and return immediately.

            Debug.Log($"[TOUCH] {fingerName} vibration start (command: {vibrationSignal})");

            // Visual feedback: turn red
            other.GetComponent<Renderer>().material.color = Color.red;

            // Send the vibration command to the Arduino
            if (ArduinoConnector.Instance != null)
            {
                ArduinoConnector.Instance.Send(vibrationSignal);
            }

        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Called when the fingertip leaves the object
        if (other.CompareTag("Touchable"))
        {
            // Visual feedback: restore white
            other.GetComponent<Renderer>().material.color = Color.white;
        }
    }
}