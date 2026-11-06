using UnityEngine;
using System.IO.Ports; // Serial communication library
using System;

public class ArduinoConnector : MonoBehaviour
{
    // Singleton so that any script can reach the connector
    public static ArduinoConnector Instance;

    [Header("Port Settings")]
    public string portName = "COM5"; // Change this to the COM port of your Arduino
    public int baudRate = 9600;      // Must match the firmware baud rate

    private SerialPort stream;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        OpenConnection();
    }

    public void OpenConnection()
    {
        try
        {
            stream = new SerialPort(portName, baudRate);
            stream.ReadTimeout = 50;
            stream.Open();
            Debug.Log($"Arduino connected ({portName})");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Connection failed: {e.Message} (check the port number or close the Arduino IDE)");
        }
    }

    // Sends a command string to the Arduino
    public void Send(string signal)
    {
        if (stream != null && stream.IsOpen)
        {
            stream.Write(signal);
            // Debug.Log($"[SEND] {signal}"); // Commented out to avoid flooding the log
        }
    }

    void OnApplicationQuit()
    {
        if (stream != null && stream.IsOpen) stream.Close();
    }
}