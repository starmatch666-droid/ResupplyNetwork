using UnityEngine;
using KSP.UI.Screens;

namespace ResupplyNetwork
{
    /// <summary>
    /// 载入设置；并提供两个调试热键（UI 尚未实现，先用它们打通全流程）：
    ///   Ctrl+Alt+R  打印当前舰船的报价示例到 KSP.log
    ///   Ctrl+Alt+T  在当前舰船上下一笔测试订单（真实扣款、真实排入交付队列）
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class ResupplyBootstrap : MonoBehaviour
    {
        private bool _buttonCreated;

        private void Awake()
        {
            ResupplySettings.Load();
            gameObject.AddComponent<ResupplyWindow>();
            DontDestroyOnLoad(this);
        }

        private void Start()
        {
            ToolbarIntegration.Register();
        }

        private void Update()
        {
            // 交付队列除了自身 Update，也由这个常驻对象驱动一次（幂等），
            // 避免依赖 ScenarioModule 的 Update 是否被 KSP 调用。
            if (ResupplyQueue.Instance != null) ResupplyQueue.Instance.ProcessPending();

            // 工具栏按钮延后到原生 launcher 就绪后再建，避免在 MainMenu 建失败。
            if (!_buttonCreated && ApplicationLauncher.Instance != null)
            {
                _buttonCreated = ToolbarIntegration.CreateButton(gameObject, OnToolbarOpen, OnToolbarClose);
            }

            if (!Input.GetKey(KeyCode.LeftControl) || !Input.GetKey(KeyCode.LeftAlt)) return;
            if (Input.GetKeyDown(KeyCode.R)) DumpSampleQuote();
            if (Input.GetKeyDown(KeyCode.T)) PlaceTestOrder();
            if (Input.GetKeyDown(KeyCode.N)) ToggleWindow();
        }

        private static void OnToolbarOpen()
        {
            if (ResupplyWindow.Instance != null) ResupplyWindow.Instance.SetVisible(true);
        }

        private static void OnToolbarClose()
        {
            if (ResupplyWindow.Instance != null) ResupplyWindow.Instance.SetVisible(false);
        }

        private static void ToggleWindow()
        {
            if (ResupplyWindow.Instance != null) ResupplyWindow.Instance.Toggle();
        }

        private static void PlaceTestOrder()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null)
            {
                Debug.Log("[ResupplyNetwork][调试] 当前没有活动舰船");
                return;
            }

            string res = null;
            var list = VesselResources.ListResources(v);
            for (int i = 0; i < list.Count; i++)
            {
                string name = list[i];
                if (ResourceCatalog.IsExcluded(name)) continue;
                var def = ResourceCatalog.Definition(name);
                if (def == null || def.density <= 0f) continue;
                if (VesselResources.GetFreeCapacity(v, name) <= 0.0) continue;
                res = name;
                break;
            }
            if (res == null)
            {
                Debug.Log("[ResupplyNetwork][调试] 找不到可补给的资源");
                return;
            }

            var cart = new System.Collections.Generic.List<CartItem>(1);
            cart.Add(new CartItem { Resource = res, Amount = 100.0 });
            MultiQuote q = CostModel.QuoteMulti(v, cart);
            if (!q.Ok)
            {
                Debug.Log("[ResupplyNetwork][调试] 无法报价: " + q.Reason);
                return;
            }
            if (Funding.Instance == null)
            {
                Debug.Log("[ResupplyNetwork][调试] 当前模式没有资金系统（非生涯）");
                return;
            }
            if (Funding.Instance.Funds < q.Total)
            {
                Debug.Log("[ResupplyNetwork][调试] 资金不足: 需要 " + q.Total.ToString("N0"));
                return;
            }
            if (ResupplyQueue.Instance == null)
            {
                Debug.LogWarning("[ResupplyNetwork][调试] 交付队列未初始化");
                return;
            }

            Funding.Instance.AddFunds(-q.Total, TransactionReasons.None);
            var rec = ResupplyQueue.Instance.Enqueue(v, q);
            Debug.Log("[ResupplyNetwork][调试] 已下单: " + CostCore.DescribeMulti(q)
                      + " | 到期 UT=" + rec.DueUT.ToString("F0") + " | 当前 UT="
                      + Planetarium.GetUniversalTime().ToString("F0"));
        }

        private static void DumpSampleQuote()
        {
            var list = FlightGlobals.Vessels;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var v = list[i];
                if (v == null) continue;
                var res = VesselResources.ListResources(v);
                if (res.Count == 0) continue;

                string pick = null;
                for (int j = 0; j < res.Count; j++)
                {
                    var def = ResourceCatalog.Definition(res[j]);
                    if (def != null && def.density > 0f && !ResourceCatalog.IsExcluded(res[j])) { pick = res[j]; break; }
                }
                if (pick == null) continue;

                var q = CostModel.Quote(v, pick, 100.0);
                Debug.Log("[ResupplyNetwork][调试] " + v.vesselName + " @ " + q.ZoneName
                          + " (" + q.FreightRate.ToString("N0") + " 资金/吨): " + CostModel.Describe(q));
            }
        }
    }
}
