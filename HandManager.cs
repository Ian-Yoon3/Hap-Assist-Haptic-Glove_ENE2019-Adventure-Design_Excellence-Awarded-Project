using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
// Required to reference HandLandmarkerRunner from the MediaPipe sample
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class HandManager : MonoBehaviour
{
    [Header("References")]
    public HandLandmarkerRunner runner;
    public Transform[] fingerTips; // 0: Thumb, 1: Index, 2: Middle, 3: Ring, 4: Pinky

    [Header("Grab Settings")]
    public float grabThreshold = 0.08f; // Thumb-index distance that triggers a grab (Unity units)
    private GameObject grabbedObject = null; // Object currently held

    [Header("Calibration")]
    public float sensitivityX = 15.0f; // Increased for larger movement
    public float sensitivityY = 15.0f;
    public float sensitivityZ = 5.0f; // Increased to exaggerate depth (important)

    private int[] landmarkIds = { 4, 8, 12, 16, 20 };
    private string[] signals = { "A", "B", "C", "D", "E" };
    private string[] names = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

    private bool[] wasTouching = { false, false, false, false, false }; // Previous contact state of each finger (5)


    void Start()
    {
        // Find the runner automatically if it is not assigned
        if (runner == null) runner = FindObjectOfType<HandLandmarkerRunner>();

        // Attach a FingerSensor to each of the five fingertips
        for (int i = 0; i < 5; i++)
        {
            if (fingerTips[i] != null)
            {
                FingerSensor sensor = fingerTips[i].GetComponent<FingerSensor>();
                if (sensor == null) sensor = fingerTips[i].gameObject.AddComponent<FingerSensor>();
                sensor.fingerName = names[i];
                sensor.vibrationSignal = signals[i];
            }
        }
    }

    void Update()
    {
        // Stop if there is no runner
        if (runner == null) return;

        var result = runner.handLandmarkerResult;

        // Run all logic only while a hand is visible
        // (All logic is placed inside this condition)
        if (result.handLandmarks != null && result.handLandmarks.Count > 0)
        {
            // ------------------------------------------------------------
            // 1. Keep the grab state (send 'G')
            // ------------------------------------------------------------
            if (grabbedObject != null)
            {
                grabbedObject.GetComponent<Renderer>().material.color = Color.green;

                // Send G only while a hand is visible, so the motors stay off at startup
                if (ArduinoConnector.Instance != null)
                {
                    ArduinoConnector.Instance.Send("G");
                }
            }

            // ------------------------------------------------------------
            // 2. Move the fingertips (coordinate mapping)
            // ------------------------------------------------------------
            var landmarks = result.handLandmarks[0].landmarks;

            for (int i = 0; i < 5; i++)
            {
                if (fingerTips[i] != null)
                {
                    Vector3 targetPos = GetWorldCoordinate(landmarks[landmarkIds[i]]);
                    fingerTips[i].position = Vector3.Lerp(fingerTips[i].position, targetPos, Time.deltaTime * 15f);
                }
            }

            // ------------------------------------------------------------
            // 3. Gesture and touch detection
            // ------------------------------------------------------------

            // Check for a grab when the thumb (0) and index (1) exist
            if (fingerTips[0] != null && fingerTips[1] != null)
            {
                CheckGrab();
            }

            // Check touch for each finger
            CheckTouch();
        }
        // No hand visible: do nothing and send no command
    }

    // Grab-related functions

    void CheckGrab()
    {
        // Distance between the thumb and index fingertips
        float distance = Vector3.Distance(fingerTips[0].position, fingerTips[1].position);
        // Use this log to tune the threshold
        Debug.Log($"Distance: {distance:F3} / Threshold: {grabThreshold}");

        // A. Try to grab: fingers are close and nothing is held
        if (distance < grabThreshold && grabbedObject == null)
        {
            TryGrab();
        }
        // B. Release: fingers moved apart while holding an object
        else if (distance > grabThreshold && grabbedObject != null)
        {
            Release();
        }
    }

    void TryGrab()
    {
        // Find objects within 0.1 units of the index fingertip
        Collider[] colliders = Physics.OverlapSphere(fingerTips[1].position, 0.1f);

        foreach (var collider in colliders)
        {
            // Only grab objects tagged 'Touchable'
            if (collider.CompareTag("Touchable"))
            {
                grabbedObject = collider.gameObject;

                // 1. Disable physics so the object can follow the hand
                Rigidbody rb = grabbedObject.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                // 2. Parent the object to the index fingertip
                grabbedObject.transform.SetParent(fingerTips[1]);

                // 3. Turn green
                grabbedObject.GetComponent<Renderer>().material.color = Color.green;

                Debug.Log("Grab (turned green)");

                // Send 'G' if the Arduino is connected
                if (ArduinoConnector.Instance != null) ArduinoConnector.Instance.Send("G");

                return; // Stop after grabbing one object
            }
        }
    }

    void Release()
    {
        if (grabbedObject == null) return;

        // 1. Restore white
        grabbedObject.GetComponent<Renderer>().material.color = Color.white;

        // 2. Detach from the hand
        grabbedObject.transform.SetParent(null);

        // 3. Re-enable physics so the object falls
        Rigidbody rb = grabbedObject.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Debug.Log("Release (sending release command)");
        grabbedObject = null;

        // Send 'R' to stop the motors
        if (ArduinoConnector.Instance != null)
        {
            ArduinoConnector.Instance.Send("R");
        }
    }

    // Touch detection for each finger
    void CheckTouch()
    {
        // Ignore individual touches while holding an object
        if (grabbedObject != null) return;

        // Check all five fingers
        for (int i = 0; i < 5; i++)
        {
            if (fingerTips[i] == null) continue;

            // 1. Is this finger touching something now?
            // (Look for an object tagged "Touchable" within a radius of 0.015 units)
            bool isTouchingNow = false;
            Collider[] hits = Physics.OverlapSphere(fingerTips[i].position, 0.015f); // 0.015 Unity units

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Touchable"))
                {
                    isTouchingNow = true;
                    break;

                    Debug.Log($"{names[i]} touched '{hit.name}'");

                    break;

                }
            }

            // 2. Send a command only when the state changes (key logic)
            // [No contact -> contact]: send uppercase (ON)
            if (isTouchingNow && !wasTouching[i])
            {
                ArduinoConnector.Instance.Send(signals[i]); // A, B, C...
                // Debug.Log($"{names[i]} touch ({signals[i]})");
            }
            // [Contact -> no contact]: send lowercase (OFF)
            else if (!isTouchingNow && wasTouching[i])
            {
                ArduinoConnector.Instance.Send(signals[i].ToLower()); // a, b, c...
                // Debug.Log($"{names[i]} release ({signals[i].ToLower()})");
            }

            // 3. Store the current state
            wasTouching[i] = isTouchingNow;
        }
    }



    // Maps a normalized landmark to world space through the main camera
    Vector3 GetWorldCoordinate(Mediapipe.Tasks.Components.Containers.NormalizedLandmark landmark)
    {
        // 1. Fall back to this object's position if there is no camera
        if (Camera.main == null) return transform.position;

        // 2. Screen X and Y coordinates (0 to 1)
        // MediaPipe puts (0,0) at the top left and Unity at the bottom left, so Y is flipped (1 - y)
        float screenX = landmark.x;
        float screenY = 1.0f - landmark.y;

        // 3. Depth (Z)
        // Distance from the camera = base distance + landmark depth * sensitivity
        float depthBase = 10.0f; // Reference plane 10 units in front of the camera (adjustable)
        float zPos = depthBase + (landmark.z * sensitivityZ);

        // 4. Convert the viewport point to a 3D world position (key function)
        Vector3 worldPos = Camera.main.ViewportToWorldPoint(new Vector3(screenX, screenY, zPos));

        return worldPos;
    }
}