using UnityEngine;
using Photon.Pun;

/// <summary>
/// Photon PUN2 기반의 플레이어 밀치기(Nudge) 전용 컴포넌트.
/// 근접 범위 내 다른 플레이어를 감지하여 힘을 적용하고,
/// 네트워크 환경에서는 RPC를 통해 대상 플레이어에게 물리 효과를 동기화합니다.
/// </summary>
/// <remarks>
/// 책임 분리 목적:
/// - 이동/입력 로직(PlayerController)과 충돌 기반 상호작용을 분리
/// - 로컬 입력은 소유자만 처리하고, 실제 물리 반응은 대상 클라이언트에서 적용
/// - 근접 판정, 각도 제한, 입력 차단 등 Nudge 행위에 필요한 최소 책임만 담당
/// </remarks>

public class PlayerNudge : MonoBehaviourPunCallbacks
{
    [Header("Push")]
    public float pushForce = 8f;
    public float pushRadius = 1f;

    // OverlapSphere 중심을 캐릭터 발 위치에서 약간 위로 보정하기 위한 오프셋
    private float radiusOffset = 0.5f;

    // 밀치기 판정에 포함될 레이어
    public LayerMask othersLayer;

    [Range(0, 90)]
    public float pushAngle = 50f;
    [Tooltip("밀쳐진 플레이어 입력 차단 되는 시간")]
    public float inputBlockDuration = 0.3f;

    private Rigidbody rb;
    private Animator anim;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            if (PhotonNetwork.IsConnectedAndReady)
                photonView.RPC("PushAndAnimate", RpcTarget.All);
            else
                PushAndAnimate();
        }
    }

    [PunRPC]
    void PushAndAnimate()
    {
        anim.SetTrigger("Push");

        int mask = (othersLayer.value == 0) ? ~0 : othersLayer;
        var center = new Vector3(transform.position.x, transform.position.y + radiusOffset, transform.position.z);
        var colls = Physics.OverlapSphere(center, pushRadius, mask, QueryTriggerInteraction.Ignore);
        float pushAngleCos = Mathf.Cos(pushAngle * Mathf.Deg2Rad);

        foreach (Collider col in colls)
        {
            Rigidbody othersRb = col.attachedRigidbody;
            if (!othersRb || othersRb == rb) continue;

            Vector3 pushDir = (col.transform.position - transform.position).normalized;

            // 전방 각도 제한: 캐릭터가 바라보는 방향 기준
            if (Vector3.Dot(transform.forward, pushDir) <= pushAngleCos) continue;

            // 대상 플레이어의 소유 클라이언트에게만 힘 적용 요청
            var otherPv = col.GetComponentInParent<PhotonView>();
            if (otherPv && otherPv != photonView)
                otherPv.RPC(nameof(ApplyNudgeForce), otherPv.Owner, pushDir * pushForce, (int)ForceMode.Impulse);

            if (!photonView.IsMine) return;
            SFXEvents.Raise(SFXKey.Bump, transform.position, true, false);
        }
    }

    [PunRPC]
    void ApplyNudgeForce(Vector3 force, int mode)
    {
        GetComponent<Rigidbody>().AddForce(force, (ForceMode)mode);

        // 밀쳐진 플레이어의 입력을 일시적으로 차단
        if (photonView.IsMine)
        {
            PlayerController pController = GetComponent<PlayerController>();
            if (pController != null)
                pController.BlockInput(inputBlockDuration);
        }

        if (!photonView.IsMine) return;
        SFXEvents.Raise(SFXKey.BumpChar, transform.position, true, true);
    }

    // Nudge Radius
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y + radiusOffset, transform.position.z), pushRadius);
    }
}