namespace ResupplyNetwork
{
    /// <summary>
    /// 难度设置页里的参数节点。
    ///
    /// 注意：GameParameters.CustomParameterNode 不是特性，KSP 在启动时通过
    /// GameParameters.GenerateParameterTypes() 自动扫描它的子类，所以不需要手动注册。
    /// 每项用 [CustomFloatParameterUI] 修饰的公有字段就是一个滑块。
    /// 读档时用 HighLogic.CurrentGame.Parameters.CustomParams&lt;ResupplyGameParams&gt;() 取回。
    /// </summary>
    public class ResupplyGameParams : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomFloatParameterUI("基础运费（资金/吨）",
            toolTip = "把 1 吨送到 LKO 的基准价，也是整套定价的锚点",
            minValue = 0f, maxValue = 20000f, stepCount = 200, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double KMass = 4000.0;

        [GameParameters.CustomFloatParameterUI("距离加价参照 Δv（m/s）",
            toolTip = "目的地的 Δv 相对这个值加价",
            minValue = 100f, maxValue = 10000f, stepCount = 100, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double DvRef = 1000.0;

        [GameParameters.CustomFloatParameterUI("时间加价（每年）",
            toolTip = "0.5 表示 1 年 ×1.5、3 年 ×2.5",
            minValue = 0f, maxValue = 5f, stepCount = 50, displayFormat = "N2",
            autoPersistance = true, unlockedDuringMission = true)]
        public double KTime = 0.5;

        [GameParameters.CustomFloatParameterUI("便利溢价",
            toolTip = "1.0＝与自己发射打平，1.25＝贵 25%",
            minValue = 0f, maxValue = 5f, stepCount = 50, displayFormat = "N2",
            autoPersistance = true, unlockedDuringMission = true)]
        public double KPremium = 1.0;

        [GameParameters.CustomFloatParameterUI("单船运力（吨/趟）",
            minValue = 1f, maxValue = 1000f, stepCount = 100, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double BTrip = 20.0;

        [GameParameters.CustomFloatParameterUI("每趟服务费（资金）",
            minValue = 0f, maxValue = 100000f, stepCount = 200, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double STrip = 800.0;

        [GameParameters.CustomFloatParameterUI("最低订单额（资金）",
            minValue = 0f, maxValue = 1000000f, stepCount = 200, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double SMin = 1000.0;

        [GameParameters.CustomFloatParameterUI("调度基础时间（小时）",
            minValue = 0f, maxValue = 240f, stepCount = 120, displayFormat = "N1",
            autoPersistance = true, unlockedDuringMission = true)]
        public double TBaseHours = 2.0;

        [GameParameters.CustomFloatParameterUI("取消失败退款比例",
            minValue = 0f, maxValue = 1f, stepCount = 100, displayFormat = "P0",
            autoPersistance = true, unlockedDuringMission = true)]
        public double CancelRefund = 0.8;

        [GameParameters.CustomFloatParameterUI("定时补给：触发阈值",
            toolTip = "余量低于容器容量的这个比例才发船",
            minValue = 0f, maxValue = 1f, stepCount = 100, displayFormat = "P0",
            autoPersistance = true, unlockedDuringMission = true)]
        public double AutoThreshold = 0.3;

        [GameParameters.CustomFloatParameterUI("定时补给：补充目标",
            minValue = 0f, maxValue = 1f, stepCount = 100, displayFormat = "P0",
            autoPersistance = true, unlockedDuringMission = true)]
        public double AutoTarget = 1.0;

        [GameParameters.CustomFloatParameterUI("定时补给：班期（天）",
            minValue = 1f, maxValue = 400f, stepCount = 100, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double AutoIntervalDays = 30.0;

        [GameParameters.CustomFloatParameterUI("定时补给：单笔预算上限",
            toolTip = "0＝不限",
            minValue = 0f, maxValue = 10000000f, stepCount = 200, displayFormat = "N0",
            addTextField = true, autoPersistance = true, unlockedDuringMission = true)]
        public double AutoMaxFunds = 50000.0;

        /// <summary>
        /// 只读参考行，由 ApplyGameParams 每次刷新时重算，
        /// 让玩家在拖动滑块时立刻看到"一次典型补给要多少钱、多久"。
        /// </summary>
        [GameParameters.CustomStringParameterUI("参考报价（随上面的滑块实时更新）", lines = 5)]
        public string Reference = "";

        public override string Title { get { return "Resupply Network"; } }
        public override string Section { get { return "RESUPPLY_NETWORK"; } }
        public override string DisplaySection { get { return "补给网 Resupply Network"; } }
        public override int SectionOrder { get { return 40; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override bool HasPresets { get { return false; } }
        public override void SetDifficultyPreset(GameParameters.Preset preset) { }
    }
}
