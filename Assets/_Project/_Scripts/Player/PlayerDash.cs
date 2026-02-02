using Photon.Pun;
using UnityEngine;

/// <summary>
/// Player의 대시 이동 로직을 담당하는 컴포넌트.
/// - 더블 탭 입력을 감지하여 대시
/// - 카메라 기준 방향으로 대시 벡터 계산
/// - Photon RPC를 통해 모든 클라이언트에 대시 이벤트 동기화
/// - 로컬 플레이어만 쿨타임 및 실제 물리 이동을 처리
/// </summary>
/// 
public class PlayerDash : MonoBehaviourPunCallbacks
{
    private Rigidbody rb;
    private PhotonView pv;
    // Use PlayerController -> camPivot
    private PlayerController pController;

    [Header("Dash Settgins")]
    public float dashForce;
    public float dashCooltime;
    [Tooltip("Doubble Tap 인정 시간")]
    public float doubleTapWindow;
    [Tooltip("Dash 적용 시간")]
    public float dashDuration;

    private float lastDashTime;
    private float dashTimer;
    private Vector3 dashDir;

    // 더블 탭 입력 판별용
    private KeyCode lastKey;
    private float lastTapTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        pv = GetComponent<PhotonView>();
        pController = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (!pv.IsMine) return;

        CheckDoubleTapInput();
    }

    void FixedUpdate()
    {
        if (!pv.IsMine) return;

        HandleDashExecution();
    }

    void CheckDoubleTapInput()
    {
        if(Time.time < lastDashTime + dashCooltime) return;

        KeyCode currTapKey = KeyCode.None;
         // 방향 키 입력 통합 처리 (WASD + 방향키)
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) currTapKey = KeyCode.W;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) currTapKey = KeyCode.S;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) currTapKey = KeyCode.A;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) currTapKey = KeyCode.D;

        if (currTapKey != KeyCode.None)
        {
             // 동일 키를 일정 시간 내에 다시 입력했을 경우 더블 탭 성공
            if(currTapKey == lastKey && Time.time < lastTapTime + doubleTapWindow)
            {
                Transform camPivot = pController.camPivot;
                if (camPivot == null) return;

                // 카메라 방향으로 dash 방향 계산
                Vector3 camFwd = camPivot.forward;
                Vector3 camRight = camPivot.right;
                camFwd.y = 0; camRight.y = 0;
                camFwd.Normalize(); camRight.Normalize();

                Vector3 dashDir = Vector3.zero;
                if(currTapKey == KeyCode.W) dashDir = camFwd;
                else if(currTapKey == KeyCode.S) dashDir = -camFwd;
                else if(currTapKey == KeyCode.A) dashDir = -camRight;
                else if(currTapKey == KeyCode.D) dashDir = camRight;

                 // 유효한 방향일 경우 모든 클라이언트에 대시 이벤트 전파
                if(dashDir.sqrMagnitude > 0.01f)
                {
                    photonView.RPC(nameof(DashRPC), RpcTarget.All, dashDir.normalized);
                }

                lastKey = KeyCode.None;
                lastTapTime = 0;
            }
            else
            {
                // 첫 탭 기록
                lastKey = currTapKey;
                lastTapTime = Time.time;
            }
        }
    }

    // 대시 알리기, 로컬 플레이어 타이머 설정.
    [PunRPC]
    void DashRPC(Vector3 dir)
    {
        if (pv.IsMine)
        {
            dashDir = dir.normalized;
            lastDashTime = Time.time;
            dashTimer = dashDuration;
        }
    }

    void HandleDashExecution()
    {
        if(dashTimer > 0)
        {
            AudioManager.Instance.PlayPooledSFX(SFXKey.Dash, transform.position);
            // 대시 중에는 중력 영향을 줄여 수평 이동감을 강조
            rb.velocity = new Vector3(dashDir.x * dashForce, rb.velocity.y, dashDir.z * dashForce);
            dashTimer -= Time.fixedDeltaTime;
        }
    }
}
