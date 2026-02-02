using Firebase;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 커스터마이징 UI 패널을 제어하는 클래스.
/// - 파츠 선택(Head / Body / Shoes)
/// - 색상(Hue) 실시간 미리보기
/// - Firebase 저장 및 Photon Custom Properties 동기화
/// </summary>

public class CustomizeSelectPanel : MonoBehaviour
{
    public CharacterCustom customizer; // 실제 커스터마이징 로직을 담당하는 객체
    public CustomizePanelController panelController;

    [Header("Hue Sliders")]
    public Slider head, body, shoes;

    // ===== 파츠 변경 =====
    public void NextHead() => customizer.Next(ItemCategory.Head);
    public void PrevHead() => customizer.Prev(ItemCategory.Head);
    public void NextBody() => customizer.Next(ItemCategory.Body);
    public void PrevBody() => customizer.Prev(ItemCategory.Body);
    public void NextShoes() => customizer.Next(ItemCategory.Shoes);
    public void PrevShoes() => customizer.Prev(ItemCategory.Shoes);

    // Color 실시간 미리보기 (저장 전까지 로컬만 반영)
    public void OnHeadColorChanged()
    {
        Color c = HueToColor(head.value);
        customizer.SetColor(ItemCategory.Head, c, false); 
        UpdateSliderHandleColor(head, c);
    }

    public void OnBodyColorChanged()
    {
        Color c = HueToColor(body.value);
        customizer.SetColor(ItemCategory.Body, c, false);
        UpdateSliderHandleColor(body, c);
    }

    public void OnShoesColorChanged()
    {
        Color c = HueToColor(shoes.value);
        customizer.SetColor(ItemCategory.Shoes, c, false);
        UpdateSliderHandleColor(shoes, c);
    }

    public async void OnConfirmCustomization()
    {
        // Firebase 저장 + Photon 전송
        try
        {
            // Firebase에 커스터마이징 데이터 저장
            await customizer.SaveToFirebase();
            
            // 저장 과정에서 CustomizationData.Local은 이미 최신 상태
            // 해당 데이터를 Photon Custom Properties로 전파
            PhotonNetwork.SetPlayerCustomProperties(CustomizationData.Local.ToPhoton());
           
            panelController.ClosePanel();
        }
        catch (FirebaseException fe)
        {
        }

        
    }

    public void OnCloseCustomization()
    {
        ResetSlidersToDefault();
        if (customizer != null) customizer.ResetCustomization();
    }

    public void Open()
    {
        // 항상 리셋하고 열기
        ResetSlidersToDefault();
        if (customizer != null) customizer.ResetCustomization();

        gameObject.SetActive(true);
    }

    void OnDisable()
    {
        // 만약 Confirm으로 저장 안 하고 그냥 닫힌 경우 → 원래대로
        ResetSlidersToDefault();
        if (customizer != null) customizer.ResetCustomization();
    }

    Color HueToColor(float v)
    {
        return Color.HSVToRGB(v, 1f, 1f); 
    }

    void UpdateSliderHandleColor(Slider slider, Color color)
    {
        if (slider.handleRect != null)
        {
            var handleImage = slider.handleRect.GetComponent<Image>();
            if (handleImage != null)
                handleImage.color = color; // 핸들 색 = 현재 선택 색상
        }
    }

    // 모든 슬라이더를 기본 상태로 되돌림
    void ResetSlidersToDefault()
    {
        if (head) head.SetValueWithoutNotify(0f);
        if (body) body.SetValueWithoutNotify(0f);
        if (shoes) shoes.SetValueWithoutNotify(0f);

        UpdateSliderHandleColor(head, Color.white);
        UpdateSliderHandleColor(body, Color.white);
        UpdateSliderHandleColor(shoes, Color.white);
    }
}
