namespace ResupplyNetwork
{
    // 难度设置页里的参数节点。
    //
    // GameParameters.CustomParameterNode 不是特性：KSP 启动时通过
    // GameParameters.GenerateParameterTypes() 自动扫描它的子类，不需要手动注册。
    //
    // 每个子类 = 左侧列表里的一个独立条目。参数多的时候这是社区通行做法
    // （CrewLight / KSPWheel / SystemHeat / FFT / DeepFreeze 等都是拆成多个）。
    // 注意：多个节点若共用同一个 Section，KSP 会把它们横排成多列并塞进同一页，
    // 还会出现重名条目 —— 所以这里每个节点用各自的 Section 与 DisplaySection。
    //
    // 标题/tooltip 直接写本地化键，KSP 绘制时会解析（原版也是这么用 #autoLOC 的）。

    /// <summary>定价：运费、时间与实时参考报价。</summary>
    public class ResupplyGameParams : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_KMass", toolTip = "#LOC_RN_P_KMassT",
            minValue = 0f, maxValue = 20000f, stepCount = 200, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double KMass = 4000.0;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_DvRef", toolTip = "#LOC_RN_P_DvRefT",
            minValue = 100f, maxValue = 10000f, stepCount = 100, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double DvRef = 1000.0;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_KTime", toolTip = "#LOC_RN_P_KTimeT",
            minValue = 0f, maxValue = 5f, stepCount = 50, displayFormat = "N2",
            autoPersistance = true, unlockedDuringMission = true)]
        public double KTime = 0.5;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_KPremium", toolTip = "#LOC_RN_P_KPremiumT",
            minValue = 0f, maxValue = 5f, stepCount = 50, displayFormat = "N2",
            autoPersistance = true, unlockedDuringMission = true)]
        public double KPremium = 1.0;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_BTrip",
            minValue = 1f, maxValue = 1000f, stepCount = 100, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double BTrip = 20.0;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_STrip",
            minValue = 0f, maxValue = 100000f, stepCount = 200, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double STrip = 800.0;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_SMin",
            minValue = 0f, maxValue = 1000000f, stepCount = 200, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double SMin = 1000.0;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_TBase",
            minValue = 0f, maxValue = 240f, stepCount = 120, displayFormat = "N1",
            autoPersistance = true, unlockedDuringMission = true)]
        public double TBaseHours = 2.0;

        [GameParameters.CustomStringParameterUI("#LOC_RN_P_Reference", lines = 5)]
        public string Reference = "";

        // Title 是页内那块绿色标题，同样会被当成本地化键解析（截图里已确认过），
        // 所以跟 DisplaySection 用同一套键，页面标题和左侧条目名才对得上。
        public override string Title { get { return "#LOC_RN_SecPrice"; } }
        public override string Section { get { return "RESUPPLY_NETWORK_A"; } }
        public override string DisplaySection { get { return "#LOC_RN_SecPrice"; } }
        public override int SectionOrder { get { return 40; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override bool HasPresets { get { return false; } }
        public override void SetDifficultyPreset(GameParameters.Preset preset) { }
    }

    /// <summary>退款与加急（含通信链路前置条件）。</summary>
    public class ResupplyGameParamsOps : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_CancelRefund",
            minValue = 0f, maxValue = 1f, stepCount = 100, displayFormat = "P0",
            autoPersistance = true, unlockedDuringMission = true)]
        public double CancelRefund = 0.8;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_VesselLostRefund", toolTip = "#LOC_RN_P_VesselLostRefundT",
            minValue = 0f, maxValue = 1f, stepCount = 100, displayFormat = "P0",
            autoPersistance = true, unlockedDuringMission = true)]
        public double VesselLostRefund = 0.5;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_RushCost", toolTip = "#LOC_RN_P_RushCostT",
            minValue = 1f, maxValue = 10f, stepCount = 180, displayFormat = "N2",
            autoPersistance = true, unlockedDuringMission = true)]
        public double RushCostMultiplier = 2.5;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_RushTime", toolTip = "#LOC_RN_P_RushTimeT",
            minValue = 0.05f, maxValue = 1f, stepCount = 95, displayFormat = "P0",
            autoPersistance = true, unlockedDuringMission = true)]
        public double RushTimeFactor = 0.4;

        [GameParameters.CustomParameterUI("#LOC_RN_P_RequireComm", toolTip = "#LOC_RN_P_RequireCommT",
            autoPersistance = true, unlockedDuringMission = true)]
        public bool RequireCommLink = true;

        public override string Title { get { return "#LOC_RN_SecOps"; } }
        public override string Section { get { return "RESUPPLY_NETWORK_B"; } }
        public override string DisplaySection { get { return "#LOC_RN_SecOps"; } }
        public override int SectionOrder { get { return 41; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override bool HasPresets { get { return false; } }
        public override void SetDifficultyPreset(GameParameters.Preset preset) { }
    }

    /// <summary>定时补给的默认值。</summary>
    public class ResupplyGameParamsAuto : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_AutoThreshold",
            minValue = 0f, maxValue = 1f, stepCount = 100, displayFormat = "P0",
            autoPersistance = true, unlockedDuringMission = true)]
        public double AutoThreshold = 0.3;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_AutoTarget",
            minValue = 0f, maxValue = 1f, stepCount = 100, displayFormat = "P0",
            autoPersistance = true, unlockedDuringMission = true)]
        public double AutoTarget = 1.0;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_CadenceDays",
            minValue = 1f, maxValue = 400f, stepCount = 100, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double AutoIntervalDays = 30.0;

        [GameParameters.CustomFloatParameterUI("#LOC_RN_P_AutoBudget",
            minValue = 0f, maxValue = 10000000f, stepCount = 200, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double AutoMaxFunds = 50000.0;

        public override string Title { get { return "#LOC_RN_SecAuto"; } }
        public override string Section { get { return "RESUPPLY_NETWORK_C"; } }
        public override string DisplaySection { get { return "#LOC_RN_SecAuto"; } }
        public override int SectionOrder { get { return 42; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override bool HasPresets { get { return false; } }
        public override void SetDifficultyPreset(GameParameters.Preset preset) { }
    }
}
