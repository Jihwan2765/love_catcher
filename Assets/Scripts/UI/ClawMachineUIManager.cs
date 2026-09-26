using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using ClawMachine.Input;
using ClawMachine.Utils;

namespace ClawMachine.UI
{
    public class ClawMachineUIManager : MonoBehaviour
    {
        public static ClawMachineUIManager Instance { get; private set; }

        [Header("UI Document")]
        [Tooltip("씬에 배치된 UI Document 컴포넌트")]
        public UIDocument uiDocument;

        // UI Elements - HUD
        private VisualElement topBar;
        private Label instaCountText;
        private Label dollCountText;
        private Label legendaryDollCountText;
        private Label winChanceText;
        private Label attemptCountText;
        private Label timerText;
        private VisualElement pityProgressBar;
        private Label pityStatusText;
        private VisualElement pityMaxBanner;
        private Button retryButton;
        private Button quitButton;

        // UI Elements - Overlays
        private VisualElement registerOverlay;
        private VisualElement successOverlay;
        private VisualElement failOverlay;
        private VisualElement quitConfirmOverlay;
        private VisualElement devModeOverlay;
        private VisualElement devResetStatsConfirmOverlay;
        private VisualElement devSceneReloadConfirmOverlay;
        private VisualElement devDbOverlay;
        private VisualElement devDeleteParticipantConfirmOverlay;
        private VisualElement failRetryOverlay;

        // Registration Card Fields
        private TextField inputName;
        private TextField inputInsta;
        private TextField inputBio;
        private Button genderBtnMale;
        private Button genderBtnFemale;
        private Button registerSubmitBtn;
        private Label registerMaleCountText;
        private Label registerFemaleCountText;
        private Label registerWarningText;
        private Label registerAccountInfoText;
        private Label failAccountInfoText;

        // Success Card Fields
        private Label matchedName;
        private Label matchedInsta;
        private Label matchedBio;
        private Button successRetryBtn;
        private Button successExitBtn;
        private VisualElement successRetryQRCodeCard;
        private Label successSubTitle;
        private Label dollPickupNotice;

        // Coin System Fields
        private Label coinCountText;
        private Label popupCoinCountText;
        private Label registerCoinCountText;
        private Button btnCoinPack1;
        private Button btnCoinPack2;
        private Button btnCoinPack3;
        private Button btnCoinPack4;
        private Button regCoinPack1;
        private Button regCoinPack2;
        private Button regCoinPack3;
        private Button regCoinPack4;
        private Button btnCoinStart;
        private int currentCoins = 0;
        private float lastPlayStartAt = -10f;

        private bool BeginPlayTransition()
        {
            if (BoothStaffAuth.Instance == null || !BoothStaffAuth.Instance.IsAuthenticated)
            {
                ShowRegistrationError("스태프가 Firebase에 로그인한 뒤 시작해 주세요.");
                return false;
            }
            if (Time.unscaledTime - lastPlayStartAt < 0.75f) return false;
            lastPlayStartAt = Time.unscaledTime;
            return true;
        }
        private int pendingCoinsToCharge = 0;

        private enum RetryPaymentOrigin
        {
            None,
            SuccessResult,
            FailResult,
            BottomBar
        }

        private RetryPaymentOrigin retryPaymentOrigin = RetryPaymentOrigin.None;

        // Quit Confirm Retention Fields & state
        private Label quitConfirmTitle;
        private Button quitConfirmYesBtn;
        private Button quitConfirmNoBtn;
        private int exitConfirmCount = 0;

        // Fail Card Fields
        private Button failCloseBtn;
        private Button failRetryCloseBtn;
        private Button failRetryAgainBtn;
        private Label failCuteMsg;
        private Label failEmoji;

        // 랜덤 실패 귀여운 멘트 풀
        private static readonly string[] FailCuteMsgs = {
            "인형이 기다리고 있어요 ㅜㅜ",
            "한 번만 더 하면 잡을 수 있어요!",
            "집게가 아직 워밍업 중이에요 🔥",
            "이번엔 진짜 잡힐 것 같은데요?",
            "인형이 '나 여기 있어~' 하고 있어요",
            "다음엔 꼭 될 거예요, 응원할게요 💕",
            "아깝다! 손끝에서 놓쳤어요 😭",
            "인형이 도망갔지만 기다리고 있을 거예요",
            "한 번 더! 이번엔 집게가 각 잡았어요",
            "포기하면 인형이 슬퍼해요 🥺"
        };
        private static readonly string[] FailEmojis = {
            "🥺", "😭", "😢", "🤧", "💔", "😿", "🙈"
        };

        // Dev Mode Fields
        private Button devOpenDbViewBtn;
        private Slider devGripForceSlider;
        private Label devGripForceLabel;
        private TextField devGripForceInput;
        private Slider devProbLegendarySlider;
        private Label devProbLegendaryLabel;
        private TextField devProbLegendaryInput;
        private Slider devProbInstagramSlider;
        private Label devProbInstagramLabel;
        private TextField devProbInstagramInput;
        private Slider devProbDollSlider;
        private Label devProbDollLabel;
        private TextField devProbDollInput;
        private Slider devProbCandySlider;
        private Label devProbCandyLabel;
        private TextField devProbCandyInput;
        private Toggle devIncludeInstagramToggle;
        private VisualElement devProbInstagramRow;
        private TextField devTotalDollsInput;
        private Button devSaveDollsBtn;
        private TextField devTotalLegendaryDollsInput;
        private Button devSaveLegendaryDollsBtn;
        private Label devProbabilityTotalWarning;
        private TextField devCoinsInput;
        private Button devSaveCoinsBtn;
        private Button devCloseBtn;

        // Dev Mode Stats Dashboard Labels
        private Label devStatRegistrations;
        private Label devStatPlays;
        private Label devStatSuccesses;
        private Label devStatRevenue;

        // Dev Mode Stats Manual Adjustments Fields
        private TextField devTotalRevenueInput;
        private Button devSaveRevenueBtn;
        private TextField devTotalRegistrationsInput;
        private Button devSaveRegistrationsBtn;
        private TextField devTotalPlaysInput;
        private Button devSavePlaysBtn;
        private TextField devTotalSuccessesInput;
        private Button devSaveSuccessesBtn;
        private Button devResetStatsBtn;
        private Button devResetStatsConfirmBtn;
        private Button devResetStatsCancelBtn;

        // Dev DB View Fields
        private ScrollView devDbScrollView;
        private Button devDbRefreshBtn;
        private Button devDbCloseBtn;
        private TextField devDbSearchInput;
        private Button devReloadSceneBtn;
        private Button devSceneReloadConfirmBtn;
        private Button devSceneReloadCancelBtn;
        private Label devDeleteParticipantTargetLabel;
        private Label devDeleteParticipantStatusLabel;
        private Button devDeleteParticipantConfirmBtn;
        private Button devDeleteParticipantCancelBtn;
        private ClawMachine.Mechanics.ParticipantData? pendingDeleteParticipant;
        private VisualElement pendingDeleteParticipantRow;
        private System.Collections.Generic.List<ClawMachine.Mechanics.ParticipantData> cachedDbData = new System.Collections.Generic.List<ClawMachine.Mechanics.ParticipantData>();

        private struct SceneRecoveryData
        {
            public string name;
            public string insta;
            public string bio;
            public string gender;
        }

        private static bool hasPendingSceneRecovery;
        private static SceneRecoveryData pendingSceneRecovery;

        private enum DevProbMode { Male, Female }
        private DevProbMode currentDevProbMode = DevProbMode.Male;
        private bool devIncludeInstagram = true;

        // Dev DB 직접 등록 폼 Fields
        private TextField devAddName;
        private TextField devAddInsta;
        private TextField devAddBio;
        private Button devAddGenderMaleBtn;
        private Button devAddGenderFemaleBtn;
        private Button devAddSubmitBtn;
        private Label devAddStatusLabel;
        private string devAddSelectedGender = "남";

        // Current Player Registration Data
        [Header("Registered Player Info (Current Session)")]
        public string registeredName;
        public string registeredInsta;
        public string registeredBio;
        public string registeredGender = "남"; // "남" or "여"

        [Header("SFX")]
        public AudioClip buttonClickSound;
        public AudioClip popupSuccessSound;
        public AudioClip popupFailSound;

