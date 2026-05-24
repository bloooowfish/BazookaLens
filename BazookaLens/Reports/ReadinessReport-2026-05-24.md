# Bazooka Lens 준비 상태 점검 리포트

작성일: 2026-05-24

## 점검 범위

이번 점검은 scaled capture, ReShade post-effects capture, 설정 UI, 단축키 촬영, 영역 오버레이 작업 이후의 첫 커밋 전 상태를 기준으로 한다.

중점적으로 확인한 항목은 다음과 같다.

- SOLID 관점의 유지보수성, 특히 의존성 방향과 큰 클래스의 책임 분리 상태
- 불필요한 하드코딩, 중복 정책값, 숫자 상수의 위치
- 줄일 수 있는 코드 표면과 죽은 입력/헬퍼
- 캡처 생명주기, 취소, 플러그인 unload, 해상도 복구 동작
- ReShade 브릿지 및 resize settle 타이밍
- GUI/명령어/설정 간 동작 일관성
- 테스트와 수동 QA가 커버하지 못하는 영역
- `bloooowfish` 서브계정 기준 Git identity/remote 안전성

## 현재 구현 상태

기능 상태:

- 설정 기반 `shoot`, GUI Shoot, 단축키 Shoot 모두 full-frame/region capture를 지원한다.
- 스케일은 1x, 1.5x, 2x 프리셋과 custom scale 입력을 지원하며, 공통 scale policy로 정규화/검증한다.
- ReShade post-effects capture는 scaled resize settle 이후에 armed되므로, resize 전 stale texture를 저장하는 문제를 피한다.
- 영역 오버레이는 플러그인 윈도우와 분리되어 있고, 열려 있는 동안 클릭스루 없이 입력을 소유한다.
- 오버레이 앵커는 영역 내부 브라켓 형태이며, 우클릭으로 닫을 수 있고 guide/grid 표시를 지원한다.
- `Use Region`은 현재 viewport 기준 중앙 0.75배 영역으로 초기화한다.
- `Edit Overlay`는 기존 enabled region이 있으면 보존/clamp하고, 없으면 0.75배 중앙 영역을 만든다.
- GUI Shoot과 hotkey Shoot은 같은 plugin-level capture entrypoint와 unload cancellation token을 공유한다.

검증 기준:

- 테스트: `dotnet test .\BazookaLens.Tests\BazookaLens.Tests.csproj -c Debug --no-restore`
- 빌드: `dotnet build .\BazookaLens\BazookaLens.csproj -c Debug -p:Platform=x64 --no-restore`
- 테스트/빌드 결과는 리포트 작성 후 다시 실행해서 최종 답변에 별도로 기록한다.

Git identity 기준:

- local `user.name`: `bloooowfish`
- local `user.email`: `285025450+bloooowfish@users.noreply.github.com`
- remote: `github-bf:bloooowfish/BazookaLens.git`
- 본 계정명/본 계정 이메일/HTTPS remote는 커밋 또는 push 전에 다시 확인해야 한다.

## 점검 중 반영한 변경

생명주기와 취소:

- GUI Shoot과 hotkey capture를 같은 plugin capture path로 통합했다.
- GUI Shoot이 `CancellationToken.None` 대신 plugin unload token을 사용하도록 했다.
- `Dispose()` 진입 시 active capture가 새 UI hide transaction을 만들지 못하도록 unload 상태를 먼저 표시하고, unload token을 조기 cancel한다.
- 이미 cancel된 capture request가 capture 상태, region commit, UI suppress state를 변경하지 않도록 테스트와 구현을 정리했다.
- unload로 인한 command/capture cancellation은 사용자에게 실패 chat을 띄우지 않고 로그로만 남긴다.

캡처와 ReShade 타이밍:

- ReShade post-effects request를 resize settle 및 region preflight 이후에 arm하도록 정리했다.
- scaled capture timing 값을 `ScaledCaptureTimingPolicy`로 분리했다.
- operation token이 cancel되었거나 framework가 unloading 중이면 ReShade restore stabilization을 건너뛴다.
- 사용되지 않는 `CaptureNextPostEffectsAsync` 헬퍼를 제거했다.

Resize/restore 진단:

- `ResizeProbeService.ProbeAsync`에서 target operation 실패 후 restore도 실패했을 때, restore 실패가 원래 실패를 덮어쓰지 않도록 했다.
- `restore-display`에서 config read 실패 시 의미가 불분명한 hardcoded screen mode fallback을 제거했다.
- `restore-display`는 before/after screen mode와 requested mode 일치 여부를 로그로 남긴다.

UI와 영역 동작:

- overlay draw에서 매 프레임 persistent configuration을 초기화/clamp하지 않도록 했다.
- region overlay의 죽은 입력 파라미터와 사용되지 않는 anchor name surface를 제거했다.
- GUI/shortcut overlay의 숫자/색/사이즈 상수를 이름 있는 상수로 올렸다.
- grid row/column bounds를 `GuideLayout`으로 중앙화했다.
- capture scale default/max/range message를 `CaptureScalePolicy`로 중앙화했다.

