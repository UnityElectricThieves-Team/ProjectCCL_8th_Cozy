---
paths:
  - "Project_Cozy/Assets/Scripts/PerformanceSetting/Viewport/**/*.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/Viewport/**/*.cs"
  - "Project_Cozy/Assets/Scripts/Gameplay/Background/**/*.cs"
  - "Project_Cozy/Assets/Scripts/Contents/ShopSystem/Background*.cs"
---

# 월드 좌표 단위 규약

월드(뷰포트·배경·캐릭터 거주 영역)의 픽셀 값이 어떤 단위인지에 대한 규칙입니다. 구체 수치는 `ViewportScreenSettings`와 씬에서 확인하세요 — 여기에는 바뀌지 않는 것만 적습니다.

## 베이스 공간 px는 마스터 캔버스 기준 px입니다

코드에서 "베이스 공간 px"라고 부르는 값(뷰포트, 최소 뷰포트 크기, 배경 띠 높이, 편집 핸들의 히트 반경)은 작업 영역의 절대 픽셀이 아니라 **마스터 캔버스(기획의 제작 기준 좌표계) 기준 픽셀**입니다. 베이스 공간의 폭은 항상 마스터 캔버스 폭이고, 높이는 작업 영역의 종횡비를 따릅니다.

화면에서는 베이스 px 하나가 **작업 영역 폭 / 마스터 캔버스 폭** 화면 px로 보입니다. 4K 작업 영역에서만 1:1이고, 1080p에서는 절반입니다.

이렇게 정한 이유는 UI와 맞추기 위해서입니다. UI 캔버스는 CanvasScaler를 Scale With Screen Size, Match=Width로 쓰고 있어서 폭 비율로 줄어듭니다. 예전에는 월드가 작업 영역 절대 픽셀 1:1이라 4K에서만 UI와 월드가 일치했고, 1080p에서는 UI만 절반으로 줄고 월드는 그대로라 배경이 화면의 두 배 비율을 차지했습니다. 폭 기준을 택한 것도 같은 이유입니다 — 높이 기준이면 작업표시줄 유무에 따라 배율이 달라져 UI와 어긋납니다.

**단위 정의의 소유자는 `ViewportScreenSettings` 하나입니다.** 작업 영역을 베이스 공간 크기로 바꾸는 계산과 마스터 캔버스 폭 상수가 거기 있습니다. 이 상수는 씬의 UIRoot CanvasScaler 기준 해상도 폭과 같아야 합니다. 씬 값은 인스펙터에서 바뀌므로 코드로 강제하지 못하니, 둘 중 하나를 바꾸면 다른 쪽도 확인합니다.

## px에서 월드로는 BaseSpaceCameraFitter만 거칩니다

`BaseSpaceCameraFitter`는 앵커(마스터 캔버스 우하단의 월드 좌표)와 PPU로 베이스 px를 월드 길이로 바꿉니다. 이 식은 px가 어떤 단위인지 모릅니다. 그래서 단위를 바꿔도 이 클래스와 소비자들은 손대지 않았습니다.

새 소비자가 베이스 px 사각형을 월드로 옮길 때는 `BaseRectToWorld`를 쓰고, 길이 하나를 옮길 때는 `PixelsPerUnit`으로 나눕니다. 직접 `Screen.width`나 작업 영역 크기를 섞어 계산하지 않습니다 — 그 순간 단위가 둘이 됩니다.

**PPU와 앵커는 건드리지 않습니다.** PPU를 바꾸면 캐릭터 스프라이트 임포트 PPU, 프리팹의 발 정렬, 중력, 이동 속도까지 함께 환산해야 합니다. 앵커를 바꾸면 월드에 놓인 모든 것이 화면에서 옮겨집니다. 이번 단위 변경은 카메라의 orthoSize와 px 값의 뜻만 바꿨습니다.

## 저장 파일의 단위

뷰포트 저장 파일의 값도 베이스 공간 px입니다. 파일에 버전 필드가 있고, 버전이 없던 옛 파일은 작업 영역 절대 px로 저장된 것이라 불러올 때 현재 배율로 환산합니다. 옛 파일에는 저장 당시 해상도가 없으므로 다른 해상도에서 열면 크기가 달라질 수 있습니다. 같은 기기에서 이어 쓰는 경우는 정확히 복원됩니다.

파일 버전을 아는 곳은 `ViewportSaveBinder`뿐입니다. `ViewportScreenSettings`는 버전을 모르고 단위만 압니다 — 옛 단위 값은 "작업 영역 px" 입구로 받아 환산합니다. 정책 쪽에 영속화 지식이 새지 않게 하기 위해서입니다.