        // Events
        public event Action<string, string, string, string, bool> OnPlayerRegistered;
        public event Action OnRetryClicked;
        public event Action OnNextPlayerReady;
        public event Action OnContinueSession;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument != null)
            {
                InitializeUIElements();
            }
        }

        private void Start()
        {
            if (ArcadeInputManager.Instance != null)
            {
                ArcadeInputManager.Instance.OnActionDown += HandleJoystickActionPressed;
            }

            // 코인 정보 로드 및 UI 초기 갱신
            currentCoins = PlayerPrefs.GetInt("LoveCatcher_Coins", 0);
            UpdateCoinUI();

            if (hasPendingSceneRecovery)
            {
                StartCoroutine(ResumeGameAfterSceneReload());
            }
        }

        private void OnDestroy()
        {
            if (ArcadeInputManager.Instance != null)
            {
                ArcadeInputManager.Instance.OnActionDown -= HandleJoystickActionPressed;
            }
        }
        private void InitializeUIElements()
        {
            var root = uiDocument.rootVisualElement;

            // HUD
            topBar = root.Q<VisualElement>("TopBar");
            instaCountText = root.Q<Label>("InstaCountText");
            dollCountText = root.Q<Label>("DollCountText");
            legendaryDollCountText = root.Q<Label>("LegendaryDollCountText");
            winChanceText = root.Q<Label>("WinChanceText");
            attemptCountText = root.Q<Label>("AttemptCountText");
            timerText = root.Q<Label>("TimerText");
            pityProgressBar = root.Q<VisualElement>("PityProgressBar");
            pityStatusText = root.Q<Label>("PityStatusText");
            pityMaxBanner = root.Q<VisualElement>("PityMaxBanner");
            retryButton = root.Q<Button>("RetryButton");
            quitButton = root.Q<Button>("QuitButton");

            // Overlays
            registerOverlay = root.Q<VisualElement>("RegisterOverlay");
            successOverlay = root.Q<VisualElement>("SuccessOverlay");
            failOverlay = root.Q<VisualElement>("FailOverlay");
            quitConfirmOverlay = root.Q<VisualElement>("QuitConfirmOverlay");
            devModeOverlay = root.Q<VisualElement>("DevModeOverlay");
            devResetStatsConfirmOverlay = root.Q<VisualElement>("DevResetStatsConfirmOverlay");
            devSceneReloadConfirmOverlay = root.Q<VisualElement>("DevSceneReloadConfirmOverlay");
            devDbOverlay = root.Q<VisualElement>("DevDbOverlay");
            devDeleteParticipantConfirmOverlay = root.Q<VisualElement>("DevDeleteParticipantConfirmOverlay");
            failRetryOverlay = root.Q<VisualElement>("FailRetryOverlay");

            // Register Panel Fields
            inputName = root.Q<TextField>("InputName");
            inputInsta = root.Q<TextField>("InputInsta");
            inputBio = root.Q<TextField>("InputBio");
            genderBtnMale = root.Q<Button>("GenderBtnMale");
            genderBtnFemale = root.Q<Button>("GenderBtnFemale");
            registerSubmitBtn = root.Q<Button>("RegisterSubmitBtn");
            registerMaleCountText = root.Q<Label>("RegisterMaleCountText");
            registerFemaleCountText = root.Q<Label>("RegisterFemaleCountText");
            registerWarningText = root.Q<Label>("RegisterWarningText");
            registerAccountInfoText = root.Q<Label>("RegisterAccountInfoText");
            failAccountInfoText = root.Q<Label>("FailAccountInfoText");

            // .env 환경변수에서 부스 결제 계좌 안내 문구 로드 및 UI 적용
            string bankAccount = EnvLoader.Get("BANK_ACCOUNT_INFO");
            if (!string.IsNullOrEmpty(bankAccount))
            {
                if (registerAccountInfoText != null) registerAccountInfoText.text = bankAccount;
                if (failAccountInfoText != null) failAccountInfoText.text = bankAccount;
            }

            // Success Panel Fields
            matchedName = root.Q<Label>("MatchedName");
            matchedInsta = root.Q<Label>("MatchedInsta");
            matchedBio = root.Q<Label>("MatchedBio");
            successRetryBtn = root.Q<Button>("SuccessRetryBtn");
            successExitBtn = root.Q<Button>("SuccessExitBtn");
            successRetryQRCodeCard = root.Q<VisualElement>("RetryQRCodeCard");
            successSubTitle = root.Q<Label>("SuccessSubTitle");
            dollPickupNotice = root.Q<Label>("DollPickupNotice");

            // Coin System Fields
            coinCountText = root.Q<Label>("CoinCountText");
            popupCoinCountText = root.Q<Label>("PopupCoinCountText");
            registerCoinCountText = root.Q<Label>("RegisterCoinCountText");
            btnCoinPack1 = root.Q<Button>("BtnCoinPack1");
            btnCoinPack2 = root.Q<Button>("BtnCoinPack2");
            btnCoinPack3 = root.Q<Button>("BtnCoinPack3");
            btnCoinPack4 = root.Q<Button>("BtnCoinPack4");
            regCoinPack1 = root.Q<Button>("RegCoinPack1");
            regCoinPack2 = root.Q<Button>("RegCoinPack2");
            regCoinPack3 = root.Q<Button>("RegCoinPack3");
            regCoinPack4 = root.Q<Button>("RegCoinPack4");
            btnCoinStart = root.Q<Button>("BtnCoinStart");

            // Quit Confirm Panel Fields
            quitConfirmTitle = root.Q<Label>("QuitConfirmTitle");
            quitConfirmYesBtn = root.Q<Button>("QuitConfirmYesBtn");
            quitConfirmNoBtn = root.Q<Button>("QuitConfirmNoBtn");

            // Fail Panel Fields
            failCloseBtn = root.Q<Button>("FailCloseBtn");
            failRetryCloseBtn = root.Q<Button>("FailRetryCloseBtn");
            failRetryAgainBtn = root.Q<Button>("FailRetryAgainBtn");
            failCuteMsg = root.Q<Label>("FailCuteMsg");
            failEmoji = root.Q<Label>("FailEmoji");

            // Dev Mode Fields
            devOpenDbViewBtn = root.Q<Button>("DevOpenDbViewBtn");
            devGripForceSlider = root.Q<Slider>("DevGripForceSlider");
            devGripForceLabel = root.Q<Label>("DevGripForceLabel");
            devGripForceInput = root.Q<TextField>("DevGripForceInput");
            devProbLegendarySlider = root.Q<Slider>("DevProbLegendarySlider");
            devProbLegendaryLabel = root.Q<Label>("DevProbLegendaryLabel");
            devProbLegendaryInput = root.Q<TextField>("DevProbLegendaryInput");
            devProbInstagramSlider = root.Q<Slider>("DevProbInstagramSlider");
            devProbInstagramLabel = root.Q<Label>("DevProbInstagramLabel");
            devProbInstagramInput = root.Q<TextField>("DevProbInstagramInput");
            devProbInstagramRow = devProbInstagramLabel?.parent;
            devProbDollSlider = root.Q<Slider>("DevProbDollSlider");
            devProbDollLabel = root.Q<Label>("DevProbDollLabel");
            devProbDollInput = root.Q<TextField>("DevProbDollInput");
            devProbCandySlider = root.Q<Slider>("DevProbCandySlider");
            devProbCandyLabel = root.Q<Label>("DevProbCandyLabel");
            devProbCandyInput = root.Q<TextField>("DevProbCandyInput");
            devTotalDollsInput = root.Q<TextField>("DevTotalDollsInput");
            devSaveDollsBtn = root.Q<Button>("DevSaveDollsBtn");
            devTotalLegendaryDollsInput = root.Q<TextField>("DevTotalLegendaryDollsInput");
            devSaveLegendaryDollsBtn = root.Q<Button>("DevSaveLegendaryDollsBtn");
            devProbabilityTotalWarning = root.Q<Label>("DevProbabilityTotalWarning");
            devCoinsInput = root.Q<TextField>("DevCoinsInput");
            devSaveCoinsBtn = root.Q<Button>("DevSaveCoinsBtn");
            devCloseBtn = root.Q<Button>("DevCloseBtn");

            // Bind Stat Labels
            devStatRegistrations = root.Q<Label>("DevStatRegistrations");
            devStatPlays = root.Q<Label>("DevStatPlays");
            devStatSuccesses = root.Q<Label>("DevStatSuccesses");
            devStatRevenue = root.Q<Label>("DevStatRevenue");

            // Bind Stat Adjustments Input Fields and Buttons
            devTotalRevenueInput = root.Q<TextField>("DevTotalRevenueInput");
            devSaveRevenueBtn = root.Q<Button>("DevSaveRevenueBtn");
            devTotalRegistrationsInput = root.Q<TextField>("DevTotalRegistrationsInput");
            devSaveRegistrationsBtn = root.Q<Button>("DevSaveRegistrationsBtn");
            devTotalPlaysInput = root.Q<TextField>("DevTotalPlaysInput");
            devSavePlaysBtn = root.Q<Button>("DevSavePlaysBtn");
            devTotalSuccessesInput = root.Q<TextField>("DevTotalSuccessesInput");
            devSaveSuccessesBtn = root.Q<Button>("DevSaveSuccessesBtn");
            devResetStatsBtn = root.Q<Button>("DevResetStatsBtn");
            devResetStatsConfirmBtn = root.Q<Button>("DevResetStatsConfirmBtn");
            devResetStatsCancelBtn = root.Q<Button>("DevResetStatsCancelBtn");

            // Dev DB View Fields
            devDbScrollView = root.Q<ScrollView>("DevDbScrollView");
            devDbRefreshBtn = root.Q<Button>("DevDbRefreshBtn");
            devDbCloseBtn = root.Q<Button>("DevDbCloseBtn");
            devDeleteParticipantTargetLabel = root.Q<Label>("DevDeleteParticipantTargetLabel");
            devDeleteParticipantStatusLabel = root.Q<Label>("DevDeleteParticipantStatusLabel");
            devDeleteParticipantConfirmBtn = root.Q<Button>("DevDeleteParticipantConfirmBtn");
            devDeleteParticipantCancelBtn = root.Q<Button>("DevDeleteParticipantCancelBtn");
            devSceneReloadConfirmBtn = root.Q<Button>("DevSceneReloadConfirmBtn");
            devSceneReloadCancelBtn = root.Q<Button>("DevSceneReloadCancelBtn");

            // Dev DB 직접 등록 폼 Fields
            devAddName = root.Q<TextField>("DevAddName");
            devAddInsta = root.Q<TextField>("DevAddInsta");
            devAddBio = root.Q<TextField>("DevAddBio");
            devAddGenderMaleBtn = root.Q<Button>("DevAddGenderMaleBtn");
            devAddGenderFemaleBtn = root.Q<Button>("DevAddGenderFemaleBtn");
            devAddSubmitBtn = root.Q<Button>("DevAddSubmitBtn");
            devAddStatusLabel = root.Q<Label>("DevAddStatusLabel");

            // UI Button SFX Helper
            Action playBtnSound = () => 
            { 
                if (buttonClickSound != null && ClawMachine.Audio.SoundManager.Instance != null) 
                    ClawMachine.Audio.SoundManager.Instance.PlaySFX(buttonClickSound); 
            };

            // Register Event Listeners
            genderBtnMale.clicked += () => { playBtnSound(); SelectGender("남"); };
            genderBtnFemale.clicked += () => { playBtnSound(); SelectGender("여"); };
            registerSubmitBtn.clicked += () => { playBtnSound(); SubmitRegistration(); };
            
            if (retryButton != null)
            {
                retryButton.clicked += () => 
                { 
                    playBtnSound(); 
                    ExecuteRetryAction(); 
                };
            }

            if (quitButton != null)
            {
                quitButton.clicked += () => { playBtnSound(); ResetToRegistration(); };
            }

            if (successRetryBtn != null)
            {
                successRetryBtn.clicked += () => 
                { 
                    playBtnSound(); 
                    ExecuteSuccessRetryClick(); 
                };
            }

            if (successExitBtn != null)
            {
                successExitBtn.clicked += () => { playBtnSound(); HandleSuccessExitClick(); };
            }

            if (quitConfirmNoBtn != null)
            {
                quitConfirmNoBtn.clicked += () => { playBtnSound(); HandleQuitConfirmNoClick(); };
            }

            if (quitConfirmYesBtn != null)
            {
                quitConfirmYesBtn.clicked += () => { playBtnSound(); HandleQuitConfirmYesClick(); };
            }

            failCloseBtn.clicked += () => { playBtnSound(); ResetToRegistration(); };
            if (failRetryCloseBtn != null)
            {
                failRetryCloseBtn.clicked += () => 
                { 
                    playBtnSound();
                    HandleRetryPaymentBack();
                };
            }
            if (failRetryAgainBtn != null)
            {
                failRetryAgainBtn.clicked += () => 
                { 
                    playBtnSound(); 
                    ExecuteFailRetryAgainClick(); 
                };
            }

            // Coin Package Button Events
            Button[] regPacks = { regCoinPack1, regCoinPack2, regCoinPack3, regCoinPack4 };
            Button[] failPacks = { btnCoinPack1, btnCoinPack2, btnCoinPack3, btnCoinPack4 };

            if (btnCoinPack1 != null) btnCoinPack1.clicked += () => { SelectCoinPackage(1, btnCoinPack1, failPacks); };
            if (btnCoinPack2 != null) btnCoinPack2.clicked += () => { SelectCoinPackage(3, btnCoinPack2, failPacks); };
            if (btnCoinPack3 != null) btnCoinPack3.clicked += () => { SelectCoinPackage(6, btnCoinPack3, failPacks); };
            if (btnCoinPack4 != null) btnCoinPack4.clicked += () => { SelectCoinPackage(13, btnCoinPack4, failPacks); };

            if (regCoinPack1 != null) regCoinPack1.clicked += () => { SelectCoinPackage(1, regCoinPack1, regPacks); };
            if (regCoinPack2 != null) regCoinPack2.clicked += () => { SelectCoinPackage(3, regCoinPack2, regPacks); };
            if (regCoinPack3 != null) regCoinPack3.clicked += () => { SelectCoinPackage(6, regCoinPack3, regPacks); };
            if (regCoinPack4 != null) regCoinPack4.clicked += () => { SelectCoinPackage(13, regCoinPack4, regPacks); };

            if (btnCoinStart != null)
            {
                btnCoinStart.clicked += () =>
                {
                    playBtnSound();
                    ExecuteCoinStartClick();
                };
            }

            // Dev Mode Events
            if (devCloseBtn != null) devCloseBtn.clicked += () => { playBtnSound(); HideOverlay(devModeOverlay); };
            if (devOpenDbViewBtn != null) devOpenDbViewBtn.clicked += () => { playBtnSound(); OpenDbView(); };

            // Dev DB View Events
            if (devDbCloseBtn != null) devDbCloseBtn.clicked += () => { playBtnSound(); CloseDbView(); };
            if (devDbRefreshBtn != null) devDbRefreshBtn.clicked += () => { playBtnSound(); RefreshDbView(); };
            if (devDeleteParticipantCancelBtn != null)
                devDeleteParticipantCancelBtn.clicked += () => { playBtnSound(); HideDevDeleteParticipantConfirmation(); };
            if (devDeleteParticipantConfirmBtn != null)
                devDeleteParticipantConfirmBtn.clicked += () => { playBtnSound(); BeginDevParticipantDelete(); };
            if (devSceneReloadCancelBtn != null)
                devSceneReloadCancelBtn.clicked += () => { playBtnSound(); HideSceneReloadConfirmation(); };
            if (devSceneReloadConfirmBtn != null)
                devSceneReloadConfirmBtn.clicked += ConfirmSceneReload;

            // Dev DB 직접 등록 폼 Events
            if (devAddGenderMaleBtn != null)
                devAddGenderMaleBtn.clicked += () => { playBtnSound(); SetDevAddGender("남"); };
            if (devAddGenderFemaleBtn != null)
                devAddGenderFemaleBtn.clicked += () => { playBtnSound(); SetDevAddGender("여"); };
            if (devAddSubmitBtn != null)
                devAddSubmitBtn.clicked += () => { playBtnSound(); HandleDevAddSubmit(); };



            if (devGripForceSlider != null)
            {
                devGripForceSlider.RegisterValueChangedCallback(evt => {
                    if (devGripForceLabel != null) devGripForceLabel.text = $"집게 힘 (악력): {evt.newValue:F1}";
                    if (devGripForceInput != null && rootVisualElement().focusController.focusedElement != devGripForceInput)
                        devGripForceInput.value = evt.newValue.ToString("F1");
                    
                    if (ClawMachine.Mechanics.GameFlowManager.Instance != null && ClawMachine.Mechanics.GameFlowManager.Instance.clawController != null)
                    {
                        var gripper = ClawMachine.Mechanics.GameFlowManager.Instance.clawController.clawGripper;
                        if (gripper != null)
                        {
                            gripper.baseGripForce = evt.newValue;
                            gripper.gripForce = evt.newValue;
                        }
                    }
                });
            }

            if (devGripForceInput != null)
            {
                devGripForceInput.RegisterValueChangedCallback(evt => {
                    if (float.TryParse(evt.newValue, out float val))
                    {
                        val = Mathf.Clamp(val, 10f, 150f);
                        if (devGripForceSlider != null && !Mathf.Approximately(devGripForceSlider.value, val))
                        {
                            devGripForceSlider.value = val;
                        }
                    }
                });
            }

            if (devProbLegendarySlider != null && devProbLegendarySlider.parent != null)
            {
                var probModeContainer = new VisualElement();
                probModeContainer.style.flexDirection = FlexDirection.Row;
                probModeContainer.style.marginBottom = 10;
                
                var btnMale = new Button { text = "남성용 설정" };
                var btnFemale = new Button { text = "여성용 설정" };
                devIncludeInstagramToggle = new Toggle("인스타 아이디 포함") { value = true };
                devIncludeInstagramToggle.style.marginLeft = 12;
                devIncludeInstagramToggle.style.color = Color.white;

                Action updateBtnColors = () => {
                    btnMale.style.backgroundColor = currentDevProbMode == DevProbMode.Male ? new Color(0, 0, 0.5f) : new Color(0.2f, 0.2f, 0.2f);
                    btnFemale.style.backgroundColor = currentDevProbMode == DevProbMode.Female ? new Color(0.5f, 0, 0) : new Color(0.2f, 0.2f, 0.2f);
                };

                btnMale.clicked += () => { currentDevProbMode = DevProbMode.Male; updateBtnColors(); RefreshDevProbSliders(); };
                btnFemale.clicked += () => { currentDevProbMode = DevProbMode.Female; updateBtnColors(); RefreshDevProbSliders(); };
                devIncludeInstagramToggle.RegisterValueChangedCallback(evt => {
                    devIncludeInstagram = evt.newValue;
                    RefreshDevProbSliders();
                });

                probModeContainer.Add(btnMale);
                probModeContainer.Add(btnFemale);
                probModeContainer.Add(devIncludeInstagramToggle);
                updateBtnColors();

                // Add above the probability label
                var parent = devProbLegendarySlider.parent;
                var indexOfLabel = parent.IndexOf(devProbLegendarySlider) - 1;
                if (indexOfLabel >= 0) {
                    parent.Insert(indexOfLabel, probModeContainer);
                }
            }

            if (devProbLegendarySlider != null)
            {
                devProbLegendarySlider.RegisterValueChangedCallback(evt => {
                    if (devProbLegendaryLabel != null) devProbLegendaryLabel.text = $"[레전더리] 확률: {evt.newValue:F1}%";
                    if (devProbLegendaryInput != null && rootVisualElement().focusController.focusedElement != devProbLegendaryInput)
                        devProbLegendaryInput.value = evt.newValue.ToString("F1");
                    
                    if (ClawMachine.Mechanics.GameFlowManager.Instance != null) {
                        var gm = ClawMachine.Mechanics.GameFlowManager.Instance;
                        if (currentDevProbMode == DevProbMode.Male)
                        {
                            if (devIncludeInstagram) gm.maleProbLegendary = evt.newValue;
                            else gm.maleNoInstaProbLegendary = evt.newValue;
                        }
                        else
                        {
                            if (devIncludeInstagram) gm.femaleProbLegendary = evt.newValue;
                            else gm.femaleNoInstaProbLegendary = evt.newValue;
                        }
                    }
                    RefreshDevProbabilitySummary();
                });
            }

            if (devProbLegendaryInput != null)
            {
                devProbLegendaryInput.RegisterValueChangedCallback(evt => {
                    if (float.TryParse(evt.newValue, out float val))
                    {
                        val = Mathf.Clamp(val, 0f, 100f);
                        if (devProbLegendarySlider != null && !Mathf.Approximately(devProbLegendarySlider.value, val))
                        {
                            devProbLegendarySlider.value = val;
                        }
                    }
                });
            }

            if (devProbInstagramSlider != null)
            {
                devProbInstagramSlider.RegisterValueChangedCallback(evt => {
                    if (devProbInstagramLabel != null) devProbInstagramLabel.text = $"[인스타 아이디] 확률: {evt.newValue:F1}%";
                    if (devProbInstagramInput != null && rootVisualElement().focusController.focusedElement != devProbInstagramInput)
                        devProbInstagramInput.value = evt.newValue.ToString("F1");
                    
                    if (ClawMachine.Mechanics.GameFlowManager.Instance != null) {
                        var gm = ClawMachine.Mechanics.GameFlowManager.Instance;
                        if (currentDevProbMode == DevProbMode.Male) gm.maleProbInstagram = evt.newValue;
                        else gm.femaleProbInstagram = evt.newValue;
                    }
                    RefreshDevProbabilitySummary();
                });
            }

            if (devProbInstagramInput != null)
            {
                devProbInstagramInput.RegisterValueChangedCallback(evt => {
                    if (float.TryParse(evt.newValue, out float val))
                    {
                        val = Mathf.Clamp(val, 0f, 100f);
                        if (devProbInstagramSlider != null && !Mathf.Approximately(devProbInstagramSlider.value, val))
                        {
                            devProbInstagramSlider.value = val;
                        }
                    }
                });
            }

            if (devProbDollSlider != null)
            {
                devProbDollSlider.RegisterValueChangedCallback(evt => {
                    if (devProbDollLabel != null) devProbDollLabel.text = $"[인형] 확률: {evt.newValue:F1}%";
                    if (devProbDollInput != null && rootVisualElement().focusController.focusedElement != devProbDollInput)
                        devProbDollInput.value = evt.newValue.ToString("F1");
                    
                    if (ClawMachine.Mechanics.GameFlowManager.Instance != null) {
                        var gm = ClawMachine.Mechanics.GameFlowManager.Instance;
                        if (currentDevProbMode == DevProbMode.Male)
                        {
                            if (devIncludeInstagram) gm.maleProbDoll = evt.newValue;
                            else gm.maleNoInstaProbDoll = evt.newValue;
                        }
                        else
                        {
                            if (devIncludeInstagram) gm.femaleProbDoll = evt.newValue;
                            else gm.femaleNoInstaProbDoll = evt.newValue;
                        }
                    }
                    RefreshDevProbabilitySummary();
                });
            }

            if (devProbDollInput != null)
            {
                devProbDollInput.RegisterValueChangedCallback(evt => {
                    if (float.TryParse(evt.newValue, out float val))
                    {
                        val = Mathf.Clamp(val, 0f, 100f);
                        if (devProbDollSlider != null && !Mathf.Approximately(devProbDollSlider.value, val))
                        {
                            devProbDollSlider.value = val;
                        }
                    }
                });
            }

            if (devProbCandySlider != null)
            {
                devProbCandySlider.RegisterValueChangedCallback(evt => {
                    if (devProbCandyLabel != null) devProbCandyLabel.text = $"[사탕] 확률: {evt.newValue:F1}%";
                    if (devProbCandyInput != null && rootVisualElement().focusController.focusedElement != devProbCandyInput)
                        devProbCandyInput.value = evt.newValue.ToString("F1");
                    
                    if (ClawMachine.Mechanics.GameFlowManager.Instance != null) {
                        var gm = ClawMachine.Mechanics.GameFlowManager.Instance;
                        if (currentDevProbMode == DevProbMode.Male)
                        {
                            if (devIncludeInstagram) gm.maleProbCandy = evt.newValue;
                            else gm.maleNoInstaProbCandy = evt.newValue;
                        }
                        else
                        {
                            if (devIncludeInstagram) gm.femaleProbCandy = evt.newValue;
                            else gm.femaleNoInstaProbCandy = evt.newValue;
                        }
                    }
                    RefreshDevProbabilitySummary();
                });
            }

            if (devProbCandyInput != null)
            {
                devProbCandyInput.RegisterValueChangedCallback(evt => {
                    if (float.TryParse(evt.newValue, out float val))
                    {
                        val = Mathf.Clamp(val, 0f, 100f);
                        if (devProbCandySlider != null && !Mathf.Approximately(devProbCandySlider.value, val))
                        {
                            devProbCandySlider.value = val;
                        }
                    }
                });
            }

            if (devSaveDollsBtn != null)
            {
                devSaveDollsBtn.clicked += () => {
                    playBtnSound();
                    if (devTotalDollsInput != null && int.TryParse(devTotalDollsInput.value, out int newCount))
                    {
                        devSaveDollsBtn.text = "...";
                        devSaveDollsBtn.SetEnabled(false);
                        if (ClawMachine.Mechanics.FirebaseRESTService.Instance != null)
                        {
                            StartCoroutine(ClawMachine.Mechanics.FirebaseRESTService.Instance.UpdateTotalDolls(newCount, success => {
                                if (success)
                                {
                                    devSaveDollsBtn.text = "완료!";
                                    if (ClawMachine.Mechanics.GameFlowManager.Instance != null)
                                    {
                                        ClawMachine.Mechanics.GameFlowManager.Instance.totalDolls = newCount;
                                        ClawMachine.Mechanics.GameFlowManager.Instance.UpdateStatsUI();
                                    }
                                }
                                else
                                {
                                    devSaveDollsBtn.text = "실패";
                                }
                                devSaveDollsBtn.SetEnabled(true);
                            }));
                        }
                    }
                };
            }

            if (devSaveLegendaryDollsBtn != null)
            {
                devSaveLegendaryDollsBtn.clicked += () => {
                    playBtnSound();
                    if (devTotalLegendaryDollsInput == null ||
                        !int.TryParse(devTotalLegendaryDollsInput.value, out int newCount)) return;

                    newCount = Mathf.Max(0, newCount);
                    devSaveLegendaryDollsBtn.text = "...";
                    devSaveLegendaryDollsBtn.SetEnabled(false);
                    var service = ClawMachine.Mechanics.FirebaseRESTService.Instance;
                    if (service == null)
                    {
                        devSaveLegendaryDollsBtn.text = "실패";
                        devSaveLegendaryDollsBtn.SetEnabled(true);
                        return;
                    }

                    StartCoroutine(service.UpdateTotalLegendaryDolls(newCount, success => {
                        devSaveLegendaryDollsBtn.text = success ? "완료!" : "실패";
                        devSaveLegendaryDollsBtn.SetEnabled(true);
                        if (success && ClawMachine.Mechanics.GameFlowManager.Instance != null)
                        {
                            ClawMachine.Mechanics.GameFlowManager.Instance.totalLegendaryDolls = newCount;
                            ClawMachine.Mechanics.GameFlowManager.Instance.UpdateStatsUI();
                            RefreshDevProbabilitySummary();
                        }
                    }));
                };
            }

            if (devSaveCoinsBtn != null)
            {
                devSaveCoinsBtn.clicked += () => {
                    playBtnSound();
                    if (devCoinsInput != null && int.TryParse(devCoinsInput.value, out int newCoins))
                    {
                        devSaveCoinsBtn.text = "...";
                        devSaveCoinsBtn.SetEnabled(false);
                        
                        currentCoins = newCoins;
                        PlayerPrefs.SetInt("LoveCatcher_Coins", currentCoins);
                        PlayerPrefs.Save();
                        UpdateCoinUI();
                        
                        devSaveCoinsBtn.text = "완료!";
                        devSaveCoinsBtn.SetEnabled(true);
                        
                        Invoke(nameof(RestoreDevSaveCoinsBtnText), 1.5f);
                    }
                };
            }

            if (devSaveRevenueBtn != null)
            {
                devSaveRevenueBtn.clicked += () => {
                    playBtnSound();
                    if (devTotalRevenueInput != null && int.TryParse(devTotalRevenueInput.value, out int newVal))
                    {
                        devSaveRevenueBtn.text = "...";
                        devSaveRevenueBtn.SetEnabled(false);
                        StartCoroutine(SaveSingleStatCoroutine("totalRevenue", newVal, success => {
                            devSaveRevenueBtn.text = success ? "완료!" : "실패";
                            devSaveRevenueBtn.SetEnabled(true);
                            RefreshDevModeStats();
                        }));
                    }
                };
            }

            if (devSaveRegistrationsBtn != null)
            {
                devSaveRegistrationsBtn.clicked += () => {
                    playBtnSound();
                    if (devTotalRegistrationsInput != null && int.TryParse(devTotalRegistrationsInput.value, out int newVal))
                    {
                        devSaveRegistrationsBtn.text = "...";
                        devSaveRegistrationsBtn.SetEnabled(false);
                        StartCoroutine(SaveSingleStatCoroutine("totalRegistrations", newVal, success => {
                            devSaveRegistrationsBtn.text = success ? "완료!" : "실패";
                            devSaveRegistrationsBtn.SetEnabled(true);
                            RefreshDevModeStats();
                        }));
                    }
                };
            }

            if (devSavePlaysBtn != null)
            {
                devSavePlaysBtn.clicked += () => {
                    playBtnSound();
                    if (devTotalPlaysInput != null && int.TryParse(devTotalPlaysInput.value, out int newVal))
                    {
                        devSavePlaysBtn.text = "...";
                        devSavePlaysBtn.SetEnabled(false);
                        StartCoroutine(SaveSingleStatCoroutine("totalPlays", newVal, success => {
                            devSavePlaysBtn.text = success ? "완료!" : "실패";
                            devSavePlaysBtn.SetEnabled(true);
                            RefreshDevModeStats();
                        }));
                    }
                };
            }

            if (devSaveSuccessesBtn != null)
            {
                devSaveSuccessesBtn.clicked += () => {
                    playBtnSound();
                    if (devTotalSuccessesInput != null && int.TryParse(devTotalSuccessesInput.value, out int newVal))
                    {
                        devSaveSuccessesBtn.text = "...";
                        devSaveSuccessesBtn.SetEnabled(false);
                        StartCoroutine(SaveSingleStatCoroutine("totalSuccesses", newVal, success => {
                            devSaveSuccessesBtn.text = success ? "완료!" : "실패";
                            devSaveSuccessesBtn.SetEnabled(true);
                            RefreshDevModeStats();
                        }));
                    }
                };
            }

            if (devResetStatsBtn != null)
            {
                devResetStatsBtn.clicked += () => {
                    playBtnSound();
                    ShowDevStatsResetConfirmation();
                };
            }

            if (devResetStatsCancelBtn != null)
            {
                devResetStatsCancelBtn.clicked += () => {
                    playBtnSound();
                    HideDevStatsResetConfirmation();
                };
            }

            if (devResetStatsConfirmBtn != null)
            {
                devResetStatsConfirmBtn.clicked += () => {
                    playBtnSound();
                    BeginDevStatsReset();
                };
            }

            // Create Dev DB Search Input dynamically
            devDbSearchInput = new TextField("이름 검색");
            devDbSearchInput.AddToClassList("form-input");
            devDbSearchInput.style.marginBottom = 10;
            devDbSearchInput.style.color = Color.white;
            devDbSearchInput.RegisterValueChangedCallback(evt => {
                FilterDbView(evt.newValue);
            });
            if (devDbOverlay != null)
            {
                var devDbCard = devDbOverlay.Q<VisualElement>("DevDbCard");
                if (devDbCard != null && devDbScrollView != null)
                {
                    devDbCard.Insert(devDbCard.IndexOf(devDbScrollView), devDbSearchInput);
                }
            }

            // Create Scene Reload Button dynamically
            devReloadSceneBtn = new Button() { text = "Scene 리로드 (씬 초기화)" };
            devReloadSceneBtn.AddToClassList("btn-primary");
            devReloadSceneBtn.style.backgroundColor = new Color(0.8f, 0f, 0f);
            devReloadSceneBtn.style.marginTop = 10;
            devReloadSceneBtn.style.width = new Length(100, LengthUnit.Percent);
            devReloadSceneBtn.clicked += ShowSceneReloadConfirmation;
            if (devModeOverlay != null)
            {
                var devPanel = devModeOverlay.Q<VisualElement>("DevModeCard");
                if (devPanel == null && devModeOverlay.childCount > 0)
                {
                    devPanel = devModeOverlay.ElementAt(0);
                }
                if (devPanel != null)
                {
                    devPanel.Add(devReloadSceneBtn);
                }
            }

            // Initial UI State
            SelectGender("남"); // 기본 성별은 남성
            HideAllOverlays();
            ShowOverlay(registerOverlay);
        }

        private void SelectGender(string gender)
        {
            registeredGender = gender;
            if (gender == "남")
            {
                genderBtnMale.AddToClassList("gender-button-male-selected");
                genderBtnFemale.RemoveFromClassList("gender-button-female-selected");
                
                // 카드에 테두리 색을 Cyan으로 설정
                rootVisualElement().Q<VisualElement>("RegisterCard").RemoveFromClassList("popup-card-female");
                registerSubmitBtn.RemoveFromClassList("btn-primary-female");
            }
            else
            {
                genderBtnMale.RemoveFromClassList("gender-button-male-selected");
                genderBtnFemale.AddToClassList("gender-button-female-selected");
                
                // 카드에 테두리 색을 Pink로 설정
                rootVisualElement().Q<VisualElement>("RegisterCard").AddToClassList("popup-card-female");
                registerSubmitBtn.AddToClassList("btn-primary-female");
            }
        }

        private bool isDuplicateRegistration = false;

        private void SubmitRegistration()
        {
            // 포커스 해제하여 커서 인덱스 예외(ArgumentOutOfRangeException) 방지
            inputName?.Blur();
            inputInsta?.Blur();
            inputBio?.Blur();

            isDuplicateRegistration = false;
            registeredName = inputName.value?.Trim();
            registeredInsta = inputInsta.value?.Trim();
            registeredBio = inputBio.value?.Trim();

            if (!ClawMachine.Mechanics.GameFlowManager.IsSupportedGender(registeredGender))
            {
                ShowRegistrationError("성별을 남성 또는 여성으로 다시 선택해주세요.");
                Debug.LogError($"[참가자 등록 실패] 잘못된 성별 값: '{registeredGender}'");
                return;
            }

            if (string.IsNullOrEmpty(registeredName) || string.IsNullOrWhiteSpace(registeredInsta))
            {
                if (registerWarningText != null)
                {
                    registerWarningText.text = "이름과 인스타 아이디를 입력해 주세요.";
                    registerWarningText.style.display = DisplayStyle.Flex;
                }
                Debug.LogWarning("필수 입력 항목 누락.");
                return;
            }

            if (registerWarningText != null) registerWarningText.style.display = DisplayStyle.None;

            if (!string.IsNullOrWhiteSpace(registeredInsta) && ClawMachine.Mechanics.FirebaseRESTService.Instance != null)
            {
                if (registerSubmitBtn != null) registerSubmitBtn.SetEnabled(false);
                
                StartCoroutine(ClawMachine.Mechanics.FirebaseRESTService.Instance.CheckInstaIdExists(registeredInsta, (exists) => {
                    if (registerSubmitBtn != null) registerSubmitBtn.SetEnabled(true);
                    if (!exists.HasValue)
                    {
                        ShowRegistrationError("참가자 중복 확인에 실패했습니다. 연결을 확인하고 다시 눌러 주세요.");
                        return;
                    }
                    isDuplicateRegistration = exists.Value;
                    CheckCoinAndProceed();
                }));
            }
            else
            {
                CheckCoinAndProceed();
            }
        }

        private void CheckCoinAndProceed()
        {
            int totalAvailable = currentCoins + pendingCoinsToCharge;
            if (totalAvailable > 0)
            {
                if (!BeginPlayTransition()) return;
                int revenue = GetRevenueFromStagedCoins(pendingCoinsToCharge);
                currentCoins = totalAvailable - 1;
                pendingCoinsToCharge = 0;

                // Clear highlights from all pack buttons
                Button[] allPackButtons = { btnCoinPack1, btnCoinPack2, btnCoinPack3, btnCoinPack4, regCoinPack1, regCoinPack2, regCoinPack3, regCoinPack4 };
                foreach (var btn in allPackButtons)
                {
                    if (btn != null)
                    {
                        btn.RemoveFromClassList("gender-button-male-selected");
                    }
                }

                PlayerPrefs.SetInt("LoveCatcher_Coins", currentCoins);
                PlayerPrefs.Save();
                UpdateCoinUI();

                // Record the play session
                RecordPlaySession(revenue);
                
                if (registerWarningText != null) registerWarningText.style.display = DisplayStyle.None;
                ProceedWithRegistration();
            }
            else
            {
                if (registerWarningText != null)
                {
                    registerWarningText.text = "코인을 먼저 충전해주세요! (우측 카드 이용)";
                    registerWarningText.style.display = DisplayStyle.Flex;
                }
            }
        }

        private void ProceedWithRegistration()
        {
            // 오버레이 숨기기
            HideOverlay(registerOverlay);
            
            // 이벤트 발행 -> Game Flow Manager에서 수신하여 타이머 시작 및 게임 가능 모드 전환
            OnPlayerRegistered?.Invoke(registeredName, registeredInsta, registeredBio, registeredGender, isDuplicateRegistration);
        }

        private void ResetToRegistration()
        {
            // 한 참가자의 세션이 끝나고 개인정보 입력 화면으로 돌아갈 때 HUD를 숨깁니다.
            SetTopBarVisible(false);

            // Clear coin selection state
            ClearStagedCoins();
            retryPaymentOrigin = RetryPaymentOrigin.None;

            // Reset actual coins to 0 upon quitting
            currentCoins = 0;
            PlayerPrefs.SetInt("LoveCatcher_Coins", 0);
            PlayerPrefs.Save();
            UpdateCoinUI();

            // 포커스 해제
            inputName?.Blur();
            inputInsta?.Blur();
            inputBio?.Blur();

            // 입력 필드 비우기
            inputName.value = "";
            inputInsta.value = "";
            inputBio.value = "";
            
            if (registerWarningText != null) registerWarningText.style.display = DisplayStyle.None;
            SelectGender("남");

            HideAllOverlays();
            ShowOverlay(registerOverlay);

            OnNextPlayerReady?.Invoke();
        }

        private void Update()
        {
            UpdateSceneReloadButtonState();

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.lKey.wasPressedThisFrame &&
                    UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed &&
                    UnityEngine.InputSystem.Keyboard.current.altKey.isPressed)
                {
                    ToggleDevMode();
                }

                // Operator R key retry override when bottom bar, fail overlay, or payment overlay is visible
                if (UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
                {
                    bool isRetryEligible = (retryButton != null && retryButton.resolvedStyle.display == DisplayStyle.Flex) ||
                                           (failRetryOverlay != null && failRetryOverlay.resolvedStyle.display == DisplayStyle.Flex) ||
                                           (failOverlay != null && failOverlay.resolvedStyle.display == DisplayStyle.Flex);

                    if (isRetryEligible && devSceneReloadConfirmOverlay?.style.display != DisplayStyle.Flex &&
                        ClawMachine.Mechanics.GameFlowManager.Instance != null)
                    {
                        HideAllOverlays();
                        ClawMachine.Mechanics.GameFlowManager.Instance.RequestRetry();
                    }
                }
            }

            // UI Navigation Update
            if (currentNavGroup != NavGroup.None && currentButtons != null && currentButtons.Length > 0)
            {
                // Support keyboard Arrow keys or WASD for UI navigation
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                    {
                        ChangeSelection(1);
                    }
                    else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                    {
                        ChangeSelection(-1);
                    }
                }

                // Support Arcade Joystick
                Vector2 joystick = Vector2.zero;
                if (ArcadeInputManager.Instance != null)
                {
                    joystick = ArcadeInputManager.Instance.JoystickInput;
                }

                float moveThreshold = 0.5f;
                if (!isJoystickDpadPressed)
                {
                    if (joystick.x > moveThreshold || joystick.y > moveThreshold)
                    {
                        isJoystickDpadPressed = true;
                        ChangeSelection(-1);
                    }
                    else if (joystick.x < -moveThreshold || joystick.y < -moveThreshold)
                    {
                        isJoystickDpadPressed = true;
                        ChangeSelection(1);
                    }
                }
                else
                {
                    if (Mathf.Abs(joystick.x) < 0.15f && Mathf.Abs(joystick.y) < 0.15f)
                    {
                        isJoystickDpadPressed = false;
                    }
                }
            }
        }

        private void UpdateSceneReloadButtonState()
        {
            if (devReloadSceneBtn == null) return;

            var firebase = ClawMachine.Mechanics.FirebaseRESTService.Instance;
            bool isSaving = firebase != null && firebase.IsWriteInProgress;
            bool canRecoverSession = HasRecoverableSceneSession();

            devReloadSceneBtn.SetEnabled(!isSaving && canRecoverSession);
            devSceneReloadConfirmBtn?.SetEnabled(!isSaving && canRecoverSession);
            if (isSaving)
            {
                devReloadSceneBtn.text = "Firebase 저장 중...";
            }
            else if (!canRecoverSession)
            {
                devReloadSceneBtn.text = "진행 중인 게임 없음";
            }
            else
            {
                devReloadSceneBtn.text = "게임 복구 (씬 리로드)";
            }
        }

        private bool HasRecoverableSceneSession()
        {
            var gameFlow = ClawMachine.Mechanics.GameFlowManager.Instance;
            return gameFlow != null && gameFlow.HasRecoverableSession &&
                   !string.IsNullOrWhiteSpace(registeredName) &&
                   ClawMachine.Mechanics.GameFlowManager.IsSupportedGender(registeredGender);
        }

        private void ShowSceneReloadConfirmation()
        {
            var firebase = ClawMachine.Mechanics.FirebaseRESTService.Instance;
            if ((firebase != null && firebase.IsWriteInProgress) || !HasRecoverableSceneSession()) return;

            if (buttonClickSound != null && ClawMachine.Audio.SoundManager.Instance != null)
                ClawMachine.Audio.SoundManager.Instance.PlaySFX(buttonClickSound);
            ShowOverlay(devSceneReloadConfirmOverlay);
        }

        private void HideSceneReloadConfirmation()
        {
            HideOverlay(devSceneReloadConfirmOverlay);
            SetNavigationGroup(NavGroup.None, null);
        }

        private void ConfirmSceneReload()
        {
            var firebase = ClawMachine.Mechanics.FirebaseRESTService.Instance;
            if ((firebase != null && firebase.IsWriteInProgress) || !HasRecoverableSceneSession()) return;

            pendingSceneRecovery = new SceneRecoveryData
            {
                name = registeredName,
                insta = registeredInsta,
                bio = registeredBio,
                gender = registeredGender
            };
            hasPendingSceneRecovery = true;

            // 보유 코인은 차감하지 않고 현재 값을 확실히 보존합니다.
            PlayerPrefs.SetInt("LoveCatcher_Coins", currentCoins);
            PlayerPrefs.Save();

            if (buttonClickSound != null && ClawMachine.Audio.SoundManager.Instance != null)
                ClawMachine.Audio.SoundManager.Instance.PlaySFX(buttonClickSound);
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private IEnumerator ResumeGameAfterSceneReload()
        {
            SceneRecoveryData recovery = pendingSceneRecovery;
            hasPendingSceneRecovery = false;

            yield return new WaitUntil(() =>
                ClawMachine.Mechanics.GameFlowManager.Instance != null &&
                ClawMachine.Mechanics.GameFlowManager.Instance.IsInitialized);

            registeredName = recovery.name;
            registeredInsta = recovery.insta;
            registeredBio = recovery.bio;
            registeredGender = recovery.gender;
            isDuplicateRegistration = true;
            pendingCoinsToCharge = 0;

            HideAllOverlays();
            ClawMachine.Mechanics.GameFlowManager.Instance.StartGameSession(
                registeredName,
                registeredInsta,
                registeredBio,
                registeredGender,
                true);

            Debug.Log($"[게임 복구] 씬 리로드 후 {registeredName} 참가자의 게임을 코인 차감 없이 다시 시작했습니다.");
        }

        private void ToggleDevMode()
        {
            if (devModeOverlay == null) return;
            if (BoothStaffAuth.Instance == null || !BoothStaffAuth.Instance.IsAdmin) return;
            if (devSceneReloadConfirmOverlay?.style.display == DisplayStyle.Flex) return;

            if (devModeOverlay.style.display == DisplayStyle.Flex)
            {
                HideOverlay(devModeOverlay);
            }
            else
            {
                // 열릴 때 현재 값으로 동기화
                RefreshDevProbSliders();
                if (ClawMachine.Mechanics.GameFlowManager.Instance != null)
                {
                    if (ClawMachine.Mechanics.GameFlowManager.Instance.clawController != null && ClawMachine.Mechanics.GameFlowManager.Instance.clawController.clawGripper != null)
                    {
                        if (devGripForceSlider != null) devGripForceSlider.value = ClawMachine.Mechanics.GameFlowManager.Instance.clawController.clawGripper.baseGripForce;
                    }
                }
                ShowOverlay(devModeOverlay);
            }
        }

        private void RestoreDevSaveCoinsBtnText()
        {
            if (devSaveCoinsBtn != null)
            {
                devSaveCoinsBtn.text = "저장";
            }
        }

        private void OpenDbView()
        {
            HideOverlay(devModeOverlay);
            ShowOverlay(devDbOverlay);
            ResetDevAddForm();
            RefreshDbView();
        }

        private void ResetDevAddForm()
        {
            if (devAddName != null) devAddName.value = "";
            if (devAddInsta != null) devAddInsta.value = "";
            if (devAddBio != null) devAddBio.value = "";
            if (devAddStatusLabel != null) devAddStatusLabel.style.display = DisplayStyle.None;
            devAddSelectedGender = "남";
            SetDevAddGender("남");
        }

        private void SetDevAddGender(string gender)
        {
            devAddSelectedGender = gender;
            if (devAddGenderMaleBtn != null)
            {
                devAddGenderMaleBtn.style.backgroundColor = gender == "남"
                    ? new StyleColor(new Color(0f, 0.55f, 1f))
                    : new StyleColor(new Color(0.31f, 0.31f, 0.31f, 0.5f));
                devAddGenderMaleBtn.style.borderTopColor = gender == "남"
                    ? new StyleColor(new Color(0f, 0.93f, 1f))
                    : new StyleColor(new Color(1f, 1f, 1f, 0.2f));
                devAddGenderMaleBtn.style.borderBottomColor = devAddGenderMaleBtn.style.borderTopColor;
                devAddGenderMaleBtn.style.borderLeftColor = devAddGenderMaleBtn.style.borderTopColor;
                devAddGenderMaleBtn.style.borderRightColor = devAddGenderMaleBtn.style.borderTopColor;
            }
            if (devAddGenderFemaleBtn != null)
            {
                devAddGenderFemaleBtn.style.backgroundColor = gender == "여"
                    ? new StyleColor(new Color(1f, 0f, 0.5f))
                    : new StyleColor(new Color(0.31f, 0.31f, 0.31f, 0.5f));
                devAddGenderFemaleBtn.style.borderTopColor = gender == "여"
                    ? new StyleColor(new Color(1f, 0.4f, 0.8f))
                    : new StyleColor(new Color(1f, 1f, 1f, 0.2f));
                devAddGenderFemaleBtn.style.borderBottomColor = devAddGenderFemaleBtn.style.borderTopColor;
                devAddGenderFemaleBtn.style.borderLeftColor = devAddGenderFemaleBtn.style.borderTopColor;
                devAddGenderFemaleBtn.style.borderRightColor = devAddGenderFemaleBtn.style.borderTopColor;
            }
        }

        private void HandleDevAddSubmit()
        {
            if (devAddName == null || devAddInsta == null) return;

            string name = devAddName.value.Trim();
            string insta = devAddInsta.value.Trim();
            string bio = devAddBio != null ? devAddBio.value.Trim() : "";

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(insta))
            {
                ShowDevAddStatus("⚠ 이름과 인스타 아이디는 필수입니다.", new Color(1f, 0.4f, 0.2f));
                return;
            }

            if (ClawMachine.Mechanics.FirebaseRESTService.Instance == null)
            {
                ShowDevAddStatus("⚠ Firebase 연결 없음", new Color(1f, 0.4f, 0.2f));
                return;
            }

            devAddSubmitBtn.SetEnabled(false);
            devAddSubmitBtn.text = "...";

            StartCoroutine(ClawMachine.Mechanics.FirebaseRESTService.Instance.RegisterPlayer(
                name, insta, bio, devAddSelectedGender, 0, success =>
                {
                    if (success)
                    {
                        ShowDevAddStatus($"✅ '{name}' ({devAddSelectedGender}) 등록 완료!", new Color(0f, 1f, 0.5f));
                        devAddName.value = "";
                        devAddInsta.value = "";
                        devAddBio.value = "";
                        RefreshDbView(); // 목록 갱신
                    }
                    else
                    {
                        ShowDevAddStatus("❌ 등록 실패. Firebase 오류를 확인하세요.", new Color(1f, 0.3f, 0.3f));
                    }
                    devAddSubmitBtn.SetEnabled(true);
                    devAddSubmitBtn.text = "등록";
                }));
        }

        private void ShowDevAddStatus(string msg, Color color)
        {
            if (devAddStatusLabel == null) return;
            devAddStatusLabel.text = msg;
            devAddStatusLabel.style.color = new StyleColor(color);
            devAddStatusLabel.style.display = DisplayStyle.Flex;
        }

        private void CloseDbView()
        {
            HideOverlay(devDbOverlay);
            ShowOverlay(devModeOverlay);
        }

        private void RefreshDbView()
        {
            if (devDbScrollView == null || ClawMachine.Mechanics.FirebaseRESTService.Instance == null) return;
            
            devDbScrollView.Clear();
            var loadingLabel = new Label("데이터를 불러오는 중...");
            loadingLabel.style.color = Color.white;
            devDbScrollView.Add(loadingLabel);

            StartCoroutine(ClawMachine.Mechanics.FirebaseRESTService.Instance.GetAllParticipants(dataList => {
                cachedDbData = dataList ?? new System.Collections.Generic.List<ClawMachine.Mechanics.ParticipantData>();
                string query = devDbSearchInput != null ? devDbSearchInput.value : "";
                FilterDbView(query);
            }));
        }

        private void FilterDbView(string query)
        {
            if (devDbScrollView == null) return;
            devDbScrollView.Clear();

            var filteredData = cachedDbData;
            if (!string.IsNullOrEmpty(query))
            {
                string lowerQuery = query.ToLower();
                filteredData = cachedDbData.FindAll(d => d.name != null && d.name.ToLower().Contains(lowerQuery));
            }

            if (filteredData == null || filteredData.Count == 0)
            {
                var emptyLabel = new Label(string.IsNullOrEmpty(query) ? "등록된 참가자가 없습니다." : "검색 결과가 없습니다.");
                emptyLabel.style.color = Color.white;
                devDbScrollView.Add(emptyLabel);
                return;
            }

            foreach (var data in filteredData)
            {
                devDbScrollView.Add(CreateDbRow(data));
            }
        }

        private VisualElement CreateDbRow(ClawMachine.Mechanics.ParticipantData data)
        {
            var row = new VisualElement();
            row.AddToClassList("db-row");

            var nameInput = new TextField() { value = data.name };
            nameInput.AddToClassList("db-input");
            nameInput.style.width = 60;

            var instaInput = new TextField() { value = data.insta };
            instaInput.AddToClassList("db-input");
            instaInput.style.width = 120;

            var genderInput = new TextField() { value = data.gender };
            genderInput.AddToClassList("db-input");
            genderInput.style.width = 40;

            var bioInput = new TextField() { value = data.bio };
            bioInput.AddToClassList("db-input");
            bioInput.style.width = 150;

            var isPickedToggle = new Toggle("뽑힘") { value = data.isPicked };
            isPickedToggle.AddToClassList("db-toggle");
            isPickedToggle.style.color = Color.white;

            var saveBtn = new Button() { text = "저장" };
            saveBtn.AddToClassList("db-btn");
            saveBtn.style.backgroundColor = new Color(0, 0.5f, 0);

            var deleteBtn = new Button() { text = "삭제" };
            deleteBtn.AddToClassList("db-btn");
            deleteBtn.style.backgroundColor = new Color(0.8f, 0, 0);

            saveBtn.clicked += () => {
                saveBtn.text = "...";
                saveBtn.SetEnabled(false);
                var newData = new ClawMachine.Mechanics.ParticipantData
                {
                    documentId = data.documentId,
                    name = nameInput.value,
                    insta = instaInput.value,
                    gender = genderInput.value,
                    bio = bioInput.value,
                    isPicked = isPickedToggle.value,
                    attempts = data.attempts
                };
                StartCoroutine(ClawMachine.Mechanics.FirebaseRESTService.Instance.UpdateParticipantFullData(newData, success => {
                    saveBtn.text = success ? "완료!" : "실패";
                    saveBtn.SetEnabled(true);
                }));
            };

            deleteBtn.clicked += () => {
                ShowDevDeleteParticipantConfirmation(data, row);
            };

            row.Add(nameInput);
            row.Add(instaInput);
            row.Add(genderInput);
            row.Add(bioInput);
            row.Add(isPickedToggle);
            row.Add(saveBtn);
            row.Add(deleteBtn);

            return row;
        }

        private void ShowDevDeleteParticipantConfirmation(
            ClawMachine.Mechanics.ParticipantData participant,
            VisualElement participantRow)
        {
            if (string.IsNullOrEmpty(participant.documentId)) return;

            pendingDeleteParticipant = participant;
            pendingDeleteParticipantRow = participantRow;

            if (devDeleteParticipantTargetLabel != null)
            {
                string displayName = string.IsNullOrWhiteSpace(participant.name) ? "(이름 없음)" : participant.name;
                string displayInsta = string.IsNullOrWhiteSpace(participant.insta) ? "(미입력)" : participant.insta;
                devDeleteParticipantTargetLabel.text = $"이름: {displayName}\n인스타 아이디: {displayInsta}";
            }

            var firebase = ClawMachine.Mechanics.FirebaseRESTService.Instance;
            bool canDelete = firebase != null && !firebase.IsWriteInProgress;
            if (devDeleteParticipantStatusLabel != null)
            {
                devDeleteParticipantStatusLabel.text = canDelete
                    ? ""
                    : "다른 Firebase 저장 작업이 진행 중입니다. 완료 후 다시 시도해 주세요.";
                devDeleteParticipantStatusLabel.style.display = canDelete
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (devDeleteParticipantConfirmBtn != null)
            {
                devDeleteParticipantConfirmBtn.text = "삭제 진행";
                devDeleteParticipantConfirmBtn.SetEnabled(canDelete);
            }
            if (devDeleteParticipantCancelBtn != null)
            {
                devDeleteParticipantCancelBtn.SetEnabled(true);
            }

            ShowOverlay(devDeleteParticipantConfirmOverlay);
        }

        private void HideDevDeleteParticipantConfirmation()
        {
            HideOverlay(devDeleteParticipantConfirmOverlay);
            ClearPendingParticipantDelete();
            SetNavigationGroup(NavGroup.None, null);
        }

        private void ClearPendingParticipantDelete()
        {
            pendingDeleteParticipant = null;
            pendingDeleteParticipantRow = null;

            if (devDeleteParticipantTargetLabel != null)
                devDeleteParticipantTargetLabel.text = "이름: -\n인스타 아이디: -";
            if (devDeleteParticipantStatusLabel != null)
                devDeleteParticipantStatusLabel.style.display = DisplayStyle.None;
            if (devDeleteParticipantConfirmBtn != null)
            {
                devDeleteParticipantConfirmBtn.text = "삭제 진행";
                devDeleteParticipantConfirmBtn.SetEnabled(true);
            }
            if (devDeleteParticipantCancelBtn != null)
                devDeleteParticipantCancelBtn.SetEnabled(true);
        }

        private void BeginDevParticipantDelete()
        {
            var firebase = ClawMachine.Mechanics.FirebaseRESTService.Instance;
            if (!pendingDeleteParticipant.HasValue || firebase == null) return;

            if (firebase.IsWriteInProgress)
            {
                if (devDeleteParticipantStatusLabel != null)
                {
                    devDeleteParticipantStatusLabel.text = "다른 Firebase 저장 작업이 진행 중입니다. 완료 후 다시 시도해 주세요.";
                    devDeleteParticipantStatusLabel.style.display = DisplayStyle.Flex;
                }
                return;
            }

            var participantToDelete = pendingDeleteParticipant.Value;
            var rowToDelete = pendingDeleteParticipantRow;

            if (devDeleteParticipantConfirmBtn != null)
            {
                devDeleteParticipantConfirmBtn.text = "Firebase 삭제 중...";
                devDeleteParticipantConfirmBtn.SetEnabled(false);
            }
            if (devDeleteParticipantCancelBtn != null)
                devDeleteParticipantCancelBtn.SetEnabled(false);
            if (devDeleteParticipantStatusLabel != null)
                devDeleteParticipantStatusLabel.style.display = DisplayStyle.None;

            StartCoroutine(firebase.DeleteParticipant(participantToDelete.documentId, success => {
                if (success)
                {
                    cachedDbData.RemoveAll(item => item.documentId == participantToDelete.documentId);
                    if (rowToDelete != null && rowToDelete.parent != null)
                        rowToDelete.RemoveFromHierarchy();
                    HideDevDeleteParticipantConfirmation();
                    RefreshDevModeStats();
                    return;
                }

                if (devDeleteParticipantStatusLabel != null)
                {
                    devDeleteParticipantStatusLabel.text = "Firebase 삭제에 실패했습니다. 연결 상태를 확인한 후 다시 시도해 주세요.";
                    devDeleteParticipantStatusLabel.style.display = DisplayStyle.Flex;
                }
                if (devDeleteParticipantConfirmBtn != null)
                {
                    devDeleteParticipantConfirmBtn.text = "다시 시도";
                    devDeleteParticipantConfirmBtn.SetEnabled(true);
                }
                if (devDeleteParticipantCancelBtn != null)
                    devDeleteParticipantCancelBtn.SetEnabled(true);
            }));
        }

        public bool IsUserTyping()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return false;
            var focused = uiDocument.rootVisualElement.focusController.focusedElement;
            if (focused != null)
            {
                var type = focused.GetType();
                if (type.Name.Contains("TextField") || type.Name.Contains("TextInput") || focused is TextField)
                {
                    return true;
                }
            }
            return false;
        }

        public enum NavGroup
        {
            None,
            BottomBar,
            Success,
            QuitConfirm,
            DevStatsResetConfirm,
            DevSceneReloadConfirm,
            DevParticipantDeleteConfirm,
            Fail
        }

        private NavGroup currentNavGroup = NavGroup.None;
        private Button[] currentButtons = null;
        private int selectedIndex = 0;
        private bool isJoystickDpadPressed = false;

        private void SetNavigationGroup(NavGroup group, Button[] buttons)
        {
            // Clean up old focus
            if (currentButtons != null)
            {
                foreach (var btn in currentButtons)
                {
                    if (btn != null) btn.RemoveFromClassList("arcade-focus");
                }
            }

            currentNavGroup = group;
            currentButtons = buttons;
            selectedIndex = 0;

            // Set new focus
            if (currentButtons != null && currentButtons.Length > 0 && currentButtons[0] != null)
            {
                currentButtons[0].AddToClassList("arcade-focus");
            }
        }

        private void ChangeSelection(int direction)
        {
            if (currentButtons == null || currentButtons.Length <= 1) return;

            // Remove focus from old
            if (selectedIndex >= 0 && selectedIndex < currentButtons.Length && currentButtons[selectedIndex] != null)
            {
                currentButtons[selectedIndex].RemoveFromClassList("arcade-focus");
            }

            // Calculate new index
            selectedIndex = (selectedIndex + direction + currentButtons.Length) % currentButtons.Length;

            // Add focus to new
            if (selectedIndex >= 0 && selectedIndex < currentButtons.Length && currentButtons[selectedIndex] != null)
            {
                currentButtons[selectedIndex].AddToClassList("arcade-focus");
                
                // Play subtle selection tick SFX
                if (buttonClickSound != null && ClawMachine.Audio.SoundManager.Instance != null)
                {
                    ClawMachine.Audio.SoundManager.Instance.PlaySFX(buttonClickSound);
                }
            }
        }

        private void HandleJoystickActionPressed()
        {
            if (currentNavGroup != NavGroup.None && currentButtons != null)
            {
                if (selectedIndex >= 0 && selectedIndex < currentButtons.Length && currentButtons[selectedIndex] != null)
                {
                    var button = currentButtons[selectedIndex];
                    if (button.enabledSelf && button.resolvedStyle.display != DisplayStyle.None)
                    {
                        TriggerButtonAction(button);
                    }
                }
            }
        }

        private void TriggerButtonAction(Button button)
        {
            if (button == null) return;
            
            // Play click sound
            if (buttonClickSound != null && ClawMachine.Audio.SoundManager.Instance != null) 
                ClawMachine.Audio.SoundManager.Instance.PlaySFX(buttonClickSound);

            if (button == retryButton)
            {
                ExecuteRetryAction();
            }
            else if (button == quitButton)
            {
                ResetToRegistration();
            }
            else if (button == successRetryBtn)
            {
                ExecuteSuccessRetryClick();
            }
            else if (button == successExitBtn)
            {
                HandleSuccessExitClick();
            }
            else if (button == quitConfirmNoBtn)
            {
                HandleQuitConfirmNoClick();
            }
            else if (button == quitConfirmYesBtn)
            {
                HandleQuitConfirmYesClick();
            }
            else if (button == devResetStatsCancelBtn)
            {
                HideDevStatsResetConfirmation();
            }
            else if (button == devResetStatsConfirmBtn)
            {
                BeginDevStatsReset();
            }
            else if (button == devSceneReloadCancelBtn)
            {
                HideSceneReloadConfirmation();
            }
            else if (button == devSceneReloadConfirmBtn)
            {
                ConfirmSceneReload();
            }
            else if (button == devDeleteParticipantCancelBtn)
            {
                HideDevDeleteParticipantConfirmation();
            }
            else if (button == devDeleteParticipantConfirmBtn)
            {
                BeginDevParticipantDelete();
            }
            else if (button == failCloseBtn)
            {
                ResetToRegistration();
            }
            else if (button == failRetryAgainBtn)
            {
                ExecuteFailRetryAgainClick();
            }
            else if (button == failRetryCloseBtn)
            {
                HandleRetryPaymentBack();
            }
            else if (button == btnCoinPack1)
            {
                SelectCoinPackage(1, btnCoinPack1, new Button[] { btnCoinPack1, btnCoinPack2, btnCoinPack3, btnCoinPack4 });
            }
            else if (button == btnCoinPack2)
            {
                SelectCoinPackage(3, btnCoinPack2, new Button[] { btnCoinPack1, btnCoinPack2, btnCoinPack3, btnCoinPack4 });
            }
            else if (button == btnCoinPack3)
            {
                SelectCoinPackage(6, btnCoinPack3, new Button[] { btnCoinPack1, btnCoinPack2, btnCoinPack3, btnCoinPack4 });
            }
            else if (button == btnCoinPack4)
            {
                SelectCoinPackage(13, btnCoinPack4, new Button[] { btnCoinPack1, btnCoinPack2, btnCoinPack3, btnCoinPack4 });
            }
            else if (button == regCoinPack1)
            {
                SelectCoinPackage(1, regCoinPack1, new Button[] { regCoinPack1, regCoinPack2, regCoinPack3, regCoinPack4 });
            }
            else if (button == regCoinPack2)
            {
                SelectCoinPackage(3, regCoinPack2, new Button[] { regCoinPack1, regCoinPack2, regCoinPack3, regCoinPack4 });
            }
            else if (button == regCoinPack3)
            {
                SelectCoinPackage(6, regCoinPack3, new Button[] { regCoinPack1, regCoinPack2, regCoinPack3, regCoinPack4 });
            }
            else if (button == regCoinPack4)
            {
                SelectCoinPackage(13, regCoinPack4, new Button[] { regCoinPack1, regCoinPack2, regCoinPack3, regCoinPack4 });
            }
            else if (button == btnCoinStart)
            {
                ExecuteCoinStartClick();
            }
        }

        // ================= COIN SYSTEM HELPERS =================

        private void SelectCoinPackage(int amount, Button clickedButton, Button[] siblingButtons)
        {
            // Play button sound
            if (buttonClickSound != null && ClawMachine.Audio.SoundManager.Instance != null) 
                ClawMachine.Audio.SoundManager.Instance.PlaySFX(buttonClickSound);

            pendingCoinsToCharge = amount;

            // Highlight selected button
            foreach (var btn in siblingButtons)
            {
                if (btn != null)
                {
                    btn.RemoveFromClassList("gender-button-male-selected");
                }
            }

            if (clickedButton != null)
            {
                clickedButton.AddToClassList("gender-button-male-selected");
            }

            UpdateCoinUI();
            Debug.Log($"[코인 선택] {amount}개 패키지 선택됨. 결제 시작 시 반영 대기중.");
        }

        private void ClearStagedCoins()
        {
            pendingCoinsToCharge = 0;

            // Clear highlights from all pack buttons
            Button[] allPackButtons = { btnCoinPack1, btnCoinPack2, btnCoinPack3, btnCoinPack4, regCoinPack1, regCoinPack2, regCoinPack3, regCoinPack4 };
            foreach (var btn in allPackButtons)
            {
                if (btn != null)
                {
                    btn.RemoveFromClassList("gender-button-male-selected");
                }
            }

            UpdateCoinUI();
        }

        public void UpdateCoinUI()
        {
            if (coinCountText != null)
            {
                coinCountText.text = $"{currentCoins}개";
            }
            if (popupCoinCountText != null)
            {
                popupCoinCountText.text = $"보유 코인: {currentCoins}개";
            }
            if (registerCoinCountText != null)
            {
                registerCoinCountText.text = $"보유 코인: {currentCoins}개";
            }

            if (btnCoinStart != null)
            {
                btnCoinStart.style.display = (currentCoins > 0 || pendingCoinsToCharge > 0) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (failRetryAgainBtn != null)
            {
                failRetryAgainBtn.text = (currentCoins > 0 || pendingCoinsToCharge > 0) ? "🎯 한 번만 더! (1코인 차감)" : "🎯 한 번만 더! (500원)";
            }

            // 충전 팝업이 활성화되어 있는 경우 네비게이션 그룹 동적 갱신
            if (failRetryOverlay != null && failRetryOverlay.resolvedStyle.display == DisplayStyle.Flex)
            {
                var buttonsList = new System.Collections.Generic.List<Button>();
                if (btnCoinPack1 != null) buttonsList.Add(btnCoinPack1);
                if (btnCoinPack2 != null) buttonsList.Add(btnCoinPack2);
                if (btnCoinPack3 != null) buttonsList.Add(btnCoinPack3);
                if (btnCoinPack4 != null) buttonsList.Add(btnCoinPack4);
                
                if (btnCoinStart != null && (currentCoins > 0 || pendingCoinsToCharge > 0))
                {
                    buttonsList.Add(btnCoinStart);
                }
                
                if (failRetryCloseBtn != null) buttonsList.Add(failRetryCloseBtn);
                
                SetNavigationGroup(NavGroup.Fail, buttonsList.ToArray());
            }
        }

        public bool HasCoins()
        {
            return currentCoins > 0;
        }

        public void DeductCoinDirectly()
        {
            if (currentCoins > 0)
            {
                if (!BeginPlayTransition()) return;
                currentCoins--;
                PlayerPrefs.SetInt("LoveCatcher_Coins", currentCoins);
                PlayerPrefs.Save();
                UpdateCoinUI();
                
                // Record play session
                RecordPlaySession(0);

                HideAllOverlays();
                
                if (ClawMachine.Mechanics.GameFlowManager.Instance != null)
                {
                    ClawMachine.Mechanics.GameFlowManager.Instance.RequestRetry();
                }
            }
        }

        private void AddCoins(int amount)
        {
            currentCoins += amount;
            PlayerPrefs.SetInt("LoveCatcher_Coins", currentCoins);
            PlayerPrefs.Save();
            UpdateCoinUI();
            Debug.Log($"[코인 충전] {amount}개 코인 충전 완료. 현재 코인: {currentCoins}개");
        }

        private void ExecuteRetryAction()
        {
            if (currentCoins > 0)
            {
                if (!BeginPlayTransition()) return;
                currentCoins--;
                PlayerPrefs.SetInt("LoveCatcher_Coins", currentCoins);
                PlayerPrefs.Save();
                UpdateCoinUI();
                
                // Record play session
                RecordPlaySession(0);

                HideAllOverlays();
                retryPaymentOrigin = RetryPaymentOrigin.None;
                if (ClawMachine.Mechanics.GameFlowManager.Instance != null)
                {
                    ClawMachine.Mechanics.GameFlowManager.Instance.RequestRetry();
                }
            }
            else
            {
                OpenRetryPayment(RetryPaymentOrigin.BottomBar);
            }
        }

        private void ExecuteSuccessRetryClick()
        {
            if (currentCoins > 0)
            {
                if (!BeginPlayTransition()) return;
                currentCoins--;
                PlayerPrefs.SetInt("LoveCatcher_Coins", currentCoins);
                PlayerPrefs.Save();
                UpdateCoinUI();
                
                // Record play session
                RecordPlaySession(0);

                exitConfirmCount = 0;
                retryPaymentOrigin = RetryPaymentOrigin.None;
                OnContinueSession?.Invoke();
            }
            else
            {
                OpenRetryPayment(RetryPaymentOrigin.SuccessResult);
            }
        }

        private void ExecuteFailRetryAgainClick()
        {
            if (currentCoins > 0)
            {
                if (!BeginPlayTransition()) return;
                currentCoins--;
                PlayerPrefs.SetInt("LoveCatcher_Coins", currentCoins);
                PlayerPrefs.Save();
                UpdateCoinUI();
                
                // Record play session
                RecordPlaySession(0);

                HideAllOverlays();
                retryPaymentOrigin = RetryPaymentOrigin.None;
                if (ClawMachine.Mechanics.GameFlowManager.Instance != null)
                {
                    ClawMachine.Mechanics.GameFlowManager.Instance.RequestRetry();
                }
            }
            else
            {
                OpenRetryPayment(RetryPaymentOrigin.FailResult);
            }
        }

        public void ShowRegistrationError(string message)
        {
            HideAllOverlays();
            ShowOverlay(registerOverlay);
            if (registerWarningText != null)
            {
                registerWarningText.text = message;
                registerWarningText.style.display = DisplayStyle.Flex;
            }
        }

        private void OpenRetryPayment(RetryPaymentOrigin origin)
        {
            retryPaymentOrigin = origin;
            ClearStagedCoins();
            HideAllOverlays();
            ShowOverlay(failRetryOverlay);
        }

        private void HandleRetryPaymentBack()
        {
            RetryPaymentOrigin origin = retryPaymentOrigin;
            retryPaymentOrigin = RetryPaymentOrigin.None;

            // 결제가 확정되지 않은 패키지 선택은 뒤로 갈 때 폐기합니다.
            ClearStagedCoins();
            HideOverlay(failRetryOverlay);

            switch (origin)
            {
                case RetryPaymentOrigin.SuccessResult:
                    ShowOverlay(successOverlay);
                    break;
                case RetryPaymentOrigin.FailResult:
                    // 기존 실패 문구와 이모지는 유지하고 팝업만 다시 표시합니다.
                    ShowOverlay(failOverlay);
                    break;
                case RetryPaymentOrigin.BottomBar:
                default:
                    ShowRetryButton(true);
                    break;
            }
        }

        private void ExecuteCoinStartClick()
        {
            int totalAvailable = currentCoins + pendingCoinsToCharge;
            if (totalAvailable > 0)
            {
                if (!BeginPlayTransition()) return;
                int revenue = GetRevenueFromStagedCoins(pendingCoinsToCharge);
                currentCoins = totalAvailable - 1;
                pendingCoinsToCharge = 0;

                // Clear highlights from all pack buttons
                Button[] allPackButtons = { btnCoinPack1, btnCoinPack2, btnCoinPack3, btnCoinPack4, regCoinPack1, regCoinPack2, regCoinPack3, regCoinPack4 };
                foreach (var btn in allPackButtons)
                {
                    if (btn != null)
                    {
                        btn.RemoveFromClassList("gender-button-male-selected");
                    }
                }

                PlayerPrefs.SetInt("LoveCatcher_Coins", currentCoins);
                PlayerPrefs.Save();
                UpdateCoinUI();

                // Record play session
                RecordPlaySession(revenue);

                HideAllOverlays();

                RetryPaymentOrigin completedOrigin = retryPaymentOrigin;
                retryPaymentOrigin = RetryPaymentOrigin.None;
                if (completedOrigin == RetryPaymentOrigin.SuccessResult)
                {
                    OnContinueSession?.Invoke();
                }
                else
                {
                    if (ClawMachine.Mechanics.GameFlowManager.Instance != null)
                    {
                        ClawMachine.Mechanics.GameFlowManager.Instance.RequestRetry();
                    }
                }
            }
        }

        // ================= PUBLIC UI CONTROLLERS =================

        public void SetStats(int instaCount, int dollCount, int legendaryDollCount, float winChance)
        {
            if (instaCountText != null) instaCountText.text = instaCount >= 0 ? $"{instaCount}개" : "확인 필요";
            if (dollCountText != null) dollCountText.text = dollCount >= 0 ? $"{dollCount}개" : "확인 필요";
            if (legendaryDollCountText != null) legendaryDollCountText.text = legendaryDollCount >= 0 ? $"{legendaryDollCount}개" : "확인 필요";
            if (winChanceText != null) winChanceText.text = $"{winChance:F1}%";
        }

        public void UpdateRegisterPoolCount(int maleCount, int femaleCount)
        {
            if (registerMaleCountText != null) registerMaleCountText.text = maleCount >= 0 ? $"남성 아이디: {maleCount}개" : "남성 아이디: 확인 필요";
            if (registerFemaleCountText != null) registerFemaleCountText.text = femaleCount >= 0 ? $"여성 아이디: {femaleCount}개" : "여성 아이디: 확인 필요";
        }

        public void SetTimer(float seconds)
        {
            if (timerText != null)
            {
                timerText.text = $"{Mathf.CeilToInt(seconds)}s";
            }
        }

        public void SetAttempts(int currentAttempts, int maxPity = 5)
        {
            if (attemptCountText != null) attemptCountText.text = $"{currentAttempts}회";
            
            if (pityProgressBar != null)
            {
                float percent = (float)currentAttempts / maxPity * 100f;
                pityProgressBar.style.width = new StyleLength(Length.Percent(Mathf.Min(percent, 100f)));
            }

            if (pityStatusText != null)
            {
                pityStatusText.text = $"{maxPity}회차에 MAX 파워 발동! ({currentAttempts}/{maxPity})";
            }

            if (pityMaxBanner != null)
            {
                bool isMax = currentAttempts >= maxPity;
                pityMaxBanner.style.display = isMax ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void ShowRetryButton(bool show)
        {
            if (retryButton != null)
            {
                retryButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (quitButton != null)
            {
                quitButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (show)
            {
                SetNavigationGroup(NavGroup.BottomBar, new Button[] { retryButton, quitButton });
            }
            else
            {
                if (currentNavGroup == NavGroup.BottomBar)
                {
                    SetNavigationGroup(NavGroup.None, null);
                }
                HideOverlay(failRetryOverlay);
            }
        }

        public void ShowRewardPopup(ClawMachine.Mechanics.RewardType reward, string name, string gender, string insta, string bio)
        {
            retryPaymentOrigin = RetryPaymentOrigin.None;
            HideAllOverlays();
            
            // 끈질긴 그만두기 버튼 카운트 초기화
            exitConfirmCount = 0;

            var successCard = rootVisualElement().Q<VisualElement>("SuccessCard");
            if (gender == "여")
            {
                successCard.AddToClassList("popup-card-female");
                matchedName.style.color = new StyleColor(new Color(1f, 0f, 0.5f)); // Pink
            }
            else
            {
                successCard.RemoveFromClassList("popup-card-female");
                matchedName.style.color = new StyleColor(new Color(0f, 0.93f, 1f)); // Cyan
            }

            if (reward == ClawMachine.Mechanics.RewardType.Candy)
            {
                if (popupSuccessSound != null && ClawMachine.Audio.SoundManager.Instance != null)
                    ClawMachine.Audio.SoundManager.Instance.PlaySFX(popupSuccessSound);

                matchedName.text = "사탕 당첨! 🍬";
                matchedInsta.text = "달콤한 위로상";
                matchedBio.text = "“데스크에서 사탕을 받아가세요!”";

                if (successSubTitle != null)
                    successSubTitle.text = "축하합니다! 달콤한 사탕 보상이 당첨되었습니다.";
                if (dollPickupNotice != null)
                    dollPickupNotice.style.display = DisplayStyle.None;
            }
            else if (reward == ClawMachine.Mechanics.RewardType.Instagram)
            {
                // 아이디만
                if (popupSuccessSound != null && ClawMachine.Audio.SoundManager.Instance != null)
                    ClawMachine.Audio.SoundManager.Instance.PlaySFX(popupSuccessSound);

                matchedName.text = $"[아이디] {name} ({gender})";
                matchedInsta.text = insta;
                matchedBio.text = string.IsNullOrEmpty(bio) ? "“인스타 친구해요!”" : $"“{bio}”";

                if (successSubTitle != null)
                    successSubTitle.text = "축하합니다! 당신과 어울리는 이성의 인스타 카드입니다.";
                if (dollPickupNotice != null)
                    dollPickupNotice.style.display = DisplayStyle.None;
            }
            else if (reward == ClawMachine.Mechanics.RewardType.Doll)
            {
                // 일반 인형 당첨
                if (popupSuccessSound != null && ClawMachine.Audio.SoundManager.Instance != null)
                    ClawMachine.Audio.SoundManager.Instance.PlaySFX(popupSuccessSound);

                matchedName.text = "인형 당첨! 🎁";
                matchedInsta.text = "귀여운 실물 인형";
                matchedBio.text = "“데스크에서 실물 인형을 받아가세요!”";

                if (successSubTitle != null)
                    successSubTitle.text = "축하합니다! 귀여운 실물 인형 보상이 당첨되었습니다.";
                if (dollPickupNotice != null)
                    dollPickupNotice.style.display = DisplayStyle.Flex;
            }
            else if (reward == ClawMachine.Mechanics.RewardType.Legendary)
            {
                if (popupSuccessSound != null && ClawMachine.Audio.SoundManager.Instance != null)
                    ClawMachine.Audio.SoundManager.Instance.PlaySFX(popupSuccessSound);

                matchedName.text = "레전더리 당첨! 🌟";
                matchedInsta.text = "희귀한 레전더리 인형";
                matchedBio.text = "“데스크에서 레전더리 인형을 받아가세요!”";

                if (successSubTitle != null)
                    successSubTitle.text = "축하합니다! 레전더리 인형 보상이 당첨되었습니다.";
                if (dollPickupNotice != null)
                    dollPickupNotice.style.display = DisplayStyle.Flex;
            }

            bool hasRemainingPlays = currentCoins > 0;
            if (successRetryBtn != null)
            {
                successRetryBtn.text = hasRemainingPlays ? "이어하기 (코인 차감)" : "또 뽑기 (결제 필요)";
                successRetryBtn.style.marginRight = hasRemainingPlays ? 0f : 8f;
            }

            // 구매한 뽑기 횟수가 남아 있다면 중간 성공 화면에서는
            // 결제 안내와 종료 선택을 숨기고 남은 횟수를 이어서 사용하게 합니다.
            if (successExitBtn != null)
            {
                successExitBtn.style.display = hasRemainingPlays ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (successRetryQRCodeCard != null)
            {
                successRetryQRCodeCard.style.display = hasRemainingPlays ? DisplayStyle.None : DisplayStyle.Flex;
            }
            successCard.style.marginRight = hasRemainingPlays ? 0f : 40f;

            ShowOverlay(successOverlay);
        }

        private void HandleSuccessRetryClick()
        {
            exitConfirmCount = 0;
            OnContinueSession?.Invoke();
        }

        private void HandleSuccessExitClick()
        {
            exitConfirmCount = 1;
            ShowQuitConfirmPopup(1);
        }

        private void ShowQuitConfirmPopup(int stage)
        {
            HideAllOverlays();
            
            if (quitConfirmTitle != null)
            {
                if (stage == 1)
                {
                    quitConfirmTitle.text = "진짜 그만둘래요? 🥺";
                }
                else if (stage == 2)
                {
                    quitConfirmTitle.text = "진짜진짜 갈 거예요? 💔";
                }
                else if (stage == 3)
                {
                    quitConfirmTitle.text = "한 판만 더 해요😭";
                }
            }

            ShowOverlay(quitConfirmOverlay);
        }

        private void HandleQuitConfirmNoClick()
        {
            exitConfirmCount = 0;
            ExecuteSuccessRetryClick();
        }

        private void HandleQuitConfirmYesClick()
        {
            exitConfirmCount++;
            if (exitConfirmCount <= 3)
            {
                ShowQuitConfirmPopup(exitConfirmCount);
            }
            else
            {
                exitConfirmCount = 0;
                ResetToRegistration();
            }
        }

        public void HideAllOverlaysPublic()
        {
            HideAllOverlays();
        }

        /// <summary>
        /// 현재 참가자의 전체 게임플레이 루프 동안 상단 HUD를 표시합니다.
        /// </summary>
        public void SetTopBarVisible(bool visible)
        {
            if (topBar != null)
            {
                topBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void ShowFailPopup()
        {
            retryPaymentOrigin = RetryPaymentOrigin.None;
            HideAllOverlays();

            // 랜덤 귀여운 멘트 & 이모지 선택
            if (failCuteMsg != null)
                failCuteMsg.text = FailCuteMsgs[UnityEngine.Random.Range(0, FailCuteMsgs.Length)];
            if (failEmoji != null)
                failEmoji.text = FailEmojis[UnityEngine.Random.Range(0, FailEmojis.Length)];

            if (popupFailSound != null && ClawMachine.Audio.SoundManager.Instance != null)
            {
                ClawMachine.Audio.SoundManager.Instance.PlaySFX(popupFailSound);
            }

            ShowOverlay(failOverlay);
        }

        // ================= HELPERS =================

        private VisualElement rootVisualElement()
        {
            return uiDocument.rootVisualElement;
        }

        private void HideAllOverlays()
        {
            SetNavigationGroup(NavGroup.None, null);

            HideOverlay(registerOverlay);
            HideOverlay(successOverlay);
            HideOverlay(failOverlay);
            HideOverlay(quitConfirmOverlay);
            HideOverlay(devModeOverlay);
            HideOverlay(devResetStatsConfirmOverlay);
            HideOverlay(devSceneReloadConfirmOverlay);
            HideOverlay(devDbOverlay);
            HideOverlay(devDeleteParticipantConfirmOverlay);
            ClearPendingParticipantDelete();
            HideOverlay(failRetryOverlay);
        }

        private void ShowOverlay(VisualElement overlay)
        {
            if (overlay == devModeOverlay && (BoothStaffAuth.Instance == null || !BoothStaffAuth.Instance.IsAdmin)) return;
            if (overlay != null)
            {
                overlay.style.display = DisplayStyle.Flex;

                if (overlay == devModeOverlay)
                {
                    if (devTotalDollsInput != null && ClawMachine.Mechanics.GameFlowManager.Instance != null)
                    {
                        devTotalDollsInput.value = ClawMachine.Mechanics.GameFlowManager.Instance.totalDolls.ToString();
                        if (devSaveDollsBtn != null) devSaveDollsBtn.text = "저장";
                    }
                    if (devTotalLegendaryDollsInput != null && ClawMachine.Mechanics.GameFlowManager.Instance != null)
                    {
                        devTotalLegendaryDollsInput.value = ClawMachine.Mechanics.GameFlowManager.Instance.totalLegendaryDolls.ToString();
                        if (devSaveLegendaryDollsBtn != null) devSaveLegendaryDollsBtn.text = "저장";
                    }
                    if (devCoinsInput != null)
                    {
                        devCoinsInput.value = currentCoins.ToString();
                        if (devSaveCoinsBtn != null) devSaveCoinsBtn.text = "저장";
                    }

                    // Populate stats fields immediately when opening DevModeOverlay
                    RefreshDevModeStats();
                }

                // UI Navigation Group Setup
                if (overlay == successOverlay)
                {
                    SetNavigationGroup(
                        NavGroup.Success,
                        currentCoins > 0
                            ? new Button[] { successRetryBtn }
                            : new Button[] { successRetryBtn, successExitBtn });
                }
                else if (overlay == quitConfirmOverlay)
                {
                    SetNavigationGroup(NavGroup.QuitConfirm, new Button[] { quitConfirmNoBtn, quitConfirmYesBtn });
                }
                else if (overlay == devResetStatsConfirmOverlay)
                {
                    SetNavigationGroup(
                        NavGroup.DevStatsResetConfirm,
                        new Button[] { devResetStatsCancelBtn, devResetStatsConfirmBtn });
                }
                else if (overlay == devSceneReloadConfirmOverlay)
                {
                    SetNavigationGroup(
                        NavGroup.DevSceneReloadConfirm,
                        new Button[] { devSceneReloadCancelBtn, devSceneReloadConfirmBtn });
                }
                else if (overlay == devDeleteParticipantConfirmOverlay)
                {
                    SetNavigationGroup(
                        NavGroup.DevParticipantDeleteConfirm,
                        new Button[] { devDeleteParticipantCancelBtn, devDeleteParticipantConfirmBtn });
                }
                else if (overlay == failOverlay)
                {
                    SetNavigationGroup(NavGroup.Fail, new Button[] { failRetryAgainBtn, failCloseBtn });
                }
                else if (overlay == failRetryOverlay)
                {
                    // 보유 코인에 맞춰 패키지 버튼 및 도전 시작 버튼을 동적으로 아케이드 조이스틱 내비게이션 그룹에 배정
                    var buttonsList = new System.Collections.Generic.List<Button>();
                    if (btnCoinPack1 != null) buttonsList.Add(btnCoinPack1);
                    if (btnCoinPack2 != null) buttonsList.Add(btnCoinPack2);
                    if (btnCoinPack3 != null) buttonsList.Add(btnCoinPack3);
                    if (btnCoinPack4 != null) buttonsList.Add(btnCoinPack4);
                    
                    if (btnCoinStart != null && currentCoins > 0)
                    {
                        buttonsList.Add(btnCoinStart);
                    }
                    
                    if (failRetryCloseBtn != null) buttonsList.Add(failRetryCloseBtn);
                    
                    SetNavigationGroup(NavGroup.Fail, buttonsList.ToArray());
                }
                else if (overlay == registerOverlay)
                {
                    SetNavigationGroup(NavGroup.None, null);
                }
            }
        }

        private void RefreshDevProbSliders()
        {
            if (ClawMachine.Mechanics.GameFlowManager.Instance == null) return;
            var gm = ClawMachine.Mechanics.GameFlowManager.Instance;
            
            float pLegendary = 0, pDoll = 0, pInstagram = 0, pCandy = 0;
            switch(currentDevProbMode) {
                case DevProbMode.Male:
                    pLegendary = devIncludeInstagram ? gm.maleProbLegendary : gm.maleNoInstaProbLegendary;
                    pDoll = devIncludeInstagram ? gm.maleProbDoll : gm.maleNoInstaProbDoll;
                    pInstagram = devIncludeInstagram ? gm.maleProbInstagram : 0f;
                    pCandy = devIncludeInstagram ? gm.maleProbCandy : gm.maleNoInstaProbCandy;
                    break;
                case DevProbMode.Female:
                    pLegendary = devIncludeInstagram ? gm.femaleProbLegendary : gm.femaleNoInstaProbLegendary;
                    pDoll = devIncludeInstagram ? gm.femaleProbDoll : gm.femaleNoInstaProbDoll;
                    pInstagram = devIncludeInstagram ? gm.femaleProbInstagram : 0f;
                    pCandy = devIncludeInstagram ? gm.femaleProbCandy : gm.femaleNoInstaProbCandy;
                    break;
            }

            // 탭 전환/창 열기에서 화면만 갱신합니다. 일반 value 대입은 콜백을 발생시켜
            // 다른 성별의 실제 확률을 UI 값으로 역덮어쓸 수 있으므로 사용하지 않습니다.
            SetDevProbabilityControl(devProbLegendarySlider, devProbLegendaryInput, devProbLegendaryLabel, "레전더리", pLegendary);
            SetDevProbabilityControl(devProbDollSlider, devProbDollInput, devProbDollLabel, "인형", pDoll);
            SetDevProbabilityControl(devProbInstagramSlider, devProbInstagramInput, devProbInstagramLabel, "인스타 아이디", pInstagram);
            SetDevProbabilityControl(devProbCandySlider, devProbCandyInput, devProbCandyLabel, "사탕", pCandy);

            DisplayStyle instagramDisplay = devIncludeInstagram ? DisplayStyle.Flex : DisplayStyle.None;
            if (devProbInstagramRow != null) devProbInstagramRow.style.display = instagramDisplay;
            if (devProbInstagramSlider != null) devProbInstagramSlider.style.display = instagramDisplay;
            RefreshDevProbabilitySummary();
        }

        private static void SetDevProbabilityControl(
            Slider slider,
            TextField input,
            Label label,
            string rewardName,
            float value)
        {
            slider?.SetValueWithoutNotify(value);
            input?.SetValueWithoutNotify(value.ToString("F1"));
            if (label != null) label.text = $"[{rewardName}] 확률: {value:F1}%";
        }

        private void RefreshDevProbabilitySummary()
        {
            if (ClawMachine.Mechanics.GameFlowManager.Instance == null) return;
            var gm = ClawMachine.Mechanics.GameFlowManager.Instance;
            string gender = currentDevProbMode == DevProbMode.Male ? "남" : "여";
            if (!gm.TryGetGenderProbabilities(gender, devIncludeInstagram, out float legendary, out float doll, out float instagram, out float candy)) return;

            float configuredTotal = legendary + doll + instagram + candy;
            bool totalIsValid = Mathf.Approximately(configuredTotal, 100f);
            if (devProbabilityTotalWarning != null)
            {
                devProbabilityTotalWarning.text = totalIsValid
                    ? $"확률 합계: {configuredTotal:F1}%"
                    : $"⚠ 확률 합계: {configuredTotal:F1}% (100%가 아닙니다)";
                devProbabilityTotalWarning.style.color = totalIsValid
                    ? new StyleColor(new Color(0.3f, 1f, 0.55f))
                    : new StyleColor(new Color(1f, 0.85f, 0f));
            }

        }

        private void HideOverlay(VisualElement overlay)
        {
            if (overlay != null) overlay.style.display = DisplayStyle.None;
        }

        // ================= STATS HELPERS & ADJUSTMENTS =================

        private void ShowDevStatsResetConfirmation()
        {
            var firebase = ClawMachine.Mechanics.FirebaseRESTService.Instance;
            if (firebase == null || firebase.IsWriteInProgress) return;

            ShowOverlay(devResetStatsConfirmOverlay);
        }

        private void HideDevStatsResetConfirmation()
        {
            HideOverlay(devResetStatsConfirmOverlay);
            SetNavigationGroup(NavGroup.None, null);
        }

        private void BeginDevStatsReset()
        {
            var firebase = ClawMachine.Mechanics.FirebaseRESTService.Instance;
            if (firebase == null || firebase.IsWriteInProgress) return;

            HideDevStatsResetConfirmation();
            devResetStatsBtn.text = "초기화 진행 중...";
            devResetStatsBtn.SetEnabled(false);

            var zeroStats = new ClawMachine.Mechanics.GameStatsData
            {
                totalRevenue = 0,
                totalPlays = 0,
                totalSuccesses = 0,
                totalDolls = (ClawMachine.Mechanics.GameFlowManager.Instance != null) ? ClawMachine.Mechanics.GameFlowManager.Instance.totalDolls : 100,
                totalLegendaryDolls = (ClawMachine.Mechanics.GameFlowManager.Instance != null) ? ClawMachine.Mechanics.GameFlowManager.Instance.totalLegendaryDolls : 10
            };

            StartCoroutine(firebase.UpdateGameStats(
                zeroStats,
                new System.Collections.Generic.List<string> { "totalRevenue", "totalPlays", "totalSuccesses" },
                success => {
                    devResetStatsBtn.text = success ? "통계 초기화 완료!" : "통계 초기화 실패";
                    devResetStatsBtn.SetEnabled(true);
                    RefreshDevModeStats();
                    Invoke(nameof(RestoreDevResetStatsBtnText), 2f);
                }));
        }

        private void RefreshDevModeStats()
        {
            if (ClawMachine.Mechanics.FirebaseRESTService.Instance == null) return;

            StartCoroutine(ClawMachine.Mechanics.FirebaseRESTService.Instance.GetGameStats(stats => {
                if (devStatPlays != null) devStatPlays.text = $"총 플레이 횟수: {stats.totalPlays}회";
                if (devStatSuccesses != null) devStatSuccesses.text = $"총 성공(뽑기) 횟수: {stats.totalSuccesses}회";
                if (devStatRevenue != null) devStatRevenue.text = $"총 누적 수입: {stats.totalRevenue:N0}원";

                if (devTotalRevenueInput != null && !IsUserTyping()) devTotalRevenueInput.value = stats.totalRevenue.ToString();
                if (devTotalPlaysInput != null && !IsUserTyping()) devTotalPlaysInput.value = stats.totalPlays.ToString();
                if (devTotalSuccessesInput != null && !IsUserTyping()) devTotalSuccessesInput.value = stats.totalSuccesses.ToString();

                // Firebase의 실제 Participants 문서 개수를 조회하여 '등록된 총 인스타 ID 수'로 갱신
                StartCoroutine(ClawMachine.Mechanics.FirebaseRESTService.Instance.GetAllParticipants(list => {
                    int actualCount = (list != null) ? list.Count : stats.totalRegistrations;
                    if (devStatRegistrations != null) devStatRegistrations.text = $"등록된 총 인스타 ID 수: {actualCount}명";
                    if (devTotalRegistrationsInput != null && !IsUserTyping()) devTotalRegistrationsInput.value = actualCount.ToString();
                }));
            }));
        }

        private void RestoreDevResetStatsBtnText()
        {
            if (devResetStatsBtn != null)
            {
                devResetStatsBtn.text = "통계 데이터 초기화 (0으로 리셋)";
            }
        }

        private IEnumerator SaveSingleStatCoroutine(string fieldName, int value, Action<bool> callback)
        {
            if (ClawMachine.Mechanics.FirebaseRESTService.Instance == null)
            {
                callback?.Invoke(false);
                yield break;
            }

            ClawMachine.Mechanics.GameStatsData currentStats = new ClawMachine.Mechanics.GameStatsData();
            bool fetchDone = false;
            yield return ClawMachine.Mechanics.FirebaseRESTService.Instance.GetGameStats(stats => {
                currentStats = stats;
                fetchDone = true;
            });
            yield return new WaitUntil(() => fetchDone);

            if (fieldName == "totalRevenue") currentStats.totalRevenue = value;
            else if (fieldName == "totalRegistrations") currentStats.totalRegistrations = value;
            else if (fieldName == "totalPlays") currentStats.totalPlays = value;
            else if (fieldName == "totalSuccesses") currentStats.totalSuccesses = value;

            yield return ClawMachine.Mechanics.FirebaseRESTService.Instance.UpdateGameStats(
                currentStats, 
                new System.Collections.Generic.List<string> { fieldName }, 
                callback
            );
        }

        private int GetRevenueFromStagedCoins(int coins)
        {
            switch (coins)
            {
                case 1: return 500;
                case 3: return 1500;
                case 6: return 2500;
                case 13: return 5000;
                default: return 0;
            }
        }

        private void RecordPlaySession(int revenue)
        {
            if (ClawMachine.Mechanics.FirebaseRESTService.Instance != null)
            {
                ClawMachine.Mechanics.FirebaseRESTService.Instance.IncrementPlayCountAndRevenue(revenue,
                    (success, error) =>
                    {
                        if (!success) ShowRegistrationError("플레이 DB 기록 확인 실패. 운영진이 결제·회차를 확인해 주세요. " + error);
                    });
            }
        }
    }
}
