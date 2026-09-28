using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using ClawMachine.Utils;

namespace ClawMachine.Mechanics
{
    public enum ParticipantRegistrationResult
    {
        Created,
        AlreadyRegistered,
        GenderConflict,
        InvalidInput,
        IndexConflict,
        Failed
    }

    public enum ParticipantRegistrationLookupResult
    {
        NewParticipant,
        ExistingParticipant,
        GenderConflict,
        Failed
    }

    public enum ProfileClaimResult
    {
        Claimed,
        CandidateUnavailable,
        Failed
    }

    public enum ParticipantUpdateResult
    {
        Saved,
        ProfileClaimLocked,
        Failed
    }

    public class FirebaseRESTService : MonoBehaviour
    {
        [Serializable] private class ClaimFields { public FirestoreStringField stockField, targetKey, kind; }
        [Serializable] private class ClaimReceipt { public string updateTime; public ClaimFields fields; }
        [Serializable] private class CountFields { public FirestoreIntField count; }
        [Serializable] private class AggregateValue { public CountFields aggregateFields; }
        [Serializable] private class AggregateItem { public AggregateValue result; }
        [Serializable] private class AggregateItems { public AggregateItem[] items; }
        [Serializable] private class ParticipantKeyFields { public FirestoreStringField participantKey; }
        [Serializable] private class ParticipantKeyDocument { public string updateTime; public ParticipantKeyFields fields; }
        [Serializable] private class RegistrationFields
        {
            public FirestoreStringField name, insta, bio, gender;
            public FirestoreBoolField isPicked;
            public FirestoreIntField attempts;
        }
        [Serializable] private class DeletePrecondition { public string updateTime; }
        [Serializable] private class DeleteWrite { public string delete; public DeletePrecondition currentDocument; }
        [Serializable] private class DeleteCommit { public DeleteWrite[] writes; }
        [Serializable] private class PlayReceiptFields
        {
            public FirestoreStringField kind, participantKey, insta;
            public FirestoreIntField revenue;
        }
        [Serializable] private class PlayReceiptDocument { public PlayReceiptFields fields; }
        private static FirebaseRESTService instance;
        public static FirebaseRESTService Instance
        {
            get
            {
                if (instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    instance = FindFirstObjectByType<FirebaseRESTService>();
#else
                    instance = FindObjectOfType<FirebaseRESTService>();
#endif
                    if (instance == null)
                    {
                        GameObject go = new GameObject("FirebaseRESTService");
                        instance = go.AddComponent<FirebaseRESTService>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("Firebase Config")]
        [Tooltip("Firebase 콘솔에서 확인할 수 있는 프로젝트 고유 ID")]
        public string firebaseProjectId;

        private int activeWriteOperationCount;
        public bool IsWriteInProgress => activeWriteOperationCount > 0;
        public bool IsReadOnlyMode { get; private set; }

        /// <summary>
        /// 테스트 세션 동안 Firestore 조회만 허용합니다. runQuery 계열 POST는 조회이므로 허용하고,
        /// commit/PATCH/DELETE 등 데이터 변경 요청은 SendAuthorized에서 최종 차단합니다.
        /// </summary>
        public void SetReadOnlyMode(bool enabled)
        {
            if (IsReadOnlyMode == enabled) return;
            IsReadOnlyMode = enabled;
            Debug.Log(enabled
                ? "[Firebase] 테스트 세션 읽기 전용 모드가 활성화되었습니다."
                : "[Firebase] 테스트 세션 읽기 전용 모드가 해제되었습니다.");
        }

        private void BeginWriteOperation()
        {
            activeWriteOperationCount++;
        }

        private void EndWriteOperation()
        {
            activeWriteOperationCount = Mathf.Max(0, activeWriteOperationCount - 1);
        }

        private IEnumerator SendAuthorized(UnityWebRequest request)
        {
            if (IsReadOnlyMode && IsFirestoreWriteRequest(request))
            {
                Debug.LogError($"[Firebase] 테스트 세션에서 쓰기 요청을 차단했습니다: {request.method} {request.url}");
                yield break;
            }

            string token = null;
            if (BoothStaffAuth.Instance == null) yield break;
            yield return BoothStaffAuth.Instance.EnsureIdToken(value => token = value);
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[Firebase] 스태프 로그인이 필요하여 요청을 중단했습니다.");
                yield break;
            }
            request.timeout = 15;
            request.SetRequestHeader("Authorization", "Bearer " + token);
            yield return request.SendWebRequest();
        }

        private static bool IsFirestoreWriteRequest(UnityWebRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.url) ||
                request.url.IndexOf("firestore.googleapis.com", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            string method = request.method ?? "";
            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
                return false;

            // Firestore REST 조회 API는 HTTP POST를 사용하지만 문서를 변경하지 않습니다.
            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                (request.url.EndsWith(":runQuery", StringComparison.OrdinalIgnoreCase) ||
                 request.url.EndsWith(":runAggregationQuery", StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfig();
        }

        [Serializable]
        private class FirebaseConfig
        {
            public string firebaseProjectId;
        }

        private void LoadConfig()
        {
            // 1순위: .env 환경변수 파일 또는 시스템 환경변수에서 로드
            string envProjectId = EnvLoader.Get("FIREBASE_PROJECT_ID");
            if (!string.IsNullOrEmpty(envProjectId) && envProjectId != "your-firebase-project-id")
            {
                firebaseProjectId = envProjectId;
                Debug.Log($"[Firebase] ✅ .env 환경설정에서 Project ID 로드 완료: {firebaseProjectId}");
                return;
            }

            // 2순위: Resources/FirebaseConfig.json에서 로드
            if (string.IsNullOrEmpty(firebaseProjectId) || firebaseProjectId == "your-firebase-project-id")
            {
                TextAsset configAsset = Resources.Load<TextAsset>("FirebaseConfig");
                if (configAsset != null)
                {
                    try
                    {
                        FirebaseConfig config = JsonUtility.FromJson<FirebaseConfig>(configAsset.text);
                        if (config != null && !string.IsNullOrEmpty(config.firebaseProjectId) && config.firebaseProjectId != "your-firebase-project-id")
                        {
                            firebaseProjectId = config.firebaseProjectId;
                            Debug.Log($"[Firebase] Resources/FirebaseConfig에서 Project ID 로드 성공: {firebaseProjectId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Firebase] FirebaseConfig 로드 실패: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 인스타 아이디를 참가자 문서와 중복 조회에 사용할 표준 형식으로 정규화합니다.
        /// </summary>
        public static bool TryNormalizeInstaId(string input, out string handle)
        {
            handle = (input ?? "").Trim().TrimStart('@').ToLowerInvariant();
            return handle.Length > 0 && handle.Length <= 30 &&
                System.Text.RegularExpressions.Regex.IsMatch(handle, "^[a-z0-9._]+$");
        }

        /// <summary>참가자 정보를 Firestore에 신규 등록합니다.</summary>
        public IEnumerator RegisterPlayer(string name, string insta, string bio, string gender, int attempts,
            Action<bool> callback, bool repairOrphanedLinks = false,
            Action<ParticipantRegistrationResult> resultCallback = null)
        {
            if (string.IsNullOrEmpty(firebaseProjectId) || !TryNormalizeInstaId(insta, out string handle))
            { CompleteRegistration(ParticipantRegistrationResult.InvalidInput, callback, resultCallback); yield break; }
            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            string prefix = $"projects/{firebaseProjectId}/databases/(default)/documents/";
            string key = "insta_" + handle;
            var fields = new RegistrationFields {
                name = new FirestoreStringField(name), insta = new FirestoreStringField(handle),
                bio = new FirestoreStringField(bio), gender = new FirestoreStringField(gender),
                isPicked = new FirestoreBoolField(false), attempts = new FirestoreIntField(0)
            };
            for (int attempt = 0; attempt < 5; attempt++)
            {
                using (var check = UnityWebRequest.Get(root + "/ParticipantKeys/" + key))
                {
                    yield return SendAuthorized(check);
                    if (check.responseCode == 200)
                    {
                        ParticipantRegistrationLookupResult indexedStatus = ParticipantRegistrationLookupResult.Failed;
                        yield return ValidateParticipantRegistration(
                            check.downloadHandler.text,
                            handle,
                            gender,
                            status => indexedStatus = status);
                        if (indexedStatus == ParticipantRegistrationLookupResult.ExistingParticipant)
                        {
                            CompleteRegistration(ParticipantRegistrationResult.AlreadyRegistered, callback, resultCallback);
                            yield break;
                        }
                        if (indexedStatus == ParticipantRegistrationLookupResult.GenderConflict)
                        {
                            CompleteRegistration(ParticipantRegistrationResult.GenderConflict, callback, resultCallback);
                            yield break;
                        }
                        if (repairOrphanedLinks && BoothStaffAuth.Instance != null && BoothStaffAuth.Instance.IsAdmin)
                        {
                            bool repaired = false;
                            yield return RemoveOrphanedParticipantLinks(handle, check.downloadHandler.text,
                                success => repaired = success);
                            if (repaired) continue;
                        }
                        Debug.LogError("[Firebase] 참가자 등록 실패: 기존 참가자 인덱스와 문서가 일치하지 않습니다.");
                        CompleteRegistration(ParticipantRegistrationResult.IndexConflict, callback, resultCallback);
                        yield break;
                    }
                    if (check.responseCode != 404)
                    {
                        LogRegistrationRequestFailure("인덱스 조회", check);
                        CompleteRegistration(ParticipantRegistrationResult.Failed, callback, resultCallback);
                        yield break;
                    }
                }
                string payload = "{\"writes\":[{\"update\":{\"name\":\"" + prefix + "ParticipantKeys/" + key +
                    "\",\"fields\":{\"participantKey\":{\"stringValue\":\"" + key +
                    "\"}}},\"currentDocument\":{\"exists\":false}},{\"update\":{\"name\":\"" +
                    prefix + "Participants/" + key + "\",\"fields\":" + JsonUtility.ToJson(fields) +
                    "},\"currentDocument\":{\"exists\":false}},{\"transform\":{\"document\":\"" +
                    prefix + "GameState/stats\",\"fieldTransforms\":[{\"fieldPath\":\"totalRegistrations\"," +
                    "\"increment\":{\"integerValue\":\"1\"}}]},\"currentDocument\":{\"exists\":true}}]}";
                BeginWriteOperation();
                using (var commit = new UnityWebRequest(root + ":commit", "POST"))
                {
                    commit.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                    commit.downloadHandler = new DownloadHandlerBuffer();
                    commit.SetRequestHeader("Content-Type", "application/json");
                    yield return SendAuthorized(commit);
                    EndWriteOperation();
                    if (commit.responseCode == 200)
                    { CompleteRegistration(ParticipantRegistrationResult.Created, callback, resultCallback); yield break; }
                    LogRegistrationRequestFailure($"commit 시도 {attempt + 1}/5", commit);
                    if (commit.responseCode != 0 && commit.responseCode != 409 && commit.responseCode != 412 && commit.responseCode != 503)
                    { CompleteRegistration(ParticipantRegistrationResult.Failed, callback, resultCallback); yield break; }
                }
            }
            CompleteRegistration(ParticipantRegistrationResult.Failed, callback, resultCallback);
        }

        private static void CompleteRegistration(ParticipantRegistrationResult result, Action<bool> callback,
            Action<ParticipantRegistrationResult> resultCallback)
        {
            resultCallback?.Invoke(result);
            callback?.Invoke(result == ParticipantRegistrationResult.Created ||
                result == ParticipantRegistrationResult.AlreadyRegistered);
        }

        /// <summary>관리자 직접 등록에서만, 참가자가 사라진 인덱스와 잠금을 버전 조건으로 정리합니다.</summary>
        private IEnumerator RemoveOrphanedParticipantLinks(string handle, string indexJson, Action<bool> callback)
        {
            ParticipantKeyDocument index = null;
            try { index = JsonUtility.FromJson<ParticipantKeyDocument>(indexJson); } catch (Exception) { }
            string participantKey = index?.fields?.participantKey?.stringValue;
            if (string.IsNullOrEmpty(index?.updateTime) ||
                string.IsNullOrEmpty(participantKey) ||
                !System.Text.RegularExpressions.Regex.IsMatch(participantKey, "^[a-zA-Z0-9._-]{1,1500}$"))
            { callback?.Invoke(false); yield break; }

            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            string prefix = $"projects/{firebaseProjectId}/databases/(default)/documents/";
            using (var person = UnityWebRequest.Get(root + "/Participants/" + Uri.EscapeDataString(participantKey)))
            {
                yield return SendAuthorized(person);
                // 실제 참가자가 있으면 인덱스가 오래됐더라도 자동으로 삭제하지 않습니다.
                if (person.responseCode != 404) { callback?.Invoke(false); yield break; }
            }

            // 다른 참가자 문서가 같은 ID를 사용 중이라면 연결을 자동 복구할 수 없습니다.
            string pageToken = null;
            do
            {
                string listUrl = root + "/Participants?pageSize=300" +
                    (string.IsNullOrEmpty(pageToken) ? "" : "&pageToken=" + Uri.EscapeDataString(pageToken));
                using (var list = UnityWebRequest.Get(listUrl))
                {
                    yield return SendAuthorized(list);
                    if (list.responseCode != 200) { callback?.Invoke(false); yield break; }
                    ListDocumentsResponse page = null;
                    try { page = JsonUtility.FromJson<ListDocumentsResponse>(list.downloadHandler.text); }
                    catch (Exception) { callback?.Invoke(false); yield break; }
                    if (page == null) { callback?.Invoke(false); yield break; }
                    if (page.documents != null)
                    {
                        foreach (FirestoreDocumentResponse document in page.documents)
                        {
                            string stored = document?.fields?.insta?.stringValue ?? document?.fields?.instaId?.stringValue;
                            if (TryNormalizeInstaId(stored, out string normalized) && normalized == handle)
                            { callback?.Invoke(false); yield break; }
                        }
                    }
                    pageToken = page.nextPageToken;
                }
            } while (!string.IsNullOrEmpty(pageToken));

            var writes = new List<DeleteWrite> {
                new DeleteWrite { delete = prefix + "ParticipantKeys/insta_" + handle,
                    currentDocument = new DeletePrecondition { updateTime = index.updateTime } }
            };
            using (var claimRequest = UnityWebRequest.Get(root + "/ProfileClaims/insta_" + handle))
            {
                yield return SendAuthorized(claimRequest);
                if (claimRequest.responseCode == 200)
                {
                    ClaimReceipt claim = null;
                    try { claim = JsonUtility.FromJson<ClaimReceipt>(claimRequest.downloadHandler.text); }
                    catch (Exception) { }
                    if (string.IsNullOrEmpty(claim?.updateTime) || claim.fields?.targetKey?.stringValue != participantKey)
                    { callback?.Invoke(false); yield break; }
                    writes.Add(new DeleteWrite { delete = prefix + "ProfileClaims/insta_" + handle,
                        currentDocument = new DeletePrecondition { updateTime = claim.updateTime } });
                }
                else if (claimRequest.responseCode != 404) { callback?.Invoke(false); yield break; }
            }

            BeginWriteOperation();
            using (var commit = new UnityWebRequest(root + ":commit", "POST"))
            {
                commit.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(new DeleteCommit { writes = writes.ToArray() })));
                commit.downloadHandler = new DownloadHandlerBuffer();
                commit.SetRequestHeader("Content-Type", "application/json");
                yield return SendAuthorized(commit);
                bool success = commit.responseCode == 200;
                if (!success)
                {
                    // 응답만 유실된 경우 두 문서가 실제로 사라졌는지 확인합니다.
                    success = true;
                    foreach (DeleteWrite write in writes)
                    {
                        using (var verify = UnityWebRequest.Get(root + "/" + write.delete.Substring(prefix.Length)))
                        {
                            yield return SendAuthorized(verify);
                            if (verify.responseCode != 404) { success = false; break; }
                        }
                    }
                }
                EndWriteOperation();
                if (!success) LogRegistrationRequestFailure("고아 인덱스 정리", commit);
                else Debug.Log("[Firebase] 고아 참가자 인덱스와 프로필 잠금 정리 완료. 등록을 다시 시도합니다.");
                callback?.Invoke(success);
            }
        }

        private static void LogRegistrationRequestFailure(string stage, UnityWebRequest request)
        {
            // 실패 응답만 기록합니다. 요청 본문과 인증 토큰은 로그에 남기지 않습니다.
            string response = request.downloadHandler?.text ?? "";
            if (response.Length > 1000) response = response.Substring(0, 1000) + "...";
            Debug.LogError($"[Firebase] 참가자 등록 {stage} 실패: HTTP {request.responseCode}, " +
                $"Unity 오류: {request.error ?? "없음"}, Firestore 응답: {response}");
        }

        /// <summary>
        /// 입력한 인스타 아이디와 성별이 기존 참가자와 모두 일치하는지 확인합니다.
        /// </summary>
        public IEnumerator CheckParticipantRegistration(string instaId, string gender,
            Action<ParticipantRegistrationLookupResult> callback)
        {
            if (string.IsNullOrEmpty(firebaseProjectId) ||
                BoothStaffAuth.Instance == null || !BoothStaffAuth.Instance.IsAuthenticated)
            {
                callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
                yield break;
            }

            if (!TryNormalizeInstaId(instaId, out string normalizedInstaId))
            { callback?.Invoke(ParticipantRegistrationLookupResult.Failed); yield break; }
            instaId = normalizedInstaId;
            string indexUrl = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/ParticipantKeys/insta_{instaId}";
            using (var indexed = UnityWebRequest.Get(indexUrl))
            {
                yield return SendAuthorized(indexed);
                if (indexed.responseCode == 200)
                {
                    ParticipantRegistrationLookupResult status = ParticipantRegistrationLookupResult.Failed;
                    yield return ValidateParticipantRegistration(
                        indexed.downloadHandler.text,
                        instaId,
                        gender,
                        result => status = result);
                    callback?.Invoke(status);
                    yield break;
                }
                if (indexed.responseCode != 404)
                {
                    callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
                    yield break;
                }
            }

            string url = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents:runQuery";

            string queryPayload = "{" +
                "\"structuredQuery\": {" +
                    "\"from\": [{\"collectionId\": \"Participants\"}]," +
                    "\"where\": {" +
                        "\"fieldFilter\": {" +
                            "\"field\": {\"fieldPath\": \"insta\"}," +
                            "\"op\": \"EQUAL\"," +
                            "\"value\": {\"stringValue\": \"" + instaId + "\"}" +
                        "}" +
                    "}," +
                    "\"limit\": 20" +
                "}" +
            "}";

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(queryPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return SendAuthorized(request);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string rawJson = request.downloadHandler.text;
                    string wrappedJson = "{\"items\":" + rawJson + "}";
                    try
                    {
                        RunQueryResponseList responseList = JsonUtility.FromJson<RunQueryResponseList>(wrappedJson);
                        if (responseList?.items == null)
                        {
                            callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
                            yield break;
                        }
                        bool foundSameInsta = false;
                        foreach (var item in responseList.items)
                        {
                            if (item.document != null && item.document.fields != null && !string.IsNullOrEmpty(item.document.name))
                            {
                                string storedInsta = item.document.fields.insta?.stringValue ??
                                                     item.document.fields.instaId?.stringValue;
                                if (!TryNormalizeInstaId(storedInsta, out string storedHandle) ||
                                    storedHandle != instaId)
                                    continue;

                                foundSameInsta = true;
                                if (item.document.fields.gender?.stringValue == gender)
                                {
                                    callback?.Invoke(ParticipantRegistrationLookupResult.ExistingParticipant);
                                    yield break;
                                }
                            }
                        }
                        callback?.Invoke(foundSameInsta
                            ? ParticipantRegistrationLookupResult.GenderConflict
                            : ParticipantRegistrationLookupResult.NewParticipant);
                        yield break;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Firebase] 중복 확인 파싱 실패: {ex.Message}");
                        callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
                        yield break;
                    }
                }
                callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
            }
        }

        private IEnumerator ValidateParticipantRegistration(string indexJson, string handle, string gender,
            Action<ParticipantRegistrationLookupResult> callback)
        {
            ParticipantKeyDocument indexed;
            try { indexed = JsonUtility.FromJson<ParticipantKeyDocument>(indexJson); }
            catch (Exception)
            {
                callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
                yield break;
            }

            string participantKey = indexed?.fields?.participantKey?.stringValue;
            if (string.IsNullOrWhiteSpace(participantKey) ||
                !System.Text.RegularExpressions.Regex.IsMatch(participantKey, "^[a-zA-Z0-9._-]{1,1500}$"))
            {
                callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
                yield break;
            }

            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            using (var person = UnityWebRequest.Get(root + "/Participants/" + Uri.EscapeDataString(participantKey)))
            {
                yield return SendAuthorized(person);
                if (person.responseCode != 200)
                {
                    callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
                    yield break;
                }

                try
                {
                    var document = JsonUtility.FromJson<FirestoreDocument>(person.downloadHandler.text);
                    string storedInsta = document?.fields?.insta?.stringValue ?? document?.fields?.instaId?.stringValue;
                    if (!TryNormalizeInstaId(storedInsta, out string storedHandle) || storedHandle != handle)
                    {
                        callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
                        yield break;
                    }

                    callback?.Invoke(document.fields.gender?.stringValue == gender
                        ? ParticipantRegistrationLookupResult.ExistingParticipant
                        : ParticipantRegistrationLookupResult.GenderConflict);
                }
                catch (Exception)
                {
                    callback?.Invoke(ParticipantRegistrationLookupResult.Failed);
                }
            }
        }

        /// <summary>
        /// 반대 성별 참가자 중 뽑히지 않았고 지급 잠금도 없는 사람을 선택합니다.
        /// </summary>
        public IEnumerator GetRandomMatch(string oppositeGender, Action<MatchedProfileResponse> callback,
            HashSet<string> excludedDocumentIds = null)
        {
            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                Debug.LogError("[Firebase] Project ID가 설정되지 않았습니다.");
                callback?.Invoke(new MatchedProfileResponse { success = false });
                yield break;
            }

            string url = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents:runQuery";

            // structuredQuery JSONPayload 직접 빌드 (JsonUtility 중첩 한계를 극복하기 위해 문자열 빌더 사용)
            string queryPayload = "{" +
                "\"structuredQuery\": {" +
                    "\"from\": [{\"collectionId\": \"Participants\"}]," +
                    "\"where\": {" +
                        "\"compositeFilter\": {" +
                            "\"op\": \"AND\"," +
                            "\"filters\": [" +
                                "{" +
                                    "\"fieldFilter\": {" +
                                        "\"field\": {\"fieldPath\": \"gender\"}," +
                                        "\"op\": \"EQUAL\"," +
                                        "\"value\": {\"stringValue\": \"" + oppositeGender + "\"}" +
                                    "}" +
                                "}," +
                                "{" +
                                    "\"fieldFilter\": {" +
                                        "\"field\": {\"fieldPath\": \"isPicked\"}," +
                                        "\"op\": \"EQUAL\"," +
                                        "\"value\": {\"booleanValue\": false}" +
                                    "}" +
                                "}" +
                            "]" +
                        "}" +
                    "}" +
                "}" +
            "}";

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(queryPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return SendAuthorized(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Firebase] 매칭 쿼리 실패: HTTP {request.responseCode}, {request.error}");
                    callback?.Invoke(new MatchedProfileResponse { success = false });
                    yield break;
                }

                List<RunQueryResponseItem> candidates = new List<RunQueryResponseItem>();
                try
                {
                    var response = JsonUtility.FromJson<RunQueryResponseList>(
                        "{\"items\":" + request.downloadHandler.text + "}");
                    if (response?.items == null) throw new Exception("매칭 응답에 목록이 없습니다.");
                    foreach (var item in response.items)
                    {
                        if (item?.document?.fields == null || string.IsNullOrEmpty(item.document.name)) continue;
                        string docId = item.document.name.Substring(item.document.name.LastIndexOf('/') + 1);
                        if (excludedDocumentIds != null && excludedDocumentIds.Contains(docId)) continue;
                        if (!TryNormalizeInstaId(item.document.fields.insta?.stringValue, out _)) continue;
                        candidates.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Firebase] 매칭 응답 파싱 실패: {ex.Message}");
                    callback?.Invoke(new MatchedProfileResponse { success = false });
                    yield break;
                }

                while (candidates.Count > 0)
                {
                    int index = UnityEngine.Random.Range(0, candidates.Count);
                    RunQueryResponseItem chosen = candidates[index];
                    candidates.RemoveAt(index);
                    string docPath = chosen.document.name;
                    string docId = docPath.Substring(docPath.LastIndexOf('/') + 1);
                    string insta = chosen.document.fields.insta.stringValue;
                    bool? claimed = null;
                    yield return GetProfileClaimState(insta, value => claimed = value);
                    if (!claimed.HasValue)
                    {
                        callback?.Invoke(new MatchedProfileResponse { success = false });
                        yield break;
                    }
                    if (claimed.Value) continue;

                    MatchedProfileResponse match = new MatchedProfileResponse
                    {
                        success = true,
                        querySucceeded = true,
                        documentId = docId,
                        name = chosen.document.fields.name?.stringValue ?? "익명",
                        gender = chosen.document.fields.gender?.stringValue ?? oppositeGender,
                        insta = insta,
                        bio = chosen.document.fields.bio?.stringValue ?? "인스타 친구해요!"
                    };
                    Debug.Log($"[Firebase] 매칭 대상 로드 성공: {match.name} ({match.insta})");
                    callback?.Invoke(match);
                    yield break;
                }

                Debug.LogWarning("[Firebase] 지급 가능한 매칭 대상이 없습니다.");
                callback?.Invoke(new MatchedProfileResponse { success = false, querySucceeded = true });
            }
        }

        private IEnumerator GetProfileClaimState(string insta, Action<bool?> callback)
        {
            if (!TryNormalizeInstaId(insta, out string handle))
            {
                callback?.Invoke(null);
                yield break;
            }
            string url = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/ProfileClaims/insta_{handle}";
            using (var request = UnityWebRequest.Get(url))
            {
                yield return SendAuthorized(request);
                if (request.responseCode == 200) callback?.Invoke(true);
                else if (request.responseCode == 404) callback?.Invoke(false);
                else
                {
                    Debug.LogError($"[Firebase] 프로필 잠금 확인 실패: HTTP {request.responseCode}, {request.error}");
                    callback?.Invoke(null);
                }
            }
        }

        private IEnumerator ReadProfileClaimForParticipant(string insta, string documentId,
            Action<ClaimReceipt, ParticipantUpdateResult> callback)
        {
            if (string.IsNullOrWhiteSpace(insta))
            {
                callback?.Invoke(null, ParticipantUpdateResult.Saved);
                yield break;
            }
            if (!TryNormalizeInstaId(insta, out string handle))
            {
                callback?.Invoke(null, ParticipantUpdateResult.Failed);
                yield break;
            }

            string url = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/ProfileClaims/insta_{handle}";
            using (var request = UnityWebRequest.Get(url))
            {
                yield return SendAuthorized(request);
                if (request.responseCode == 404)
                {
                    callback?.Invoke(null, ParticipantUpdateResult.Saved);
                    yield break;
                }
                if (request.responseCode != 200)
                {
                    callback?.Invoke(null, ParticipantUpdateResult.Failed);
                    yield break;
                }
                ClaimReceipt claim = null;
                try { claim = JsonUtility.FromJson<ClaimReceipt>(request.downloadHandler.text); }
                catch (Exception) { }
                if (string.IsNullOrEmpty(claim?.updateTime) ||
                    claim.fields?.targetKey?.stringValue != documentId)
                {
                    callback?.Invoke(null, ParticipantUpdateResult.ProfileClaimLocked);
                    yield break;
                }
                callback?.Invoke(claim, ParticipantUpdateResult.Saved);
            }
        }

        private IEnumerator CommitParticipantUpdateAndReleaseClaim(string documentId, string insta,
            string participantUpdateTime, string claimUpdateTime, string fieldsJson, string fieldPathsJson,
            Action<bool> callback)
        {
            if (BoothStaffAuth.Instance == null || !BoothStaffAuth.Instance.IsAdmin ||
                !TryNormalizeInstaId(insta, out string handle) ||
                !System.Text.RegularExpressions.Regex.IsMatch(documentId, "^[a-zA-Z0-9._@ -]{1,200}$"))
            { callback?.Invoke(false); yield break; }

            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            string prefix = $"projects/{firebaseProjectId}/databases/(default)/documents/";
            string body = "{\"writes\":[{\"update\":{\"name\":\"" + prefix + "Participants/" + documentId +
                "\",\"fields\":" + fieldsJson + "},\"updateMask\":{\"fieldPaths\":" + fieldPathsJson +
                "},\"currentDocument\":{\"updateTime\":\"" + participantUpdateTime +
                "\"}},{\"delete\":\"" + prefix + "ProfileClaims/insta_" + handle +
                "\",\"currentDocument\":{\"updateTime\":\"" + claimUpdateTime + "\"}}]}";

            BeginWriteOperation();
            using (var request = new UnityWebRequest(root + ":commit", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return SendAuthorized(request);
                EndWriteOperation();
                if (request.responseCode != 200)
                    Debug.LogError($"[Firebase] 참가자 뽑힘 해제와 잠금 삭제 실패: HTTP {request.responseCode}, {request.error}");
                callback?.Invoke(request.responseCode == 200);
            }
        }

        /// <summary>
        /// 아직 뽑히지 않은 남성 및 여성 참가자 수를 조회합니다.
        /// </summary>
        public IEnumerator GetUnpickedCounts(Action<int, int> callback)
        {
            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                callback?.Invoke(-1, -1);
                yield break;
            }
            int[] counts = { -1, -1 };
            string[] genders = { "남", "여" };
            string url = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents:runAggregationQuery";
            for (int i = 0; i < 2; i++)
            {
                string body = "{\"structuredAggregationQuery\":{\"aggregations\":[{\"count\":{},\"alias\":\"count\"}]," +
                    "\"structuredQuery\":{\"from\":[{\"collectionId\":\"Participants\"}],\"where\":{\"compositeFilter\":{" +
                    "\"op\":\"AND\",\"filters\":[{\"fieldFilter\":{\"field\":{\"fieldPath\":\"gender\"}," +
                    "\"op\":\"EQUAL\",\"value\":{\"stringValue\":\"" + genders[i] + "\"}}},{\"fieldFilter\":{" +
                    "\"field\":{\"fieldPath\":\"isPicked\"},\"op\":\"EQUAL\",\"value\":{\"booleanValue\":false}}}]}}}}}";
                using (var request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    yield return SendAuthorized(request);
                    if (request.responseCode != 200) continue;
                    try
                    {
                        var data = JsonUtility.FromJson<AggregateItems>("{\"items\":" + request.downloadHandler.text + "}");
                        if (data?.items == null || data.items.Length == 0 ||
                            !int.TryParse(data.items[0]?.result?.aggregateFields?.count?.integerValue, out counts[i]))
                            counts[i] = -1;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[Firebase] 참가자 수 집계 응답 파싱 실패: " + ex.Message);
                        counts[i] = -1;
                    }
                }
            }
            callback?.Invoke(counts[0], counts[1]);
        }

        private IEnumerator ValidateParticipantKey(string indexJson, string handle, Action<bool> callback)
        {
            ParticipantKeyDocument indexed;
            try { indexed = JsonUtility.FromJson<ParticipantKeyDocument>(indexJson); }
            catch (Exception) { callback?.Invoke(false); yield break; }
            string participantKey = indexed?.fields?.participantKey?.stringValue;
            if (string.IsNullOrWhiteSpace(participantKey) ||
                !System.Text.RegularExpressions.Regex.IsMatch(participantKey, "^[a-zA-Z0-9._-]{1,1500}$"))
            { callback?.Invoke(false); yield break; }

            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            using (var person = UnityWebRequest.Get(root + "/Participants/" + Uri.EscapeDataString(participantKey)))
            {
                yield return SendAuthorized(person);
                if (person.responseCode != 200) { callback?.Invoke(false); yield break; }
                try
                {
                    var doc = JsonUtility.FromJson<FirestoreDocument>(person.downloadHandler.text);
                    string stored = (doc?.fields?.insta?.stringValue ?? "").Trim().TrimStart('@').ToLowerInvariant();
                    callback?.Invoke(stored == handle);
                }
                catch (Exception) { callback?.Invoke(false); }
            }
        }

        /// <summary>
        /// 참가자의 뽑힘 상태를 변경합니다. 해제 시 지급 잠금도 같은 commit에서 삭제합니다.
        /// </summary>
        public IEnumerator ClaimProfile(string roundId, MatchedProfileResponse candidate, Action<bool> callback,
            Action<ProfileClaimResult> resultCallback = null)
        {
            string handle = (candidate.insta ?? "").Trim().TrimStart('@').ToLowerInvariant();
            if (string.IsNullOrEmpty(roundId) || string.IsNullOrEmpty(candidate.documentId) ||
                !System.Text.RegularExpressions.Regex.IsMatch(handle, "^[a-z0-9._]{1,30}$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(candidate.documentId, "^[a-zA-Z0-9._@ -]{1,200}$"))
            { CompleteProfileClaim(ProfileClaimResult.Failed, callback, resultCallback); yield break; }
            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            string prefix = $"projects/{firebaseProjectId}/databases/(default)/documents/";
            string receipt = "MatchResults/love_" + roundId;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                using (var check = UnityWebRequest.Get(root + "/" + receipt))
                {
                    yield return SendAuthorized(check);
                    if (check.responseCode == 200)
                    {
                        ClaimReceipt saved = null;
                        try { saved = JsonUtility.FromJson<ClaimReceipt>(check.downloadHandler.text); } catch { }
                        CompleteProfileClaim(saved?.fields?.targetKey?.stringValue == candidate.documentId
                            ? ProfileClaimResult.Claimed : ProfileClaimResult.Failed, callback, resultCallback);
                        yield break;
                    }
                    if (check.responseCode != 404)
                    { CompleteProfileClaim(ProfileClaimResult.Failed, callback, resultCallback); yield break; }
                }
                FirestoreDocumentResponse person = null;
                long personResponseCode;
                using (var get = UnityWebRequest.Get(root + "/Participants/" + Uri.EscapeDataString(candidate.documentId)))
                {
                    yield return SendAuthorized(get);
                    personResponseCode = get.responseCode;
                    if (get.responseCode == 200)
                        try { person = JsonUtility.FromJson<FirestoreDocumentResponse>(get.downloadHandler.text); } catch { }
                }
                if (person?.fields == null || string.IsNullOrEmpty(person.updateTime) ||
                    person.fields.isPicked?.booleanValue == true ||
                    (person.fields.insta?.stringValue ?? "").Trim().TrimStart('@').ToLowerInvariant() != handle)
                {
                    CompleteProfileClaim(personResponseCode == 404 || person?.fields != null
                        ? ProfileClaimResult.CandidateUnavailable : ProfileClaimResult.Failed,
                        callback, resultCallback);
                    yield break;
                }
                bool? alreadyClaimed = null;
                yield return GetProfileClaimState(handle, value => alreadyClaimed = value);
                if (!alreadyClaimed.HasValue)
                { CompleteProfileClaim(ProfileClaimResult.Failed, callback, resultCallback); yield break; }
                if (alreadyClaimed.Value)
                { CompleteProfileClaim(ProfileClaimResult.CandidateUnavailable, callback, resultCallback); yield break; }
                string body = "{\"writes\":[{\"update\":{\"name\":\"" + prefix + receipt +
                    "\",\"fields\":{\"targetKey\":{\"stringValue\":\"" + candidate.documentId +
                    "\"}}},\"currentDocument\":{\"exists\":false}},{\"update\":{\"name\":\"" +
                    prefix + "ProfileClaims/insta_" + handle +
                    "\",\"fields\":{\"targetKey\":{\"stringValue\":\"" + candidate.documentId +
                    "\"}}},\"currentDocument\":{\"exists\":false}},{\"update\":{\"name\":\"" +
                    prefix + "Participants/" + candidate.documentId +
                    "\",\"fields\":{\"isPicked\":{\"booleanValue\":true}}},\"updateMask\":{\"fieldPaths\":[\"isPicked\"]}," +
                    "\"currentDocument\":{\"updateTime\":\"" + person.updateTime + "\"}},{\"transform\":{\"document\":\"" +
                    prefix + "GameState/stats\",\"fieldTransforms\":[{\"fieldPath\":\"totalSuccesses\",\"increment\":{\"integerValue\":\"1\"}}]}," +
                    "\"currentDocument\":{\"exists\":true}}]}";
                using (var commit = new UnityWebRequest(root + ":commit", "POST"))
                {
                    commit.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    commit.downloadHandler = new DownloadHandlerBuffer();
                    commit.SetRequestHeader("Content-Type", "application/json");
                    yield return SendAuthorized(commit);
                    if (commit.responseCode == 200)
                    { CompleteProfileClaim(ProfileClaimResult.Claimed, callback, resultCallback); yield break; }
                    Debug.LogWarning($"[Firebase] 인스타 지급 확정 재시도: HTTP {commit.responseCode}, 대상 {candidate.documentId}, 회차 {roundId}");
                    if (commit.responseCode != 409 && commit.responseCode != 412 && commit.responseCode != 503 && commit.responseCode != 0)
                    { CompleteProfileClaim(ProfileClaimResult.Failed, callback, resultCallback); yield break; }
                }
            }
            CompleteProfileClaim(ProfileClaimResult.Failed, callback, resultCallback);
        }

        private static void CompleteProfileClaim(ProfileClaimResult result, Action<bool> callback,
            Action<ProfileClaimResult> resultCallback)
        {
            resultCallback?.Invoke(result);
            callback?.Invoke(result == ProfileClaimResult.Claimed);
        }

        /// <summary>사탕 결과와 성공 횟수를 같은 회차 영수증으로 한 번만 확정합니다.</summary>
        public IEnumerator ClaimCandy(string roundId, Action<bool> callback)
        {
            if (string.IsNullOrEmpty(firebaseProjectId) ||
                !System.Text.RegularExpressions.Regex.IsMatch(roundId ?? "", "^[a-f0-9]{32}$"))
            { callback?.Invoke(false); yield break; }

            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            string prefix = $"projects/{firebaseProjectId}/databases/(default)/documents/";
            string receipt = "GameRounds/love_candy_" + roundId;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                using (var existing = UnityWebRequest.Get(root + "/" + receipt))
                {
                    yield return SendAuthorized(existing);
                    if (existing.responseCode == 200)
                    {
                        ClaimReceipt saved = null;
                        try { saved = JsonUtility.FromJson<ClaimReceipt>(existing.downloadHandler.text); } catch { }
                        callback?.Invoke(saved?.fields?.kind?.stringValue == "love_candy");
                        yield break;
                    }
                    if (existing.responseCode != 404) { callback?.Invoke(false); yield break; }
                }

                string body = "{\"writes\":[{\"update\":{\"name\":\"" + prefix + receipt +
                    "\",\"fields\":{\"kind\":{\"stringValue\":\"love_candy\"}}},\"currentDocument\":{\"exists\":false}},{" +
                    "\"transform\":{\"document\":\"" + prefix + "GameState/stats\",\"fieldTransforms\":[{" +
                    "\"fieldPath\":\"totalSuccesses\",\"increment\":{\"integerValue\":\"1\"}}]}," +
                    "\"currentDocument\":{\"exists\":true}}]}";
                using (var commit = new UnityWebRequest(root + ":commit", "POST"))
                {
                    commit.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    commit.downloadHandler = new DownloadHandlerBuffer();
                    commit.SetRequestHeader("Content-Type", "application/json");
                    yield return SendAuthorized(commit);
                    if (commit.responseCode == 200) { callback?.Invoke(true); yield break; }
                    if (commit.responseCode != 0 && commit.responseCode != 409 &&
                        commit.responseCode != 412 && commit.responseCode != 503)
                    { callback?.Invoke(false); yield break; }
                }
            }
            callback?.Invoke(false);
        }

        public IEnumerator UpdatePickedStatus(string documentId, bool isPicked, Action<bool> callback,
            Action<ParticipantUpdateResult> resultCallback = null)
        {
            if (string.IsNullOrEmpty(firebaseProjectId) || string.IsNullOrEmpty(documentId))
            {
                Debug.LogError("[Firebase] Project ID 또는 Document ID가 유효하지 않습니다.");
                CompleteParticipantUpdate(ParticipantUpdateResult.Failed, callback, resultCallback);
                yield break;
            }

            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            FirestoreDocumentResponse person = null;
            using (var get = UnityWebRequest.Get(root + "/Participants/" + Uri.EscapeDataString(documentId)))
            {
                yield return SendAuthorized(get);
                if (get.responseCode == 200)
                    try { person = JsonUtility.FromJson<FirestoreDocumentResponse>(get.downloadHandler.text); }
                    catch (Exception) { }
            }
            if (string.IsNullOrEmpty(person?.updateTime) || person.fields == null)
            {
                CompleteParticipantUpdate(ParticipantUpdateResult.Failed, callback, resultCallback);
                yield break;
            }

            if (!isPicked)
            {
                string insta = person.fields.insta?.stringValue ?? person.fields.instaId?.stringValue;
                ClaimReceipt claim = null;
                ParticipantUpdateResult claimStatus = ParticipantUpdateResult.Failed;
                yield return ReadProfileClaimForParticipant(insta, documentId,
                    (value, status) => { claim = value; claimStatus = status; });
                if (claimStatus != ParticipantUpdateResult.Saved)
                {
                    CompleteParticipantUpdate(claimStatus, callback, resultCallback);
                    yield break;
                }
                if (claim != null)
                {
                    string fieldsJson = JsonUtility.ToJson(new FirestoreUpdateFields {
                        isPicked = new FirestoreBoolField(false)
                    });
                    bool saved = false;
                    yield return CommitParticipantUpdateAndReleaseClaim(documentId, insta,
                        person.updateTime, claim.updateTime, fieldsJson, "[\"isPicked\"]",
                        success => saved = success);
                    CompleteParticipantUpdate(saved ? ParticipantUpdateResult.Saved : ParticipantUpdateResult.Failed,
                        callback, resultCallback);
                    yield break;
                }
            }

            // updateMask 쿼리 파라미터를 사용해 오직 'isPicked' 필드만 교체(PATCH)
            string url = root + "/Participants/" + Uri.EscapeDataString(documentId) +
                "?updateMask.fieldPaths=isPicked&currentDocument.updateTime=" + Uri.EscapeDataString(person.updateTime);

            // PATCH 바디용 데이터 빌드
            FirestoreUpdateDocument patchDoc = new FirestoreUpdateDocument();
            patchDoc.fields = new FirestoreUpdateFields();
            patchDoc.fields.isPicked = new FirestoreBoolField(isPicked);

            string jsonPayload = JsonUtility.ToJson(patchDoc);

            BeginWriteOperation();
            using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return SendAuthorized(request);
                EndWriteOperation();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[Firebase] 문서({documentId}) isPicked={isPicked} 업데이트 완료");
                    CompleteParticipantUpdate(ParticipantUpdateResult.Saved, callback, resultCallback);
                }
                else
                {
                    Debug.LogError($"[Firebase] 문서 업데이트 실패: {request.error}\n응답: {request.downloadHandler.text}");
                    CompleteParticipantUpdate(ParticipantUpdateResult.Failed, callback, resultCallback);
                }
            }
        }

        private static void CompleteParticipantUpdate(ParticipantUpdateResult result, Action<bool> callback,
            Action<ParticipantUpdateResult> resultCallback)
        {
            resultCallback?.Invoke(result);
            callback?.Invoke(result == ParticipantUpdateResult.Saved);
        }

        /// <summary>현재 Participants 컬렉션의 문서 수를 집계합니다. 실패하면 null을 반환합니다.</summary>
        public IEnumerator GetParticipantCount(Action<int?> callback)
        {
            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                callback?.Invoke(null);
                yield break;
            }

            string url = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents:runAggregationQuery";
            const string body = "{\"structuredAggregationQuery\":{\"aggregations\":[{\"count\":{},\"alias\":\"count\"}]," +
                "\"structuredQuery\":{\"from\":[{\"collectionId\":\"Participants\"}]}}}";
            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return SendAuthorized(request);
                if (request.responseCode != 200)
                {
                    Debug.LogError($"[Firebase] 참가자 수 조회 실패: HTTP {request.responseCode}, {request.error}");
                    callback?.Invoke(null);
                    yield break;
                }

                try
                {
                    var data = JsonUtility.FromJson<AggregateItems>("{\"items\":" + request.downloadHandler.text + "}");
                    if (data?.items != null && data.items.Length > 0 &&
                        int.TryParse(data.items[0]?.result?.aggregateFields?.count?.integerValue, out int count))
                    {
                        callback?.Invoke(count);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Firebase] 참가자 수 파싱 실패: {ex.Message}");
                }
            }
            callback?.Invoke(null);
        }

