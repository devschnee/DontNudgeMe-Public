<div align="center">

# 🎮 Don't Nudge Me
### 실시간 멀티플레이 캐주얼 배틀 로얄 게임

<a href="https://youtu.be/VlyXmGQhBaM">
  <img width="100" height="100" alt="Youtube_logo"
    src="https://github.com/user-attachments/assets/2aa6f449-7ffa-4dd2-9086-232f5499456f" />
</a>

<br>
<a href="https://www.notion.so/2e8eddbae78d80128221c47e6ab6edd1?source=copy_link">기술문서 PDF 다운로드</a>
<br><br>

<table>
  <tr>
    <td align="center" width="33%">
      <img alt="movement" src="https://github.com/user-attachments/assets/45fa8394-22ff-4e06-a278-b4ecfb62ddd4" />
      <br/>
      <b>플레이어 이동</b>
    </td>
    <td align="center" width="33%">
      <img alt="nudge" src="https://github.com/user-attachments/assets/fc229762-c4d2-4935-8104-17ea70c8a1d3" />
      <br/>
      <b>넛지 상호작용</b>
    </td>
    <td align="center" width="33%">
      <img alt="slide" src="https://github.com/user-attachments/assets/a4332dfe-c27f-4f9b-8821-ac44ff96ca34" />
      <br/>
      <b>슬라이딩 기믹</b>
    </td>
  </tr>
</table>

<br>

**Don’t Nudge Me**는  
플레이어 간 밀치기(Nudge)를 중심으로 한  
**실시간 멀티플레이 캐주얼 배틀 로얄 게임**입니다.

플레이어 이동과 상호작용의 물리적 재미에 집중하여,  
짧은 플레이 타임 안에서도 긴장감 있는 경쟁을 제공합니다.

</div>

<br><br><br>

---

## 📋 목차

