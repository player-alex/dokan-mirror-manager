[목적]
.NET 8.0, WPF, DokanNet을 기반으로 하는 윈도우 애플리케이션을 개발해야 한다.

[요약]
처음 실행하면 MainWindow가 나온다.
MainWindow에는 리스트뷰가 있는데

리스트뷰의 구조는 다음과 같다. (필요 시 알아서 리스트뷰를 조정할 것)
Src(TextBlock) | Dest(ComboBox) | R/W(Checkbox) | Status(TextBlock)

리스트뷰에 대한 Add / Remove(또는 Delete, Context에 적합한 단어를 고려할 것) / Mount / Unmount 버튼이 있다.
Mount / Unmount 버튼은 리스트뷰에 내장해도 되며 대체적으로 MahApps.IconPacks를 기반으로 하는 버튼을 사용하라.
리스트뷰에서 Dest를 선택하면 ComboBox로 사용중이지 않은 드라이브 Letter를 제시해야 한다.
Mount 된 상태에서는 Dest를 선택하지 못하도록 차단해야 한다.

Add를 누르면 Select source directory dialog가 나오고 정상적으로 선택되면
리스트뷰에 추가되고 Dest(대상) Letter는 EMpty 상태로 변경된다.

사용자가 Dest를 선택하면 Mount 버튼은 활성화되며 클릭 시 Mount 되도록 해야한다.

프로그램을 닫으면 Tray로 전환되어야 하며 Tray 메뉴를 더블클릭하면 MainWindow가 다시 나온다. Tray의 Menu는

Open
Exit

가 있어야 한다.(Context를 고려해서 추가적으로 필요하다면 버튼을 추가하거나 하라)

[종속성]
필요한 종속성 DokanNet, MahApps.Metro, MahApps.IconPacks, Caliburn.Micro는 설치되어 있다.
추가로 필요하다면 NuGet를 통해 설치하라.

[디자인 패턴]
Caliburn.Micro를 기반으로 하는 MVVM을 사용하라.
Models, ViewModels, Views 디렉터리를 생성하고 프로젝트에 Import 한 뒤 작업하라.

[개발 가이드라인]
Console로 개발된 Reference는 ReferenceProgram.cs에 포함되어 있다. 우리는 콘솔 기반의 예제를 WPF로 이식할 것이며
간단한 UI를 통해 제어하려고 하는 것이다.

프로젝트는 Visual Studio 2022로 생성되었다.

UI는 MahApps.Metro, MahApps.IconPacks을 적극적으로 활용하라.

테스트 혹은 디버깅은 스스로 할 수 없다면(UI 제어) 내가 실행 후 결과를 응답해줄 것이다.

[MCP]
필요하다면 아래의 MCP를 사용할 수 있다.
sequential-thinking MCP
