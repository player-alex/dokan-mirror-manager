# ShellViewModel.cs 리팩토링 작업 계획

## 작업 개요
- **현재 상태**: ShellViewModel.cs (1,420줄, 59KB)
- **목표**: 5개의 서비스 클래스로 분리하여 유지보수성 및 테스트 가능성 향상
- **예상 소요 시간**: 각 Phase당 약 30-60분

---

## Phase 0: 준비 작업 (사전 검증)
- [x] 0.1. 현재 브랜치 확인 및 새 브랜치 생성 (`refactor/split-shellviewmodel`) - 사용자가 수동 완료
- [x] 0.2. 현재 프로젝트 빌드 성공 확인 - 사용자가 수동 완료
- [x] 0.3. Services 디렉토리 생성
- [x] 0.4. Interfaces 디렉토리 생성 (Services/Interfaces)
- [ ] 0.5. 백업 커밋 생성 (리팩토링 시작 전) - 사용자가 커밋 관리

**예상 컨텍스트 사용량**: 10%

---

## Phase 1: ConfigurationService 분리 (가장 독립적) ✅ **완료**
### 1.1. 인터페이스 정의
- [x] 1.1.1. `IConfigurationService.cs` 인터페이스 작성
  - `Task<List<MountItem>> LoadConfigurationAsync()`
  - `Task SaveConfigurationAsync(IEnumerable<MountItem> items)`

### 1.2. 구현 클래스 작성
- [x] 1.2.1. `ConfigurationService.cs` 클래스 작성
- [x] 1.2.2. ShellViewModel.cs에서 다음 코드 추출:
  - `LoadConfiguration()` 메서드 (라인 1257-1348)
  - `SaveConfigurationAsync()` 메서드 (라인 1351-1377)
  - `MountItemDto` 내부 클래스 (라인 1413-1419)
- [x] 1.2.3. 환경 변수 확장 로직 포함

### 1.3. ShellViewModel 통합
- [x] 1.3.1. ShellViewModel에 `IConfigurationService` 의존성 주입
- [x] 1.3.2. 생성자에서 서비스 초기화
- [x] 1.3.3. 기존 메서드 호출을 서비스 호출로 변경
- [x] 1.3.4. 빌드 및 동작 확인
- [ ] 1.3.5. 커밋: `refactor: Extract ConfigurationService from ShellViewModel` - 사용자가 커밋 관리

**실제 코드량**:
- ConfigurationService.cs: 103줄
- MountItemDto.cs: 12줄
- IConfigurationService.cs: 20줄
- ShellViewModel.cs 감소: ~100줄

**실제 컨텍스트 사용량**: 85% (171k/200k)

---

## Phase 2: DriveLetterManager 분리 ✅ **완료**
### 2.1. 인터페이스 정의
- [x] 2.1.1. `IDriveLetterManager.cs` 인터페이스 작성
  - `List<string> GetAvailableDriveLetters(IEnumerable<MountItem> items, MountItem? currentItem)`
  - `string? AutoSelectDriveLetter(MountItem item, List<string> availableLetters)`
  - `void UpdateAllDriveLetters(ObservableCollection<MountItem> items)`

### 2.2. 구현 클래스 작성
- [x] 2.2.1. `DriveLetterManager.cs` 클래스 작성
- [x] 2.2.2. ShellViewModel.cs에서 다음 코드 추출:
  - `AvailableDriveLetters` 프로퍼티 로직 (라인 79-99)
  - `UpdateAllAvailableDriveLetters()` 메서드 (라인 1202-1255)
  - `AutoSelectDriveLetter()` 메서드 (라인 1186-1200)
  - `_isUpdatingDriveLetters` 플래그 제거

### 2.3. ShellViewModel 통합
- [x] 2.3.1. ShellViewModel에 `IDriveLetterManager` 의존성 주입
- [x] 2.3.2. MountItem PropertyChanged 이벤트 핸들러 수정
- [x] 2.3.3. App.xaml.cs에 서비스 등록
- [x] 2.3.4. 빌드 및 동작 확인
- [ ] 2.3.5. 커밋: `refactor: Extract DriveLetterManager from ShellViewModel` - 사용자가 커밋 관리

**실제 코드량**:
- IDriveLetterManager.cs: 32줄
- DriveLetterManager.cs: 161줄
- ShellViewModel.cs 감소: ~140줄

