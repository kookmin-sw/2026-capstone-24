# XR 전용 지침

> 이 파일은 XROrigin, XR Interaction Toolkit, OpenXR, XR Hands 관련 작업(이동, 입력, 손 상호작용, 컨트롤러 동작 등) 시에만 읽으세요.

- 플레이어 리그는 `XROrigin`과 XR Interaction Toolkit provider를 기반으로 한다고 가정합니다.
- 이동, 입력, 손 상호작용, 컨트롤러 동작을 변경할 때는 XR 리그와 interaction manager의 참조 상태를 검증합니다.
- 커스텀 폴링 로직보다 Input System 액션과 기존 XRI 컴포넌트를 우선 사용합니다.
- OpenXR 및 XR Hands 샘플 오브젝트는 바인딩이 빠지면 컴파일 타임이 아니라 런타임에 실패하는 경우가 많으므로 주의합니다.
- XRI 컴포넌트를 추가하기 전에 패키지 코드나 기존 직렬화 데이터를 확인하여 `RequireComponent` 관계로 인한 중복 컴포넌트를 피합니다.
- XR Interaction Toolkit Starter Assets 샘플에서 XRI locomotion provider를 연결할 때는 샘플 프리셋이 요청된 조작 체계와 일치한다고 가정하지 않습니다. 좌우 액션 슬롯을 명시적으로 검증하고 사용하지 않는 손 바인딩은 정리합니다.
- 이동/입력 변경 시에는 실제 패키지 스크립트나 프리셋을 확인하여 직렬화 필드 이름, 기대되는 액션 ID, 참조가 런타임 추론인지 직렬화 기반인지 구분합니다. 직렬화 기반 참조는 프리셋·프리팹에서, 런타임 추론 참조는 코드에서 수정합니다.
