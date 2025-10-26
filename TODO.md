# TODO: IPC Mount Point Query 디버깅

## 현재 상황
- ✅ IPC 기능 완전 구현 완료 (C# 서버, Python 클라이언트)
- ✅ 빌드 성공
- ❌ **메시지가 WndProc에 도달하지 않음** - 핵심 문제!

## 문제 증상
1. Python 클라이언트가 윈도우 핸들을 정확히 찾음 (0xC0BFC)
2. `SendMessage` 호출 성공 (result: 0)
3. `dwData`, `cbData`, `lpData` 모두 정확
4. **하지만 `ipc.log`와 `wndproc.log` 둘 다 생성되지 않음**
5. WndProc Hook이 메시지를 받지 못하는 것으로 추정

## 디버깅 코드 추가됨
- `ShellViewModel.cs` WndProc에 모든 메시지 로깅 추가
- `wndproc.log`에 모든 메시지 기록하도록 설정

## 다음 단계

### 1. WndProc Hook 확인
**파일**: `dokan-mirror-manager\ViewModels\ShellViewModel.cs:116-124`

```csharp
private void SetupWindowMessageHook(object view)
{
    if (view is Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);
    }
}
```

**확인사항**:
- `helper.Handle`이 Python이 찾은 핸들(0xC0BFC)과 동일한지
- `_hwndSource`가 null이 아닌지
- Hook이 성공적으로 추가되었는지

**디버깅 방법**:
```csharp
private void SetupWindowMessageHook(object view)
{
    if (view is Window window)
    {
        var helper = new WindowInteropHelper(window);
        var handle = helper.Handle;

        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        File.AppendAllText(logPath, $"[{DateTime.Now}] Window handle: 0x{handle.ToInt64():X}\n");

        _hwndSource = HwndSource.FromHwnd(handle);

        if (_hwndSource == null)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now}] ERROR: HwndSource is null!\n");
        }
        else
        {
            _hwndSource.AddHook(WndProc);
            File.AppendAllText(logPath, $"[{DateTime.Now}] Hook added successfully\n");
        }
    }
}
```

### 2. 타이밍 문제 확인
**문제**: WPF 윈도우가 완전히 초기화되기 전에 Hook 설정 시도

**해결책**: `OnViewLoaded` 대신 `Window.Loaded` 이벤트 사용

```csharp
protected override async void OnViewLoaded(object view)
{
    base.OnViewLoaded(view);
    StatusMessage = "Ready";

    if (view is Window window)
    {
        _trayIconManager.Initialize(window, ShowWindow, ExitApplicationAsync, msg => StatusMessage = msg);

        // Wait for window to be fully loaded
        window.Loaded += (s, e) =>
        {
            SetupWindowMessageHook(window);
        };
    }

    // ... rest of code
}
```

### 3. SendMessage vs PostMessage
**현재**: Python이 `SendMessage` 사용 (동기)
**문제**: 프로세스 간 SendMessage는 메모리 매핑 문제 가능

**대안**: `PostMessage` 시도
```python
PostMessage = windll.user32.PostMessageW
PostMessage.argtypes = [c_void_p, UINT, WPARAM, c_void_p]
PostMessage.restype = c_bool

result = PostMessage(hwnd, WM_COPYDATA, 0, addressof(cds))
```

### 4. 메시지 값 재확인
**파일들**:
- `App.xaml.cs:17` - `public const int WM_GET_MOUNT_POINTS = 0x8002;`
- `NativeMethods.cs:100` - `public const int WM_COPYDATA = 0x004A;`
- Python: `WM_COPYDATA = 0x004A`, `WM_GET_MOUNT_POINTS = 0x8002`

모두 일치함 ✓

### 5. 대체 방안: RegisterWindowMessage 사용
**현재 방식**: 하드코딩된 메시지 ID 사용
**문제**: 다른 앱과 충돌 가능

**개선**:
```csharp
// C# - App.xaml.cs
[DllImport("user32.dll", CharSet = CharSet.Auto)]
private static extern int RegisterWindowMessage(string lpString);

public static readonly int WM_GET_MOUNT_POINTS =
    RegisterWindowMessage("WM_DOKAN_GET_MOUNT_POINTS");
```

```python
# Python
RegisterWindowMessage = windll.user32.RegisterWindowMessageW
RegisterWindowMessage.argtypes = [c_wchar_p]
RegisterWindowMessage.restype = c_uint

WM_GET_MOUNT_POINTS = RegisterWindowMessage("WM_DOKAN_GET_MOUNT_POINTS")
```

## 테스트 파일 위치
- Python 클라이언트: `Examples/MountPointQuery/Python/mount_point_query_client.py`
- 윈도우 찾기 테스트: `Examples/MountPointQuery/Python/test_find_window.py`
- 빌드 출력: `dokan-mirror-manager/bin/Debug/net8.0-windows10.0.17763.0/`

## 예상 로그 파일
- `startup.log` - Hook 설정 로그
- `wndproc.log` - 모든 윈도우 메시지 로그
- `ipc.log` - IPC 핸들러 로그

## Git 브랜치
`remote-mount-points`

## 다음 세션에서 할 일
1. `startup.log` 추가하여 Hook 설정 확인
2. Hook이 설정되었는지 확인
3. `wndproc.log`에 메시지가 있는지 확인
4. 필요시 PostMessage로 전환
5. 모든 것이 실패하면 RegisterWindowMessage 사용