**실제 컨텍스트 사용량**: 66% (132k/200k)

---

## Phase 3: TrayIconManager 분리
### 3.1. 인터페이스 정의
- [ ] 3.1.1. `ITrayIconManager.cs` 인터페이스 작성
  - `void Initialize(Window window, Action showWindowAction, Func<Task> exitAction)`
  - `void ShowWindow()`
  - `void HideWindow()`
  - `void ShowBalloonTip(string title, string message, BalloonIcon icon)`
  - `void Dispose()`

### 3.2. 구현 클래스 작성
- [ ] 3.2.1. `TrayIconManager.cs` 클래스 작성
- [ ] 3.2.2. ShellViewModel.cs에서 다음 코드 추출:
  - `InitializeTaskbarIcon()` 메서드 (라인 144-190)
  - `ShowWindow()` 메서드 (라인 205-214)
  - `HideWindow()` 메서드 (라인 216-233)
  - `ExitMenuItem_Click()` 메서드 (라인 235-277)
  - `_taskbarIcon` 필드 및 관련 플래그들

### 3.3. ShellViewModel 통합
- [ ] 3.3.1. ShellViewModel에 `ITrayIconManager` 의존성 주입
- [ ] 3.3.2. OnViewLoaded에서 서비스 초기화 호출
- [ ] 3.3.3. 빌드 및 동작 확인
- [ ] 3.3.4. 커밋: `refactor: Extract TrayIconManager from ShellViewModel`

**예상 코드량**:
- TrayIconManager.cs: ~250줄
- ShellViewModel.cs 감소: ~200줄

**예상 컨텍스트 사용량**: 20%

---

## Phase 4: MountMonitoringService 분리
### 4.1. 인터페이스 정의
- [ ] 4.1.1. `IMountMonitoringService.cs` 인터페이스 작성
  - `Task StartMonitoringAsync(DokanInstance instance, MountItem item, string driveLetter, Action<MountItem> onUnmountDetected)`
  - `void CancelMonitoring(MountItem item)`
  - `void CancelAllMonitoring()`

### 4.2. 구현 클래스 작성
- [ ] 4.2.1. `MountMonitoringService.cs` 클래스 작성
- [ ] 4.2.2. ShellViewModel.cs에서 다음 코드 추출:
  - `MonitorFileSystemClosure()` 메서드 (라인 762-860)
  - `_monitoringTokens` ConcurrentDictionary 및 관련 로직

### 4.3. ShellViewModel 통합
- [ ] 4.3.1. ShellViewModel에 `IMountMonitoringService` 의존성 주입
- [ ] 4.3.2. MountInternal 메서드에서 모니터링 시작 호출 수정
- [ ] 4.3.3. Unmount 메서드에서 모니터링 취소 호출 수정
- [ ] 4.3.4. 빌드 및 동작 확인
- [ ] 4.3.5. 커밋: `refactor: Extract MountMonitoringService from ShellViewModel`

**예상 코드량**:
- MountMonitoringService.cs: ~180줄
- ShellViewModel.cs 감소: ~120줄

**예상 컨텍스트 사용량**: 15%

---

## Phase 5: MountService 분리 (가장 복잡)
### 5.1. 인터페이스 정의
- [ ] 5.1.1. `IMountService.cs` 인터페이스 작성
  - `Task<MountResult> MountAsync(MountItem item, bool isAutoMount)`
  - `Task<UnmountResult> UnmountAsync(MountItem item)`
- [ ] 5.1.2. `MountResult`, `UnmountResult` DTO 클래스 작성

### 5.2. 구현 클래스 작성 (Part 1: Mount)
- [ ] 5.2.1. `MountService.cs` 클래스 기본 구조 작성
- [ ] 5.2.2. ShellViewModel.cs에서 Mount 관련 코드 추출:
  - `MountInternal()` 메서드 (라인 383-759)
  - `_mountLocks` ConcurrentDictionary 및 관련 로직
  - 타임아웃 처리 로직

### 5.3. 구현 클래스 작성 (Part 2: Unmount)
- [ ] 5.3.1. ShellViewModel.cs에서 Unmount 관련 코드 추출:
  - `Unmount()` 메서드 (라인 862-1136)
  - 타임아웃 및 프로세스 감지 로직

