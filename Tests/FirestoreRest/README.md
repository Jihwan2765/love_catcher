# Firestore REST 요청 검사

`dotnet run --project Tests/FirestoreRest/FirestoreRestChecks.csproj`는 실제
`FirebaseRESTService.cs`와 `BoothStaffAuth.cs`를 작은 Unity 모의 객체로 컴파일하고,
등록·프로필 선점·재고 차감·집계·플레이 요청의 JSON을 검사합니다. 기존 참가자
인덱스가 정상인 경우와 대상 문서가 사라진 경우도 확인합니다.

실제 Firebase, 서버 보안 규칙, Unity 직렬화, 네트워크 오류 및 Windows 빌드를
검사하지 않습니다. 운영 전에는 별도 테스트 Firebase 프로젝트에서 확인해야 합니다.
