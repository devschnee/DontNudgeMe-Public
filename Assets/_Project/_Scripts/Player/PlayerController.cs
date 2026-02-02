using ithappy;
using Photon.Pun;
using System.Collections;
using UnityEngine;

/// <summary>
/// 포톤 네트워크(Photon PUN2) 기반의 플레이어 캐릭터 컨트롤러 클래스입니다.
/// 로컬 플레이어의 입력 처리, Rigidbody 기반 이동, 
/// 애니메이션 제어 및 네트워크 객체 간의 위치/일부 상태 값 동기화를 관리합니다.
/// </summary>
/// <remarks>
/// 주요 기능:
/// - 카메라 방향 기반의 이동 및 경사면 보정 로직
/// - 점프, 슬라이딩, 낙하 등 상태 플래그 및 애니메이션 파라미터 기반 캐릭터 상태 처리
/// - 아이템 인벤토리 및 타겟팅 시스템 연동
/// - Rideable 오브젝트 탑승을 위한 Parenting 기반 플랫폼 처리
/// - 네트워크 보간(Lerp)을 이용한 원격 플레이어 움직임 최적화
/// </remarks>
///

[RequireComponent(typeof(Rigidbody), typeof(PhotonView))]
public class PlayerController : MonoBehaviourPunCallbacks, IPunObservable
{
    #region Variables
    [Header("Movement")]
    [Tooltip("최대 목표 속도")]
    public float moveSpeed;
    public float rotSmoothTime = 0.15f;
    [Tooltip("가속도(얼마나 빨리 목표 속도에 도달할지)")]
    public float accel;
    private float currRotY;
    private float rotYVel;
    private Vector3 moveDir;
    private bool isGrounded;

    [Header("Jump")]
    public float jumpForce;
    public float groundCheckDist = 0.2f;
    public LayerMask groundMask;
    private Coroutine blockJumpCoroutine;
    private bool isJumpBlocked;
    private bool jumpRequested;
    private bool isFalling = false;
    [Tooltip("점프력을 높일땐 무조건 이 변수 값도 높여야 함. 낙하 속도 판정.")]
    public float fallingSpeed = 5f; // 낙하 애니메이션 전환 기준 속도

    // NOTE : Movement와 연관됨.
    [Header("Camera")]
    public Transform camPivot;
    public Transform mainCamera;
    public Camera Camera;
    public float mouseSensitivity = 3f;
    private float camRotY;

    private Rigidbody rb;
    private Animator anim;

    private PlayerSliding playerSliding;
    private CapsuleCollider col; // Collider는 슬라이딩 종료 시 필요할 수 있으므로 유지 (슬라이딩 로직에서 제어)

    public bool isNudged;
    private Coroutine blockInputCoroutine; // 밀쳐지면 input 입력 막음
    private Vector3 networkPos;
    private Quaternion networkRot;
    private bool networkIsSliding;
    private bool networkIsNudged;
    [HideInInspector] public int checkpointIndex = -1; // -1 = 시작지점
    [SerializeField] LayerMask rideableLayers; // 탈 수 있는 플랫폼만(예 : 회전 원판)

    [SerializeField] TargetingSystem targetingSystem;
    [SerializeField] PlayerSkillItemInventory inventory;
    [SerializeField] float checkInterval = 0.2f;
    [SerializeField] float useCooldown = 0.5f;
    private float cooldownTimer = 0f;
    private float checkTimer;
    private bool networkIsFalling;
    public ShieldTing shield;

    [Header("Effects")]
    public ParticleSystem wallHitParticle;
    public ParticleSystem landParticle;
    public ParticleSystem nudgeParticle;
    public float minLandSpeed = 5f;

    private const int PARTICLE_LAND = 1;
    private const int PARTICLE_NUDGE = 2;
    #endregion

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        anim = GetComponent<Animator>();
        col = GetComponent<CapsuleCollider>();

        targetingSystem = GetComponent<TargetingSystem>();
        inventory = GetComponent<PlayerSkillItemInventory>();
        checkTimer = checkInterval;

        playerSliding = GetComponent<PlayerSliding>();

        jumpRequested = false;

