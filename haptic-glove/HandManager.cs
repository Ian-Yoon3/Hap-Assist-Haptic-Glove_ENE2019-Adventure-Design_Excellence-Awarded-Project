using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
// 보내주신 코드의 핵심: 이 줄이 있어야 오류가 안 납니다!
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class HandManager : MonoBehaviour
{
    [Header("연결할 것들")]
    public HandLandmarkerRunner runner;
    public Transform[] fingerTips; // 0:엄지, 1:검지...

    [Header("잡기 설정 (추가됨)")]
    public float grabThreshold = 0.08f; // 잡기 감지 거리 (8cm)
    private GameObject grabbedObject = null; // 현재 잡고 있는 물체

    [Header("보정값")]
    public float sensitivityX = 15.0f; // 움직임을 위해 조금 키웠습니다
    public float sensitivityY = 15.0f;
    public float sensitivityZ = 5.0f; // 깊이감을 위해 키웠습니다 (중요!)

    private int[] landmarkIds = { 4, 8, 12, 16, 20 };
    private string[] signals = { "A", "B", "C", "D", "E" };
    private string[] names = { "엄지", "검지", "중지", "약지", "소지" };

    private bool[] wasTouching = { false, false, false, false, false }; //각 손가락의 충돌 상태를 기억할 변수 (5개)


    void Start()
    {
        // 러너 자동 찾기 (혹시 연결 안 되어 있을까봐 추가)
        if (runner == null) runner = FindObjectOfType<HandLandmarkerRunner>();

        // 5개 구슬 센서 자동 부착 (보내주신 코드 그대로 유지)
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
        // 1. 러너가 없으면 중단
        if (runner == null) return;

        var result = runner.handLandmarkerResult;

        // ★ [수정된 부분] 손이 화면에 보일 때만! 모든 것을 실행합니다 ★
        // (이 조건문 안으로 모든 로직을 집어넣었습니다)
        if (result.handLandmarks != null && result.handLandmarks.Count > 0)
        {
            // ------------------------------------------------------------
            // 1. 잡기 상태 유지 ('G' 신호 전송)
            // ------------------------------------------------------------
            if (grabbedObject != null)
            {
                grabbedObject.GetComponent<Renderer>().material.color = Color.green;

                // 손이 있을 때만 G를 보냅니다. (이제 시작하자마자 진동 안 옴!)
                if (ArduinoConnector.Instance != null)
                {
                    ArduinoConnector.Instance.Send("G");
                }
            }

            // ------------------------------------------------------------
            // 2. 손가락 이동 (좌표 변환)
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
            // 3. 제스처 및 터치 감지
            // ------------------------------------------------------------

            // 엄지(0)와 검지(1)가 있으면 잡기(Grab) 체크
            if (fingerTips[0] != null && fingerTips[1] != null)
            {
                CheckGrab();
            }

            // 개별 손가락 터치(Touch) 체크
            CheckTouch();
        }
        // 손이 안 보이면? -> 아무것도 안 함 (신호도 안 보냄)
    }

    //  여기서부터는 새로 추가된 잡기 관련 함수들입니다 

    void CheckGrab()
    {
        // 엄지와 검지 사이의 거리 계산
        float distance = Vector3.Distance(fingerTips[0].position, fingerTips[1].position);
        //이 로그를 보고 값을 조절
        Debug.Log($"현재 거리: {distance:F3} / 설정된 기준: {grabThreshold}");

        // A. 잡기 시도: 거리가 가깝고 + 아직 잡은 게 없을 때
        if (distance < grabThreshold && grabbedObject == null)
        {
            TryGrab();
        }
        // B. 놓기 시도: 거리가 멀어지고 + 잡은 게 있을 때
        else if (distance > grabThreshold && grabbedObject != null)
        {
            Release();
        }
    }

    void TryGrab()
    {
        // 검지 손가락 끝 주변 10cm 안에 있는 물체를 찾음
        Collider[] colliders = Physics.OverlapSphere(fingerTips[1].position, 0.1f);

        foreach (var collider in colliders)
        {
            // 'Touchable' 태그가 붙은 녀석만 잡음
            if (collider.CompareTag("Touchable"))
            {
                grabbedObject = collider.gameObject;

                // 1. 물리 기능 끄기 (손에 붙이기 위해)
                Rigidbody rb = grabbedObject.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                // 2. 손(검지)의 자식으로 설정 -> 손을 따라다님
                grabbedObject.transform.SetParent(fingerTips[1]);

                // 3.  [색상 변경] 초록색!
                grabbedObject.GetComponent<Renderer>().material.color = Color.green;

                Debug.Log(" Grab! (초록색으로 변신)");

                // 아두이노 연결되어 있으면 'G' 신호 전송
                if (ArduinoConnector.Instance != null) ArduinoConnector.Instance.Send("G");

                return; // 하나 잡았으면 끝
            }
        }
    }

    void Release()
    {
        if (grabbedObject == null) return;

        // 1.  [색상 복구] 흰색!
        grabbedObject.GetComponent<Renderer>().material.color = Color.white;

        // 2. 손에서 떼어내기
        grabbedObject.transform.SetParent(null);

        // 3. 물리 기능 켜기 (바닥으로 떨어지게)
        Rigidbody rb = grabbedObject.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Debug.Log(" Release! (놓기 신호 전송)");
        grabbedObject = null;

        //  [추가] 놓았으니까 멈추라는 신호 'R' 전송!
        if (ArduinoConnector.Instance != null)
        {
            ArduinoConnector.Instance.Send("R");
        }
    }

    // 개별 손가락 터치 감지 함수 (새로 추가)
    void CheckTouch()
    {
        // 잡기 모드일 때는 개별 터치 무시 (충돌 방지)
        if (grabbedObject != null) return;

        // 5개 손가락 모두 검사
        for (int i = 0; i < 5; i++)
        {
            if (fingerTips[i] == null) continue;

            // 1. 현재 이 손가락이 무언가와 닿아있는가?
            // (반경 1.5cm 안에 "Interactable" 태그가 붙은 물체가 있는지 확인)
            bool isTouchingNow = false;
            Collider[] hits = Physics.OverlapSphere(fingerTips[i].position, 0.015f); // 1.5cm

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Touchable"))
                {
                    isTouchingNow = true;
                    break;

                    Debug.Log($"범인 검거! {names[i]}가 '{hit.name}'와 닿았습니다!");

                    break;

                }
            }

            // 2. 상태가 변했을 때만 신호 전송 (핵심!)
            // [안 닿음 -> 닿음] : 대문자 전송 (ON)
            if (isTouchingNow && !wasTouching[i])
            {
                ArduinoConnector.Instance.Send(signals[i]); // A, B, C...
                // Debug.Log($"{names[i]} 닿음! ({signals[i]})");
            }
            // [닿음 -> 안 닿음] : 소문자 전송 (OFF)
            else if (!isTouchingNow && wasTouching[i])
            {
                ArduinoConnector.Instance.Send(signals[i].ToLower()); // a, b, c...
                // Debug.Log($"{names[i]} 떨어짐! ({signals[i].ToLower()})");
            }

            // 3. 상태 기억
            wasTouching[i] = isTouchingNow;
        }
    }



    //  이 코드로 바꿔주세요! 카메라 중심으로 보는 코드
    Vector3 GetWorldCoordinate(Mediapipe.Tasks.Components.Containers.NormalizedLandmark landmark)
    {
        // 1. 카메라가 없으면 그냥 기존 방식대로 (에러 방지)
        if (Camera.main == null) return transform.position;

        // 2. 화면상의 X, Y 좌표 (0 ~ 1)
        // MediaPipe는 (0,0)이 왼쪽 위, Unity는 왼쪽 아래라서 Y를 뒤집어야 함 (1 - y)
        float screenX = landmark.x;
        float screenY = 1.0f - landmark.y;

        // 3. 깊이(Z) 계산
        // 카메라로부터 얼마나 떨어뜨릴 것인가? (기본 거리 + 손의 깊이 * 감도)
        float depthBase = 10.0f; // 카메라 앞 10m 지점을 기준점으로 잡음 (조절 가능)
        float zPos = depthBase + (landmark.z * sensitivityZ);

        // 4. "화면의 점을 3D 월드 좌표로 변환해라" (핵심 함수!)
        Vector3 worldPos = Camera.main.ViewportToWorldPoint(new Vector3(screenX, screenY, zPos));

        return worldPos;
    }
}