using System;
using System.Collections.Generic;
using UnityEngine;

namespace ResupplyNetwork
{
    /// <summary>独立的 mod 窗口，从工具栏按钮打开。支持多物资清单式下单。</summary>
    public class ResupplyWindow : MonoBehaviour
    {
        public static ResupplyWindow Instance { get; private set; }

        private const string LockId = "ResupplyNetworkWindow";
        private const int WindowId = 0x524E0001;
        private const int RulesWindowId = 0x524E0002;
        private const float MinW = 720f, MinH = 520f, MaxW = 1900f, MaxH = 1400f;
        private const float GripSize = 20f;

        private bool _visible;
        private bool _resizing;
        private Rect _rect = new Rect(200f, 60f, 1000f, 900f);

        private Vector2 _vesselScroll;
        private Vector2 _resourceScroll;
        private Vector2 _cartScroll;
        private Vector2 _pendingScroll;
        private Vector2 _bodyScroll;
        private Vector2 _typeScroll;

        private string _bodyFilter;                                   // null = 全部天体
        private readonly HashSet<VesselType> _typeFilter = new HashSet<VesselType>();

        private Guid _vesselId = Guid.Empty;
        private string _resource;
        private string _amountText = "100";
        private string _status = string.Empty;
        private readonly List<CartItem> _cart = new List<CartItem>();

        // 定时补给规则（独立窗口）
        private bool _rulesVisible;
        private Rect _rulesRect = new Rect(340f, 140f, 760f, 560f);
        private Vector2 _rulesScroll;
        private string _autoThresholdText = "30";
        private string _autoTargetText = "100";
        private string _autoIntervalText = "30";
        private string _autoBudgetText = "50000";

        private static readonly HashSet<VesselType> SkippedTypes = new HashSet<VesselType>
        {
            VesselType.Debris, VesselType.Flag, VesselType.EVA,
            VesselType.SpaceObject, VesselType.Unknown, VesselType.DroppedPart,
        };

        private void Awake()
        {
            Instance = this;
            var s = ResupplySettings.Instance;
            _autoThresholdText = (s.AutoThreshold * 100.0).ToString("0.###");
            _autoTargetText = (s.AutoTarget * 100.0).ToString("0.###");
            _autoIntervalText = s.AutoIntervalDays.ToString("0.###");
            _autoBudgetText = s.AutoMaxFunds.ToString("0");
        }

        private void OnDestroy()
        {
            ReleaseLocks();
            if (Instance == this) Instance = null;
        }

        public bool IsVisible { get { return _visible; } }

        public void Toggle() { SetVisible(!_visible); }

        public void SetVisible(bool v)
        {
            _visible = v;
            if (v) InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS, LockId);
            else { _rulesVisible = false; ReleaseLocks(); }
            ToolbarIntegration.SetButtonActive(v);
        }

        public void ToggleRules()
        {
            _rulesVisible = !_rulesVisible;
            if (_rulesVisible && !_visible) SetVisible(true);
        }

        private void ReleaseLocks()
        {
            InputLockManager.RemoveControlLock(LockId);
            InputLockManager.RemoveControlLock(LockId + "_cam");
        }

