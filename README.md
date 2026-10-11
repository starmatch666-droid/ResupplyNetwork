# Resupply Network

Pay funds and wait for deliveries. Resupply any of your vessels - stations, ships,
landers, probes - with **any** resource, without flying a tanker there yourself.
Think commercial resupply flights to a station.

## What it does

* **On-demand orders** - pick a vessel, pick resources, add them to a cart, see the
  price and the lead time, order. One order can carry many resources at once.
* **Scheduled resupply** - a rule checks a resource every N days and only sends a
  flight when the level has dropped below a threshold, topping it back up to a
  target level. A real resupply cadence.
* **Costs come from real orbital data** - freight scales with the delta-v needed to
  get there and the transfer time, estimated from each body's orbit. Works with any
  planet pack; there is no hardcoded body list.
* **Any resource** - prices and densities are read from the game's own
  `PartResourceDefinition` data, so modded resources (CommunityResourcePack, Far
  Future Technologies, CryoEngines, USI, KerbalAtomics, ...) work automatically.
* **Scarcity multipliers and an exclusion list** for resources you do not want to
  sell (abstract resources, atmospheric intakes, ...).
* **Difficulty settings page** - every coefficient is a slider in the game's
  difficulty options, with a live reference quote showing what a full 2.5 m tank
  costs and how long it takes, so you can see the effect while you drag.
* **Localized resource names** - resources are shown in your game's language.
* **Amount slider** - choose how much to inject as a percentage, or type an exact number.
* **Rush orders** - pay a premium to cut the lead time, for when you cannot wait.
* **Search and sort** - filter a large resource list by name and sort by free capacity or price.
* **Bulk top-up** - one button adds every resource below the threshold to the cart.
* **Rule scope** - a single scheduled rule can cover this vessel, all vessels of the
  same type, or everything on the same body.
* **Localized UI** - English and Chinese are both included.
* **CommNet requirement** - optionally refuse to resupply a vessel with no working
  comms link (on by default).
* **Remembers window layout** between sessions.

## Requirements

* Kerbal Space Program 1.12.x
* [ToolbarController](https://spacedock.info/mod/1931) - the toolbar button is
  registered through it, so it is a hard dependency.

## Install

Drop the `ResupplyNetwork` folder into `GameData`, or install via CKAN.

## Using it

Open the window from the toolbar button (or `Ctrl+Alt+N`).

1. Pick the target vessel. Filter by celestial body or vessel type if the list is long.
2. Pick a resource, set an amount, click Add to Cart. Repeat for other resources.
3. Review the combined quote and place the order.

The "定时补给规则" (scheduled resupply rules) button opens the cadence window:
threshold %, top-up %, cadence in days, and a per-order budget cap.

## Configuration

`GameData/ResupplyNetwork/Config/ResupplySettings.cfg` holds every coefficient:
base freight rate, time markup, trips per order, service fee, scarcity multipliers,
the exclusion list, and per-body overrides. Unlisted bodies are estimated from their
orbits, so planet packs need no configuration.

## Notes and limits

* Delivery is abstract - no physical tanker is spawned. The pricing is built so the
  service never becomes cheaper than launching the fuel yourself.
* An order is charged when placed; cancelling refunds a configurable fraction.
* If a rule's cadence is longer than the vessel's endurance, the resource can run
  out before the next flight. The mod logs a warning when it detects this from the
  observed consumption rate.
* Resources inside **cargo containers** (KSP 1.12 inventory parts) are deliberately
  not seen or filled. The service works on the vessel's actual tanks; counting
  stowed inventory as live capacity would be misleading.

## License

MIT

---

# 补给网 Resupply Network

花钱 + 花时间，直接给任意航天器（空间站、飞船、着陆器、探测器）补给**任意资源**，
不用自己发油船。定位就是现实里的商业补给航班。

## 功能

* **按需下单** - 选航天器、选资源、加入清单，看报价与交付时间，下单；一笔订单可含多种物资
* **定时补给** - 每 N 天检查一次余量，低于阈值才发船，补到补充目标，模拟真实的补给班期
* **成本由真实轨道数据算出** - 运费随目的地 Δv 与转移时间变化，自动适配任意行星包，不硬编码天体
* **支持全部资源** - 直接从游戏自身的 `PartResourceDefinition` 读价格与密度，mod 资源自动生效
* **稀缺溢价与排除表** - 抽象/环境类资源可排除；反物质、核燃料之类可设溢价
* **难度设置页** - 所有系数都是游戏难度选项里的滑块，并实时显示"加满一个 2.5m 油罐要多少钱、多久"作为参考
* **资源名本地化** - 按游戏语言显示资源名
* **数量滑块** - 按百分比选取注入量，也可以直接输入精确值
* **加急订单** - 多付钱缩短交付时间
* **搜索与排序** - 资源多时按名称搜索，按可注入量或单价排序
* **一键补满** - 把低于阈值的资源一次性加进清单
* **规则作用域** - 一条定时规则可以覆盖当前舰船、同类型全部舰船，或同一天体上的全部舰船
* **界面双语** - 内置英文与简体中文
* **通信链路要求** - 可要求目标必须有可用通信链路（默认开启）
* **记住窗口位置与尺寸**

## 依赖

* KSP 1.12.x
* **ToolbarController**（硬依赖，工具栏按钮通过它注册）

## 使用

工具栏按钮打开窗口（也可用 `Ctrl+Alt+N`）。选航天器 → 选资源 → 填数量 → 加入清单 →
确认下单。`定时补给规则…` 打开班期设置窗口。

所有系数都在 `GameData/ResupplyNetwork/Config/ResupplySettings.cfg`，随时可调。

## 已知边界

* 装在**货舱**（KSP 1.12 的物品栏部件）里的资源容器不参与补给，也不计入余量——
  这是有意为之。服务针对的是舰上真实的储罐，把收纳起来的货物当作可用容量会误导人。

## 授权

MIT
