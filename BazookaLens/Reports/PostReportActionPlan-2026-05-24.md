# Bazooka Lens Post-Report Action Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 리포트에서 식별한 release-readiness gap을 첫 baseline commit 전에 작게 정리하고, 구조적 리스크는 이후 refactor 단위로 나눌 수 있게 만든다.

**Architecture:** 첫 단계는 behavior 변경이 작은 문서/검증/문구 정리로 제한한다. `PluginServices` 의존성 해소와 큰 클래스 분리는 baseline commit 이후 adapter seam 단위로 진행한다. restore wait 정책은 해상도 복구 안정성과 unload 응답성의 tradeoff를 명시하고, 바로 대규모 변경하지 않는다.

**Tech Stack:** C#/.NET 10, Dalamud plugin, xUnit, PowerShell, Git SSH alias `github-bf`.

---

## 실행 원칙

- baseline commit 전에는 동작 안정성을 흔드는 대규모 리팩터링을 하지 않는다.
- first commit 전 필수 범위는 README/사용법, command help/parser drift 방지, identity/remote preflight, 최종 테스트/빌드다.
- 로그 verbosity와 설정 UI 문구는 안전한 문구/상수 정리만 먼저 하고, runtime log volume 축소는 한 번 더 인게임 QA 후 진행한다.
- `PluginServices` DIP 개선은 별도 refactor branch 또는 baseline commit 이후 작업으로 분리한다.

## 파일 구조 계획

- Create: `README.md`
  - 사용자용 명령어, GUI, 단축키, region overlay, save path, ReShade caveat, 검증된 renderer path를 설명한다.
- Create: `BazookaLens.Tests/CommandRouterHelpTextTests.cs`
  - help text와 parser command 목록이 어긋나지 않는지 보호한다.
- Modify: `BazookaLens/Commands/CommandRouter.cs`
  - help text command 목록을 테스트 가능하게 노출하거나 command descriptor 형태로 최소 정리한다.
- Modify: `BazookaLens/Windows/BazookaLensWindow.cs`
  - 설정 UI 문구 중 release 전에 혼동 여지가 있는 label/help 문구를 상수화하거나 정리한다.
- Modify: `BazookaLens/Configuration.cs`
  - 기본값이 policy class에서 온다는 사실을 유지하고, README와 일치하는지 확인한다.
- Create: `tools/Verify-RepoIdentity.ps1`
  - local git identity, remote, HTTPS remote, 금지 문자열 스캔을 한 번에 확인한다.
- Optional Modify: `.gitignore`
  - root `docs/` ignore 정책을 유지할지, 문서 위치를 README 중심으로 가져갈지 결정한다. 이번 계획에서는 `README.md`를 root에 두므로 `.gitignore` 변경은 필수가 아니다.

---

### Task 1: README/사용법 작성

**Files:**
- Create: `README.md`

- [x] **Step 1: README 초안 작성**

포함할 항목:

- Bazooka Lens 목적
- `/blens shoot [scale]`
- `/blens open-folder`
- `/blens status`
- `/blens reshade-status`
- legacy/manual capture commands
- GUI Shoot
- scale preset/custom scale
- hotkey 설정 규칙
- save path 설정
- region overlay 조작
- right-click close
- ReShade enabled/disabled 동작
- DLAA/FSR 검증 완료, DLSS 동일 경로 가정
- known limitations: first capture after resize may need settle, runtime QA 필요

- [x] **Step 2: README와 현재 command help text 대조**

Run:

```powershell
rg -n "HelpText|shoot|open-folder|reshade-status|capture-region-scale" .\BazookaLens .\README.md -g "*.cs" -g "*.md"
```

Expected:

- README의 명령어 설명이 `CommandRouter.HelpText`와 모순되지 않는다.

- [x] **Step 3: 문서만 검증**

Run:

```powershell
git diff -- README.md
```

Expected:

- 명령어/GUI/단축키/영역/저장 경로 설명이 모두 들어 있다.

---

### Task 2: command help/parser drift 방지

**Files:**
- Modify: `BazookaLens/Commands/CommandRouter.cs`
- Create: `BazookaLens.Tests/CommandRouterHelpTextTests.cs`

- [x] **Step 1: failing test 작성**

테스트 의도:

- help text가 command spec에서 생성된다.
- command spec에 있는 sample invocation이 실제 parser에서 받아들여진다.
- usage fragment와 sample invocation이 함께 유지되어 option signature drift를 줄인다.

권장 구조:

```csharp
internal readonly record struct BlensCommandHelpEntry(
    string Command,
    string Usage,
    string SampleInvocation);
```

예시 test cases:

```csharp
[Theory]
[MemberData(nameof(HelpEntries))]
public void HelpEntrySampleParses(BlensCommandHelpEntry entry)
{
    Assert.Contains(entry.Usage, CommandRouter.HelpText, StringComparison.Ordinal);
    _ = CommandRouter.Parse(entry.SampleInvocation);
}
```