        /// <summary>鼠标在窗口上时锁住相机，避免滚轮把后面的场景一起缩放。</summary>
        private void SetCameraLock(bool on)
        {
            string id = LockId + "_cam";
            if (on) InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS, id);
            else InputLockManager.RemoveControlLock(id);
        }

        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!_visible)
            {
                SetCameraLock(false);
                return;
            }

            if (HighLogic.Skin != null) GUI.skin = HighLogic.Skin;
            _rect = GUILayout.Window(WindowId, _rect, DrawWindow, ToolbarIntegration.ModName,
                                     GUILayout.MinWidth(MinW), GUILayout.MinHeight(MinH));

            bool over = _rect.Contains(Event.current.mousePosition);
            if (_rulesVisible)
            {
                _rulesRect = GUILayout.Window(RulesWindowId, _rulesRect, DrawRulesWindow, "定时补给规则",
                                              GUILayout.MinWidth(680f), GUILayout.MinHeight(420f));
                over |= _rulesRect.Contains(Event.current.mousePosition);
            }
            SetCameraLock(over);
        }

        private void DrawWindow(int id)
        {
            float h = _rect.height;
            float vesselH = Mathf.Max(210f, h * 0.24f);
            float resourceH = Mathf.Max(170f, h * 0.22f);
            float cartH = Mathf.Max(110f, h * 0.14f);
            float pendingH = Mathf.Max(110f, h * 0.14f);

            GUILayout.BeginVertical();

            DrawVesselSection(vesselH);
            DrawResourceSection(resourceH);
            DrawCartSection(cartH);
            DrawPendingSection(pendingH);

            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
            DrawResizeGrip();
        }

        private void DrawResizeGrip()
        {
            var grip = new Rect(_rect.width - GripSize, _rect.height - GripSize, GripSize, GripSize);
            GUI.Label(grip, "◢");

            if (Event.current.type == EventType.MouseDown && grip.Contains(Event.current.mousePosition))
            {
                _resizing = true;
                Event.current.Use();
            }
            if (_resizing && Event.current.type == EventType.MouseDrag)
            {
                _rect.width = Mathf.Clamp(_rect.width + Event.current.delta.x, MinW, MaxW);
                _rect.height = Mathf.Clamp(_rect.height + Event.current.delta.y, MinH, MaxH);
                Event.current.Use();
            }
            if (Event.current.type == EventType.MouseUp) _resizing = false;
        }

        // ------------------------------------------------------------------

        private void DrawVesselSection(float height)
        {
            GUILayout.Label("1. 目标航天器");

            List<Vessel> all = EligibleVessels();
            if (all.Count == 0)
            {
                GUILayout.Label("（当前场景没有可补给的航天器）");
                return;
            }

            // ---- 收集筛选维度：只列出实际存在的天体与类型 ----
            var bodyKeys = new List<string>();
            var bodyLabels = new Dictionary<string, string>();
            var typeKeys = new List<VesselType>();
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i];
                if (v.mainBody != null)
                {
                    string key = v.mainBody.bodyName;
                    if (!string.IsNullOrEmpty(key) && !bodyKeys.Contains(key))
                    {
                        bodyKeys.Add(key);
                        bodyLabels[key] = BodyLabel(v.mainBody);
                    }
                }
                if (!typeKeys.Contains(v.vesselType)) typeKeys.Add(v.vesselType);
            }
            bodyKeys.Sort((a, b) => string.Compare(bodyLabels[a], bodyLabels[b], StringComparison.Ordinal));
            typeKeys.Sort((a, b) => string.Compare(VesselTypeLabel(a), VesselTypeLabel(b), StringComparison.Ordinal));

            if (_bodyFilter != null && !bodyKeys.Contains(_bodyFilter)) _bodyFilter = null;
            _typeFilter.RemoveWhere(t => !typeKeys.Contains(t));

            // ---- 天体筛选 ----
            GUILayout.BeginHorizontal();
            GUILayout.Label("天体", GUILayout.Width(40f));
            _bodyScroll = GUILayout.BeginScrollView(_bodyScroll, false, false, GUILayout.Height(26f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_bodyFilter == null, "全部", GUILayout.Width(64f)) != (_bodyFilter == null))
                _bodyFilter = null;
            for (int i = 0; i < bodyKeys.Count; i++)
            {
                string key = bodyKeys[i];
                bool on = _bodyFilter == key;
                if (GUILayout.Toggle(on, bodyLabels[key], GUILayout.Width(96f)) != on)
                    _bodyFilter = key;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();

            // ---- 类型筛选（可多选）----
            GUILayout.BeginHorizontal();
            GUILayout.Label("类型", GUILayout.Width(40f));
            _typeScroll = GUILayout.BeginScrollView(_typeScroll, false, false, GUILayout.Height(26f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("全部", GUILayout.Width(64f))) _typeFilter.Clear();
            for (int i = 0; i < typeKeys.Count; i++)
            {
                VesselType t = typeKeys[i];
                bool on = _typeFilter.Contains(t);
                if (GUILayout.Toggle(on, VesselTypeLabel(t), GUILayout.Width(96f)) != on)
                {
                    if (on) _typeFilter.Remove(t);
                    else _typeFilter.Add(t);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();

            // ---- 应用筛选 ----
            var vessels = new List<Vessel>();
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i];
                if (_bodyFilter != null)
                {
                    if (v.mainBody == null || v.mainBody.bodyName != _bodyFilter) continue;
                }
                if (_typeFilter.Count > 0 && !_typeFilter.Contains(v.vesselType)) continue;
                vessels.Add(v);
            }
            GUILayout.Label("显示 " + vessels.Count + " / " + all.Count + " 艘");

            _vesselScroll = GUILayout.BeginScrollView(_vesselScroll, GUILayout.Height(height));
            if (vessels.Count == 0)
            {
                GUILayout.Label("（当前筛选条件下没有航天器）");
            }
            for (int i = 0; i < vessels.Count; i++)
            {
                var v = vessels[i];
                bool selected = v.id == _vesselId;
                string label = v.vesselName + "   —   " + BodyLabel(v.mainBody) + " / " + v.situation
                               + (v.loaded ? "" : "   (未加载)");
                if (GUILayout.Toggle(selected, label) != selected)
                {
                    _vesselId = v.id;
                    _resource = null;
                    _cart.Clear();
                    _status = string.Empty;
                }
            }
            GUILayout.EndScrollView();
        }

        private static string BodyLabel(CelestialBody body)
        {
            if (body == null) return "(未知)";
            string name = body.displayName;
            if (string.IsNullOrEmpty(name)) name = body.bodyName;
            return name;
        }

        /// <summary>舰船类型的中文标签；未知类型回退到枚举名。</summary>
        private static string VesselTypeLabel(VesselType t)
        {
            switch (t)
            {
                case VesselType.Ship: return "飞船";
                case VesselType.Station: return "空间站";
                case VesselType.Probe: return "探测器";
                case VesselType.Lander: return "着陆器";
                case VesselType.Rover: return "巡视车";
                case VesselType.Base: return "地面基地";
                case VesselType.Plane: return "飞机";
                case VesselType.Relay: return "中继星";
                case VesselType.DeployedScienceController: return "科学站";
                case VesselType.DeployedSciencePart: return "科考设备";
                case VesselType.DeployedGroundPart: return "地面部署件";
                case VesselType.Debris: return "碎片";
                case VesselType.Flag: return "旗帜";
                case VesselType.EVA: return "宇航员";
                case VesselType.SpaceObject: return "小天体";
                case VesselType.Unknown: return "未知";
                default: return t.ToString();
            }
        }

        private void DrawResourceSection(float height)
        {
            GUILayout.Label("2. 资源（选中后加入清单）");
            Vessel v = SelectedVessel();
            if (v == null)
            {
                GUILayout.Label("（先选一个航天器）");
                return;
            }

            _resourceScroll = GUILayout.BeginScrollView(_resourceScroll, GUILayout.Height(height));
            var names = VesselResources.ListResources(v);
            if (names.Count == 0)
            {
                GUILayout.Label("（该航天器没有任何资源容器）");
            }
            else
            {
                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    double amount = VesselResources.GetAmount(v, name);
                    double free = VesselResources.GetFreeCapacity(v, name);
                    string tail = ResourceCatalog.IsExcluded(name) ? "   [排除]" : "";
                    string label = ResourceCatalog.DisplayName(name) + "   " + amount.ToString("N0") + " / "
                                   + (amount + free).ToString("N0") + "   可注入 " + free.ToString("N0") + tail;

                    bool selected = name == _resource;
                    if (GUILayout.Toggle(selected, label) != selected)
                    {
                        _resource = name;
                        _amountText = free > 0 ? Math.Floor(free).ToString("F0") : "0";
                        _status = string.Empty;
                    }
                }
            }
            GUILayout.EndScrollView();

            // 滑块按“容器剩余容量”的百分比走，0-100%；旁边的输入框保留精确输入
            double freeCap = string.IsNullOrEmpty(_resource) ? 0.0 : VesselResources.GetFreeCapacity(v, _resource);
            double typed;
            if (!double.TryParse(_amountText, out typed) || typed < 0.0) typed = 0.0;

            GUILayout.BeginHorizontal();
            GUILayout.Label("数量", GUILayout.Width(40f));

            float pctIn = freeCap > 0.0 ? (float)Math.Min(1.0, Math.Max(0.0, typed / freeCap)) : 0f;
            float pctOut = GUILayout.HorizontalSlider(pctIn, 0f, 1f, GUILayout.MinWidth(180f));
            if (Math.Abs(pctOut - pctIn) > 0.0005f)
                _amountText = Math.Floor(freeCap * pctOut).ToString("F0");
            GUILayout.Label((pctOut * 100f).ToString("N0") + "%", GUILayout.Width(46f));

            _amountText = GUILayout.TextField(_amountText, GUILayout.Width(110f));
            GUILayout.Label("/ " + freeCap.ToString("N0"), GUILayout.Width(110f));

            if (GUILayout.Button("25%", GUILayout.Width(50f))) SetAmountFraction(v, 0.25);
            if (GUILayout.Button("50%", GUILayout.Width(50f))) SetAmountFraction(v, 0.50);
            if (GUILayout.Button("满", GUILayout.Width(46f))) SetAmountFraction(v, 1.0);

            GUI.enabled = !string.IsNullOrEmpty(_resource);
            if (GUILayout.Button("加入清单", GUILayout.Width(110f))) AddToCart();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawCartSection(float height)
        {
            GUILayout.Label("3. 清单（一次下单可包含多种物资）");
            if (_cart.Count == 0)
            {
                GUILayout.Label("（清单为空，先在上面选资源并加入）");
                return;
            }

            _cartScroll = GUILayout.BeginScrollView(_cartScroll, GUILayout.Height(height));
            for (int i = _cart.Count - 1; i >= 0; i--)
            {
                var c = _cart[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(ResourceCatalog.DisplayName(c.Resource) + "   " + c.Amount.ToString("N0"), GUILayout.Width(320f));
                if (GUILayout.Button("移除", GUILayout.Width(70f))) _cart.RemoveAt(i);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            Vessel v = SelectedVessel();
            if (v == null) return;

            MultiQuote q = CostModel.QuoteMulti(v, _cart);
            if (!q.Ok)
            {
                GUILayout.Label("无法报价: " + q.Reason);
                return;
            }

            GUILayout.Label("合计 " + q.TotalMass.ToString("N3") + " t   运力 " + q.Trips + " 趟   目的地 " + q.ZoneName
                            + "   运费率 " + q.FreightRate.ToString("N0") + " 资金/吨");
            GUILayout.Label("物资 " + q.MaterialCost.ToString("N0")
                            + "  +  运费 " + q.FreightCost.ToString("N0")
                            + "  +  服务 " + q.ServiceCost.ToString("N0")
                            + "  =  " + q.Total.ToString("N0") + " 资金");
            GUILayout.Label("预计交付 " + CostCore.FormatDuration(q.LeadTime)
                            + (q.AnyCapped ? "   ⚠ 部分物资已按容器剩余容量截断" : string.Empty));

            bool hasQueue = ResupplyQueue.Instance != null;
            bool hasFunds = Funding.Instance != null && Funding.Instance.Funds >= q.Total;

            GUILayout.BeginHorizontal();
            GUI.enabled = hasQueue && hasFunds;
            if (GUILayout.Button(hasQueue
                    ? (hasFunds ? "确认下单（" + q.Total.ToString("N0") + " 资金）" : "资金不足")
                    : "场景未就绪", GUILayout.Width(300f)))
            {
                PlaceOrder(v, q);
            }
            GUI.enabled = true;
            if (GUILayout.Button("清空清单", GUILayout.Width(110f))) _cart.Clear();
            GUILayout.EndHorizontal();
        }

        private void DrawPendingSection(float height)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("4. 待交付", GUILayout.Width(120f));
            int ruleCount = ResupplyQueue.Instance != null ? ResupplyQueue.Instance.AutoRules.Count : 0;
            if (GUILayout.Button("定时补给规则…（" + ruleCount + "）", GUILayout.Width(220f)))
                ToggleRules();
            GUILayout.EndHorizontal();

            if (ResupplyQueue.Instance == null)
            {
                GUILayout.Label("（队列未就绪）");
                return;
            }

            var pending = ResupplyQueue.Instance.Pending;
            if (pending.Count == 0)
            {
                GUILayout.Label("（没有待交付订单）");
                return;
            }

            double ut = Planetarium.GetUniversalTime();
            _pendingScroll = GUILayout.BeginScrollView(_pendingScroll, GUILayout.Height(height));
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var d = pending[i];
                double remain = d.DueUT - ut;
                string desc = (string.IsNullOrEmpty(d.VesselName) ? "(未知)" : d.VesselName)
                              + "   " + d.KindCount + " 种物资"
                              + "   还有 " + CostCore.FormatDuration(remain < 0 ? 0 : remain);

                GUILayout.BeginHorizontal();
                GUILayout.Label(desc, GUILayout.Width(420f));
                if (GUILayout.Button("明细", GUILayout.Width(70f)))
                {
                    var sb = new System.Text.StringBuilder();
                    for (int j = 0; j < d.Items.Count; j++)
                    {
                        if (sb.Length > 0) sb.Append("，");
                        sb.Append(ResourceCatalog.DisplayName(d.Items[j].Resource)).Append(" ").Append(d.Items[j].Amount.ToString("N0"));
                    }
                    _status = "订单明细: " + sb;
                }
                if (GUILayout.Button("取消", GUILayout.Width(70f)))
                {
                    double refund = ResupplyQueue.Instance.CancelWithRefund(i);
                    _status = "已取消，退回 " + refund.ToString("N0") + " 资金";
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------

        private static List<Vessel> EligibleVessels()
        {
            var list = new List<Vessel>();
            var all = FlightGlobals.Vessels;
            if (all == null) return list;
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i];
                if (v == null) continue;
                if (SkippedTypes.Contains(v.vesselType)) continue;
                list.Add(v);
            }
            return list;
        }

        private Vessel SelectedVessel()
        {
            if (_vesselId == Guid.Empty) return null;
            return ResupplyQueue.FindVessel(_vesselId);
        }

        private void SetAmountFraction(Vessel v, double fraction)
        {
            if (v == null || string.IsNullOrEmpty(_resource)) return;
            double free = VesselResources.GetFreeCapacity(v, _resource);
            _amountText = Math.Floor(free * fraction).ToString("F0");
            _status = string.Empty;
        }

        private void AddToCart()
        {
            if (string.IsNullOrEmpty(_resource)) return;
            if (ResourceCatalog.IsExcluded(_resource))
            {
                _status = "\"" + ResourceCatalog.DisplayName(_resource) + "\" 在排除表里，不能补给";
                return;
            }

            double amount;
            if (!double.TryParse(_amountText, out amount) || amount <= 0.0)
            {
                _status = "数量无效";
                return;
            }

            for (int i = 0; i < _cart.Count; i++)
            {
                if (_cart[i].Resource == _resource)
                {
                    _cart[i].Amount = amount;
                    _status = "已更新清单中的 " + ResourceCatalog.DisplayName(_resource);
                    return;
                }
            }

            _cart.Add(new CartItem { Resource = _resource, Amount = amount });
            _status = "已加入清单: " + ResourceCatalog.DisplayName(_resource) + " " + amount.ToString("N0");
        }

        private void PlaceOrder(Vessel v, MultiQuote q)
        {
            try
            {
                Funding.Instance.AddFunds(-q.Total, TransactionReasons.None);
                ResupplyQueue.Instance.Enqueue(v, q);
                _status = "已下单: " + q.Lines.Count + " 种物资，合计 " + q.TotalMass.ToString("N3")
                          + " t，" + CostCore.FormatDuration(q.LeadTime) + " 后到达";
                Debug.Log("[ResupplyNetwork] " + _status);
                _cart.Clear();
            }
            catch (Exception e)
            {
                _status = "下单失败: " + e.Message;
                Debug.LogError("[ResupplyNetwork] 下单异常: " + e);
            }
        }

        // ------------------------------------------------------------------
        //  定时补给规则窗口
        // ------------------------------------------------------------------

        private void DrawRulesWindow(int id)
        {
            GUILayout.BeginVertical();

            if (ResupplyQueue.Instance == null)
            {
                GUILayout.Label("（队列未就绪：需要在生涯存档的飞行 / 航天中心 / 追踪站场景内）");
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
                return;
            }

            GUILayout.Label("模拟现实里的补给班期：每 N 天检查一次余量，低于阈值线才发船。");
            GUILayout.Label("规则使用主窗口当前选中的航天器与资源。若该物资已有在途订单，本次检查会跳过。");

            Vessel v = SelectedVessel();
            string res = _resource;
            GUILayout.Label(v == null
                ? "目标: （主窗口里还没选航天器）"
                : "目标: " + v.vesselName + "    资源: "
                  + (string.IsNullOrEmpty(res) ? "（未选）" : ResourceCatalog.DisplayName(res)));

            GUILayout.BeginHorizontal();
            GUILayout.Label("阈值 %", GUILayout.Width(48f));
            _autoThresholdText = GUILayout.TextField(_autoThresholdText, GUILayout.Width(58f));
            GUILayout.Label("补到 %", GUILayout.Width(52f));
            _autoTargetText = GUILayout.TextField(_autoTargetText, GUILayout.Width(58f));
            GUILayout.Label("每 N 天", GUILayout.Width(52f));
            _autoIntervalText = GUILayout.TextField(_autoIntervalText, GUILayout.Width(58f));
            GUILayout.Label("单笔预算", GUILayout.Width(58f));
            _autoBudgetText = GUILayout.TextField(_autoBudgetText, GUILayout.Width(90f));
            GUILayout.EndHorizontal();

            GUI.enabled = v != null && !string.IsNullOrEmpty(res);
            if (GUILayout.Button("添加 / 更新规则", GUILayout.Width(180f))) AddRuleFromSelection(v, res);
            GUI.enabled = true;

            GUILayout.Label("已有规则（" + ResupplyQueue.Instance.AutoRules.Count + " 条）");

            var rules = ResupplyQueue.Instance.AutoRules;
            float listH = Mathf.Max(140f, _rulesRect.height * 0.45f);
            _rulesScroll = GUILayout.BeginScrollView(_rulesScroll, GUILayout.Height(listH));

            if (rules.Count == 0)
            {
                GUILayout.Label("（还没有规则）");
            }
            else
            {
                double now = Planetarium.GetUniversalTime();
                for (int i = rules.Count - 1; i >= 0; i--)
                {
                    var r = rules[i];
                    GUILayout.BeginHorizontal();

                    bool enabled = r.Enabled;
                    if (GUILayout.Toggle(enabled, "", GUILayout.Width(24f)) != enabled) r.Enabled = !enabled;

                    string next;
                    if (!r.Checked) next = "立即";
                    else
                    {
                        double remain = r.LastCheckUT + r.Interval - now;
                        next = CostCore.FormatDuration(remain < 0.0 ? 0.0 : remain);
                    }

                    string desc = (string.IsNullOrEmpty(r.VesselName) ? "(未知)" : r.VesselName)
                                  + " · " + ResourceCatalog.DisplayName(r.Resource)
                                  + " · 低于 " + (r.Threshold * 100.0).ToString("N0")
                                  + "% 补到 " + (r.Target * 100.0).ToString("N0") + "%"
                                  + " · 每 " + r.IntervalDays + " 天"
                                  + (r.Budget > 0.0 ? " · 预算 " + r.Budget.ToString("N0") : " · 不限预算")
                                  + " · 下次检查 " + next;
                    GUILayout.Label(desc, GUILayout.Width(540f));

                    if (GUILayout.Button("删除", GUILayout.Width(60f)))
                    {
                        ResupplyQueue.Instance.RemoveRule(i);
                        _status = "已删除一条定时补给规则";
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void AddRuleFromSelection(Vessel v, string resource)
        {
            if (ResupplyQueue.Instance == null) return;
            if (v == null || string.IsNullOrEmpty(resource)) return;
            if (ResourceCatalog.IsExcluded(resource))
            {
                _status = "\"" + ResourceCatalog.DisplayName(resource) + "\" 在排除表里，不能建定时补给规则";
                return;
            }

            double threshold = ParsePercent(_autoThresholdText, 30.0);
            double target = ParsePercent(_autoTargetText, 100.0);
            double days = ParseNumber(_autoIntervalText, 30.0);
            double budget = ParseNumber(_autoBudgetText, 50000.0);

            if (target < threshold)
            {
                _status = "「补到」不能低于「阈值」，否则会陷入反复下单";
                return;
            }
            if (days <= 0.0)
            {
                _status = "间隔必须大于 0 天";
                return;
            }

            ResupplyQueue.Instance.AddRule(v, resource, threshold, target,
                                           days * CostCore.KerbinDay, budget);
            _status = "已保存规则: " + v.vesselName + " · " + ResourceCatalog.DisplayName(resource)
                      + "（低于 " + (threshold * 100.0).ToString("N0")
                      + "% 补到 " + (target * 100.0).ToString("N0")
                      + "%，每 " + days.ToString("0.##") + " 天检查）";
            Debug.Log("[ResupplyNetwork] " + _status);
        }

        private static double ParsePercent(string text, double fallbackPercent)
        {
            double v = ParseNumber(text, fallbackPercent);
            if (v > 100.0) v = 100.0;
            if (v < 0.0) v = 0.0;
            return v / 100.0;
        }

        private static double ParseNumber(string text, double fallback)
        {
            double d;
            if (!string.IsNullOrEmpty(text) && double.TryParse(text.Trim(), out d)) return d;
            return fallback;
        }
    }
}
