# Lang2Launcher

**랑그릿사 II (Windows판)를 Windows 10/11에서 편하게 실행하기 위한 런처**예요.

게임 볼륨 조절과 [cnc-ddraw](https://github.com/FunkyFr3sh/cnc-ddraw) 화면 설정을 창 하나에서 할 수 있어요.

> ⚠️ 이 저장소에는 **게임 파일이 포함되어 있지 않아요.** 게임은 직접 소유한 정품에서 준비해 주세요.

---

## 주요 기능

### 🔊 게임 볼륨만 따로 조절
- 원작에는 볼륨 조절 기능이 없어서, 런처가 Windows 볼륨 믹서의 **이 게임 항목만** 슬라이더 값으로 맞춰줘요.
- 게임을 시작할 때 자동으로 적용되고, **게임하는 중에도 슬라이더를 움직이면 바로 반영돼요.**
- 다른 프로그램 소리에는 영향을 주지 않아요.

### 🖥️ 화면 설정 (cnc-ddraw)
| 항목 | 설명 |
|---|---|
| 표시 모드 | 창 모드 / 전체화면(테두리 없음) / 전체화면 |
| 창 크기 | 1배(640×480) ~ 3배(1920×1440), 또는 사용자 지정 |
| 화질 필터 | **매끈하게 (xBRZ)** / **도트 보간 강하게 (xBR-lv3)** |
| 기타 | 4:3 비율 유지, 정수배로만 확대, 수직동기, 창 크기 직접 조절 |

- 설정은 `ddraw.ini`의 `[ddraw]` 섹션에서 **해당 항목만** 바꾸고, 나머지 설정은 건드리지 않아요.
- 화면 설정은 **다음에 게임을 시작할 때** 적용돼요.

### 기타
- 런처 설정(볼륨, 종료 옵션)은 `Lang2Launcher.ini`에 자동 저장돼요.
- "게임 종료 시 런처도 닫기" 옵션이 있어요.
- PowerShell이나 추가 런타임 없이 단독 실행 파일 하나로 동작해요. (.NET Framework 4.x는 Windows에 기본으로 들어 있어요.)

---

## 준비물

| 항목 | 받는 곳 | 비고 |
|---|---|---|
| 랑그릿사 II Windows판 게임 파일 | 직접 소유한 정품 | 실행 파일 이름이 `LANGPC2.EXE`여야 해요 |
| cnc-ddraw | [공식 릴리스](https://github.com/FunkyFr3sh/cnc-ddraw/releases) | `cnc-ddraw.zip` |
| Lang2Launcher | 이 저장소의 Releases 또는 직접 빌드 | |

---

## 설치 방법

1. **cnc-ddraw 넣기**
   [cnc-ddraw 릴리스](https://github.com/FunkyFr3sh/cnc-ddraw/releases)에서 `cnc-ddraw.zip`을 받아서, 아래 파일을 게임 폴더(`LANGPC2.EXE`가 있는 폴더)에 복사해 주세요.
   - `ddraw.dll`
   - `ddraw.ini`
   - `Shaders\xbrz\xbrz-freescale-multipass.glsl`
   - `Shaders\xbrz\xbrz-freescale-multipass.glsl.pass1`

2. **xBR-lv3 필터 넣기 (선택)**
   같은 zip 안의 `Shaders\shader-package.zip`을 열어서 `Shaders\xbr\xbr-lv3.glsl`을 게임 폴더의 같은 경로에 복사해 주세요.

3. **런처 넣기**
   `Lang2Launcher.exe`를 게임 폴더에 넣어 주세요.

4. **실행**
   `Lang2Launcher.exe`를 실행하고 **게임 시작**을 누르면 돼요.

### 설치 후 폴더 구성

```
게임 폴더/
├─ LANGPC2.EXE              ← 게임 (직접 준비)
├─ (그 외 게임 데이터 파일들)
├─ ddraw.dll                ← cnc-ddraw
├─ ddraw.ini                ← cnc-ddraw 설정 (런처가 편집)
├─ Shaders/
│  ├─ xbrz/xbrz-freescale-multipass.glsl
│  ├─ xbrz/xbrz-freescale-multipass.glsl.pass1
│  └─ xbr/xbr-lv3.glsl
├─ Lang2Launcher.exe        ← 이 런처
└─ Lang2Launcher.ini        ← 런처 설정 (자동 생성)
```

---

## 직접 빌드하기

Visual Studio가 없어도 돼요. Windows에 기본으로 들어 있는 .NET Framework 4.x 컴파일러(`csc.exe`)를 써요.

```bat
build.bat
```

빌드가 끝나면 `dist\Lang2Launcher.exe`가 만들어져요. `icon.ico`가 있으면 아이콘으로 들어가요.

---

## 동작 방식

- **볼륨 조절**: Windows Core Audio API(`IAudioSessionManager2`)로 기본 출력 장치의 오디오 세션 중에서 게임 프로세스(PID)의 세션을 찾고, `ISimpleAudioVolume`으로 볼륨을 설정해요. 게임이 사운드를 초기화하기 전에는 세션이 없어서, 런처가 0.3초 간격으로 확인하다가 세션이 생기면 적용해요.
- **화면 설정**: `ddraw.ini`의 `[ddraw]` 섹션에서 다음 키만 읽고 써요.
  `windowed`, `fullscreen`, `width`, `height`, `shader`, `renderer`, `d3d9_filter`, `maintas`, `boxing`, `vsync`, `resizable`
- xBRZ, xBR-lv3 필터는 OpenGL에서만 동작해서, 필터를 고르면 `renderer=opengl`로 같이 설정돼요.

---

## 문제 해결

**처음 실행할 때 "Windows의 PC 보호" 경고가 떠요**
서명되지 않은 프로그램이라 SmartScreen이 경고하는 거예요. **추가 정보 → 실행**을 누르면 돼요.

**필터를 바꿨는데 게임을 껐다 켜면 런처에서 필터가 다른 값으로 돌아가 있어요**
해당 셰이더 파일이 없거나, 그래픽 드라이버에서 그 셰이더를 지원하지 않는 경우예요. `Shaders` 폴더 경로가 맞는지 확인해 주세요.

**볼륨이 적용되지 않아요**
기본 출력 장치를 기준으로 동작해요. 게임 실행 중에 출력 장치를 바꿨다면 슬라이더를 한 번 움직여 주세요.

**호환성 모드를 켜야 하나요?**
아니요. Windows 95/98 호환 모드는 Windows 11에서 관리자 권한을 요구하고 부작용이 있어서, **호환성 모드 없이 실행하는 것을 권장**해요.

---

## 라이선스

- 이 런처의 소스코드는 [MIT 라이선스](LICENSE)를 따라요.
- **cnc-ddraw**는 FunkyFr3sh의 프로젝트이고 MIT 라이선스를 따라요. 이 저장소에는 포함되어 있지 않아요.
- **셰이더(xBRZ, xBR-lv3)** 는 [libretro glsl-shaders](https://github.com/libretro/glsl-shaders)에서 나온 것이고, 각 파일의 라이선스를 따라요. 이 저장소에는 포함되어 있지 않아요.

## 면책

- 이 프로젝트는 비공식 팬 제작 도구이고, 랑그릿사 시리즈의 저작권자나 판권사와 관련이 없어요.
- "랑그릿사(Langrisser)"를 비롯한 관련 명칭과 게임 내용의 권리는 각 권리자에게 있어요.
- 이 저장소는 게임 파일, 게임에서 추출한 리소스(이미지, 아이콘, 음악 등)를 포함하지 않으며, 앞으로도 포함하지 않아요.
