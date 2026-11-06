using UnityEngine;

public class FingerSensor : MonoBehaviour
{
    [Header("손가락 설정")]
    public string fingerName;       // 예: 엄지, 검지...
    public string vibrationSignal;  // 아두이노로 보낼 신호 (A, B, C. D, E)

    private void OnTriggerEnter(Collider other)
    {
        // 'Touchable' 태그가 달린 물체(큐브)와 닿았을 때
        if (other.CompareTag("Touchable"))
        {
            if (other.transform.parent != null) return; //[추가된 부분] 핵심 해결책!
            // 만약 물체의 부모(Parent)가 있다면? = 이미 잡혀서 손을 따라다니는 중이라면?
            // -> 색깔을 빨간색으로 바꾸지 말고 그냥 함수 종료(return)!

            Debug.Log($"[TOUCH] {fingerName} 진동 시작! (신호: {vibrationSignal})");

            // 시각적 효과: 빨간색으로 변함
            other.GetComponent<Renderer>().material.color = Color.red;

            //[핵심] 아두이노로 신호 쏘기!
            if (ArduinoConnector.Instance != null)
            {
                ArduinoConnector.Instance.Send(vibrationSignal);
            }

        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 물체에서 떨어졌을 때
        if (other.CompareTag("Touchable"))
        {
            // 시각적 효과: 원래 하얀색으로 복구
            other.GetComponent<Renderer>().material.color = Color.white;
        }
    }
}