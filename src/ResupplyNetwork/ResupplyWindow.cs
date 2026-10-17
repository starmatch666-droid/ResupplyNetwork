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

        private string _bodyFilter;
        private readonly HashSet<VesselType> _typeFilter = new HashSet<VesselType>();

        private Guid _vesselId = Guid.Empty;
        private string _resource;
        private string _amountText = "100";
        private string _status = string.Empty;
        private readonly List<CartItem> _cart = new List<CartItem>();
        private bool _rush;

        private string _resSearch = string.Empty;
        private int _resSort;

        private bool _rulesVisible;
        private Rect _rulesRect = new Rect(340f, 140f, 800f, 580f);
        private Vector2 _rulesScroll;
        private string _autoThresholdText = "30";
        private string _autoTargetText = "100";
        private string _autoIntervalText = "30";
        private string _autoBudgetText = "50000";
        private int _ruleScope;
        private bool _layoutLoaded;
        private bool _layoutDirty;
        private float _lastLayoutSave;

        // 四个分区的高度权重（相对窗口高度）。拖动分隔条只在相邻两个之间转移权重，
        // 总和不变 —— 所以各区能单独放大缩小，但永远撑不出窗口。
        private readonly float[] _secW = { 0.22f, 0.19f, 0.14f, 0.14f };
        private const float MinSecWeight = 0.05f;
        private int _dragSplitter = -1;

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
            if (_layoutLoaded) SaveLayout();
            if (Instance == this) Instance = null;
        }

        public bool IsVisible { get { return _visible; } }

        public void Toggle() { SetVisible(!_visible); }

        public void SetVisible(bool v)
        {
            if (v && !_layoutLoaded)
            {
                WindowLayout.Load("main", ref _rect, MinW, MinH, MaxW, MaxH);
                WindowLayout.Load("rules", ref _rulesRect, MinW, MinH, MaxW, MaxH);
                WindowLayout.LoadFloats("sections", _secW);
                _layoutLoaded = true;
            }
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

        private void SaveLayout()
        {
            WindowLayout.Save("main", _rect);
            WindowLayout.Save("rules", _rulesRect);
            WindowLayout.SaveFloats("sections", _secW);
            _layoutDirty = false;
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

            Rect before = _rect;
            _rect = GUILayout.Window(WindowId, _rect, DrawWindow, Loc.T("#LOC_RN_Title"),
                                     GUILayout.MinWidth(MinW), GUILayout.MinHeight(MinH));
            if (before != _rect) _layoutDirty = true;

            bool over = _rect.Contains(Event.current.mousePosition);
            if (_rulesVisible)
            {
                Rect rBefore = _rulesRect;
                _rulesRect = GUILayout.Window(RulesWindowId, _rulesRect, DrawRulesWindow, Loc.T("#LOC_RN_RulesTitle"),
                                              GUILayout.MinWidth(MinW), GUILayout.MinHeight(MinH));
                if (rBefore != _rulesRect) _layoutDirty = true;
                over |= _rulesRect.Contains(Event.current.mousePosition);
            }
            SetCameraLock(over);

            if (_layoutDirty && Time.realtimeSinceStartup - _lastLayoutSave > 5f)
            {
                _lastLayoutSave = Time.realtimeSinceStartup;
                SaveLayout();
            }
        }

        private void DrawWindow(int id)
        {
            float h = _rect.height;
            GUILayout.BeginVertical();
            DrawVesselSection(h * _secW[0]);
            DrawSplitter(0, h);
            DrawResourceSection(h * _secW[1]);
            DrawSplitter(1, h);
            DrawCartSection(h * _secW[2]);
            DrawSplitter(2, h);
            DrawPendingSection(h * _secW[3]);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.T("#LOC_RN_Close"), GUILayout.Width(90f))) SetVisible(false);
            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
            DrawResizeGrip();
        }

        /// <summary>
        /// 分区之间的分隔条。拖动时把权重从下面那区转移到上面那区（反之亦然），
        /// 两者之和不变，因此四个区的总高度恒定，永远不会超出窗口。
        /// </summary>
        private void DrawSplitter(int index, float h)
        {
            Rect r = GUILayoutUtility.GetRect(0f, 7f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                                new Color(1f, 1f, 1f, 0.10f), 0f, 0f);

            if (r.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown) { _dragSplitter = index; Event.current.Use(); }
            }
            if (_dragSplitter == index && Event.current.type == EventType.MouseDrag)
            {
                float delta = Event.current.delta.y / h;
                float upper = _secW[index] + delta;
                float lower = _secW[index + 1] - delta;
                if (upper >= MinSecWeight && lower >= MinSecWeight)
                {
                    _secW[index] = upper;
                    _secW[index + 1] = lower;
                    _layoutDirty = true;
                }
                Event.current.Use();
            }
            if (Event.current.type == EventType.MouseUp) _dragSplitter = -1;
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
                _layoutDirty = true;
                Event.current.Use();
            }
            if (Event.current.type == EventType.MouseUp) _resizing = false;
        }

        // ------------------------------------------------------------------

        private void DrawVesselSection(float height)
        {
            GUILayout.Label(Loc.T("#LOC_RN_Sec_Vessel"));

            List<Vessel> all = EligibleVessels();
            if (all.Count == 0)
            {
                GUILayout.Label(Loc.T("#LOC_RN_NoVessels"));
                return;
            }

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

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("#LOC_RN_Filter_Body"), GUILayout.Width(40f));
            _bodyScroll = GUILayout.BeginScrollView(_bodyScroll, false, false, GUILayout.Height(26f));
            GUILayout.BeginHorizontal();
            // 不设固定宽度：天体名和类型名长度差很大（DeployedScienceController 这种），
            // 写死宽度会让文字溢出、互相盖住。交给自适应，横向滚动条负责容纳。
            if (GUILayout.Toggle(_bodyFilter == null, Loc.T("#LOC_RN_Filter_All")) != (_bodyFilter == null))
                _bodyFilter = null;
            for (int i = 0; i < bodyKeys.Count; i++)
            {
                string key = bodyKeys[i];
                bool on = _bodyFilter == key;
                if (GUILayout.Toggle(on, bodyLabels[key]) != on) _bodyFilter = key;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("#LOC_RN_Filter_Type"), GUILayout.Width(40f));
            _typeScroll = GUILayout.BeginScrollView(_typeScroll, false, false, GUILayout.Height(26f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Loc.T("#LOC_RN_Filter_All"))) _typeFilter.Clear();
            for (int i = 0; i < typeKeys.Count; i++)
            {
                VesselType t = typeKeys[i];
                bool on = _typeFilter.Contains(t);
                if (GUILayout.Toggle(on, VesselTypeLabel(t)) != on)
                {
                    if (on) _typeFilter.Remove(t);
                    else _typeFilter.Add(t);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();

            var vessels = new List<Vessel>();
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i];
                if (_bodyFilter != null && (v.mainBody == null || v.mainBody.bodyName != _bodyFilter)) continue;
                if (_typeFilter.Count > 0 && !_typeFilter.Contains(v.vesselType)) continue;
                vessels.Add(v);
            }
            GUILayout.Label(Loc.T("#LOC_RN_Count", vessels.Count, all.Count));

            _vesselScroll = GUILayout.BeginScrollView(_vesselScroll, GUILayout.Height(height));
            if (vessels.Count == 0) GUILayout.Label(Loc.T("#LOC_RN_NoMatch"));
            for (int i = 0; i < vessels.Count; i++)
            {
                var v = vessels[i];
                bool selected = v.id == _vesselId;
                string label = v.vesselName + "   —   " + BodyLabel(v.mainBody) + " / " + v.situation
                               + (v.loaded ? "" : "   " + Loc.T("#LOC_RN_Unloaded"));
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

        private void DrawResourceSection(float height)
        {
            GUILayout.Label(Loc.T("#LOC_RN_Sec_Resource"));
            Vessel v = SelectedVessel();
            if (v == null)
            {
                GUILayout.Label(Loc.T("#LOC_RN_PickVessel"));
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("#LOC_RN_Search"), GUILayout.Width(44f));
            _resSearch = GUILayout.TextField(_resSearch, GUILayout.Width(180f));
            GUILayout.Label("|", GUILayout.Width(10f));
            if (GUILayout.Toggle(_resSort == 0, Loc.T("#LOC_RN_Sort_Name"), GUILayout.Width(70f))) _resSort = 0;
            if (GUILayout.Toggle(_resSort == 1, Loc.T("#LOC_RN_Sort_Free"), GUILayout.Width(70f))) _resSort = 1;
            if (GUILayout.Toggle(_resSort == 2, Loc.T("#LOC_RN_Sort_Price"), GUILayout.Width(70f))) _resSort = 2;
            GUILayout.EndHorizontal();

            var rows = ResourceRows(v);
            _resourceScroll = GUILayout.BeginScrollView(_resourceScroll, GUILayout.Height(height));
            if (rows.Count == 0) GUILayout.Label(Loc.T("#LOC_RN_NoTanks"));
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                string tail = r.Excluded ? "   " + Loc.T("#LOC_RN_Excluded") : "";
                string label = r.Label + "   " + r.Amount.ToString("N0") + " / " + r.Capacity.ToString("N0")
                               + "   " + Loc.T("#LOC_RN_Inject", r.Free.ToString("N0")) + tail;
                bool selected = r.Name == _resource;
                if (GUILayout.Toggle(selected, label) != selected)
                {
                    _resource = r.Name;
                    _amountText = r.Free > 0 ? Math.Floor(r.Free).ToString("F0") : "0";
                    _status = string.Empty;
                }
            }
            GUILayout.EndScrollView();

            double freeCap = string.IsNullOrEmpty(_resource) ? 0.0 : VesselResources.GetFreeCapacity(v, _resource);
            double typed;
            if (!double.TryParse(_amountText, out typed) || typed < 0.0) typed = 0.0;

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("#LOC_RN_Amount"), GUILayout.Width(40f));
            float pctIn = freeCap > 0.0 ? (float)Math.Min(1.0, Math.Max(0.0, typed / freeCap)) : 0f;
            float pctOut = GUILayout.HorizontalSlider(pctIn, 0f, 1f, GUILayout.MinWidth(150f));
            if (Math.Abs(pctOut - pctIn) > 0.0005f) _amountText = Math.Floor(freeCap * pctOut).ToString("F0");
            GUILayout.Label((pctOut * 100f).ToString("N0") + "%", GUILayout.Width(46f));
            _amountText = GUILayout.TextField(_amountText, GUILayout.Width(100f));
            GUILayout.Label("/ " + freeCap.ToString("N0"), GUILayout.Width(100f));
            if (GUILayout.Button("25%", GUILayout.Width(48f))) SetAmountFraction(v, 0.25);
            if (GUILayout.Button("50%", GUILayout.Width(48f))) SetAmountFraction(v, 0.50);
            GUI.enabled = !string.IsNullOrEmpty(_resource);
            if (GUILayout.Button(Loc.T("#LOC_RN_AddToCart"), GUILayout.Width(100f))) AddToCart(v);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            double thr = ResupplySettings.Instance.AutoThreshold * 100.0;
            if (GUILayout.Button(Loc.T("#LOC_RN_AddAllBelow", thr.ToString("N0") + "%"), GUILayout.Width(240f)))
                AddAllBelow(v);
        }

        private struct ResRow
        {
            public string Name;
            public string Label;
            public double Amount;
            public double Free;
            public double Capacity;
            public double Price;
            public bool Excluded;
        }

        private List<ResRow> ResourceRows(Vessel v)
        {
            var names = VesselResources.ListResources(v);
            var rows = new List<ResRow>(names.Count);
            string needle = (_resSearch ?? string.Empty).Trim().ToLowerInvariant();

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                string label = ResourceCatalog.DisplayName(name);
                if (needle.Length > 0)
                {
                    if (label.ToLowerInvariant().IndexOf(needle, StringComparison.Ordinal) < 0 &&
                        name.ToLowerInvariant().IndexOf(needle, StringComparison.Ordinal) < 0) continue;
                }

                var def = ResourceCatalog.Definition(name);
                var row = new ResRow();
                row.Name = name;
                row.Label = label;
                row.Amount = VesselResources.GetAmount(v, name);
                row.Free = VesselResources.GetFreeCapacity(v, name);
                row.Capacity = row.Amount + row.Free;
                row.Price = ResourceCatalog.UnitPrice(name, def);
                row.Excluded = ResourceCatalog.IsExcluded(name);
                rows.Add(row);
            }

            if (_resSort == 1) rows.Sort((a, b) => b.Free.CompareTo(a.Free));
            else if (_resSort == 2) rows.Sort((a, b) => b.Price.CompareTo(a.Price));
            else rows.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.Ordinal));
            return rows;
        }

        private void DrawCartSection(float height)
        {
            GUILayout.Label(Loc.T("#LOC_RN_Sec_Cart"));

            Vessel v = SelectedVessel();
            if (v == null)
            {
                GUILayout.Label(Loc.T("#LOC_RN_PickVessel"));
                return;
            }

            if (_cart.Count == 0)
            {
                GUILayout.Label(Loc.T("#LOC_RN_CartEmpty"));
                return;
            }

            _cartScroll = GUILayout.BeginScrollView(_cartScroll, GUILayout.Height(height));
            for (int i = _cart.Count - 1; i >= 0; i--)
            {
                var c = _cart[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(ResourceCatalog.DisplayName(c.Resource) + "   " + c.Amount.ToString("N0"), GUILayout.Width(320f));
                if (GUILayout.Button(Loc.T("#LOC_RN_Remove"), GUILayout.Width(80f))) _cart.RemoveAt(i);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            var s = ResupplySettings.Instance;
            _rush = GUILayout.Toggle(_rush, Loc.T("#LOC_RN_Rush",
                s.RushCostMultiplier.ToString("0.##"), s.RushTimeFactor.ToString("0.##")));

            MultiQuote q = CostModel.QuoteMulti(v, _cart, _rush);
            if (!q.Ok)
            {
                GUILayout.Label(Loc.T("#LOC_RN_CantOrder", q.Reason));
                return;
            }

            GUILayout.Label(Loc.T("#LOC_RN_Line_Origin", q.OriginName ?? string.Empty, q.ZoneName));
            GUILayout.Label(Loc.T("#LOC_RN_Line_Mass", q.TotalMass.ToString("N3"), q.Trips,
                                  q.ZoneName, q.FreightRate.ToString("N0")));
            GUILayout.Label(Loc.T("#LOC_RN_Line_Cost", q.MaterialCost.ToString("N0"),
                                  q.FreightCost.ToString("N0"), q.ServiceCost.ToString("N0"), q.Total.ToString("N0")));
            if (q.Rush) GUILayout.Label(Loc.T("#LOC_RN_Line_Rush", q.RushCost.ToString("N0")));
            GUILayout.Label(Loc.T("#LOC_RN_Line_Lead", CostCore.FormatDuration(q.LeadTime))
                            + (q.AnyCapped ? "   ⚠ " + Loc.T("#LOC_RN_Capped") : string.Empty));

            bool hasQueue = ResupplyQueue.Instance != null;
            // 沙盒/任务存档没有 Funding 模块，Funding.Instance 为 null ——
            // 那种情况下不扣款也不拦截，否则根本没法下单。
            bool noEconomy = Funding.Instance == null;
            bool hasFunds = noEconomy || Funding.Instance.Funds >= q.Total;

            GUILayout.BeginHorizontal();
            GUI.enabled = hasQueue && hasFunds;
            if (GUILayout.Button(hasQueue
                    ? (hasFunds ? Loc.T("#LOC_RN_PlaceOrder", q.Total.ToString("N0")) : Loc.T("#LOC_RN_NoFunds"))
                    : Loc.T("#LOC_RN_NotReady"), GUILayout.Width(300f)))
            {
                PlaceOrder(v, q);
            }
            GUI.enabled = true;
            if (GUILayout.Button(Loc.T("#LOC_RN_ClearCart"), GUILayout.Width(110f))) _cart.Clear();
            GUILayout.EndHorizontal();
            if (noEconomy) GUILayout.Label(Loc.T("#LOC_RN_NoEconomy"));
        }

        private void DrawPendingSection(float height)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("#LOC_RN_Sec_Pending"), GUILayout.Width(120f));
            int ruleCount = ResupplyQueue.Instance != null ? ResupplyQueue.Instance.AutoRules.Count : 0;
            if (GUILayout.Button(Loc.T("#LOC_RN_RulesButton", ruleCount), GUILayout.Width(220f))) ToggleRules();
            GUILayout.EndHorizontal();

            if (ResupplyQueue.Instance == null)
            {
                GUILayout.Label(Loc.T("#LOC_RN_QueueNotReady"));
                return;
            }

            var pending = ResupplyQueue.Instance.Pending;
            if (pending.Count == 0)
            {
                GUILayout.Label(Loc.T("#LOC_RN_NoPending"));
                return;
            }

            double ut = Planetarium.GetUniversalTime();
            _pendingScroll = GUILayout.BeginScrollView(_pendingScroll, GUILayout.Height(height));
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var d = pending[i];
                double remain = d.DueUT - ut;
                string name = string.IsNullOrEmpty(d.VesselName) ? "?" : d.VesselName;

                GUILayout.BeginHorizontal();
                string origin = string.IsNullOrEmpty(d.OriginName)
                    ? Loc.T("#LOC_RN_Origin_Home")
                    : d.OriginName;
                GUILayout.Label(Loc.T("#LOC_RN_Line_Pending", name, d.KindCount, origin,
                                      CostCore.FormatDuration(remain < 0 ? 0.0 : remain)), GUILayout.Width(560f));
                if (GUILayout.Button(Loc.T("#LOC_RN_Detail"), GUILayout.Width(70f)))
                {
                    var sb = new System.Text.StringBuilder();
                    for (int j = 0; j < d.Items.Count; j++)
                    {
                        if (sb.Length > 0) sb.Append(" | ");
                        sb.Append(ResourceCatalog.DisplayName(d.Items[j].Resource)).Append(" ").Append(d.Items[j].Amount.ToString("N0"));
                    }
                    _status = Loc.T("#LOC_RN_OrderDetail", sb.ToString());
                }
                if (GUILayout.Button(Loc.T("#LOC_RN_Cancel"), GUILayout.Width(70f)))
                {
                    double refund = ResupplyQueue.Instance.CancelWithRefund(i);
                    _status = Loc.T("#LOC_RN_Refunded", refund.ToString("N0"));
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

        private void AddToCart(Vessel v)
        {
            if (string.IsNullOrEmpty(_resource)) return;
            if (ResourceCatalog.IsExcluded(_resource))
            {
                _status = Loc.T("#LOC_RN_ExcludedCant", ResourceCatalog.DisplayName(_resource));
                return;
            }
            double amount;
            if (!double.TryParse(_amountText, out amount) || amount <= 0.0)
            {
                _status = Loc.T("#LOC_RN_BadAmount");
                return;
            }
            AddOrUpdate(_resource, amount);
        }

        private void AddOrUpdate(string resource, double amount)
        {
            for (int i = 0; i < _cart.Count; i++)
            {
                if (_cart[i].Resource == resource)
                {
                    _cart[i].Amount = amount;
                    _status = Loc.T("#LOC_RN_Updated", ResourceCatalog.DisplayName(resource));
                    return;
                }
            }
            _cart.Add(new CartItem { Resource = resource, Amount = amount });
            _status = Loc.T("#LOC_RN_Added", ResourceCatalog.DisplayName(resource), amount.ToString("N0"));
        }

        /// <summary>把船上所有低于阈值的资源一次性加进清单。</summary>
        private void AddAllBelow(Vessel v)
        {
            double threshold = ResupplySettings.Instance.AutoThreshold;
            var names = VesselResources.ListResources(v);
            int added = 0;
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (ResourceCatalog.IsExcluded(name)) continue;
                double amount = VesselResources.GetAmount(v, name);
                double free = VesselResources.GetFreeCapacity(v, name);
                double cap = amount + free;
                if (cap <= 0.0 || free <= 0.0) continue;
                if (amount / cap >= threshold) continue;
                AddOrUpdate(name, Math.Floor(free));
                added++;
            }
            _status = added > 0
                ? Loc.T("#LOC_RN_AddedBelow", added, (threshold * 100.0).ToString("N0") + "%")
                : Loc.T("#LOC_RN_NothingBelow");
        }

        private void PlaceOrder(Vessel v, MultiQuote q)
        {
            try
            {
                if (Funding.Instance != null) Funding.Instance.AddFunds(-q.Total, TransactionReasons.None);
                ResupplyQueue.Instance.Enqueue(v, q);
                _status = Loc.T("#LOC_RN_OrderPlaced", q.Lines.Count, q.TotalMass.ToString("N3") + " t",
                                CostCore.FormatDuration(q.LeadTime));
                Debug.Log("[ResupplyNetwork] order placed: " + q.Lines.Count + " kinds, " + q.Total.ToString("N0") + " funds");
                _cart.Clear();
            }
            catch (Exception e)
            {
                _status = Loc.T("#LOC_RN_OrderFailed", e.Message);
                Debug.LogError("[ResupplyNetwork] order failed: " + e);
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
                GUILayout.Label(Loc.T("#LOC_RN_QueueNotReady"));
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
                return;
            }

            GUILayout.Label(Loc.T("#LOC_RN_RulesHelp1"));
            GUILayout.Label(Loc.T("#LOC_RN_RulesHelp2"));

            Vessel v = SelectedVessel();
            string res = _resource;
            GUILayout.Label(v == null
                ? Loc.T("#LOC_RN_RulesNoTarget")
                : Loc.T("#LOC_RN_RulesTarget", v.vesselName,
                        string.IsNullOrEmpty(res) ? "-" : ResourceCatalog.DisplayName(res)));

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("#LOC_RN_Threshold"), GUILayout.Width(76f));
            _autoThresholdText = GUILayout.TextField(_autoThresholdText, GUILayout.Width(58f));
            GUILayout.Label(Loc.T("#LOC_RN_Target"), GUILayout.Width(92f));
            _autoTargetText = GUILayout.TextField(_autoTargetText, GUILayout.Width(58f));
            GUILayout.Label(Loc.T("#LOC_RN_Cadence"), GUILayout.Width(84f));
            _autoIntervalText = GUILayout.TextField(_autoIntervalText, GUILayout.Width(58f));
            GUILayout.Label(Loc.T("#LOC_RN_Budget"), GUILayout.Width(84f));
            _autoBudgetText = GUILayout.TextField(_autoBudgetText, GUILayout.Width(90f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("#LOC_RN_Scope"), GUILayout.Width(76f));
            if (GUILayout.Toggle(_ruleScope == 0, Loc.T("#LOC_RN_Scope_Vessel"), GUILayout.Width(100f))) _ruleScope = 0;
            if (GUILayout.Toggle(_ruleScope == 1, Loc.T("#LOC_RN_Scope_Type"), GUILayout.Width(100f))) _ruleScope = 1;
            if (GUILayout.Toggle(_ruleScope == 2, Loc.T("#LOC_RN_Scope_Body"), GUILayout.Width(100f))) _ruleScope = 2;
            GUILayout.EndHorizontal();

            GUI.enabled = v != null && !string.IsNullOrEmpty(res);
            if (GUILayout.Button(Loc.T("#LOC_RN_AddRule"), GUILayout.Width(200f))) AddRuleFromSelection(v, res);
            GUI.enabled = true;

            GUILayout.Label(Loc.T("#LOC_RN_ExistingRules", ResupplyQueue.Instance.AutoRules.Count));

            var rules = ResupplyQueue.Instance.AutoRules;
            float listH = Mathf.Max(140f, _rulesRect.height * 0.42f);
            _rulesScroll = GUILayout.BeginScrollView(_rulesScroll, GUILayout.Height(listH));

            if (rules.Count == 0)
            {
                GUILayout.Label(Loc.T("#LOC_RN_NoRules"));
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

                    string next = !r.Checked
                        ? Loc.T("#LOC_RN_Immediate")
                        : CostCore.FormatDuration(Math.Max(0.0, r.LastCheckUT + r.Interval - now));

                    string scopeTag = r.ScopeKind == AutoRule.ScopeVesselType
                        ? Loc.T("#LOC_RN_ScopeTag", Loc.T("#LOC_RN_Scope_Type"))
                        : (r.ScopeKind == AutoRule.ScopeBody ? Loc.T("#LOC_RN_ScopeTag", Loc.T("#LOC_RN_Scope_Body")) : "");

                    GUILayout.Label(Loc.T("#LOC_RN_RuleLine",
                        (string.IsNullOrEmpty(r.VesselName) ? "?" : r.VesselName) + scopeTag,
                        ResourceCatalog.DisplayName(r.Resource),
                        (r.Threshold * 100.0).ToString("N0"),
                        (r.Target * 100.0).ToString("N0"),
                        r.IntervalDays,
                        r.Budget > 0.0 ? Loc.T("#LOC_RN_RuleBudget", r.Budget.ToString("N0")) : Loc.T("#LOC_RN_RuleNoBudget"),
                        next), GUILayout.Width(560f));

                    if (GUILayout.Button(Loc.T("#LOC_RN_Delete"), GUILayout.Width(70f)))
                    {
                        ResupplyQueue.Instance.RemoveRule(i);
                        _status = Loc.T("#LOC_RN_RuleDeleted");
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
                _status = Loc.T("#LOC_RN_ExcludedRule", ResourceCatalog.DisplayName(resource));
                return;
            }

            double threshold = ParsePercent(_autoThresholdText, 30.0);
            double target = ParsePercent(_autoTargetText, 100.0);
            double days = ParseNumber(_autoIntervalText, 30.0);
            double budget = ParseNumber(_autoBudgetText, 50000.0);

            if (target < threshold) { _status = Loc.T("#LOC_RN_Err_TargetBelow"); return; }
            if (days <= 0.0) { _status = Loc.T("#LOC_RN_Err_Days"); return; }

            ResupplyQueue.Instance.AddRule(v, resource, threshold, target, days * CostCore.KerbinDay, budget, _ruleScope);
            _status = Loc.T("#LOC_RN_RuleSaved", v.vesselName, ResourceCatalog.DisplayName(resource));
            Debug.Log("[ResupplyNetwork] rule saved: scope=" + _ruleScope + " " + resource);
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

        // ------------------------------------------------------------------

        private static string BodyLabel(CelestialBody body)
        {
            return ColonyOrigin.BodyName(body);
        }

        private static string VesselTypeLabel(VesselType t)
        {
            switch (t)
            {
                case VesselType.Ship: return Loc.T("#LOC_RN_VT_Ship");
                case VesselType.Station: return Loc.T("#LOC_RN_VT_Station");
                case VesselType.Probe: return Loc.T("#LOC_RN_VT_Probe");
                case VesselType.Lander: return Loc.T("#LOC_RN_VT_Lander");
                case VesselType.Rover: return Loc.T("#LOC_RN_VT_Rover");
                case VesselType.Base: return Loc.T("#LOC_RN_VT_Base");
                case VesselType.Plane: return Loc.T("#LOC_RN_VT_Plane");
                case VesselType.Relay: return Loc.T("#LOC_RN_VT_Relay");
                case VesselType.DeployedScienceController: return Loc.T("#LOC_RN_VT_ScienceCtrl");
                case VesselType.DeployedSciencePart: return Loc.T("#LOC_RN_VT_SciencePart");
                case VesselType.DeployedGroundPart: return Loc.T("#LOC_RN_VT_GroundPart");
                default: return t.ToString();
            }
        }
    }
}