        // 오프라인 모드 대응
        if (!PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("LOCAL");
            if (rb.isKinematic) rb.isKinematic = false;
        }

        // 내 캐릭터와 타 캐릭터의 물리 설정 구분
        if (photonView.IsMine)
        {
            rb.isKinematic = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            rb.isKinematic = true;
        }

#if UNITY_EDITOR
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
#endif
    }

    void Start()
    {
        if (photonView.IsMine)
        {
            // 오직 내 카메라만 켠다
            Camera.enabled = true;
            camPivot.gameObject.SetActive(true);
            mainCamera.gameObject.SetActive(true);
        }
        else
        {
            // 원격 플레이어 카메라는 비활성
            Camera.enabled = false;
            camPivot.gameObject.SetActive(true);
        }

        // 관전 타겟 등록은 필요 시
        SpectatorManager.Instance.AddTarget(transform);
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            CamControl();
            Movement();

            SkillItemData currentItem = inventory.PeekSkillItem(0);
            if (currentItem == null) return;

            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;

            if (Input.GetKeyDown(KeyCode.Q))
            {
                inventory.SwapSlots(0, 1);
            }
            switch (currentItem.castType)
            {
                // 즉발형 우클릭 누르는 순간 발동
                case SkillItemType.SelfInstant:
                case SkillItemType.EnemyInstant:
                    if (Input.GetMouseButtonDown(1))
                    {
                        inventory.UseSkillItem(0);
                        cooldownTimer = useCooldown;
                    }
                    break;
                // 타겟형 우클릭 유지 시 타겟팅, 뗐을 때 발동
                case SkillItemType.Targeted:
                    if (Input.GetMouseButton(1)) // 우클릭 유지
                    {
                        checkTimer -= Time.deltaTime;
                        if (checkTimer <= 0f)
                        {
                            targetingSystem.RefreshTarget();
                            checkTimer = checkInterval;
                        }
                    }
                    if (Input.GetMouseButtonUp(1)) // 우클릭 뗌
                    {
                        Transform target = targetingSystem.CurrentTarget;
                        inventory.UseSkillItem(0, target);
                        targetingSystem.ClearTarget();
                        cooldownTimer = useCooldown;
                    }
                    break;
            }
            //
        }

        GroundCheck();
    }

    void FixedUpdate()
    {
        bool isSliding = playerSliding != null && playerSliding.IsSliding();

        if (photonView.IsMine)
        {
            if (isSliding)
            {
                moveDir = Vector3.zero;
                anim.SetBool("IsRun", false);
                SetFallingState(false);
                return;
            }
            // 착지 판정 위해 현재 속도 저장
            float previousVY = rb.velocity.y;
            HandleMovement();

            HandleJump();

            // NOTE : rb.velocity < fallingSpeed <= 낙하속도. 점프력을 높이면 낙하속도도 높여줘야 진짜로 '낙하'할때 낙하 모션이 나올 수 있음.
            if (!isGrounded && !jumpRequested && rb.velocity.y < -fallingSpeed)
            {
                SetFallingState(true);
            }
            else if (isGrounded)
            {
                if (previousVY < -minLandSpeed)
                {
                    Vector3 landPos = Vector3.down * (col.height / 2f) + Vector3.up * 1f;
                    photonView.RPC(nameof(PlayParticleRPC), RpcTarget.All, PARTICLE_LAND, landPos, Quaternion.Euler(-90f, 0f, 0f));
                }
                SetFallingState(false);
            }
        }
        else
        {
            // 네트워크를 통한 원격 캐릭터 위치 보간
            transform.position = Vector3.Lerp(transform.position, networkPos, Time.fixedDeltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRot, Time.fixedDeltaTime * 10f);

            anim.SetBool("IsSliding", networkIsSliding);
            anim.SetBool("IsNudged", networkIsNudged);
            anim.SetBool("IsFall", networkIsFalling);
        }
        GroundCheck();
    }

    #region HandleMovement
    void HandleMovement()
    {
        if (moveDir.sqrMagnitude > 0.01f)
        {
            // 움직임(낮은 턱 자동 오르기)
            CheckAndStepUp();

            Vector3 targetVel = moveDir * moveSpeed;
            Vector3 velocityChange = targetVel - new Vector3(rb.velocity.x, 0, rb.velocity.z);
            rb.AddForce(velocityChange * accel, ForceMode.Acceleration);

            // 경사면 고려한 캐릭터 회전 로직
            Vector3 groundNormal = Vector3.up;
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 1.0f, groundMask))
            {
                groundNormal = slopeHit.normal;
            }

            Vector3 desiredMoveDir = Vector3.ProjectOnPlane(moveDir, groundNormal).normalized;
            float targetRotY = Mathf.Atan2(desiredMoveDir.x, desiredMoveDir.z) * Mathf.Rad2Deg;
            currRotY = Mathf.SmoothDampAngle(currRotY, targetRotY, ref rotYVel, rotSmoothTime);
            Quaternion newRot = Quaternion.Euler(0, currRotY, 0);
            rb.MoveRotation(newRot);
        }
        else
        {
            // 정지 시 미끄러짐 방지
            rb.velocity = new Vector3(Mathf.Lerp(rb.velocity.x, 0, accel * Time.fixedDeltaTime), rb.velocity.y, Mathf.Lerp(rb.velocity.z, 0, accel * Time.fixedDeltaTime));
        }
    }
    #endregion

    #region Handle Jump
    void HandleJump()
    {
        // 점프
        if (jumpRequested && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            if ((PhotonNetwork.IsConnectedAndReady))
                photonView.RPC(nameof(JumpRPC), RpcTarget.All);
            else
                JumpRPC();
            
            SFXEvents.Raise(SFXKey.Jump, transform.position, true, false);
            
            isGrounded = false;
            jumpRequested = false;
        }
    }

    [PunRPC]
    void JumpRPC()
    {
        anim.SetTrigger("Jump");
        // ▼ [더블 점프 방지] 점프 막는 시간을 조절하고 싶으면 여기를 수정하세요.
        BlockJump(0.1f);
    }

    void BlockJump(float duration)
    {
        if (blockJumpCoroutine != null)
            StopCoroutine(blockJumpCoroutine);
        blockJumpCoroutine = StartCoroutine(BlockJumpCoroutine(duration));
    }

    private IEnumerator BlockJumpCoroutine(float duration)
    {
        isJumpBlocked = true;
        jumpRequested = false;
        yield return new WaitForSeconds(duration);
        blockJumpCoroutine = null;
        isJumpBlocked = false;
    }
    #endregion

    void SetFallingState(bool falling)
    {
        if (isFalling == falling) return;

        isFalling = falling;
        anim.SetBool("IsFall", falling);
    }

    public void UpdateCheckpoint(int newIndex)
    {
        if (newIndex > checkpointIndex)
        {
            checkpointIndex = newIndex;
        }
        else
        {
            //Debug.Log($"[PlayerController] Checkpoint {newIndex} ignored, current: {checkpointIndex}");
        }
    }

    // OnPhotonSerializeView is a required function of the IPunObservable interface.
    // This function is used to send and receive data over the network.
    // 포톤 네트워크 동기화
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Local Player : Send to others ; Position, Rotation, Speed
        if (stream.IsWriting)
        {
            stream.SendNext(rb.position);
            stream.SendNext(rb.rotation);
            stream.SendNext(rb.velocity);
            stream.SendNext(anim.GetBool("IsRun"));
            stream.SendNext(playerSliding != null ? playerSliding.IsSliding() : false); // Sliding state Sync
            stream.SendNext(isNudged);
            stream.SendNext(isFalling);
        }
        // Other Players : Receive the data and store it in a local storage
        else
        {
            networkPos = (Vector3)stream.ReceiveNext();
            networkRot = (Quaternion)stream.ReceiveNext();
            Vector3 tmpVel = (Vector3)stream.ReceiveNext();
            anim.SetBool("IsRun", (bool)stream.ReceiveNext());
            networkIsSliding = (bool)stream.ReceiveNext();
            networkIsNudged = (bool)stream.ReceiveNext();
            networkIsFalling = (bool)stream.ReceiveNext();
        }
    }

    // BoxCast : detect low obstacles(ex : bump)
    // 낮은 장애물 만났을 때 살짝 띄워주는 로직
    void CheckAndStepUp()
    {
        RaycastHit hit;
        Vector3 boxCenter = new Vector3(transform.position.x, transform.position.y + 0.15f, transform.position.z);
        Vector3 boxSize = new Vector3(GetComponent<CapsuleCollider>().radius * 0.8f, 0.1f, GetComponent<CapsuleCollider>().radius * 0.8f);
        float rayDistance = GetComponent<CapsuleCollider>().radius + 0.1f;

        if (Physics.BoxCast(boxCenter, boxSize, transform.forward, out hit, transform.rotation, rayDistance, groundMask))
        {
            // 무릎보다 낮으면 탐지
            float characterKneeY = transform.position.y + GetComponent<CapsuleCollider>().center.y - (GetComponent<CapsuleCollider>().height / 4f);
            if (hit.point.y < characterKneeY)
            {
                if (hit.collider.CompareTag("LowObstacles") && Vector3.Dot(hit.normal, Vector3.up) < 0.1f)
                {
                    // AddForce once
                    rb.AddForce(Vector3.up * jumpForce * 0.1f, ForceMode.Impulse);
                    BlockJump(0.6f);
                }
            }
        }
    }

    #region Input System
    public void BlockInput(float duration)
    {
        if (blockInputCoroutine != null)
            StopCoroutine(blockInputCoroutine);
        blockInputCoroutine = StartCoroutine(BlockInputcoroutine(duration));

        if (anim != null)
        {
            anim.SetBool("IsRun", false);
        }
    }

    private IEnumerator BlockInputcoroutine(float duration)
    {
        isNudged = true;
        if (anim != null)
            anim.SetBool("IsNudged", true);
            
        Vector3 nudgePos = Vector3.up * (col.center.y + col.height / 2f + 0.01f);

        photonView.RPC(nameof(PlayParticleRPC), RpcTarget.All, PARTICLE_NUDGE, nudgePos, Quaternion.Euler(-90f, 0, 0));

        yield return new WaitForSeconds(duration);

        isNudged = false;
        blockInputCoroutine = null;
        if (anim != null)
            anim.SetBool("IsNudged", false);
    }

    void Movement()
    {
        bool isSliding = playerSliding != null && playerSliding.IsSliding();

        if (isNudged)
        {
            moveDir = Vector3.zero;
            jumpRequested = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isSliding && isGrounded && !jumpRequested && !isJumpBlocked)
            {
                jumpRequested = true;
            }
        }

        if (isSliding)
        {
            moveDir = Vector3.zero;
            anim.SetBool("IsRun", false);
            return;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        if (Mathf.Approximately(x, 0) && Mathf.Approximately(z, 0) && !Input.GetKeyDown(KeyCode.Space))
        {
            moveDir = Vector3.zero;
            anim.SetBool("IsRun", false);
            return;
        }

        Vector3 camFwd = camPivot.forward.normalized;
        Vector3 camRight = camPivot.right.normalized;

        bool isMoving = Mathf.Abs(x) > 0.01f || Mathf.Abs(z) > 0.01f;

        moveDir = isMoving ? (camFwd * z + camRight * x).normalized : Vector3.zero;

        anim.SetBool("IsRun", isMoving);

    }
    #endregion
    void CamControl()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        camRotY += mouseX;
        camPivot.rotation = Quaternion.Euler(0, camRotY, 0);
    }

    void GroundCheck()
    {
        // 캡슐 하단 가장자리에서 아주 짧게 쏘는 레이
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col)
        {
            // 캡슐 월드 바운드 하단(y)
            float footY = transform.position.y + col.center.y - (col.height * 0.5f) + col.radius;
            Vector3 foot = new Vector3(transform.position.x, footY + 0.02f, transform.position.z);

            // 짧은 거리로만 체크 (지면 접촉 판정 안정화)
            const float shortDist = 1f;

            isGrounded = Physics.Raycast(foot, Vector3.down, shortDist, groundMask);
        }
        else
        {
            // 콜라이더 없으면
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDist, groundMask);
        }
    }

    #region Particle Effects

    [PunRPC]
    void PlayParticleRPC(int type, Vector3 pos, Quaternion rot)
    {
        ParticleSystem particlePrefab = null;

        switch (type)
        {
            case PARTICLE_LAND:
                particlePrefab = landParticle;
                break;
            case PARTICLE_NUDGE:
                particlePrefab = nudgeParticle;
                break;
            default:
                return;
        }

        if(particlePrefab == null)
        {
            return;
        }

        ParticleSystem newPart = null;
        
        newPart = Instantiate(particlePrefab, transform);
        newPart.transform.localPosition = pos;
        newPart.transform.localRotation = rot;
        

        newPart.Play();

        float destroyDelay = newPart.main.duration + newPart.main.startLifetime.constantMax;

        if(destroyDelay > 0.5f)
        {
            destroyDelay = 0.5f;
        }

        Destroy(newPart.gameObject, destroyDelay);
    }
    #endregion

    // Required Rigidbody(isKinematic, Not use Gravity) <- other Objects (e.g. rotating disk)
    void OnCollisionEnter(Collision collision)
    {
        if (!photonView.IsMine) return; // 내 로컬만 탑승 처리

        var other = collision.transform;
        var otherRb = collision.rigidbody;

        // 플레이어 객체면 탑승 금지
        if (other.root.GetComponent<PlayerController>() != null) return;
        if (other.root.GetComponent<PhotonView>() != null) return;

        var rs = other.gameObject.GetComponent<RotationScript>();

        if ((rideableLayers.value & (1 << other.gameObject.layer)) == 0) return;

        if (isGrounded && rs != null)
            transform.SetParent(other);
    }

    void OnCollisionExit(Collision collision)
    {
        if (!photonView.IsMine) return;

        // 실제 탑승한 객체에서 부모-자식 해제
        if (transform.parent != null &&
       (collision.transform == transform.parent || collision.transform.IsChildOf(transform.parent)))
        {
            transform.SetParent(null);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!photonView.IsMine) return; // 자신만 처리

        if (other.CompareTag("Water"))
        {
            SpectatorManager.Instance.RemoveTarget(transform);

            if (StageManager.Instance != null)
            {
                // 플레이어 생성만 호출
                StageManager.Instance.SpawnPlayer();
            }

            if (StageFourManager.Instance != null)
            {
                photonView.RPC(nameof(RPC_ReportDNF), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);

                SpectatorManager.Instance.EnterSpectatorMode();
            }

            // 기존 오브젝트 파괴는 마지막에
            PhotonNetwork.Destroy(gameObject);
        }

        if (other.CompareTag("SkillItemBox"))
        {
            AudioManager.Instance.PlayOneShotSFX(SFXKey.GetItem, transform.position);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        // Draw BoxCast
        Vector3 boxCenter = new Vector3(transform.position.x, transform.position.y + 0.15f, transform.position.z);
        Vector3 boxSize = new Vector3(GetComponent<CapsuleCollider>().radius * 0.8f, 0.1f, GetComponent<CapsuleCollider>().radius * 0.8f);
        float rayDistance = GetComponent<CapsuleCollider>().radius + 0.1f;

        // BoxCast's start vertex and vector
        Gizmos.DrawWireCube(boxCenter, boxSize * 2);
        Gizmos.DrawRay(boxCenter, transform.forward * rayDistance);

        // BoxCast's end vertex
        Gizmos.DrawWireCube(boxCenter + transform.forward * rayDistance, boxSize * 2);
    }

    [PunRPC]
    void RPC_ReportDNF(int actorNumber)
    {
        // 마스터에서만 실행됨
        RaceManager.Instance.RegisterDNF(actorNumber, true);

        if (!RaceManager.Instance.stageFourDNFActors.Contains(actorNumber))
            RaceManager.Instance.stageFourDNFActors.Add(actorNumber);

        // Property 갱신해서 클라이언트들도 DNF 반영
        string csv = string.Join(",", RaceManager.Instance.stageFourDNFActors);
        var props = new ExitGames.Client.Photon.Hashtable() { { "StageFourDNF", csv } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    //LSH테스트소리
    private void PlayStepSFX()
    {
        AudioManager.Instance.PlayPooledSFX(SFXKey.Footstep, transform.position);
    }
}