- [x] **Step 2: test fail/pass 확인**

Run:

```powershell
dotnet test .\BazookaLens.Tests\BazookaLens.Tests.csproj -c Debug --no-restore --filter CommandRouterHelpTextTests
```

Expected:

- 처음 작성 시 빠진 command가 있으면 fail.
- help text/parser 정리 후 pass.

- [x] **Step 3: 최소 구현**

권장:

- 첫 baseline 전에는 full command descriptor table까지 가지 않는다.
- 대신 `CommandRouter`에 `internal static readonly` help entry 목록을 두고, `HelpText`를 그 목록에서 생성한다.
- parser switch 자체를 생성형으로 바꾸는 refactor는 baseline 이후로 미룬다.

---

### Task 3: 설정 UI 문구와 기본값 정합성 정리

**Files:**
- Modify: `BazookaLens/Windows/BazookaLensWindow.cs`
- Modify: `BazookaLens/Configuration.cs`
- Optional Modify: `BazookaLens/UI/RegionSelectionDefaults.cs`
- Optional Modify: `BazookaLens/Capture/CaptureScalePolicy.cs`

- [x] **Step 1: UI label/text inventory 확인**

Run:

```powershell
rg -n 'ImGui\.Text|ImGui\.Button|ImGui\.Checkbox|ImGui\.Input|DrawSectionHeader|DrawScalePreset' .\BazookaLens\Windows\BazookaLensWindow.cs
```

Expected:

- 사용자에게 보이는 설정 UI 문구를 한 번에 확인할 수 있다.

- [x] **Step 2: README와 다른 표현 정리**

정리 기준:

- scale preset은 README와 동일하게 `1x`, `1.5x`, `2x`로 설명한다.
- custom scale은 소수점 2자리 제한을 문서/오류 문구와 맞춘다.
- region 기본값은 "현재 viewport 중앙 75%"로 통일한다.
- save path는 경로 표시 정책을 README와 맞춘다.

- [x] **Step 3: 기본값 source 재확인**

Run:

```powershell
rg -n "DefaultScale|MaxScale|DefaultViewportFraction|DefaultGridRows|DefaultGridColumns|Scale =" .\BazookaLens -g "*.cs"
```

Expected:

- 기본값이 `CaptureScalePolicy`, `RegionSelectionDefaults`, `GuideLayout` 쪽으로 모여 있다.

- [x] **Step 4: targeted tests 실행**

Run:

```powershell
dotnet test .\BazookaLens.Tests\BazookaLens.Tests.csproj -c Debug --no-restore --filter "CaptureScalePolicyTests|RegionSelectionStateTests|Configuration"
```

Expected:

- 관련 테스트 pass.

---

### Task 4: 로그 verbosity 정책 결정

**Files:**
- Review: `BazookaLens/Diagnostics/ReShadeEventBridgeLogPolicy.cs`
- Review: `BazookaLens/Diagnostics/ReShadeEventBridge.cs`
- Review: `BazookaLens/Capture/CaptureCoordinator.cs`
- Review: `BazookaLens/Diagnostics/ResizeProbeService.cs`
- Optional Modify: `BazookaLens/Diagnostics/ReShadeEventBridgeLogPolicy.cs`

- [x] **Step 1: 로그 call site inventory 확인**

Run:

```powershell
rg -n "Log\\.(Verbose|Debug|Information|Warning|Error)" .\BazookaLens -g "*.cs"
```

Expected:

- capture, ReShade, resize restore의 routine success log와 diagnostic log가 구분된다.

- [x] **Step 2: release 전 유지/축소 기준 결정**

판단 기준:

- ReShade/resize 안정화에 필요한 로그는 유지한다.
- 프레임마다 나올 수 있는 로그는 throttle policy를 통해서만 출력한다.
- 성공 path에서 같은 정보를 반복하는 로그는 다음 QA 이후 축소 후보로 표시한다.
- baseline 전 pass/fail 기준은 "새로운 high-frequency unthrottled log를 추가하지 않는다"로 둔다.

- [x] **Step 3: 이번 baseline에서는 code change 여부 결정**

권장:

- 이번 baseline 전에는 로그 축소 patch를 크게 하지 않는다.
- README의 "diagnostics may be verbose while ReShade support is stabilizing" 정도로 문서화한다.
- code change를 한다면 `ReShadeEventBridgeLogPolicy` 같은 policy 파일에 한정한다.

---

### Task 5: restore wait와 unload 정책 명시

**Files:**
- Review: `BazookaLens/Capture/CaptureCoordinator.cs`
- Review: `BazookaLens/Diagnostics/ResizeProbeService.cs`
- Optional Create: `BazookaLens/Capture/RestoreTimingPolicy.cs`
- Optional Test: `BazookaLens.Tests/RestoreTimingPolicyTests.cs`

