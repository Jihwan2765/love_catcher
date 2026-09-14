using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ClawMachine.UI;

namespace ClawMachine.Mechanics
{
    public enum RewardType
    {
        Legendary,
        Doll,
        Instagram,
        Candy
    }

    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("References")]
        public ClawMachineController clawController;
        public ClawPityMagnet pityMagnet;
        public FirebaseRESTService firebaseService;

        [Header("Game Session Settings")]
        [Tooltip("한 세션당 제한 시간(초)")]
        public float sessionTimeLimit = 60f;
        [Tooltip("천장이 발동할 누적 시도 횟수")]
        public int pityTriggerCount = 5;
        
        [Header("Male Specific Probabilities (%)")]
        [Range(0f, 100f)] public float maleProbLegendary = 10f;
        [Range(0f, 100f)] public float maleProbDoll = 30f;
        [Range(0f, 100f)] public float maleProbInstagram = 40f;
        [Range(0f, 100f)] public float maleProbCandy = 20f;

        [Header("Female Specific Probabilities (%)")]
        [Range(0f, 100f)] public float femaleProbLegendary = 10f;
        [Range(0f, 100f)] public float femaleProbDoll = 40f;
        [Range(0f, 100f)] public float femaleProbInstagram = 30f;
        [Range(0f, 100f)] public float femaleProbCandy = 20f;
        
        [Header("Mock Database (Firebase 연동 대기용)")]
        [Tooltip("남성 참가자 목록 (여성이 플레이할 때 매칭 대상)")]
        public List<MatchedProfile> maleProfiles = new List<MatchedProfile>
        {
            new MatchedProfile("김민수", "남", "@sample_boy1", "원신 좋아하시는 분 찾습니다^^"),
            new MatchedProfile("윤서준", "남", "@sample_boy2", "코딩과 독서를 사랑하는 공대생!"),
            new MatchedProfile("이지훈", "남", "@sample_boy3", "운동 좋아해요! 같이 주말에 등산하실 분?"),
            new MatchedProfile("박준혁", "남", "@sample_boy4", "카페 투어랑 맛집 탐방이 취미입니다.")
        };

        [Tooltip("여성 참가자 목록 (남성이 플레이할 때 매칭 대상)")]
        public List<MatchedProfile> femaleProfiles = new List<MatchedProfile>
        {
            new MatchedProfile("이수진", "여", "@sample_girl1", "청춘을 함께 즐길 분 찾아요!"),
            new MatchedProfile("김유진", "여", "@sample_girl2", "필라테스 강사입니다! 친하게 지내요~"),
            new MatchedProfile("박서현", "여", "@sample_girl3", "보드게임이랑 애니메이션 좋아해요!"),
            new MatchedProfile("최수아", "여", "@sample_girl4", "음악 페스티벌 같이 가실 분 구해요 🎵")
        };

        [Header("Game Statistics")]
        public int totalInstaCards = 20;
        public int totalLegendaryDolls = 10;
        public int totalDolls = 100;
        public int totalAttempts = 0;
        public int oppositeGenderCount = 20;

        // Session variables
        private float timeRemaining;
        private int sessionAttempts = 0;
        private bool isGameActive = false;
        private bool isDollScoredThisAttempt = false;
        private List<GameObject> scoredDollsThisAttempt = new List<GameObject>();

        public bool IsInitialized { get; private set; }
        public bool HasRecoverableSession => sessionAttempts > 0;

        private const float DefaultMaleLegendary = 10f;
        private const float DefaultMaleDoll = 30f;
        private const float DefaultMaleInstagram = 40f;
        private const float DefaultMaleCandy = 20f;
        private const float DefaultFemaleLegendary = 10f;
        private const float DefaultFemaleDoll = 40f;
        private const float DefaultFemaleInstagram = 30f;
        private const float DefaultFemaleCandy = 20f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // C# 필드 초기값보다 Unity 씬에 직렬화된 과거 값이 우선 적용될 수 있습니다.
            // 매 실행 시 승인된 기본값으로 시작하고, 이후 개발자 모드 변경은 현재 실행 동안 즉시 적용합니다.
            ApplyDefaultProbabilities();
        }

        private void ApplyDefaultProbabilities()
        {
            maleProbLegendary = DefaultMaleLegendary;
            maleProbDoll = DefaultMaleDoll;
            maleProbInstagram = DefaultMaleInstagram;
            maleProbCandy = DefaultMaleCandy;

            femaleProbLegendary = DefaultFemaleLegendary;
            femaleProbDoll = DefaultFemaleDoll;
            femaleProbInstagram = DefaultFemaleInstagram;
            femaleProbCandy = DefaultFemaleCandy;
        }

        private void Start()
        {
            // Firebase 서비스 컴포넌트 자동 바인딩 (인스펙터 누락 방지)
            if (firebaseService == null)
            {
                firebaseService = FirebaseRESTService.Instance;
                if (firebaseService == null)
                {
#if UNITY_2023_1_OR_NEWER
                    firebaseService = FindFirstObjectByType<FirebaseRESTService>();
#else
                    firebaseService = FindObjectOfType<FirebaseRESTService>();
#endif
                }
            }

            // 이벤트 리스너 등록
            if (ClawMachineUIManager.Instance != null)
            {
                ClawMachineUIManager.Instance.OnPlayerRegistered += StartGameSession;
                ClawMachineUIManager.Instance.OnRetryClicked += HandleRetry;
                ClawMachineUIManager.Instance.OnNextPlayerReady += ResetGameSession;
                ClawMachineUIManager.Instance.OnContinueSession += ContinueGameSession;
            }

            GoalBoxTrigger.OnDollScored += HandleDollScored;

            // 저장된 남은 인형 개수 불러오기 (Firebase)
            if (firebaseService != null && !string.IsNullOrEmpty(firebaseService.firebaseProjectId))
            {
                StartCoroutine(firebaseService.GetTotalDolls((count) => {
                    totalDolls = count;
                    UpdateStatsUI();
                }));
                StartCoroutine(firebaseService.GetTotalLegendaryDolls((count) => {
                    totalLegendaryDolls = count;
                    UpdateStatsUI();
                }));
            }
            else
            {
                UpdateStatsUI();
            }
            
            // 처음에는 조작 금지
            if (clawController != null) clawController.enabled = false;
            IsInitialized = true;
        }

        private void OnDestroy()
        {
            if (ClawMachineUIManager.Instance != null)
            {
                ClawMachineUIManager.Instance.OnPlayerRegistered -= StartGameSession;
                ClawMachineUIManager.Instance.OnRetryClicked -= HandleRetry;
                ClawMachineUIManager.Instance.OnNextPlayerReady -= ResetGameSession;
                ClawMachineUIManager.Instance.OnContinueSession -= ContinueGameSession;
            }

            GoalBoxTrigger.OnDollScored -= HandleDollScored;
        }

        private void Update()
        {
            if (!isGameActive) return;

            // 타이머 카운트다운
            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
                ClawMachineUIManager.Instance.SetTimer(timeRemaining);

                if (timeRemaining <= 0)
                {
                    TimeOutSession();
                }
            }
        }

        /// <summary>
        /// 참가자 등록이 완료되면 게임 세션을 시작합니다.
        /// </summary>
        public void StartGameSession(string name, string insta, string bio, string gender, bool isDuplicateRegistration = false)
        {
            if (!IsSupportedGender(gender))
            {
                const string message = "성별 정보가 올바르지 않아 게임을 시작할 수 없습니다. 남성 또는 여성을 다시 선택해주세요.";
                Debug.LogError($"[게임 세션 시작 실패] 잘못된 성별 값: '{gender}'");
                if (ClawMachineUIManager.Instance != null)
                {
                    ClawMachineUIManager.Instance.ShowRegistrationError(message);
                }
                return;
            }

            sessionAttempts = 1; // 1회차부터 표시하도록 수정
            timeRemaining = sessionTimeLimit;
            isDollScoredThisAttempt = false;
            scoredDollsThisAttempt.Clear();
            isGameActive = true;

            // UI HUD 업데이트
            ClawMachineUIManager.Instance.SetAttempts(sessionAttempts, pityTriggerCount);
            ClawMachineUIManager.Instance.SetTimer(timeRemaining);
            ClawMachineUIManager.Instance.ShowRetryButton(false);
            
            // 천장 및 자석 꺼둠
            if (pityMagnet != null) pityMagnet.SetPityActive(false);

            // 조작 활성화 및 물리적 리셋 연출 실행
            if (clawController != null)
            {
                clawController.enabled = true;
                clawController.TriggerReset();
            }

            // Firebase 실시간 등록 연동 (비동기)
            if (firebaseService != null && !isDuplicateRegistration && !string.IsNullOrWhiteSpace(insta))
            {
                StartCoroutine(firebaseService.RegisterPlayer(name, insta, bio, gender, sessionAttempts, (success) => {
                    if (success)
                    {
                        Debug.Log("[Firebase] 참가자 DB 실시간 백업 완료");
                        firebaseService.IncrementRegistrationCount();
                    }
                }));
            }
            else if (isDuplicateRegistration)
            {
                Debug.Log("[Firebase] 중복 등록이므로 DB에 저장하지 않습니다.");
            }
            
            UpdateStatsUI();

            Debug.Log($"[게임 세션 시작] 플레이어: {name} ({gender})");
        }

        /// <summary>
        /// 인스타 아이디 매칭 후 "또 뽑기"를 선택했을 때 세션을 유지하며 기회를 한 번 더 줍니다.
        /// </summary>
        private void ContinueGameSession()
        {
            // 세션 정보(이름, 인스타 등)는 그대로 유지하면서 시도 횟수를 1회부터 다시 시작
            sessionAttempts = 1;
            isDollScoredThisAttempt = false;
            scoredDollsThisAttempt.Clear();
            timeRemaining = sessionTimeLimit;
            isGameActive = true;

            // HUD 업데이트
            ClawMachineUIManager.Instance.SetAttempts(sessionAttempts, pityTriggerCount);
            ClawMachineUIManager.Instance.SetTimer(timeRemaining);
            ClawMachineUIManager.Instance.ShowRetryButton(false);

            // 천장(Pity) 및 자석 리셋
            if (pityMagnet != null) pityMagnet.SetPityActive(false);
            if (clawController != null && clawController.clawGripper != null)
            {
                clawController.clawGripper.ResetGripForce();
            }

            // 오버레이 다 닫기
            ClawMachineUIManager.Instance.HideAllOverlaysPublic(); 

            // 조작 활성화 및 리셋
            if (clawController != null)
            {
                clawController.enabled = true;
                clawController.TriggerReset();
            }
            
            Debug.Log($"[또 뽑기 세션 시작] 플레이어: {ClawMachineUIManager.Instance.registeredName} 이어서 도전!");
        }

        /// <summary>
        /// 인형이 배출구에 들어왔을 때 트리거에서 호출됩니다.
        /// </summary>
        private void HandleDollScored(GameObject doll)
        {
            if (!isGameActive) return;
            
            if (!scoredDollsThisAttempt.Contains(doll))
            {
                scoredDollsThisAttempt.Add(doll);
                UpdateStatsUI();
                Debug.Log($"[게임 진행] 인형 획득 성공! 현재까지 {scoredDollsThisAttempt.Count}개");
            }
            
            // 첫 번째 인형일 때만 코루틴 타이머를 켬
            if (!isDollScoredThisAttempt)
            {
                isDollScoredThisAttempt = true;
                StartCoroutine(ProcessSuccessSequence());
            }
        }

        private IEnumerator ProcessSuccessSequence()
        {
            // 집게 릴리즈가 완전히 끝날 때까지 대기
            // 이 대기 시간 동안 추가 인형이 떨어지면 scoredDollsThisAttempt 리스트에 누적됨
            yield return new WaitForSeconds(1.5f);

            // 성공 시 모인 모든 인형 파괴 (풀 반환) 및 새 인형 스폰
            // 동시 인형 획득 시 HandleDollScored 가 리스트를 수정하여 InvalidOperationException이 발생하는 것을 방지하기 위해 복제본 순회
            List<GameObject> dollsToProcess = new List<GameObject>(scoredDollsThisAttempt);
            foreach (var doll in dollsToProcess)
            {
                if (doll != null)
                {
                    if (DollSpawner.Instance != null)
                    {
                        DollSpawner.Instance.RefillDoll(doll);
                    }
                    else
                    {
                        Destroy(doll);
                    }
                }
            }
            // 매칭 한 번 끝나면 리스트 초기화 (혹은 다음 턴에 초기화)
            // UI 매칭 및 차감은 1회만 처리되도록 유지 (기획 의도: 인형은 2개, 매칭은 1명)

            isGameActive = false;
            if (clawController != null) clawController.enabled = false;

            // Increment successful extraction count
            if (firebaseService != null)
            {
                firebaseService.IncrementSuccessCount();
            }

            string currentGender = ClawMachineUIManager.Instance.registeredGender;
            if (!TryGetGenderProbabilities(
                    currentGender,
                    out float legendaryWeight,
                    out float dollWeight,
                    out float instagramWeight,
                    out float candyWeight))
            {
                const string message = "성별 정보가 올바르지 않아 보상 추첨을 진행할 수 없습니다.";
                Debug.LogError($"[보상 추첨 실패] 잘못된 성별 값: '{currentGender}'");
                if (ClawMachineUIManager.Instance != null)
                {
                    ClawMachineUIManager.Instance.ShowRegistrationError(message);
                }
                yield break;
            }

            // 인스타 미입력 참가자는 인스타 몫을 사탕에 합산합니다.
            bool hasInstagram = !string.IsNullOrWhiteSpace(ClawMachineUIManager.Instance.registeredInsta);
            if (!hasInstagram)
            {
                candyWeight += instagramWeight;
                instagramWeight = 0f;
            }

            // 레전더리 재고가 없으면 해당 보상을 제외하고 나머지 비율대로 자동 정규화합니다.
            if (totalLegendaryDolls <= 0)
            {
                legendaryWeight = 0f;
            }

            RewardType reward = RollReward(legendaryWeight, dollWeight, instagramWeight, candyWeight);
            MatchedProfileResponse matchResult = new MatchedProfileResponse { success = false };

            // 인스타 보상이 실제 당첨된 경우에만 Firebase에서 지급 가능한 상대를 조회합니다.
            if (reward == RewardType.Instagram)
            {
                bool isQueryFinished = false;
                string oppositeGender = currentGender == "남" ? "여" : "남";

                if (firebaseService != null)
                {
                    StartCoroutine(firebaseService.GetRandomMatch(oppositeGender, result => {
                        matchResult = result;
                        isQueryFinished = true;
                    }));
                }

                float timeout = 3f;
                while (!isQueryFinished && timeout > 0f)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }

                bool canGiveInstagram = isQueryFinished && matchResult.success &&
                                        !string.IsNullOrWhiteSpace(matchResult.insta) &&
                                        !string.IsNullOrWhiteSpace(matchResult.documentId);
                if (!canGiveInstagram)
                {
                    Debug.LogWarning("[보상 재추첨] 지급 가능한 이성 인스타가 없거나 Firebase 조회에 실패하여 인스타를 제외하고 재추첨합니다.");
                    reward = RollReward(legendaryWeight, dollWeight, 0f, candyWeight);
                }
                else
                {
                    bool isLockFinished = false;
                    bool isLockSuccessful = false;
                    StartCoroutine(firebaseService.UpdatePickedStatus(matchResult.documentId, true, updateSuccess => {
                        isLockSuccessful = updateSuccess;
                        isLockFinished = true;
                    }));

                    float lockTimeout = 3f;
                    while (!isLockFinished && lockTimeout > 0f)
                    {
                        lockTimeout -= Time.deltaTime;
                        yield return null;
                    }

                    if (!isLockFinished || !isLockSuccessful)
                    {
                        Debug.LogWarning("[보상 재추첨] 인스타 카드 잠금에 실패하여 중복 지급을 막기 위해 인스타를 제외하고 재추첨합니다.");
                        reward = RollReward(legendaryWeight, dollWeight, 0f, candyWeight);
                    }
                    else
                    {
                        Debug.Log($"[Firebase] {matchResult.name} 카드 실시간 잠금 완료");
                    }
                }
            }

            ClawMachineUIManager.Instance.ShowRewardPopup(
                reward,
                matchResult.name,
                matchResult.gender,
                matchResult.insta,
                matchResult.bio);

            if (reward == RewardType.Doll)
            {
                totalDolls = Mathf.Max(0, totalDolls - 1);
                if (firebaseService != null && !string.IsNullOrEmpty(firebaseService.firebaseProjectId))
                {
                    StartCoroutine(firebaseService.UpdateTotalDolls(totalDolls, (success) => {
                        UpdateStatsUI();
                    }));
                }
                else
                {
                    UpdateStatsUI();
                }
            }

            else if (reward == RewardType.Legendary)
            {
                totalLegendaryDolls = Mathf.Max(0, totalLegendaryDolls - 1);
                if (firebaseService != null && !string.IsNullOrEmpty(firebaseService.firebaseProjectId))
                {
                    StartCoroutine(firebaseService.UpdateTotalLegendaryDolls(totalLegendaryDolls, success => UpdateStatsUI()));
                }
                else
                {
                    UpdateStatsUI();
                }
            }
            else if (reward == RewardType.Instagram)
            {
                totalInstaCards = Mathf.Max(0, totalInstaCards - 1);
                UpdateStatsUI();
            }
        }

        private RewardType RollReward(float legendary, float doll, float instagram, float candy)
        {
            legendary = Mathf.Max(0f, legendary);
            doll = Mathf.Max(0f, doll);
            instagram = Mathf.Max(0f, instagram);
            candy = Mathf.Max(0f, candy);

            float total = legendary + doll + instagram + candy;
            if (total <= 0f)
            {
                Debug.LogError("[보상 추첨] 모든 보상 가중치가 0이므로 안전 보상인 사탕을 지급합니다.");
                return RewardType.Candy;
            }

            float roll = UnityEngine.Random.Range(0f, total);
            if (roll < legendary) return RewardType.Legendary;
            roll -= legendary;
            if (roll < doll) return RewardType.Doll;
            roll -= doll;
            if (roll < instagram) return RewardType.Instagram;
            return RewardType.Candy;
        }

        /// <summary>
        /// 제한 시간이 다 되어 세션이 종료되었을 때 호출됩니다.
        /// </summary>
        private void TimeOutSession()
        {
            isGameActive = false;
            if (clawController != null) clawController.enabled = false;

            // 꽝 팝업 출력
            ClawMachineUIManager.Instance.ShowFailPopup();
        }

        /// <summary>
        /// 외부(물리 R키 또는 아케이드 리셋 버튼 등)에서 재시도를 안전하게 요청하기 위한 공용 인터페이스
        /// </summary>
        public void RequestRetry()
        {
            if (!isGameActive)
            {
                HandleRetry();
            }
        }

        /// <summary>
        /// 시도 실패 후 '다시 시도'를 클릭했을 때
        /// </summary>
        private void HandleRetry()
        {
            if (isGameActive) return;

            sessionAttempts++;
            totalAttempts++;
            isDollScoredThisAttempt = false;
            scoredDollsThisAttempt.Clear();
            
            // 시간 다시 충전
            timeRemaining = sessionTimeLimit;
            isGameActive = true;

            // UI 업데이트
            ClawMachineUIManager.Instance.SetAttempts(sessionAttempts, pityTriggerCount);
            ClawMachineUIManager.Instance.SetTimer(timeRemaining);
            ClawMachineUIManager.Instance.ShowRetryButton(false);
            ClawMachineUIManager.Instance.HideAllOverlaysPublic();

            // 천장(Pity) 검사
            if (sessionAttempts >= pityTriggerCount)
            {
                // 천장 발동: 물리 버프 및 미세 자석 활성화
                if (clawController != null && clawController.clawGripper != null)
                {
                    // 튕겨나가는 킹받는 힘 + 악력 대폭 버프를 위해 설정 추가
                    clawController.clawGripper.AddPityBuff(100f); 
                }

                if (pityMagnet != null)
                {
                    pityMagnet.SetPityActive(true);
                }
            }

            // 집게 활성화 및 위치 초기화 (정교한 물리 리셋 사용)
            if (clawController != null)
            {
                clawController.enabled = true;
                clawController.TriggerReset();
            }
        }

        /// <summary>
        /// 다음 참가자를 위해 기계를 완벽하게 원상복구합니다.
        /// </summary>
        private void ResetGameSession()
        {
            isGameActive = false;
            sessionAttempts = 0;
            isDollScoredThisAttempt = false;
            scoredDollsThisAttempt.Clear();

            if (clawController != null)
            {
                clawController.enabled = false;
                clawController.TriggerReset();
            }

            if (pityMagnet != null) pityMagnet.SetPityActive(false);
            
            // 천장 악력 리셋 (기본 힘으로 복원)
            if (clawController != null && clawController.clawGripper != null)
            {
                clawController.clawGripper.ResetGripForce();
            }
        }

        /// <summary>
        /// 동적 확률 및 현황판 계산
        /// </summary>
        public void UpdateStatsUI()
        {
            if (ClawMachineUIManager.Instance == null) return;

            string statsGender = ClawMachineUIManager.Instance.registeredGender;
            float winChance = 0f;
            if (TryGetGenderProbabilities(
                    statsGender,
                    out float legendary,
                    out float doll,
                    out float instagram,
                    out float candy))
            {
                if (totalLegendaryDolls <= 0) legendary = 0f;
                bool hasInstagram = !string.IsNullOrWhiteSpace(ClawMachineUIManager.Instance.registeredInsta);
                float total = legendary + doll + candy + instagram;
                if (total > 0f)
                {
                    winChance = (legendary + doll + (hasInstagram ? instagram : 0f)) / total * 100f;
                }
            }

            if (firebaseService != null && !string.IsNullOrEmpty(firebaseService.firebaseProjectId))
            {
                StartCoroutine(firebaseService.GetUnpickedCounts((maleCount, femaleCount) => {
                    if (maleCount != -1 && femaleCount != -1)
                    {
                        ClawMachineUIManager.Instance.UpdateRegisterPoolCount(maleCount, femaleCount);
                        
                        // 현재 내 성별과 반대되는 이성 아이디 수 계산
                        string currentGender = ClawMachineUIManager.Instance.registeredGender;
                        if (string.IsNullOrEmpty(currentGender)) currentGender = "남";

                        int oppositeCount = (currentGender == "남") ? femaleCount : maleCount;
                        oppositeGenderCount = oppositeCount; // 캐싱
                        
                        ClawMachineUIManager.Instance.SetStats(oppositeCount, totalDolls, totalLegendaryDolls, winChance);
                    }
                    else
                    {
                        ClawMachineUIManager.Instance.UpdateRegisterPoolCount(maleProfiles.Count, femaleProfiles.Count);
                        
                        string currentGender = ClawMachineUIManager.Instance.registeredGender;
                        if (string.IsNullOrEmpty(currentGender)) currentGender = "남";
                        oppositeGenderCount = (currentGender == "남") ? femaleProfiles.Count : maleProfiles.Count;
                        
                        ClawMachineUIManager.Instance.SetStats(oppositeGenderCount, totalDolls, totalLegendaryDolls, winChance);
                    }
                }));
            }
            else
            {
                ClawMachineUIManager.Instance.UpdateRegisterPoolCount(maleProfiles.Count, femaleProfiles.Count);
                
                string currentGender = ClawMachineUIManager.Instance.registeredGender;
                if (string.IsNullOrEmpty(currentGender)) currentGender = "남";
                oppositeGenderCount = (currentGender == "남") ? femaleProfiles.Count : maleProfiles.Count;
                
                ClawMachineUIManager.Instance.SetStats(oppositeGenderCount, totalDolls, totalLegendaryDolls, winChance);
            }
        }

        public static bool IsSupportedGender(string gender)
        {
            return gender == "남" || gender == "여";
        }

        public bool TryGetGenderProbabilities(
            string gender,
            out float legendary,
            out float doll,
            out float instagram,
            out float candy)
        {
            if (gender == "남")
            {
                legendary = maleProbLegendary;
                doll = maleProbDoll;
                instagram = maleProbInstagram;
                candy = maleProbCandy;
                return true;
            }

            if (gender == "여")
            {
                legendary = femaleProbLegendary;
                doll = femaleProbDoll;
                instagram = femaleProbInstagram;
                candy = femaleProbCandy;
                return true;
            }

            legendary = 0f;
            doll = 0f;
            instagram = 0f;
            candy = 0f;
            return false;
        }

        /// <summary>
        /// ClawMachineController 가 작업을 끝내고 결과를 체크하라고 알릴 때 호출
        /// </summary>
        public void OnClawRoutineFinished()
        {
            StartCoroutine(CheckRoundResultDelay());
        }

        private IEnumerator CheckRoundResultDelay()
        {
            // 인형이 골인 트리거를 통과할 수 있는 여유 시간 대기
            yield return new WaitForSeconds(2.0f);

            if (!isDollScoredThisAttempt && isGameActive)
            {
                isGameActive = false;
                if (clawController != null) clawController.enabled = false;

                // 코인이 존재하는 경우 꽝 팝업 없이 즉시 1코인 차감 및 자동 재도전 진행!
                if (ClawMachineUIManager.Instance != null && ClawMachineUIManager.Instance.HasCoins())
                {
                    Debug.Log("[시도 실패] 코인이 존재하여 꽝 팝업 없이 즉시 차감 및 자동 시작합니다.");
                    ClawMachineUIManager.Instance.DeductCoinDirectly();
                }
                else
                {
                    ClawMachineUIManager.Instance.ShowFailPopup();
                    Debug.Log("[시도 실패] 코인이 없으므로 꽝 팝업을 출력합니다.");
                }
            }
        }
    }

    [System.Serializable]
    public struct MatchedProfile
    {
        public string name;
        public string gender;
        public string insta;
        public string bio;

        public MatchedProfile(string n, string g, string i, string b)
        {
            name = n;
            gender = g;
            insta = i;
            bio = b;
        }
    }
}
