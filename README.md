# Resupply Network

Pay funds and wait for delivery. Resupply any of your vessels - stations, ships,
landers, probes - with **any** resource, without flying a tanker there yourself.
Think commercial resupply flights to a station.

## What it does

- **On-demand orders** - pick a vessel, add resources to a cart, see the combined
  price and lead time, order. One order can carry many resources at once.
- **Scheduled resupply** - a rule checks a resource every N days and only sends a
  flight when the level has dropped below a threshold, topping it up to a target.
  A rule can cover one vessel, every vessel of the same type, or everything on the
  same body.
- **Costs come from real orbital data** - freight scales with the delta-v needed to
  reach the destination and the transfer time, estimated from each body's orbit.
  Works with any planet pack; there is no hardcoded body list.
- **Any resource** - prices and densities are read from the game's own
  `PartResourceDefinition` data, so modded resources work automatically.
- **Rush orders** - pay a multiplier to cut the lead time.
- **Colony supply origins** - if you have a working colony on the target's body,
  deliveries to that body come from the colony: local launch cost and hours, instead
  of interplanetary shipping from Kerbin.
- **Scarcity multipliers and an exclusion list** for resources you do not want to sell.
- **English and Chinese UI** - everything follows your game language.

## Requirements

- Kerbal Space Program 1.12.x
- [ToolbarController](https://spacedock.info/mod/1931) - the toolbar button is
  registered through it, so it is a hard dependency.

## Install

Unzip the `GameData` folder into your KSP install, or install via CKAN.

## Using it

Open the window from the toolbar button, or with `Ctrl+Alt+N`. It has four sections,
and the dividers between them can be dragged to give any section more or less room -
the total always fits the window. The button at the bottom closes it.

1. **Target vessel** - filter by celestial body or vessel type.
2. **Resource** - search by name, sort by name / free capacity / price, pick an
   amount with the slider or type an exact number. `Add all below N%` fills the cart
   with everything currently under the threshold.
3. **Cart** - the combined quote, the rush toggle, and the order button.
4. **Pending** - orders in flight, with a detail view and cancel (partial refund).

Scheduled rules live behind the `Scheduled rules...` button.

## How the pricing works

```
material = sum(amount x unitCost)          # the game's own resource prices
freight  = mass x rate                     # rate depends on where it is going
service  = trips x per-trip fee            # trips = ceil(mass / cargo per trip)
total    = max(material + freight + service, minimum order)
lead     = base handling time + trips x transfer time

rate = base rate x (1 + dv / dv_ref) x (1 + time markup x years) x premium
```

Every coefficient is adjustable (see below). The defaults are anchored so the
service is never cheaper than launching the fuel yourself: the base rate equals the
cost of lifting one tonne to LKO, and the transfer time is a real Hohmann estimate
from the target's orbit.

As a reference, at default settings, filling a 2.5 m Jumbo-64 at 250 km costs about
**132,700 funds** and takes about **2.3 Kerbin days**. The same line is shown live on
the difficulty page so you can see what a change does before you commit to it.

## Difficulty options

Three sections under the game's difficulty options:

- **Resupply · Pricing** - freight rates, time markup, premium, cargo per trip,
  service fee, minimum order, handling time, plus the live reference quote.
- **Resupply · Refunds & Rush** - cancellation refund, refund when the target vessel
  is lost, rush cost/time multipliers, and the CommNet link requirement.
- **Resupply · Scheduling** - default threshold, top-up target, cadence and per-order
  budget for new rules.
- **Resupply · Colonies** - enable colony supply, the minimum crew a base needs, and a
  freight multiplier.

## Colony supply

When a vessel is in the SOI of a body that also has a qualifying colony, the quote
switches its origin from Kerbin to that colony. The quote window shows it as
`Origin <colony> (surface) → <destination>`.

A base qualifies when it is **landed or splashed** on that body, has at least the
configured **minimum crew** (3 by default), and carries at least one
**resource converter, harvester or logistics module**. Module names are matched
generically (`*Converter*`, `*Harvester*`, `*LogisticsConsumer*`), so it works with
stock converters, USI parts and PlanetaryBaseInc bases without depending on any one
colony mod.

Freight is then estimated as a local launch:

```
colony rate = K_mass x (local ascent dv / Kerbin ascent dv) x premium x factor
```

with the ascent delta-v approximated from the body's gravity and atmosphere. As a
rough guide, Duna works out to about 40% of the Kerbin rate and Minmus to about 6%.
Lead time drops to a local launch (6 hours per trip) instead of a transfer from
Kerbin. Material prices are unchanged - the colony "sells" the goods at the normal
market price.

Colony supply only applies **within the same body's SOI**. A colony does not ship to
other planets; that still comes from Kerbin.

The `Config/ResupplySettings.cfg` file holds the same values as *defaults* (used for
new saves and by the offline pricing tool) plus the list-shaped settings that do not
fit on that page: scarcity multipliers, the exclusion list, and per-body overrides.
Values saved in the difficulty options take priority over the file.

## Compatibility

Prices and densities come from the game's own resource definitions, so any mod that
defines resources the standard way works without configuration. Verified against
200+ resources in a heavily modded 1.12.5 install: stock, CommunityResourcePack,
Far Future Technologies, CryoEngines/CryoTanks, USI/MKS, Near Future, KerbalAtomics,
plus OPM, MPE and Kcalbeloh as destinations.

Excluded by default, because they are bookkeeping or environmental rather than
something you can buy: `Megajoules`, `ChargedParticles`, `VacuumPlasma`,
`ExoticMatter`, `WasteHeat`, `ThermalPower`, `KDVolume`, `IntakeAir`, `IntakeAtm`,
`IntakeLqd`, `FanIntakeAir`, `SolarWind`.

Scarcity multipliers are applied on top of the game's prices. Antimatter and
anti-hydrogen are the interesting case: CRP already prices both at exactly 100,000
funds per gram, and a x20 multiplier makes that 2,000,000 funds per gram.

## Notes and limits

- Delivery is abstract - no physical tanker is spawned. The pricing is built so the
  service never becomes cheaper than launching the fuel yourself.
- Orders are charged when placed; cancelling refunds a configurable fraction.
- The docking-port requirement was **dropped by design**: checking ports on unloaded
  vessels would mean depending on save-file snapshot fields (`state`, `dockUId`),
  which is fragile. An optional CommNet link requirement is available instead.
- Resources inside **cargo containers** (KSP 1.12 inventory parts) are deliberately
  not seen or filled. The service works on the vessel's actual tanks; counting
  stowed inventory as live capacity would be misleading.
- Crew, science data and stored parts are not moved.
- If a rule's cadence is longer than the vessel's endurance, the resource can run out
  before the next flight. The mod logs a warning when it detects this from the
  observed consumption rate.

## License

MIT

---

# 补给网 Resupply Network

花钱 + 花时间，直接给任意航天器（空间站、飞船、着陆器、探测器）补给**任意资源**，
不用自己发油船。定位就是现实里的商业补给航班。

## 功能

- **按需下单** - 选航天器、把资源加进清单，看合计报价与交付时间；一笔订单可含多种物资
- **定时补给** - 每 N 天检查一次余量，低于阈值才发船，补到目标水位。规则可作用于单艘舰船、
  同类型全部舰船，或同一天体上的全部舰船
- **成本由真实轨道数据算出** - 运费随目的地 Δv 与转移时间变化，自动适配任意行星包
- **支持全部资源** - 直接从游戏自身的 `PartResourceDefinition` 读价格与密度，mod 资源自动生效
- **加急订单 / 稀缺溢价 / 排除表**
- **殖民地补给** - 目标天体上若有运作中的殖民地，补给改由该殖民地本地发货，只需本地发射的费用和几小时，而不是从坎巴拉跨越星际运来
- **界面双语**，跟着游戏语言走

## 依赖

- KSP 1.12.x
- **ToolbarController**（硬依赖，工具栏按钮通过它注册）

## 使用

工具栏按钮或 `Ctrl+Alt+N` 打开窗口。窗口分四段，段间的分隔条可以拖动调整高度，
总和始终不超出窗口；底部有「关闭」按钮。

1. **目标航天器** - 可按天体或舰船类型筛选
2. **资源** - 按名称搜索、按名称/可注入量/单价排序、滑块或精确输入取数量；
   「把低于 N% 的全部加入清单」一键填充
3. **清单** - 合计报价、加急开关、下单按钮
4. **待交付** - 在途订单，可看明细、可取消（按比例退款）

定时补给规则在「定时补给规则…」按钮里。

## 定价方式

```
物资 = Σ 数量 × unitCost            游戏自身的资源价格
运费 = 质量 × 费率                  费率取决于目的地
服务 = 趟数 × 每趟服务费             趟数 = ceil(质量 / 单船运力)
总价 = max(物资 + 运费 + 服务, 最低订单额)
时长 = 调度基础时间 + 趟数 × 转移时间

费率 = 基础运费 × (1 + Δv/Δv参照) × (1 + 时间加价 × 年数) × 便利溢价
```

默认值刻意锚定在"**不比你自己发射更便宜**"：基础运费等于把 1 吨送上 LKO 的成本，
转移时间是按目标轨道算出来的真实霍曼转移。

参考：默认参数下，在 250 km 轨道加满一个 2.5m 最大油罐（Jumbo-64）约
**132,700 资金 / 2.3 坎巴拉日**。这一行会实时显示在难度页上，拖滑块就能看到影响。

## 难度设置页

三个独立条目：

- **补给网 · 定价** - 运费率、时间加价、便利溢价、单船运力、服务费、最低订单额、调度时间
  + 实时参考报价
- **补给网 · 退款与加急** - 取消退款、舰船损毁退款、加急费用/时间倍率、通信链路要求
- **补给网 · 定时补给** - 新规则的默认阈值、补充目标、班期、单笔预算
- **补给网 · 殖民地** - 是否启用、殖民地最少乘员、运费系数

## 殖民地补给

当目标舰船所在的天体上存在合格殖民地时，报价的来源会从坎巴拉切换成该殖民地，
窗口里显示为 `来源 <殖民地>（地表） → <目的地>`。

殖民地的判定条件是：**停在表面**（陆地或海上）、**乘员不少于设定值**（默认 3）、
并且**至少有一个转换器 / 采掘器 / 后勤模块**。模块名按通用特征匹配
（`*Converter*`、`*Harvester*`、`*LogisticsConsumer*`），所以原版转换器、USI
和 PlanetaryBaseInc 的基地都能识别，不绑定任何一个殖民地 mod。

运费按"本地发射"估算：

```
殖民地运费率 = K_mass × (当地入轨 Δv / 坎巴拉入轨 Δv) × 便利溢价 × 系数
```

入轨 Δv 由天体的重力和大气粗略估算。参考值：Duna 约为坎巴拉的四成，Minmus 约为 6%。
交付时间缩短为本地发射（每趟 6 小时），不再是从坎巴拉出发的星际航程。
物资价格不变——殖民地按正常市价"出售"这些物资。

殖民地补给**只在同一天体的 SOI 内生效**；跨行星的补给仍然来自坎巴拉。

`Config/ResupplySettings.cfg` 里存的是同名参数的**默认值**（新存档用，离线定价工具也读它），
外加放不进那个页面的列表类配置：稀缺溢价、排除表、目的地覆盖。**难度页里的值优先于文件。**

## 兼容性

价格与密度取自游戏自身的资源定义，任何用标准方式定义资源的 mod 都自动生效。
在一个重度 mod 化的 1.12.5 环境里实测覆盖 200+ 种资源：原版、CommunityResourcePack、
Far Future Technologies、CryoEngines/CryoTanks、USI/MKS、Near Future、KerbalAtomics，
目的地支持 OPM / MPE / Kcalbeloh。

默认排除（属于记账或环境资源，不是"能买到的货"）：`Megajoules`、`ChargedParticles`、
`VacuumPlasma`、`ExoticMatter`、`WasteHeat`、`ThermalPower`、`KDVolume`、
`IntakeAir`、`IntakeAtm`、`IntakeLqd`、`FanIntakeAir`、`SolarWind`。

稀缺溢价叠加在游戏原价上。反物质与反氢是个典型：CRP 原本就把两者都定在
**10 万资金/克**，×20 后是 **200 万资金/克**。

## 已知边界

- 交付是抽象的，不生成实体补给船；定价保证不比你自己发射便宜
- 下单即扣款，取消按可配置比例退款
- **对接口要求已按设计取消**（检查未加载舰船的对接口要依赖存档快照字段，太脆弱），
  改用可选的通信链路要求
- **货舱**（KSP 1.12 物品栏部件）里的资源容器不参与补给，也不计入余量
- 不搬运船员、科学数据、货物零件
- 班期比续航还长时可能在到货前断货，检测到会往日志写警告

## 授权

MIT