테스트:

- cancel된 capture가 capture/UI 상태를 변경하지 않는 테스트를 추가했다.
- overlay 편집 진입 시 기존 region을 보존하거나 새 기본 region을 만드는 동작을 테스트했다.
- 중복되던 ReShade post-effects policy test case를 줄였다.

## 정적 스캔 결과

SOLID/DIP:

- `PluginServices.` 참조 수: production source 기준 187개
- 대표 위치:
  - `BazookaLens/Capture/CaptureCoordinator.cs`
  - `BazookaLens/Diagnostics/ResizeProbeService.cs`
  - `BazookaLens/Capture/ViewportCaptureService.cs`
  - `BazookaLens/Commands/CommandRouter.cs`
  - `BazookaLens/Diagnostics/ReShadeEventBridge.cs`

큰 파일:

- `BazookaLens/Diagnostics/ReShadeEventBridge.cs`: 759 lines
- `BazookaLens/Commands/CommandRouter.cs`: 442 lines
- `BazookaLens/Capture/CaptureCoordinator.cs`: 415 lines
- `BazookaLens/Capture/PngAlphaPostProcessor.cs`: 404 lines
- `BazookaLens/Windows/BazookaLensWindow.cs`: 373 lines
- `BazookaLens/Diagnostics/ResizeProbeService.cs`: 346 lines

숫자 상수:

- production source에서 남은 주요 숫자 정책값은 이름 있는 위치로 좁혀져 있다.
- `BazookaLens/Capture/CaptureScalePolicy.cs`: `MaxScale = 4.0`
- `BazookaLens/UI/RegionSelectionDefaults.cs`: `DefaultViewportFraction = 0.75`
- `BazookaLens/Windows/BazookaLensWindow.cs`: `"1.5x"` scale preset UI
- 이번 스캔 기준 `2560`, `1440`, `3840`, `2160`, `5120`, `2880`, `12000`, `5000` 같은 capture/해상도/timeout 숫자는 production source에 불필요하게 직접 박혀 있지 않다.

취소 토큰:

- production source의 `CancellationToken.None` 사용은 현재 restore path에 남아 있다.
- `CaptureCoordinator`와 `ResizeProbeService`의 restore wait는 capture cancellation 이후에도 해상도 복구를 최대한 수행하기 위한 의도적인 사용으로 판단했다.
- 다만 `BazookaLens/Capture/CaptureCoordinator.cs`의 scaled capture restore wait와 `BazookaLens/Diagnostics/ResizeProbeService.cs`의 resize-probe restore wait는 unload/cancel을 즉시 중단하지 않고 restore wait가 끝날 때까지 붙잡을 수 있다.
- 이 부분은 "무조건 제거" 대상이 아니라, restore를 우선할지 빠른 unload를 우선할지 정책을 명확히 정하고 bounded wait/adapter seam을 만든 뒤 테스트 가능하게 만드는 쪽이 맞다.

기타 흔적:

- `TODO`, `FIXME`, `HACK` 패턴은 production/test source에서 발견되지 않았다.

## 남은 리스크

### 1. `PluginServices` service locator 결합

우선순위: 중요

`PluginServices` 참조가 여전히 넓다. pure policy와 UI geometry 쪽은 테스트 가능하게 분리되었지만, orchestration class들은 아직 Dalamud global service에 직접 닿는다.

영향:

- DIP가 부분적으로만 만족된다.
- capture/unload/resize/ReShade path의 단위 테스트가 어렵다.
- 런타임 이슈를 재현하려면 인게임 smoke test 의존도가 높다.

판단:

- 첫 baseline commit 전 대규모 rewrite 대상은 아니다.
- 다만 다음 구조 개선의 1순위는 adapter 도입이다.

권장 순서:

1. framework tick/scheduler adapter
2. chat/log adapter
3. game config adapter
4. texture capture/readback adapter

가장 먼저 분리할 후보는 `ResizeProbeService` 또는 `ViewportCaptureService`다. 외부 의존성이 명확하고, 실패/복구 동작을 테스트할 가치가 높다.

### 2. 큰 오케스트레이션 클래스

우선순위: 중간

`ReShadeEventBridge`, `CommandRouter`, `CaptureCoordinator`, `ResizeProbeService`는 기능이 정상 동작하더라도 리뷰 비용과 회귀 위험이 커진다.

권장 분리 방향:

- `ReShadeEventBridge`
  - addon registration
  - event callback dispatch
  - post-effects capture request handling
  - status formatting
- `CommandRouter`
  - parsing은 유지
  - command execution handler를 나중에 분리
- `CaptureCoordinator`
  - resize orchestration
  - UI hide/restore transaction
  - post-effects capture orchestration
- `ResizeProbeService`
  - probe execution
  - display restore
  - game config snapshot/log formatting

`PngAlphaPostProcessor`는 크지만 책임이 비교적 명확하고 테스트가 있으므로 우선순위가 낮다.