### 5.4. ShellViewModel 통합
- [ ] 5.4.1. ShellViewModel에 `IMountService` 의존성 주입
- [ ] 5.4.2. Mount/Unmount 커맨드 메서드를 서비스 호출로 변경
- [ ] 5.4.3. StatusMessage 업데이트 로직을 이벤트 기반으로 변경
- [ ] 5.4.4. 빌드 및 동작 확인
- [ ] 5.4.5. 커밋: `refactor: Extract MountService from ShellViewModel`

**예상 코드량**:
- MountService.cs: ~500줄
- ShellViewModel.cs 감소: ~650줄

**예상 컨텍스트 사용량**: 25%

---

## Phase 6: 의존성 주입 설정 및 최종 정리
### 6.1. Bootstrapper 설정
- [ ] 6.1.1. App.xaml.cs에서 Caliburn.Micro Bootstrapper 확인
- [ ] 6.1.2. IoC Container에 서비스 등록
  - `container.Singleton<IConfigurationService, ConfigurationService>()`
  - `container.Singleton<IDriveLetterManager, DriveLetterManager>()`
  - `container.Singleton<ITrayIconManager, TrayIconManager>()`
  - `container.Singleton<IMountMonitoringService, MountMonitoringService>()`
  - `container.Singleton<IMountService, MountService>()`

### 6.2. ShellViewModel 최종 정리
- [ ] 6.2.1. 사용하지 않는 using 문 제거
- [ ] 6.2.2. 코드 포맷팅 정리
- [ ] 6.2.3. 주석 정리 및 문서화

### 6.3. 최종 검증
- [ ] 6.3.1. 전체 프로젝트 빌드 성공 확인
- [ ] 6.3.2. 애플리케이션 실행 및 기능 테스트
  - AddMount 테스트
  - Mount/Unmount 테스트
  - AutoMount 테스트
  - 드라이브 레터 자동 선택 테스트
  - 트레이 아이콘 테스트
  - 설정 저장/로드 테스트
- [ ] 6.3.3. 최종 커밋: `refactor: Complete ShellViewModel refactoring with DI setup`

**예상 컨텍스트 사용량**: 15%

---

## Phase 7: 문서화 및 마무리
- [ ] 7.1. REFACTORING.md 작성 (변경 사항 문서화)
- [ ] 7.2. 각 서비스 클래스에 XML 문서 주석 추가
- [ ] 7.3. README.md 업데이트 (아키텍처 섹션)
- [ ] 7.4. Pull Request 생성 (mount-stablization → main)
- [ ] 7.5. 커밋: `docs: Add refactoring documentation`

**예상 컨텍스트 사용량**: 10%

---

## 총 예상 컨텍스트 사용량
- **Phase 0**: 10%
- **Phase 1**: 15%
- **Phase 2**: 15%
- **Phase 3**: 20%
- **Phase 4**: 15%
- **Phase 5**: 25%
- **Phase 6**: 15%
- **Phase 7**: 10%
- **총합**: 125% (각 Phase마다 새 세션 권장)

---

## 컨텍스트 관리 전략
1. **각 Phase 완료 후 사용자 확인**: 진행 여부 질문
2. **Phase별 커밋**: 문제 발생 시 롤백 가능
3. **긴 Phase는 2개 세션으로 분할** (Phase 5를 5.2와 5.3으로 분할)
4. **각 세션 시작 시 이전 작업 요약 확인**

---

## 위험 관리
- **위험**: 리팩토링 중 기능 손실
- **완화**: 각 Phase마다 빌드 및 간단한 기능 테스트
- **위험**: 컨텍스트 오버
- **완화**: Phase별 진행, 각 Phase 완료 후 사용자 확인

---

## 현재 진행 상태
- [x] **Phase 0 완료** (디렉토리 구조 준비)
- [x] **Phase 1 완료** (ConfigurationService 분리 및 통합)
- [x] **Phase 2 완료** (DriveLetterManager 분리 및 통합)
- [ ] **Phase 3 대기 중** (TrayIconManager 분리)

---

## 참고사항
- 각 Phase는 독립적으로 커밋되므로 중간에 중단 가능
- ShellViewModel.cs 최종 예상 라인 수: ~250줄
- 총 5개 서비스 클래스: ~1,260줄
- 순 감소: ~170줄 (중복 제거)
