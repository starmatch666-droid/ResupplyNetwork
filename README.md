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

## License

MIT

---

# 补给网 Resupply Network

花钱 + 花时间，直接给任意航天器（空间站、飞船、着陆器、探测器）补给**任意资源**，
不用自己发油船。定位就是现实里的商业补给航班。

## 功能

* **按需下单** - 选航天器、选资源、加入清单，看报价与交付时间，下单；一笔订单可含多种物资
* **定时补给** - 每 N 天检查一次余量，低于阈值线才发船，补到目标水位，模拟真实的补给班期
* **成本由真实轨道数据算出** - 运费随目的地 Δv 与转移时间变化，自动适配任意行星包，不硬编码天体
* **支持全部资源** - 直接从游戏自身的 `PartResourceDefinition` 读价格与密度，mod 资源自动生效
* **稀缺溢价与排除表** - 抽象/环境类资源可排除；反物质、核燃料之类可设溢价

## 依赖

* KSP 1.12.x
* **ToolbarController**（硬依赖，工具栏按钮通过它注册）

## 使用

工具栏按钮打开窗口（也可用 `Ctrl+Alt+N`）。选航天器 → 选资源 → 填数量 → 加入清单 →
确认下单。`定时补给规则…` 打开班期设置窗口。

所有系数都在 `GameData/ResupplyNetwork/Config/ResupplySettings.cfg`，随时可调。

## 授权

MIT