- [x] **Step 1: 현재 restore wait 위치 확인**

Run:

```powershell
rg -n "CancellationToken\\.None|restoreWait|WaitForPresentationTargetAsync|WaitForTargetAsync" .\BazookaLens\Capture .\BazookaLens\Diagnostics -g "*.cs"
```

Expected:

- cancel 이후에도 restore를 우선하는 지점이 명확히 보인다.

- [x] **Step 2: baseline 정책 결정**

선택지:

- A. 현재처럼 restore 우선. 리포트/README에 unload가 restore wait를 기다릴 수 있음을 기록한다.
- B. bounded restore wait policy를 추가해 unload 지연 상한을 코드로 둔다.

권장:

- 첫 baseline은 A.
- 인게임 unload/reload QA에서 지연이 체감되면 B를 별도 작업으로 진행한다.
- baseline 전 pass/fail 기준은 "README와 리포트가 현재 restore 우선 정책을 명시하고, code behavior와 문서가 모순되지 않는다"로 둔다.

- [x] **Step 3: 별도 refactor ticket 기록**

기록할 내용:

- `RestoreTimingPolicy` 도입
- restore wait max ticks 분리
- restore wait 결과를 로그/상태로 노출
- adapter seam 이후 unit test 추가

---

### Task 6: Git identity/remote preflight 자동화

**Files:**
- Create: `tools/Verify-RepoIdentity.ps1`

- [x] **Step 1: 수동 점검 명령 확정**

Run:

```powershell
git config --local user.name
git config --local user.email
git remote -v
powershell -ExecutionPolicy Bypass -File .\tools\Verify-RepoIdentity.ps1
```

Expected:

- name: `bloooowfish`
- email: `285025450+bloooowfish@users.noreply.github.com`
- remote: `github-bf:bloooowfish/BazookaLens.git`
- identity scan: no matches

- [x] **Step 2: 반복 실행용 script 작성**

필수 동작:

- local `user.name`이 `bloooowfish`가 아니면 fail.
- local `user.email`이 `285025450+bloooowfish@users.noreply.github.com`가 아니면 fail.
- remote에 HTTPS URL이 있으면 fail.
- `origin`이 `github-bf:bloooowfish/BazookaLens.git`가 아니면 fail.
- 금지 문자열 scan에서 match가 나오면 fail.

- [x] **Step 3: script 검증**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Verify-RepoIdentity.ps1
```

Expected:

- exit code 0
- subaccount identity와 SSH alias remote 확인 출력

---

### Task 7: 최종 검증과 첫 baseline commit

**Files:**
- Stage only intentional source/docs files.

- [x] **Step 1: 전체 테스트 실행**

Run:

```powershell
dotnet test .\BazookaLens.Tests\BazookaLens.Tests.csproj -c Debug --no-restore
```

Expected:

- 0 failed
- 전체 테스트 통과

- [x] **Step 2: x64 Debug build 실행**

Run:

```powershell
dotnet build .\BazookaLens\BazookaLens.csproj -c Debug -p:Platform=x64 --no-restore
```

Expected:

- warning 0
- error 0

- [x] **Step 3: identity/remote 재확인**

Run:

```powershell
git config --local user.name
git config --local user.email
git remote -v
powershell -ExecutionPolicy Bypass -File .\tools\Verify-RepoIdentity.ps1
```

Expected:

- subaccount identity만 사용
- HTTPS remote 없음
- main account trace 없음

- [ ] **Step 4: stage 범위 확인**

Run:

```powershell
git status --short
git ls-files --others --exclude-standard
git add --dry-run -- .gitignore BazookaLens.sln README.md tools BazookaLens BazookaLens.Tests
git add -- .gitignore BazookaLens.sln README.md tools BazookaLens BazookaLens.Tests
git status --short
```

Expected:

- `bin/`, `obj/`, `.vs/`, ignored docs는 stage되지 않는다.
- README와 계획/리포트 포함 여부는 의도적으로 결정한다.
- `git add --dry-run` 출력 검토 후 실제 stage한다.

- [ ] **Step 5: 첫 commit**

Run:

```powershell
git commit -m "feat: add Bazooka Lens capture workflow"
```

Expected:

- author identity가 `bloooowfish <285025450+bloooowfish@users.noreply.github.com>`로 기록된다.

---

## Baseline 이후 refactor backlog

1. `ResizeProbeService` adapter seam 도입
2. `ViewportCaptureService` texture/readback adapter 도입
3. `CommandRouter` command descriptor table 도입
4. `ReShadeEventBridge` registration/dispatch/status/capture request 분리
5. routine success log 축소 및 explicit diagnostics mode 설계
6. plugin unload/reload integration QA 결과 기반 restore wait bounded policy 도입
