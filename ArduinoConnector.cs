using UnityEngine;
using System.IO.Ports; //  시리얼 통신 라이브러리
using System;

public class ArduinoConnector : MonoBehaviour
{
    // 어디서든 부를 수 있게 '싱글톤'으로 만듭니다
    public static ArduinoConnector Instance;

    [Header("포트 설정")]
    public string portName = "COM5"; //  내 컴퓨터 포트번호로 꼭 바꿔야 함!
    public int baudRate = 9600;      // 통신 속도

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
            Debug.Log($"아두이노 연결 성공! ({portName})");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"연결 실패: {e.Message} (포트 번호를 확인하거나 아두이노 IDE를 꺼주세요)");
        }
    }

    // 신호를 보내는 함수
    public void Send(string signal)
    {
        if (stream != null && stream.IsOpen)
        {
            stream.Write(signal);
            // Debug.Log($"[전송] {signal}"); // 로그가 너무 많으면 주석 처리
        }
    }

    void OnApplicationQuit()
    {
        if (stream != null && stream.IsOpen) stream.Close();
    }
}