### 3. 런타임 전용 통합 커버리지

우선순위: 중간

현재 테스트는 policy, parsing, geometry, shortcut validation, PNG 처리에 강하다. 반면 다음 영역은 실제 Dalamud/게임/ReShade 환경 의존성이 높다.

- active scaled capture 중 plugin unload/reload
- device resize 실패 후 restore
- live ReShade addon registration/unregistration
- texture readback save와 alpha post-processing의 실제 service 경로
- `/blens` command execution side effect 전체

권장:

- 다음 QA pass에서 unload/reload 중 capture를 명시적으로 테스트한다.
- ReShade disabled, enabled, preset reload, timeout fallback을 각각 한 번씩 확인한다.
- adapter seam을 만들기 전에는 이 영역을 억지로 unit test로 밀어 넣지 않는다.

### 4. 로그 볼륨

우선순위: 낮음에서 중간

ReShade event와 module resolver log는 throttle 정책이 있지만, capture 성공/복구/상태 로그는 여전히 상세하다.

현재 판단:

- 지금은 ReShade/resize 안정화 단계라 verbose log가 유용하다.
- 첫 릴리즈 전에는 일상 성공 로그를 줄이고, 상세 snapshot은 status/debug command에서 보도록 옮길 수 있다.

### 5. 사용자 문서

우선순위: 낮음, 단 push 전에는 권장

tracked README 또는 usage 문서가 아직 없다. root `docs/`는 `.gitignore`에 의해 무시되므로, 문서를 커밋하려면 README를 root에 두거나 ignore 정책을 바꿔야 한다.

README에 들어갈 최소 항목:

- `/blens shoot [scale]`
- GUI Shoot
- hotkey 설정
- region overlay 조작법
- save path 동작
- ReShade enabled/disabled caveat
- DLAA/FSR 검증 완료, DLSS는 동일 경로로 동작한다고 보는 현재 가정

### 6. 명령어 help/parser drift

우선순위: 낮음에서 중간, publish 전에는 권장

`CommandRouter.HelpText`와 실제 parser switch가 같은 파일 안에 있지만 별도의 authoritative source로 유지된다. 지금은 눈으로 맞춰져 있으나, command가 늘어나면 help text와 parser behavior가 어긋날 수 있다.

영향:

- 사용자가 `/blens help` 기준으로 잘못된 명령어를 실행할 수 있다.
- command parser 테스트가 help text와 parser의 일치까지 보장하지 않는다.

권장:

- 첫 publish 전에는 최소한 help text/parser smoke test를 추가한다.
- 장기적으로는 command descriptor table에서 help text와 parser routing을 같이 생성하는 구조가 낫다.

### 7. CI/릴리즈 파이프라인 부재

우선순위: 낮음에서 중간

현재 검증은 로컬 `dotnet test`와 x64 Debug build에 의존한다. repo root에 `.github` workflow가 없어 remote publish 이후에도 같은 검증이 자동으로 반복되지는 않는다.

권장:

- 공개 repo로 push하기 전, 최소한 build/test workflow를 추가할지 결정한다.
- 당장은 private/local baseline commit이면 필수는 아니지만, remote 협업 또는 release artifact를 만들기 시작하면 자동화해야 한다.

## 첫 커밋 전 체크리스트

- 테스트와 x64 Debug build 재실행
- 본 계정명/본 계정 이메일/HTTPS remote 재스캔
- local git identity가 `bloooowfish`/noreply인지 확인
- `origin`이 `github-bf:bloooowfish/BazookaLens.git`인지 확인
- 이 리포트를 `BazookaLens/Reports/`에 커밋할지, 로컬 작업 문서로 둘지 결정
- remote publish 전 README 추가 여부 결정
- `CommandRouter.HelpText`와 parser command 목록이 어긋나지 않는지 확인
- restore wait가 unload를 늦출 수 있다는 점을 baseline commit에서 받아들일지 결정

## 다음 QA 체크리스트

- full-frame 1x, 1.5x, 2x
- region 1.5x, 2x
- GUI Shoot
- hotkey Shoot
- overlay edit, anchor drag, move, right-click close
- save path apply/default/open folder
- ReShade enabled/disabled
- plugin reload/unload after idle
- plugin reload/unload 직후 또는 active capture 중 동작
- 출력 이미지의 focus settle, ReShade effect, region bounds, alpha correctness

## 결론

현재 상태는 첫 local baseline commit 후보로 볼 수 있다. 다만 구조적으로 가장 큰 빚은 `PluginServices`에 대한 넓은 의존성이고, 다음 단계에서 adapter seam을 작게 도입해 capture/resize/ReShade orchestration을 테스트 가능하게 만들어야 한다.

지금 당장 큰 rewrite를 진행하는 것보다는 다음 순서가 낫다.

1. 테스트/build/identity 재검증
2. 최소 README 작성 여부 결정
3. 첫 baseline commit
4. 인게임 QA pass
5. `ResizeProbeService` 또는 `ViewportCaptureService`부터 adapter 기반 리팩터링