        /// <summary>
        /// 모든 참가자 데이터를 조회하여 리스트로 반환합니다.
        /// </summary>
        public IEnumerator GetAllParticipants(Action<List<ParticipantData>> callback)
        {
            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                callback?.Invoke(new List<ParticipantData>());
                yield break;
            }

            string baseUrl = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/Participants";
            string currentUrl = baseUrl + "?pageSize=300";
            List<ParticipantData> result = new List<ParticipantData>();

            while (!string.IsNullOrEmpty(currentUrl))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(currentUrl))
                {
                    yield return SendAuthorized(request);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[Firebase] 전체 문서 가져오기 실패: {request.error}");
                        callback?.Invoke(result);
                        yield break;
                    }

                    string rawJson = request.downloadHandler.text;
                    ListDocumentsResponse responseList = null;
                    try
                    {
                        responseList = JsonUtility.FromJson<ListDocumentsResponse>(rawJson);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Firebase] 파싱 에러: {ex.Message}");
                        callback?.Invoke(result);
                        yield break;
                    }

                    if (responseList != null && responseList.documents != null)
                    {
                        foreach (var doc in responseList.documents)
                        {
                            if (doc == null || string.IsNullOrEmpty(doc.name)) continue;

                            string docPath = doc.name;
                            string docId = docPath.Substring(docPath.LastIndexOf('/') + 1);
                            int attemptsVal = 0;
                            if (doc.fields != null && doc.fields.attempts != null && !string.IsNullOrEmpty(doc.fields.attempts.integerValue))
                            {
                                int.TryParse(doc.fields.attempts.integerValue, out attemptsVal);
                            }

                            if (doc.fields != null)
                            {
                                result.Add(new ParticipantData
                                {
                                    documentId = docId,
                                    name = doc.fields.name?.stringValue ?? "",
                                    insta = doc.fields.insta?.stringValue ?? "",
                                    bio = doc.fields.bio?.stringValue ?? "",
                                    gender = doc.fields.gender?.stringValue ?? "",
                                    isPicked = doc.fields.isPicked?.booleanValue ?? false,
                                    attempts = attemptsVal
                                });
                            }
                        }
                    }

                    if (responseList != null && !string.IsNullOrEmpty(responseList.nextPageToken))
                    {
                        currentUrl = baseUrl + "?pageSize=300&pageToken=" + responseList.nextPageToken;
                    }
                    else
                    {
                        currentUrl = null;
                    }
                }
            }
            callback?.Invoke(result);
        }

        /// <summary>
        /// 참가자의 편집 가능한 필드를 갱신합니다. 뽑힘 해제 시 지급 잠금도 함께 삭제합니다.
        /// </summary>
        public IEnumerator UpdateParticipantFullData(ParticipantData data, Action<bool> callback,
            Action<ParticipantUpdateResult> resultCallback = null)
        {
            if (string.IsNullOrEmpty(firebaseProjectId) || string.IsNullOrEmpty(data.documentId))
            {
                CompleteParticipantUpdate(ParticipantUpdateResult.Failed, callback, resultCallback);
                yield break;
            }

            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            FirestoreDocumentResponse current = null;
            using (var get = UnityWebRequest.Get(root + "/Participants/" + Uri.EscapeDataString(data.documentId)))
            {
                yield return SendAuthorized(get);
                if (get.responseCode == 200)
                    try { current = JsonUtility.FromJson<FirestoreDocumentResponse>(get.downloadHandler.text); }
                    catch (Exception) { }
            }
            if (string.IsNullOrEmpty(current?.updateTime) || current.fields == null)
            {
                CompleteParticipantUpdate(ParticipantUpdateResult.Failed, callback, resultCallback);
                yield break;
            }

            ClaimReceipt existingClaim = null;
            string currentInsta = current.fields.insta?.stringValue ?? current.fields.instaId?.stringValue;
            if (!data.isPicked)
            {
                ParticipantUpdateResult claimStatus = ParticipantUpdateResult.Failed;
                yield return ReadProfileClaimForParticipant(currentInsta, data.documentId,
                    (value, status) => { existingClaim = value; claimStatus = status; });
                if (claimStatus != ParticipantUpdateResult.Saved)
                {
                    CompleteParticipantUpdate(claimStatus, callback, resultCallback);
                    yield break;
                }
                if (existingClaim != null && current.fields.isPicked?.booleanValue != true)
                {
                    // 이미 어긋난 데이터의 일반 필드 저장이 지급 잠금을 암묵적으로 해제하지 않도록 합니다.
                    CompleteParticipantUpdate(ParticipantUpdateResult.ProfileClaimLocked, callback, resultCallback);
                    yield break;
                }
                if (!string.IsNullOrWhiteSpace(data.insta) && data.insta != currentInsta)
                {
                    bool? newHandleClaimed = null;
                    yield return GetProfileClaimState(data.insta, value => newHandleClaimed = value);
                    if (!newHandleClaimed.HasValue)
                    { CompleteParticipantUpdate(ParticipantUpdateResult.Failed, callback, resultCallback); yield break; }
                    if (newHandleClaimed.Value)
                    { CompleteParticipantUpdate(ParticipantUpdateResult.ProfileClaimLocked, callback, resultCallback); yield break; }
                }
            }

            string url = root + "/Participants/" + Uri.EscapeDataString(data.documentId) +
                "?updateMask.fieldPaths=name&updateMask.fieldPaths=insta&updateMask.fieldPaths=bio" +
                "&updateMask.fieldPaths=gender&updateMask.fieldPaths=isPicked&updateMask.fieldPaths=attempts" +
                "&currentDocument.updateTime=" + Uri.EscapeDataString(current.updateTime);

            var fields = new RegistrationFields {
                name = new FirestoreStringField(data.name), insta = new FirestoreStringField(data.insta),
                bio = new FirestoreStringField(data.bio), gender = new FirestoreStringField(data.gender),
                isPicked = new FirestoreBoolField(data.isPicked), attempts = new FirestoreIntField(data.attempts)
            };
            string jsonPayload = "{\"fields\":" + JsonUtility.ToJson(fields) + "}";

            if (existingClaim != null)
            {
                bool saved = false;
                yield return CommitParticipantUpdateAndReleaseClaim(data.documentId, currentInsta,
                    current.updateTime, existingClaim.updateTime, JsonUtility.ToJson(fields),
                    "[\"name\",\"insta\",\"bio\",\"gender\",\"isPicked\",\"attempts\"]",
                    success => saved = success);
                CompleteParticipantUpdate(saved ? ParticipantUpdateResult.Saved : ParticipantUpdateResult.Failed,
                    callback, resultCallback);
                yield break;
            }

            BeginWriteOperation();
            using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return SendAuthorized(request);
                EndWriteOperation();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[Firebase] 데이터 업데이트 성공: {data.documentId}");
                    CompleteParticipantUpdate(ParticipantUpdateResult.Saved, callback, resultCallback);
                }
                else
                {
                    string response = request.downloadHandler?.text ?? "";
                    if (response.Length > 1000) response = response.Substring(0, 1000) + "...";
                    Debug.LogError($"[Firebase] 데이터 업데이트 실패: HTTP {request.responseCode}, {request.error}. 응답: {response}");
                    CompleteParticipantUpdate(ParticipantUpdateResult.Failed, callback, resultCallback);
                }
            }
        }

        /// <summary>
        /// 참가자와 연결 인덱스·프로필 잠금을 한 번에 삭제합니다.
        /// </summary>
        public IEnumerator DeleteParticipant(string documentId, Action<bool> callback)
        {
            if (string.IsNullOrEmpty(firebaseProjectId) || string.IsNullOrEmpty(documentId) || documentId.Contains("/"))
            {
                callback?.Invoke(false);
                yield break;
            }

            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            string prefix = $"projects/{firebaseProjectId}/databases/(default)/documents/";
            FirestoreDocument participant = null;
            using (var get = UnityWebRequest.Get(root + "/Participants/" + Uri.EscapeDataString(documentId)))
            {
                yield return SendAuthorized(get);
                if (get.responseCode == 200)
                { try { participant = JsonUtility.FromJson<FirestoreDocument>(get.downloadHandler.text); } catch (Exception) { } }
            }
            if (string.IsNullOrEmpty(participant?.updateTime) ||
                !TryNormalizeInstaId(participant.fields?.insta?.stringValue ?? participant.fields?.instaId?.stringValue, out string handle))
            { callback?.Invoke(false); yield break; }

            var writes = new List<DeleteWrite> {
                new DeleteWrite { delete = prefix + "Participants/" + documentId,
                    currentDocument = new DeletePrecondition { updateTime = participant.updateTime } }
            };
            using (var get = UnityWebRequest.Get(root + "/ParticipantKeys/insta_" + handle))
            {
                yield return SendAuthorized(get);
                if (get.responseCode == 200)
                {
                    ParticipantKeyDocument index = null;
                    try { index = JsonUtility.FromJson<ParticipantKeyDocument>(get.downloadHandler.text); } catch (Exception) { }
                    if (string.IsNullOrEmpty(index?.updateTime) || index.fields?.participantKey?.stringValue != documentId)
                    { callback?.Invoke(false); yield break; }
                    writes.Add(new DeleteWrite { delete = prefix + "ParticipantKeys/insta_" + handle,
                        currentDocument = new DeletePrecondition { updateTime = index.updateTime } });
                }
                else if (get.responseCode != 404) { callback?.Invoke(false); yield break; }
            }
            using (var get = UnityWebRequest.Get(root + "/ProfileClaims/insta_" + handle))
            {
                yield return SendAuthorized(get);
                if (get.responseCode == 200)
                {
                    ClaimReceipt claim = null;
                    try { claim = JsonUtility.FromJson<ClaimReceipt>(get.downloadHandler.text); } catch (Exception) { }
                    if (string.IsNullOrEmpty(claim?.updateTime) || claim.fields?.targetKey?.stringValue != documentId)
                    { callback?.Invoke(false); yield break; }
                    writes.Add(new DeleteWrite { delete = prefix + "ProfileClaims/insta_" + handle,
                        currentDocument = new DeletePrecondition { updateTime = claim.updateTime } });
                }
                else if (get.responseCode != 404) { callback?.Invoke(false); yield break; }
            }

            string payload = JsonUtility.ToJson(new DeleteCommit { writes = writes.ToArray() });
            BeginWriteOperation();
            using (var request = new UnityWebRequest(root + ":commit", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return SendAuthorized(request);
                bool success = request.responseCode == 200;
                if (!success)
                {
                    // 응답만 유실되었다면 실제 삭제 상태를 확인해 같은 작업을 실패로 표시하지 않습니다.
                    success = true;
                    foreach (DeleteWrite write in writes)
                    {
                        using (var verify = UnityWebRequest.Get(root + "/" + write.delete.Substring(prefix.Length)))
                        {
                            yield return SendAuthorized(verify);
                            if (verify.responseCode != 404) { success = false; break; }
                        }
                    }
                }
                EndWriteOperation();
                if (!success) Debug.LogError($"[Firebase] 참가자 및 연결 문서 삭제 실패: HTTP {request.responseCode}");
                callback?.Invoke(success);
            }
        }

        // =========================================================================
        // GameState Stats API
        // =========================================================================

        /// <summary>
        /// GameState/stats 문서에서 실물 인형 개수(totalDolls)를 가져옵니다.
        /// </summary>
        public IEnumerator GetTotalDolls(Action<int> callback)
        {
            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                callback?.Invoke(-1);
                yield break;
            }

            string getUrl = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/GameState/stats";
            using (UnityWebRequest request = UnityWebRequest.Get(getUrl))
            {
                yield return SendAuthorized(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Firebase] GameState/stats 조회 실패 (초기값이 없을 수 있음): {request.error}");
                    callback?.Invoke(-1);
                    yield break;
                }

                string rawJson = request.downloadHandler.text;
                try
                {
                    var doc = JsonUtility.FromJson<GameStateDocument>(rawJson);
                    if (doc != null && doc.fields != null && doc.fields.totalDolls != null && !string.IsNullOrEmpty(doc.fields.totalDolls.integerValue))
                    {
                        if (int.TryParse(doc.fields.totalDolls.integerValue, out int count))
                        {
                            callback?.Invoke(count);
                            yield break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Firebase] totalDolls 파싱 에러: {ex.Message}");
                }
                
                callback?.Invoke(-1);
            }
        }

        /// <summary>
        /// GameState/stats 문서의 totalDolls 필드를 갱신합니다.
        /// </summary>
        public IEnumerator ClaimPrize(string roundId, bool legendary, Action<bool> callback)
        {
            string field = legendary ? "totalLegendaryDolls" : "totalDolls";
            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            string receipt = "InventoryChanges/love_" + roundId;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                using (var check = UnityWebRequest.Get(root + "/" + receipt))
                {
                    yield return SendAuthorized(check);
                    if (check.responseCode == 200)
                    {
                        ClaimReceipt saved = null;
                        try { saved = JsonUtility.FromJson<ClaimReceipt>(check.downloadHandler.text); } catch { }
                        callback?.Invoke(saved?.fields?.stockField?.stringValue == field);
                        yield break;
                    }
                    if (check.responseCode != 404) { callback?.Invoke(false); yield break; }
                }
                GameStateDocument stats = null;
                using (var get = UnityWebRequest.Get(root + "/GameState/stats"))
                {
                    yield return SendAuthorized(get);
                    if (get.responseCode == 200)
                        try { stats = JsonUtility.FromJson<GameStateDocument>(get.downloadHandler.text); } catch { }
                }
                int count, successes;
                if (stats?.fields == null || string.IsNullOrEmpty(stats.updateTime) ||
                    !int.TryParse((legendary ? stats.fields.totalLegendaryDolls : stats.fields.totalDolls)?.integerValue, out count) || count <= 0 ||
                    !int.TryParse(stats.fields.totalSuccesses?.integerValue, out successes) || successes == int.MaxValue)
                { callback?.Invoke(false); yield break; }
                string path = $"projects/{firebaseProjectId}/databases/(default)/documents/";
                string body = "{\"writes\":[{\"update\":{\"name\":\"" + path + receipt +
                    "\",\"fields\":{\"stockField\":{\"stringValue\":\"" + field +
                    "\"}}},\"currentDocument\":{\"exists\":false}},{\"update\":{\"name\":\"" + path +
                    "GameState/stats\",\"fields\":{\"" + field + "\":{\"integerValue\":\"" + (count - 1) +
                    "\"},\"totalSuccesses\":{\"integerValue\":\"" + (successes + 1) +
                    "\"}}},\"updateMask\":{\"fieldPaths\":[\"" + field +
                    "\",\"totalSuccesses\"]},\"currentDocument\":{\"updateTime\":\"" + stats.updateTime + "\"}}]}";
                using (var commit = new UnityWebRequest(root + ":commit", "POST"))
                {
                    commit.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    commit.downloadHandler = new DownloadHandlerBuffer();
                    commit.SetRequestHeader("Content-Type", "application/json");
                    yield return SendAuthorized(commit);
                    if (commit.responseCode == 200) { callback?.Invoke(true); yield break; }
                    if (commit.responseCode != 409 && commit.responseCode != 412 && commit.responseCode != 503 && commit.responseCode != 0)
                    { callback?.Invoke(false); yield break; }
                }
            }
            callback?.Invoke(false);
        }

        public IEnumerator UpdateTotalDolls(int count, Action<bool> callback)
        {
            yield return SetInventory("totalDolls", count, callback);
        }

        public IEnumerator UpdateTotalLegendaryDolls(int count, Action<bool> callback)
        {
            yield return SetInventory("totalLegendaryDolls", count, callback);
        }

        private IEnumerator SetInventory(string field, int count, Action<bool> callback)
        {
            if (count < 0 || BoothStaffAuth.Instance == null || !BoothStaffAuth.Instance.IsAdmin)
            { callback?.Invoke(false); yield break; }
            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            string prefix = $"projects/{firebaseProjectId}/databases/(default)/documents/";
            string id = Guid.NewGuid().ToString("N");
            string receipt = "InventoryChanges/love_admin_" + id;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using (var check = UnityWebRequest.Get(root + "/" + receipt))
                {
                    yield return SendAuthorized(check);
                    if (check.responseCode == 200) { callback?.Invoke(true); yield break; }
                    if (check.responseCode != 404) { callback?.Invoke(false); yield break; }
                }
                GameStateDocument stats = null;
                using (var get = UnityWebRequest.Get(root + "/GameState/stats"))
                {
                    yield return SendAuthorized(get);
                    if (get.responseCode == 200)
                        try { stats = JsonUtility.FromJson<GameStateDocument>(get.downloadHandler.text); } catch { }
                }
                if (string.IsNullOrEmpty(stats?.updateTime)) { callback?.Invoke(false); yield break; }
                string body = "{\"writes\":[{\"update\":{\"name\":\"" + prefix + receipt +
                    "\",\"fields\":{\"stockField\":{\"stringValue\":\"" + field +
                    "\"}}},\"currentDocument\":{\"exists\":false}},{\"update\":{\"name\":\"" + prefix +
                    "GameState/stats\",\"fields\":{\"" + field + "\":{\"integerValue\":\"" + count +
                    "\"}}},\"updateMask\":{\"fieldPaths\":[\"" + field +
                    "\"]},\"currentDocument\":{\"updateTime\":\"" + stats.updateTime + "\"}}]}";
                using (var commit = new UnityWebRequest(root + ":commit", "POST"))
                {
                    commit.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    commit.downloadHandler = new DownloadHandlerBuffer();
                    commit.SetRequestHeader("Content-Type", "application/json");
                    yield return SendAuthorized(commit);
                    if (commit.responseCode == 200) { callback?.Invoke(true); yield break; }
                    if (commit.responseCode != 0 && commit.responseCode != 503)
                    { callback?.Invoke(false); yield break; }
                }
            }
            callback?.Invoke(false);
        }

        private IEnumerator UnsafeUpdateTotalDollsLegacy(int count, Action<bool> callback)
        {
            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                callback?.Invoke(false);
                yield break;
            }

            string url = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/GameState/stats?updateMask.fieldPaths=totalDolls";

            string jsonPayload = $"{{\"fields\":{{\"totalDolls\":{{\"integerValue\":\"{count}\"}}}}}}";

            BeginWriteOperation();
            using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return SendAuthorized(request);
                EndWriteOperation();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[Firebase] 남은 인형 개수 업데이트 성공: {count}개");
                    callback?.Invoke(true);
                }
                else
                {
                    Debug.LogError($"[Firebase] 남은 인형 개수 업데이트 실패: {request.error}");
                    callback?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// GameState/stats 문서에서 레전더리 재고(totalLegendaryDolls)를 가져옵니다.
        /// </summary>
        public IEnumerator GetTotalLegendaryDolls(Action<int> callback)
        {
            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                callback?.Invoke(-1);
                yield break;
            }

            string getUrl = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/GameState/stats";
            using (UnityWebRequest request = UnityWebRequest.Get(getUrl))
            {
                yield return SendAuthorized(request);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Firebase] 레전더리 재고 조회 실패: {request.error}");
                    callback?.Invoke(-1);
                    yield break;
                }

                try
                {
                    var doc = JsonUtility.FromJson<GameStateDocument>(request.downloadHandler.text);
                    if (doc?.fields?.totalLegendaryDolls != null &&
                        int.TryParse(doc.fields.totalLegendaryDolls.integerValue, out int count))
                    {
                        PlayerPrefs.SetInt("Stats_TotalLegendaryDolls", count);
                        PlayerPrefs.Save();
                        callback?.Invoke(count);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Firebase] totalLegendaryDolls 파싱 에러: {ex.Message}");
                }

                callback?.Invoke(-1);
            }
        }

        /// <summary>
        /// GameState/stats 문서의 레전더리 재고를 별도 필드로 저장합니다.
        /// </summary>
        private IEnumerator UnsafeUpdateTotalLegendaryDollsLegacy(int count, Action<bool> callback)
        {
            count = Mathf.Max(0, count);
            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                callback?.Invoke(false);
                yield break;
            }

            string url = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/GameState/stats?updateMask.fieldPaths=totalLegendaryDolls";
            string jsonPayload = $"{{\"fields\":{{\"totalLegendaryDolls\":{{\"integerValue\":\"{count}\"}}}}}}";
            BeginWriteOperation();
            using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return SendAuthorized(request);
                EndWriteOperation();

                bool success = request.result == UnityWebRequest.Result.Success;
                if (success) Debug.Log($"[Firebase] 남은 레전더리 개수 업데이트 성공: {count}개");
                else Debug.LogError($"[Firebase] 레전더리 개수 업데이트 실패: {request.error}");
                callback?.Invoke(success);
            }
        }

        // =========================================================================
        // GameState Stats API - Extended Metrics
        // =========================================================================

        /// <summary>
        /// GameState/stats 문서에서 모든 통계 데이터를 가져옵니다.
        /// </summary>
        public IEnumerator GetGameStats(Action<GameStatsData> callback)
        {
            GameStatsData defaultStats = new GameStatsData
            {
                totalDolls = -1,
                totalLegendaryDolls = -1,
                totalRevenue = -1,
                totalRegistrations = -1,
                totalPlays = -1,
                totalSuccesses = -1
            };

            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                callback?.Invoke(defaultStats);
                yield break;
            }

            string getUrl = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/GameState/stats";
            using (UnityWebRequest request = UnityWebRequest.Get(getUrl))
            {
                yield return SendAuthorized(request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Firebase] GameState/stats 조회 실패 (초기값이 없을 수 있음): {request.error}");
                    callback?.Invoke(defaultStats);
                    yield break;
                }

                string rawJson = request.downloadHandler.text;
                try
                {
                    var doc = JsonUtility.FromJson<GameStateDocument>(rawJson);
                    if (doc != null && doc.fields != null)
                    {
                        GameStatsData stats = new GameStatsData();
                        
                        stats.totalDolls = (doc.fields.totalDolls != null && int.TryParse(doc.fields.totalDolls.integerValue, out int td)) ? td : -1;
                        stats.totalLegendaryDolls = (doc.fields.totalLegendaryDolls != null && int.TryParse(doc.fields.totalLegendaryDolls.integerValue, out int tld)) ? tld : -1;
                        stats.totalRevenue = (doc.fields.totalRevenue != null && int.TryParse(doc.fields.totalRevenue.integerValue, out int tr)) ? tr : -1;
                        stats.totalRegistrations = (doc.fields.totalRegistrations != null && int.TryParse(doc.fields.totalRegistrations.integerValue, out int treg)) ? treg : -1;
                        stats.totalPlays = (doc.fields.totalPlays != null && int.TryParse(doc.fields.totalPlays.integerValue, out int tp)) ? tp : -1;
                        stats.totalSuccesses = (doc.fields.totalSuccesses != null && int.TryParse(doc.fields.totalSuccesses.integerValue, out int ts)) ? ts : -1;

                        PlayerPrefs.SetInt("Stats_TotalDolls", stats.totalDolls);
                        PlayerPrefs.SetInt("Stats_TotalLegendaryDolls", stats.totalLegendaryDolls);
                        PlayerPrefs.SetInt("Stats_TotalRevenue", stats.totalRevenue);
                        PlayerPrefs.SetInt("Stats_TotalRegistrations", stats.totalRegistrations);
                        PlayerPrefs.SetInt("Stats_TotalPlays", stats.totalPlays);
                        PlayerPrefs.SetInt("Stats_TotalSuccesses", stats.totalSuccesses);
                        PlayerPrefs.Save();

                        callback?.Invoke(stats);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Firebase] GameStats 파싱 에러: {ex.Message}");
                }
                
                callback?.Invoke(defaultStats);
            }
        }

        /// <summary>
        /// GameState/stats 문서의 특정 필드들을 업데이트합니다.
        /// </summary>
        public IEnumerator UpdateGameStats(GameStatsData stats, List<string> fieldsToUpdate, Action<bool> callback)
        {
            if (fieldsToUpdate == null || fieldsToUpdate.Count == 0 ||
                stats.totalDolls < 0 || stats.totalLegendaryDolls < 0 || stats.totalRevenue < 0 ||
                stats.totalRegistrations < 0 || stats.totalPlays < 0 || stats.totalSuccesses < 0 ||
                BoothStaffAuth.Instance == null || !BoothStaffAuth.Instance.IsAdmin)
            {
                callback?.Invoke(false);
                yield break;
            }

            if (string.IsNullOrEmpty(firebaseProjectId))
            {
                callback?.Invoke(false);
                yield break;
            }

            GameStateDocument current = null;
            string statsUrl = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/GameState/stats";
            using (var get = UnityWebRequest.Get(statsUrl))
            {
                yield return SendAuthorized(get);
                if (get.responseCode == 200)
                    try { current = JsonUtility.FromJson<GameStateDocument>(get.downloadHandler.text); } catch { }
            }
            if (string.IsNullOrEmpty(current?.updateTime)) { callback?.Invoke(false); yield break; }

            StringBuilder maskBuilder = new StringBuilder();
            StringBuilder fieldsBuilder = new StringBuilder();

            fieldsBuilder.Append("{");
            for (int i = 0; i < fieldsToUpdate.Count; i++)
            {
                string fieldName = fieldsToUpdate[i];
                maskBuilder.Append($"updateMask.fieldPaths={fieldName}");
                if (i < fieldsToUpdate.Count - 1)
                {
                    maskBuilder.Append("&");
                }

                int val = 0;
                if (fieldName == "totalDolls") val = stats.totalDolls;
                else if (fieldName == "totalLegendaryDolls") val = stats.totalLegendaryDolls;
                else if (fieldName == "totalRevenue") val = stats.totalRevenue;
                else if (fieldName == "totalRegistrations") val = stats.totalRegistrations;
                else if (fieldName == "totalPlays") val = stats.totalPlays;
                else if (fieldName == "totalSuccesses") val = stats.totalSuccesses;

                fieldsBuilder.Append($"\"{fieldName}\":{{\"integerValue\":\"{val}\"}}");
                if (i < fieldsToUpdate.Count - 1)
                {
                    fieldsBuilder.Append(",");
                }
            }
            fieldsBuilder.Append("}");

            string url = statsUrl + "?" + maskBuilder + "&currentDocument.updateTime=" + Uri.EscapeDataString(current.updateTime);
            string jsonPayload = $"{{\"fields\":{fieldsBuilder.ToString()}}}";

            BeginWriteOperation();
            using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return SendAuthorized(request);
                EndWriteOperation();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[Firebase] 통계 업데이트 성공: {fieldsBuilder.ToString()}");
                    callback?.Invoke(true);
                }
                else
                {
                    Debug.LogError($"[Firebase] 통계 업데이트 실패: {request.error}\n응답: {request.downloadHandler.text}");
                    callback?.Invoke(false);
                }
            }
        }

        public void IncrementPlayCountAndRevenue(int revenue, string insta, string roundId,
            Action<bool, string> callback = null)
        {
            StartCoroutine(IncrementPlayAndRevenueCoroutine(revenue, insta, roundId, callback));
        }

        private IEnumerator IncrementPlayAndRevenueCoroutine(int revenue, string insta, string roundId,
            Action<bool, string> callback)
        {
            bool isGuest = string.IsNullOrWhiteSpace(insta);
            string handle = (insta ?? "").Trim().TrimStart('@').ToLowerInvariant();
            string receiptKind = isGuest ? "love_guest" : "love";
            if (revenue < 0 || (!isGuest && !System.Text.RegularExpressions.Regex.IsMatch(handle, "^[a-z0-9._]{1,30}$")) ||
                !System.Text.RegularExpressions.Regex.IsMatch(roundId ?? "", "^[a-f0-9]{32}$") ||
                string.IsNullOrEmpty(firebaseProjectId))
            { callback?.Invoke(false, "참가자 또는 Firebase 설정을 확인해 주세요."); yield break; }

            string root = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents";
            string prefix = $"projects/{firebaseProjectId}/databases/(default)/documents/";
            string receiptPath = "GameRounds/love_" + roundId;
            using (var existing = UnityWebRequest.Get(root + "/" + receiptPath))
            {
                yield return SendAuthorized(existing);
                if (existing.responseCode == 200)
                {
                    PlayReceiptDocument saved = null;
                    try { saved = JsonUtility.FromJson<PlayReceiptDocument>(existing.downloadHandler.text); }
                    catch (Exception) { }
                    bool same = saved?.fields?.kind?.stringValue == receiptKind &&
                        saved.fields.insta?.stringValue == handle &&
                        saved.fields.revenue?.integerValue == revenue.ToString();
                    callback?.Invoke(same, same ? null : "회차 기록 충돌: " + roundId);
                    yield break;
                }
                if (existing.responseCode != 404)
                { callback?.Invoke(false, "회차 조회 실패: " + roundId); yield break; }
            }
            string participantKey = "";
            // 첫 플레이는 UI의 등록 요청과 함께 시작될 수 있으므로 인덱스가 생길 때까지
            // 코루틴으로만 기다립니다. 메인 스레드를 막거나 임의의 프로필을 만들지 않습니다.
            for (int attempt = 0; !isGuest && attempt < 20; attempt++)
            {
                using (var index = UnityWebRequest.Get(root + "/ParticipantKeys/insta_" + handle))
                {
                    yield return SendAuthorized(index);
                    if (index.responseCode == 200)
                    {
                        try
                        {
                            participantKey = JsonUtility.FromJson<ParticipantKeyDocument>(index.downloadHandler.text)
                                ?.fields?.participantKey?.stringValue;
                        }
                        catch (Exception) { participantKey = null; }
                        if (string.IsNullOrWhiteSpace(participantKey) ||
                            !System.Text.RegularExpressions.Regex.IsMatch(participantKey, "^[a-zA-Z0-9._-]{1,1500}$"))
                        { callback?.Invoke(false, "참가자 인덱스 확인 필요: " + roundId); yield break; }
                        bool valid = false;
                        yield return ValidateParticipantKey(index.downloadHandler.text, handle, result => valid = result);
                        if (!valid)
                        { callback?.Invoke(false, "참가자 문서 확인 필요: " + roundId); yield break; }
                        break;
                    }
                    if (index.responseCode != 404)
                    { callback?.Invoke(false, "참가자 조회 실패: " + roundId); yield break; }
                }
                yield return new WaitForSecondsRealtime(1f);
            }
            if (!isGuest && string.IsNullOrEmpty(participantKey))
            { callback?.Invoke(false, "참가자 등록 확인 필요: " + roundId); yield break; }

            string receiptFields = "\"kind\":{\"stringValue\":\"" + receiptKind + "\"},\"participantKey\":{\"stringValue\":\"" +
                participantKey + "\"},\"insta\":{\"stringValue\":\"" + handle +
                "\"},\"revenue\":{\"integerValue\":\"" + revenue + "\"}";
            string participantWrite = isGuest ? "" :
                ", {\"transform\":{\"document\":\"" + prefix + "Participants/" + participantKey +
                "\",\"fieldTransforms\":[{\"fieldPath\":\"attempts\",\"increment\":{\"integerValue\":\"1\"}}]}," +
                "\"currentDocument\":{\"exists\":true}}";
            string payload = "{\"writes\":[{\"update\":{\"name\":\"" + prefix + receiptPath +
                "\",\"fields\":{" + receiptFields + "}},\"currentDocument\":{\"exists\":false}}," +
                "{\"transform\":{\"document\":\"" + prefix + "GameState/stats\",\"fieldTransforms\":[" +
                "{\"fieldPath\":\"totalPlays\",\"increment\":{\"integerValue\":\"1\"}}," +
                "{\"fieldPath\":\"totalRevenue\",\"increment\":{\"integerValue\":\"" + revenue + "\"}}]}," +
                "\"currentDocument\":{\"exists\":true}}" + participantWrite + "]}";
            using (var request = new UnityWebRequest(root + ":commit", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return SendAuthorized(request);
                if (request.responseCode == 200) { callback?.Invoke(true, null); yield break; }
                using (var check = UnityWebRequest.Get(root + "/" + receiptPath))
                {
                    yield return SendAuthorized(check);
                    if (check.responseCode == 200)
                    {
                        PlayReceiptDocument saved = null;
                        try { saved = JsonUtility.FromJson<PlayReceiptDocument>(check.downloadHandler.text); }
                        catch (Exception) { }
                        if (saved?.fields?.kind?.stringValue == receiptKind &&
                            saved.fields.participantKey?.stringValue == participantKey &&
                            saved.fields.insta?.stringValue == handle &&
                            saved.fields.revenue?.integerValue == revenue.ToString())
                        { callback?.Invoke(true, null); yield break; }
                    }
                }
                callback?.Invoke(false, "회차 기록 확인 필요: " + roundId);
            }
        }

    }

    // =========================================================================
    // REST API JSON 직렬화용 데이터 세트 (JsonUtility 규격 준수 - null 비교 및 안전성을 위해 class로 선언)
    // =========================================================================

    [Serializable]
    public class FirestoreDocument
    {
        public string name, updateTime;
        public FirestoreFields fields;
    }

    [Serializable]
    public class FirestoreFields
    {
        public FirestoreStringField name;
        public FirestoreStringField insta;
        public FirestoreStringField instaId;
        public FirestoreStringField bio;
        public FirestoreStringField gender;
        public FirestoreBoolField isPicked;
        public FirestoreIntField attempts;
    }

    [Serializable]
    public class GameStateDocument
    {
        public string updateTime;
        public GameStateFields fields;
    }

    [Serializable]
    public class GameStateFields
    {
        public FirestoreIntField totalDolls;
        public FirestoreIntField totalLegendaryDolls;
        public FirestoreIntField totalRevenue;
        public FirestoreIntField totalRegistrations;
        public FirestoreIntField totalPlays;
        public FirestoreIntField totalSuccesses;
    }

    [Serializable]
    public class FirestoreUpdateDocument
    {
        public FirestoreUpdateFields fields;
    }

    [Serializable]
    public class FirestoreUpdateFields
    {
        public FirestoreBoolField isPicked;
    }

    [Serializable]
    public class FirestoreStringField
    {
        public string stringValue;
        public FirestoreStringField() { }
        public FirestoreStringField(string val) => stringValue = val;
    }

    [Serializable]
    public class FirestoreBoolField
    {
        public bool booleanValue;
        public FirestoreBoolField() { }
        public FirestoreBoolField(bool val) => booleanValue = val;
    }

    [Serializable]
    public class FirestoreIntField
    {
        public string integerValue; // Firestore API 상에서 int형도 string 형태 전송
        public FirestoreIntField() { }
        public FirestoreIntField(int val) => integerValue = val.ToString();
    }

    // =========================================================================
    // :runQuery Array 응답 파싱용 데이터 세트 (JsonUtility 규격 준수 - null 비교 및 안전성을 위해 class로 선언)
    // =========================================================================

    [Serializable]
    public class RunQueryResponseList
    {
        public RunQueryResponseItem[] items;
    }

    [Serializable]
    public class RunQueryResponseItem
    {
        public FirestoreDocumentResponse document;
    }

    [Serializable]
    public class FirestoreDocumentResponse
    {
        public string name; // projects/{projectId}/databases/(default)/documents/Participants/{documentId}
        public string updateTime;
        public FirestoreFields fields;
    }

    // =========================================================================
    // :List Documents 응답 파싱용 데이터 세트
    // =========================================================================

    [Serializable]
    public class ListDocumentsResponse
    {
        public FirestoreDocumentResponse[] documents;
        public string nextPageToken;
    }

    // =========================================================================
    // 행사 통계용 구조체
    // =========================================================================
    public struct GameStatsData
    {
        public int totalDolls;
        public int totalLegendaryDolls;
        public int totalRevenue;
        public int totalRegistrations;
        public int totalPlays;
        public int totalSuccesses;
    }

    // =========================================================================
    // 콜백 연동용 최종 클린 매칭 구조체
    // =========================================================================

    public struct MatchedProfileResponse
    {
        public bool success;
        public bool querySucceeded;
        public string documentId;
        public string name;
        public string gender;
        public string insta;
        public string bio;
    }

    // =========================================================================
    // DB 뷰어/에디터용 데이터 구조체
    // =========================================================================

    public struct ParticipantData
    {
        public string documentId;
        public string name;
        public string insta;
        public string bio;
        public string gender;
        public bool isPicked;
        public int attempts;
    }
}
