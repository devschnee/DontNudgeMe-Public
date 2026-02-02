using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어의 간단한 감정 표현(퀵 이모지)을 담당하는 컴포넌트.
/// - 로컬 입력을 통해 이모지를 선택
/// - Photon RPC로 모든 클라이언트에 이모지 표시 이벤트를 전파
/// - 월드 캔버스 기반으로 일정 시간 동안 이모지를 노출
/// </summary>

public class PlayerQuickEmoji : MonoBehaviourPunCallbacks
{
    [Header("Quick Emoji")]
    public Sprite[] emojis; // 선택 가능한 이모지 스프라이트 목록
    public Image emojiImage; // 월드 캔버스에 표시될 이모지 이미지
    public float emojiLifeTime = 2f; // 이모지가 화면에 유지되는 시간
    private float emojiTimer = 0f; // 이모지 표시 타이머 (로컬/원격 공통)

    void Awake()
    {
        // World Canvas의 이모지 이미지 비활성화
        if (emojiImage)
        {
            emojiImage.enabled = false;
            emojiImage.sprite = null;
        }
    }

    void Update()
    {
        // 이모지 입력은 로컬 플레이어만 처리
        if (photonView.IsMine)
        {
            HandleEmojiInput();
        }

        // 이모지 표시 타이머 처리
        // RPC로 활성화된 이후, 모든 클라이언트에서 동일하게 시간 감소
        if (emojiImage && emojiImage.enabled)
        {
            emojiTimer -= Time.deltaTime;
            if (emojiTimer <= 0f)
            {
                emojiImage.enabled = false;
                emojiImage.sprite = null;
            }
        }
    }

    void HandleEmojiInput()
    {
        int idx = -1;

        // 숫자 키 입력에 따라 이모지 인덱스 결정
        if (Input.GetKeyDown(KeyCode.Alpha1))
        { idx = 0; SFXEvents.Raise(SFXKey.Emote, transform.position, false, false); }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        { idx = 1; SFXEvents.Raise(SFXKey.Emote, transform.position, false, false); }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        { idx = 2; SFXEvents.Raise(SFXKey.Emote, transform.position, false, false); }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        { idx = 3; SFXEvents.Raise(SFXKey.Emote, transform.position, false, false); }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        { idx = 4; SFXEvents.Raise(SFXKey.Emote, transform.position, false, false); }
        if (Input.GetKeyDown(KeyCode.Alpha6))
        { idx = 5; SFXEvents.Raise(SFXKey.Emote, transform.position, false, false); }

        // 유효한 이모지 선택 시, 모든 클라이언트에 표시 요청
        // 이모지는 입력이 아닌 연출 이벤트이므로 RPC로 동기화
        if (idx >= 0 && idx < emojis.Length)
        {
            photonView.RPC(nameof(ShowEmojiRPC), RpcTarget.All, idx, emojiLifeTime);
        }
    }

    // 모든 클라이언트에서 동일한 이모지를 동일한 시간 동안 표시
    [PunRPC]
    void ShowEmojiRPC(int idx, float life)
    {
        if (emojiImage == null) return;
        if (idx < 0 || idx >= emojis.Length) return;

        // 이모지 이미지 설정 및 타이머 초기화
        emojiImage.sprite = emojis[idx];
        emojiImage.enabled = true;
        emojiTimer = life;
    }
}