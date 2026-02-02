using Photon.Pun;
using UnityEngine;

/// <summary>
/// 슬라이더(레일) 오브젝트 위에서의 강제 이동 로직을 담당하는 컴포넌트.
/// - SliderPath의 웨이포인트를 따라 자동 이동
/// - 슬라이딩 중에는 물리 연산을 비활성화
/// - 회전 및 애니메이션을 통해 눕는 연출 처리
/// </summary>

public class PlayerSliding : MonoBehaviourPunCallbacks
{
    // Import the components that need to be referenced in PlayerController.
    private PhotonView pv;
    private Rigidbody rb;
    private Animator anim;
    private CapsuleCollider col;

    [Header("Sliding Settings")]
    public float slideSpeed = 5f;
    public float rotSpeedSliding = 5f;

    // Sliding status and path information
    private bool isSliding = false;
    private SliderPath currSlider; // NOTE : SliderPath 클래스가 외부에서 정의되어 있어야 함(경로 컴포넌트 참조)
    private int currWaypointIdx; // 진행 중인 웨이포인트 인덱스

    void Awake()
    {
        pv = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        col = GetComponent<CapsuleCollider>();
    }

    void FixedUpdate()
    {
        if (!pv.IsMine) return;

        HandleSlidingMovement();
    }

    public void HandleSlidingMovement()
    {
        if (!isSliding) return;

        // Sliding Logic
        // 웨이포인트가 남아있는 동안 슬라이딩 지속
        if (currSlider != null && currWaypointIdx < currSlider.pathWaypoints.Length)
        {
            Transform targetPoint = currSlider.pathWaypoints[currWaypointIdx];
            Vector3 dir = (targetPoint.position - transform.position).normalized;

            transform.position = Vector3.MoveTowards(transform.position, targetPoint.position, slideSpeed * Time.fixedDeltaTime);

            Quaternion targetRot = Quaternion.LookRotation(dir);
            Quaternion lyingRot = Quaternion.Euler(-70f, 0, 0); // Setting the lying angle

            Quaternion finalRot = targetRot * lyingRot;

            transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, rotSpeedSliding * Time.fixedDeltaTime);

             // 목표 지점 도달 시 다음 웨이포인트로 이동
            if (Vector3.Distance(transform.position, targetPoint.position) < 0.2f)
            {
                currWaypointIdx++;
            }
        }
        else
        {
            // End Sliding
            isSliding = false;
            rb.isKinematic = false;
            col.enabled = true; // Recover Collider

            anim.SetBool("IsSliding", false);

            // Recover Rotation -> World y Axis
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!pv.IsMine) return;

        if (other.CompareTag("Slider"))
        {
            isSliding = true;
            currSlider = other.GetComponentInParent<SliderPath>();
            anim.SetBool("IsSliding", true);
            currWaypointIdx = 0;
            rb.isKinematic = true; // Stop Physics Engine
            col.enabled = false; // Disable colliders to prevent overlap
        }
    }

    // 외부 시스템에서 현재 슬라이딩 상태 여부를 확인하기 위한 Getter
    public bool IsSliding()
    {
        return isSliding;
    }
}