- [기술 스택](#tech-stack)
- [게임 소개](#overview)
- [시스템 아키텍처](#architecture)
- [주요 구현 시스템](#systems)
  - [플레이어 이동](#player-move)
  - [대시](#dash)
  - [넛지 시스템](#nudge)
  - [슬라이딩](#sliding)
  - [이모트](#emote)
  - [커스터마이징](#customizing)
- [개발자](#developer)

<br><br>

---

<a id="tech-stack"></a>
## 🧰 기술 스택

- Engine: Unity 2022
- Language: C#
- Network: Photon PUN2
- Tool: GitHub, Unity Editor

<br><br>

---
## 🤲 시스템 아키텍처
#### 📂 Source Entry
- [`/Assets/_Proj/Scripts`](https://github.com/devschnee/DontNudgeMe-Public/blob/main/Assets/_Project/_Scripts)

### 플레이어 아키텍처

<br>
<table>
  <tr>
    <td width="49%" align="center">
      <a href="https://github.com/user-attachments/assets/40cb94a5-1708-461f-a80d-50695395b18a"><img alt="player sturcture" src="https://github.com/user-attachments/assets/40cb94a5-1708-461f-a80d-50695395b18a" width="100%" /></a>
      <br/><br/><b>플레이어 구조</b>
    </td>
    <!-- 세로 구분선 -->
    <td width="2%" align="center">
      <div style="width:1px; height:100%; background-color:#cccccc;"></div>
    </td>
    <td width="49%" align="center">
      <a href="https://github.com/user-attachments/assets/b760bf7b-96d1-438f-b89d-77a1b155c9fb"><img alt="player flow" src="https://github.com/user-attachments/assets/b760bf7b-96d1-438f-b89d-77a1b155c9fb" width="100%" /></a>
      <br/><b>플레이어 흐름</b>
    </td>
  </tr>
</table>
<br>

### 커스터마이징 구조
<div align="center"><a href="https://github.com/user-attachments/assets/02547863-6c92-4ba5-8148-e9d235fed075"><img width="60%" alt="custom structure" src="https://github.com/user-attachments/assets/02547863-6c92-4ba5-8148-e9d235fed075" /></a></div>

<br><br>

---

<a id="overview"></a>
## 🎯 게임 소개

- 장르: 실시간 멀티플레이 캐주얼 배틀 로얄
- 플랫폼: PC
- 개발 엔진: Unity 2022
- 개발 기간: 2025.09.22 ~ 2025.10.13
- 개발 인원: 팀 프로젝트 (5명)

> 본 README는 팀 프로젝트 중  
> **제가 담당한 플레이어 및 상호작용 시스템 중심으로 정리**되어 있습니다.

<br><br>

---

<a id="systems"></a>
## 💻 주요 구현 시스템

<a id="player-move"></a>
### 🕹️ 플레이어 이동

- [`PlayerController`](https://github.com/devschnee/DontNudgeMe-Public/blob/main/Assets/_Project/_Scripts/Player/PlayerController.cs)

- Rigidbody 기반 이동 구조
- Update / FixedUpdate 분리로 입력과 물리 처리 안정화
- 카메라 기준 방향 이동으로 조작 일관성 확보

<br>

---

<a id="dash"></a>
### 🤜 대시

- [`PlayerDash`](https://github.com/devschnee/DontNudgeMe-Public/blob/main/Assets/_Project/_Scripts/Player/PlayerDash.cs)

- 연속 입력을 감지하여 짧은 시간 동안 순간 가속 이동
- 이동 스킬 특성상 트리거 이벤트 중심으로 처리하여 네트워크 부하 최소화

<br>

---

<a id="nudge"></a>
### 🤜 넛지 시스템

- [`PlayerNudge`](https://github.com/devschnee/DontNudgeMe-Public/blob/main/Assets/_Project/_Scripts/Player/PlayerNudge.cs)

- 근접 범위 내 플레이어 감지 후 물리적 힘 적용
- Photon RPC 기반으로 넛지 이벤트 동기화
- 실제 힘 적용과 입력 차단은 피격 대상 Owner 클라이언트 기준으로 처리
- 입력 차단 시간 적용으로 연속 밀치기 방지

<br>

---

<a id="sliding"></a>
### 🛝 슬라이딩

- [`PlayerSliding`](https://github.com/devschnee/DontNudgeMe-Public/blob/main/Assets/_Project/_Scripts/Player/PlayerSliding.cs)

- 트리거 진입 시 슬라이딩 상태로 전환
- 슬라이딩 중 PlayerController 이동/점프 제어를 차단하여 상태 충돌 방지
- 레일 경로(waypoint) 기반 이동 및 회전 보간으로 연출 안정성 확보

<br>

---

<a id="emote"></a>
### 😀 이모트

- [`PlayerQuickEmoji`](https://github.com/devschnee/DontNudgeMe-Public/blob/main/Assets/_Project/_Scripts/Player/PlayerQuickEmoji.cs)

- 간단한 입력으로 감정을 표현하는 비언어적 커뮤니케이션
- RPC 이벤트로 모든 클라이언트에 동일하게 표시
- 표시 시간 제한을 두어 시각적 혼잡 방지

<br>

---

<a id="customizing"></a>
### 🎨 커스터마이징

- [`CharacterCustom`](https://github.com/devschnee/DontNudgeMe-Public/blob/main/Assets/_Project/_Scripts/Player/CharacterCustom.cs)
- [`CustomizeSelectPanel`](https://github.com/devschnee/DontNudgeMe-Public/blob/main/Assets/_Project/_Scripts/Player/CustomizeSelectPanel.cs)

- 로비에서 캐릭터 외형을 설정하고 즉시 미리보기 적용
- CustomizationData 기반으로 외형 데이터를 구조화하여 관리
- 저장 시 Photon Custom Properties로 변환하여 멀티플레이 환경에 반영
- 외형 표현 중심의 보조 시스템

<br><br>

---

<a id="developer"></a>
## 👨‍💻 개발자
<div align="center">

**김현지**

<br>

<a href="https://github.com/devschnee">
  <img src="https://img.shields.io/badge/devschnee-blue?style=for-the-badge&logo=GitHub&logoColor=ffffff&label=GitHub&labelColor=Black"/>
</a>

</div>
