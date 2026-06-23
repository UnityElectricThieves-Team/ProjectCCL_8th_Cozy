# ⚠️ DEPRECATED — 사용 금지

이 폴더(`TestCharacter/`)와 `CharacterAnimation.controller`는 초기 **4-state 실험 자산**입니다.

develop-kk의 캐릭터 애니메이션은 다음 구조로 대체되었습니다:
- `Animations/AnimationSystem/BaseCharacterAnimatorController` — 13-state 중립 베이스
- `White_Cat_Override` / `White_Girl_Override` — 폼(동물/소녀)별 AnimatorOverrideController

주의:
- **새 작업에 사용하지 마세요.**
- 현재 옛 테스트 씬(TestHyeonScene / PerformanceSystemScene)과 `Prefabs/Character_kkukka.prefab` 만 참조 중입니다.
- 해당 참조까지 정리되면 이 폴더째 삭제 예정.
