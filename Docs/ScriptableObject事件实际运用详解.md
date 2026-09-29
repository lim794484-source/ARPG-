# ScriptableObject 事件实际运用详解

> 本文档把项目里每一个 ScriptableObject 事件都揉碎了讲：定义、发布者、订阅者、完整调用链、Unity 验证步骤。看完你能把整个事件网络在脑子里画出来。

---

## 总览：项目里的 10 个 SO 事件

| SO 事件 | 类型 | 参数 | 用途 |
|---------|------|------|------|
| `SceneLoadEventSO` | 有参 | `(GameSceneSO, Vector3, bool)` | 场景切换请求 |
| `VoidEventSO` | 无参 | 无 | 通用空事件 |
| `ToggleCanvasEventSO` | 有参 | `(bool)` | UI 画布开关 |
| `LootEventSO` | 有参 | `(ItemSO, int, Loot)` | 物品拾取 |
| `DataSaveEventSO` | 有参 | `(SaveType)` | 存档触发 |
| `ShopLoadEventSO` | 有参 | `(List, List, List, Transform)` | 商店数据加载 |
| `LoadQuestEventSO` | 有参 | `(List<QuestSO>)` | 任务列表加载 |
| `QuestOptionsEventSO` | 有参 | `(QuestState)` | 任务选项选择 |
| `OpenSaveLoadPanelEventSO` | 有参 | `(SaveLoadPanelType)` | 存档面板开关 |
| `InventorySlotsStatsSO` | 双向 | `(ItemSO, int, int)` / `(bool[])` | 商店-背包通信 |

---

## 一、SceneLoadEventSO —— 场景切换的核心枢纽

### 1.1 定义

```csharp
public class SceneLoadEventSO : ScriptableObject
{
    public event Action<GameSceneSO, Vector3, bool> LoadRequestEvent;

    public void RaiseLoadRequestEvent(GameSceneSO scene, Vector3 position, bool isToFade)
    {
        LoadRequestEvent?.Invoke(scene, position, isToFade);
    }
}
```

- 参数1 `scene`：要加载的场景 SO
- 参数2 `position`：玩家在新场景的出生位置
- 参数3 `isToFade`：是否播放淡入淡出动画

### 1.2 发布者（谁调用 RaiseLoadRequestEvent）

| 脚本 | 位置 | 触发时机 |
|------|------|---------|
| `Teleport` (SceneToggler) | 内容场景 | 玩家走进传送点 |
| `SaveSystem.LoadSave` | PersistentScene | 读档时切到存档场景 |
| `ButtonSceneToggler` | 内容场景 | 点击按钮切场景 |
| `RetryManager` | PersistentScene | 重试时切场景 |

**最典型的发布者 —— Teleport：**

```csharp
// Teleport.cs（实际类名是 SceneToggler）
public class SceneToggler : MonoBehaviour
{
    [SerializeField] private SceneLoadEventSO loadEventSO;   // ⭐ 拖入 SO 资产
    [SerializeField] private Vector3 newPosition;
    [SerializeField] private GameSceneSO sceneToLoad;
    [SerializeField] private bool isToFade = true;

    private void OnTriggerEnter2D(Collider2D collider)
    {
        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, newPosition, isToFade);
    }
}
```

### 1.3 订阅者（谁订阅 LoadRequestEvent）

| 脚本 | 回调方法 | 收到事件后做什么 |
|------|---------|-----------------|
| `SceneChanger` | `OnLoadRequestEvent` | 执行场景异步加载/卸载 |
| `DataManager` | `OnAutoSave` | 采集当前场景数据，存档 |
| `UIManager` | `OnLoadScene` | 关闭所有 UI 画布 |
| `SaveSystem` | （不订阅，是发布者） | — |

### 1.4 完整调用链

```
玩家走进传送点
    │
    ▼
Teleport.OnTriggerEnter2D()
    │ 调用 loadEventSO.RaiseLoadRequestEvent(scene, pos, fade)
    ▼
SceneLoadEventSO.LoadRequestEvent 广播
    │
    ├─▶ SceneChanger.OnLoadRequestEvent()
    │       ├── 淡出动画
    │       ├── 卸载旧场景
    │       ├── 加载新场景（Additive）
    │       └── 完成后触发 sceneLoadedEvent.OnEventRaised()
    │
    ├─▶ DataManager.OnAutoSave()
    │       ├── 遍历 ISaveable 列表采集数据
    │       ├── 判断场景类型决定是否存档
    │       └── 触发 dataSavedEvent.RaiseDataSaveEvent()
    │               └── SaveSystem.OnSaveEvent() → 写 JSON 文件
    │
    └─▶ UIManager.OnLoadScene()
            └── ResetCanvas() 关闭所有 UI 画布
```

### 1.5 🔍 在 Unity 里验证

| 步骤 | 操作 |
|------|------|
| 1 | `Project` → `Assets/GameSO/Events/` 找到 `SceneLoadEventSO.asset` |
| 2 | 打开 Scene1，找传送点对象，看 `SceneToggler` 组件的 `loadEventSO` 字段 |
| 3 | 打开 PersistentScene，找 `SceneManager`，看 SceneChanger 的 `loadEventSO` 字段 |
| 4 | 确认两边拖的是**同一个** `.asset` 文件 |
| 5 | 运行游戏，走进传送点，在 SceneChanger.OnLoadRequestEvent 打断点 |
| 6 | 再在 DataManager.OnAutoSave 打断点，确认两个都会触发 |

---

## 二、VoidEventSO —— 最通用的空事件

### 2.1 定义

```csharp
public class VoidEventSO : ScriptableObject
{
    public event Action VoidEvent;

    public void OnEventRaised()
    {
        VoidEvent?.Invoke();
    }
}
```

无参数，纯粹的"通知"。谁订阅了谁就收到"有事发生了"的信号。

### 2.2 项目里 VoidEventSO 的所有用法

VoidEventSO 是本项目用得最多的事件，**不同功能用不同的资产实例**。

| 资产实例名 | 发布者 | 订阅者 | 用途 |
|-----------|--------|--------|------|
| `sceneLoadedEvent` | SceneChanger | DataManager | 场景加载完成，触发读档 |
| `openQuestEventSO` | QuestBoardManager | QuestManager | 打开任务面板 |
| `slashActionFinishedEvent` | PlayerCombat | PlayerCombat | 挥砍动画完成 |
| `shootActionFinishedEvent` | PlayerBow | PlayerBow | 射箭动画完成 |
| 其他 | 各按钮/脚本 | 各管理器 | 重试、关闭面板等 |

### 2.3 典型用法1：场景加载完成 → 读档

**发布者 —— SceneChanger：**

```csharp
// SceneChanger.cs
[SerializeField] private VoidEventSO sceneLoadedEvent;

private void OnLoadCompleted(AsyncOperationHandle<SceneInstance> handle)
{
    // ... 设置玩家位置等
    sceneLoadedEvent?.OnEventRaised();   // ⭐ 广播"场景加载完成"
}
```

**订阅者 —— DataManager：**

```csharp
// DataManager.cs
[SerializeField] private VoidEventSO sceneLoadedEvent;

private void OnEnable()
{
    sceneLoadedEvent.VoidEvent += OnAutoLoad;
}
private void OnDisable()
{
    sceneLoadedEvent.VoidEvent -= OnAutoLoad;
}

void OnAutoLoad()
{
    foreach (var saveable in saveables.ToList())
    {
        saveable.LoadData(dataToSave);   // 把存档数据应用到新场景
    }
}
```

**为什么用 VoidEventSO 而不是直接调用？**
> SceneChanger 在场景加载完成后要通知"很多人"做不同的事。如果直接调用，SceneChanger 要引用 DataManager、InventoryManager、QuestManager... 耦合爆炸。用 SO 事件，SceneChanger 只喊一声，谁需要谁自己听。

### 2.4 典型用法2：玩家攻击动画完成

**发布者 —— PlayerCombat：**

```csharp
// PlayerCombat.cs
[SerializeField] private VoidEventSO slashActionFinishedEvent;

// 在动画事件里调用
public void OnSlashAnimationFinished()
{
    slashActionFinishedEvent.OnEventRaised();
}
```

**订阅者 —— PlayerCombat 自己：**

```csharp
private void OnEnable()
{
    slashActionFinishedEvent.VoidEvent += EnableAttackInput;
}
private void OnDisable()
{
    slashActionFinishedEvent.VoidEvent -= EnableAttackInput;
}

void EnableAttackInput()
{
    canAttack = true;   // 动画播完了，可以继续攻击
}
```

**为什么自己发布自己订阅？**
> 动画事件是 Unity 的 Animation 系统调用的，代码不好直接控制时序。用 SO 事件做中转，把"动画完成"这个信号变成代码能订阅的事件。

### 2.5 🔍 在 Unity 里验证

| 步骤 | 操作 |
|------|------|
| 1 | `Assets/GameSO/Events/` 下有多个 VoidEventSO 资产（sceneLoadedEvent、openQuestEvent 等） |
| 2 | 注意：**同一个脚本类可以有多个不同的资产实例**，它们是独立的 |
| 3 | 看 PlayerCombat 的 `slashActionFinishedEvent` 字段拖的是哪个资产 |
| 4 | 看 PlayerBow 的 `shootActionFinishedEvent` 拖的是另一个资产 |
| 5 | 运行游戏攻击一次，在 EnableAttackInput 打断点 |

---

## 三、ToggleCanvasEventSO —— UI 画布的统一开关

### 3.1 定义

```csharp
public class ToggleCanvasEventSO : ScriptableObject
{
    public event Action<bool> toggleCanvasEvent;
    public event Action focusEvent;

    public MyEnums.CanvasToToggle canvasToToggle;   // ⭐ 这个 SO 对应哪个画布

    public void RaiseToggleCanvasEvent(bool state)
    {
        toggleCanvasEvent?.Invoke(state);
    }
    public void RaiseFocusEvent()
    {
        focusEvent?.Invoke();
    }
}
```

- `toggleCanvasEvent`：开/关画布（true=开，false=关）
- `focusEvent`：画布置顶（只调排序，不开关）
- `canvasToToggle`：标识这个 SO 管哪个画布（枚举）

### 3.2 核心机制

每个画布对应**一个独立的 ToggleCanvasEventSO 资产实例**。UIManager 维护一个列表，根据 `canvasToToggle` 找到对应的 SO 来广播。

```
UIManager
  └── List<ToggleCanvasEventSO> toggleCanvasEvents
          ├── BackPackCanvasSO    (canvasToToggle = BackPack)
          ├── QuestCanvasSO       (canvasToToggle = Quest)
          ├── ShopCanvasSO        (canvasToToggle = Shop)
          ├── StatsCanvasSO       (canvasToToggle = Stats)
          ├── ESCCanvasSO         (canvasToToggle = ESC)
          └── ...
```

### 3.3 发布者：UIManager

```csharp
// UIManager.cs
[SerializeField] private List<ToggleCanvasEventSO> toggleCanvasEvents;

// 根据 canvasToToggle 找到对应的 SO，然后广播
private void RaiseCanvasEvent(CanvasToToggle target, bool state)
{
    foreach (var eventSO in toggleCanvasEvents)
    {
        if (eventSO.canvasToToggle != target) continue;
        eventSO.RaiseToggleCanvasEvent(state);   // ⭐ 广播
        return;
    }
}
```

**UIManager 什么时机会调用 RaiseCanvasEvent？**
- 玩家按了 I 键（背包）→ `ApplyFocusChange(CanvasToToggle.BackPack)` → 最终调 `RaiseCanvasEvent(BackPack, true)`
- 场景切换时 → `ResetCanvas()` → 遍历所有 SO 调 `RaiseToggleCanvasEvent(false)` 关闭全部

### 3.4 订阅者：各画布管理器

每个画布管理器都订阅属于自己的那个 SO：

```csharp
// BackpackCanvasManager.cs
[SerializeField] private ToggleCanvasEventSO toggleCanvasEvent;  // ⭐ 拖 BackPackCanvasSO

private void OnEnable()
{
    toggleCanvasEvent.toggleCanvasEvent += OnToggle;
    toggleCanvasEvent.focusEvent += OnFocus;
}
private void OnDisable()
{
    toggleCanvasEvent.toggleCanvasEvent -= OnToggle;
    toggleCanvasEvent.focusEvent -= OnFocus;
}

void OnToggle(bool state)
{
    canvas.enabled = state;   // 开/关画布
}
void OnFocus()
{
    RefreshCanvaOrder();      // 调整排序置顶
}
```

**注意：每个画布管理器只订阅自己那个 SO，不会收到别人的开关信号。**

### 3.5 完整调用链（以打开背包为例）

```
玩家按 I 键
    │
    ▼
UIManager.Update() 检测到按键
    │
    ▼
stack.ApplyFocusChange(CanvasToToggle.BackPack)
    │
    ▼
stack 回调 stack.OnCanvasToggleRequested = RaiseCanvasEvent
    │
    ▼
UIManager.RaiseCanvasEvent(BackPack, true)
    │ 遍历 toggleCanvasEvents 列表
    │ 找到 canvasToToggle == BackPack 的 SO
    ▼
BackPackCanvasSO.RaiseToggleCanvasEvent(true)
    │
    ▼
BackpackCanvasManager.OnToggle(true)
    │
    ▼
背包画布显示
```

### 3.6 🔍 在 Unity 里验证

| 步骤 | 操作 |
|------|------|
| 1 | `Assets/GameSO/Events/` 下找 `ToggleCanvasEventSO` 相关的资产（可能有多个） |
| 2 | 点开其中一个，看 `canvasToToggle` 字段是哪个枚举 |
| 3 | 在 PersistentScene 找 UIManager，看 `toggleCanvasEvents` 列表 |
| 4 | 找 BackpackCanvasManager，看它的 `toggleCanvasEvent` 字段拖的 SO 的 `canvasToToggle` 是不是 BackPack |
| 5 | 运行游戏按 I 键，在 BackpackCanvasManager.OnToggle 打断点 |

---

## 四、LootEventSO —— 物品拾取

### 4.1 定义

```csharp
public class LootEventSO : ScriptableObject
{
    public event Action<ItemSO, int, Loot> LootEvent;

    public void OnEventRaised(ItemSO item, int quantity, Loot loot)
    {
        LootEvent?.Invoke(item, quantity, loot);
    }
}
```

- `item`：拾取的物品 SO
- `quantity`：数量
- `loot`：拾取的 Loot 对象本身（用于回传处理，比如隐藏）

### 4.2 发布者：Loot

```csharp
// Loot.cs
public class Loot : MonoBehaviour, ISaveable
{
    public ItemSO item;
    public int quantity;
    public LootEventSO lootEvent;   // ⭐ 拖入 LootEventSO 资产

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && canBePick)
        {
            animator.Play("Pickup");
            lootEvent.OnEventRaised(item, quantity, this);   // ⭐ 广播"我被拾取了"
            hasBeenPicked = true;
        }
    }
}
```

### 4.3 订阅者：InventoryManager

```csharp
// InventoryManager.cs
[SerializeField] private LootEventSO lootEvent;   // ⭐ 拖同一个 LootEventSO 资产

private void OnEnable()
{
    lootEvent.LootEvent += OnItemLootedHandler;
}
private void OnDisable()
{
    lootEvent.LootEvent -= OnItemLootedHandler;
}

private void OnItemLootedHandler(ItemSO item, int quantity, Loot lootObj)
{
    UpdateInventorySlots(item, quantity, lootObj);
    // UpdateInventorySlots 内部：
    //   - 如果是金币：goldAmount += quantity
    //   - 如果是经验：ExpManager.GainExp(quantity)
    //   - 如果是物品：放进背包空格/堆叠
    //   - 放完后 lootObj.MarkAsDisable() 隐藏地上的物品
}
```

### 4.4 完整调用链

```
玩家走到物品上
    │
    ▼
Loot.OnTriggerEnter2D()
    │ 播放拾取动画
    │ lootEvent.OnEventRaised(item, quantity, this)
    ▼
LootEventSO.LootEvent 广播
    │
    ▼
InventoryManager.OnItemLootedHandler(item, quantity, lootObj)
    │
    ├── 金币 → goldAmount += quantity → 刷新UI
    ├── 经验 → ExpManager.GainExp(quantity)
    └── 物品 → 遍历背包格子，AddItem
            │
            └── 放完 → lootObj.MarkAsDisable() → 地上物品消失
```

### 4.5 🔍 在 Unity 里验证

| 步骤 | 操作 |
|------|------|
| 1 | `Assets/GameSO/Events/` 找 `LootEventSO.asset` |
| 2 | 打开 Scene1，找一个地上的物品（Loot 对象），看 `lootEvent` 字段 |
| 3 | 打开 PersistentScene，找 InventoryManager，看 `lootEvent` 字段 |
| 4 | 确认是同一个资产 |
| 5 | 运行游戏，捡一个金币，在 InventoryManager.OnItemLootedHandler 打断点 |

---

## 五、DataSaveEventSO —— 存档触发

### 5.1 定义

```csharp
public class DataSaveEventSO : ScriptableObject
{
    public event Action<MyEnums.SaveType> DataSaveEvent;

    public void RaiseDataSaveEvent(MyEnums.SaveType saveType)
    {
        DataSaveEvent?.Invoke(saveType);
    }
}
```

- `SaveType`：`SystemSave`（自动存档）/ `ManualSave`（手动存档）

### 5.2 发布者：DataManager

```csharp
// DataManager.cs
[SerializeField] private DataSaveEventSO dataSavedEvent;   // Send 区

private void OnAutoSave(GameSceneSO sceneToLoadSO, Vector3 pos, bool isToFade)
{
    // ... 采集数据到 dataToSave ...

    if (lastSceneType != MyEnums.SceneType.Menu)
    {
        dataSavedEvent.RaiseDataSaveEvent(MyEnums.SaveType.SystemSave);   // ⭐ 广播"数据准备好了"
    }
}
```

### 5.3 订阅者：SaveSystem

```csharp
// SaveSystem.cs
[SerializeField] private DataSaveEventSO dataSavedEvent;   // Receive 区

private void OnEnable()
{
    dataSavedEvent.DataSaveEvent += OnSaveEvent;
}
private void OnDisable()
{
    dataSavedEvent.DataSaveEvent -= OnSaveEvent;
}

public void OnSaveEvent(MyEnums.SaveType saveType)
{
    WriteSave(saveType);   // 把 DataManager 的 dataToSave 写成 JSON 文件
}
```

### 5.4 为什么分两层？

DataManager 和 SaveSystem 职责分离：
- **DataManager**：负责"采集数据"（遍历 ISaveable，组装 Data 对象）
- **SaveSystem**：负责"写文件"（序列化为 JSON，存到磁盘）

用 SO 事件连接两者。DataManager 采集完了喊一声"数据好了"，SaveSystem 听到就写盘。

**好处：** 如果以后要改存档格式（比如从 JSON 改成二进制），只改 SaveSystem，DataManager 不用动。

### 5.5 完整调用链

```
场景切换前
    │
    ▼
DataManager.OnAutoSave()
    │ 遍历 ISaveable 列表
    │ 每个 saveable.SaveData(dataToSave) 把数据塞进 Data 对象
    │
    ▼
dataSavedEvent.RaiseDataSaveEvent(SystemSave)
    │
    ▼
DataSaveEventSO.DataSaveEvent 广播
    │
    ▼
SaveSystem.OnSaveEvent(SystemSave)
    │
    ▼
SaveSystem.WriteSave(SystemSave)
    │ 读取 DataManager.Instance.GetData
    │ JsonConvert.SerializeObject(save)
    │ File.WriteAllText(path, json)
    ▼
存档写入 persistentDataPath/SystemSave_xxx.json
```

### 5.6 🔍 在 Unity 里验证

| 步骤 | 操作 |
|------|------|
| 1 | `Assets/GameSO/Events/` 找 `DataSaveEventSO.asset` |
| 2 | 找 DataManager，看 `dataSavedEvent`（Send 区） |
| 3 | 找 SaveSystem，看 `dataSavedEvent`（Receive 区） |
| 4 | 确认是同一个资产 |
| 5 | 运行游戏，切一次场景，看 Console 输出 `SaveRoute: ...` |

---

## 六、InventorySlotsStatsSO —— 商店与背包的双向通信

### 6.1 定义

这是项目里**唯一一个双向事件的 SO**：

```csharp
public class InventorySlotsStatsSO : ScriptableObject
{
    // 商店 → 背包：请求更新物品
    public event Action<ItemSO, int, int> InventoryUpdateRequestEvent;
    // 背包 → 商店：返回操作结果
    public event Action<bool[]> InventoryRespondEvent;

    public void RaiseInventoryUpdateRequest(ItemSO item, int price, int amount)
    {
        InventoryUpdateRequestEvent?.Invoke(item, price, amount);
    }

    public void RaiseInventoryRespondEvent(bool[] stats)
    {
        InventoryRespondEvent?.Invoke(stats);
    }
}
```

- `InventoryUpdateRequestEvent` 参数：`(item, price, amount)`
  - `amount > 0`：购买，请求背包加物品
  - `amount < 0`：出售，请求背包减物品
- `InventoryRespondEvent` 参数：`bool[] stats`
  - `stats[0]`：商店有无足量物品
  - `stats[1]`：背包有无空位

### 6.2 两个不同的资产实例

同一个 `InventorySlotsStatsSO` 类，有**两个独立的资产实例**：

| 资产实例 | 谁用 | 用途 |
|---------|------|------|
| `ShoppingRequest` | ShopManager ↔ InventoryManager | 商店购买/出售 |
| `QuestRewardRequest` | QuestManager ↔ InventoryManager | 任务奖励发放 |

**关键：两个资产是独立的，ShoppingRequest 的事件不会触发 QuestRewardRequest 的订阅者。**

### 6.3 发布者1：ShopManager（购买/出售）

```csharp
// ShopManager.cs
[SerializeField] private InventorySlotsStatsSO InventoryUpdateRequest;  // ⭐ ShoppingRequest 资产

// 购买
public void BuyItem(ItemSO item, int price)
{
    InventoryUpdateRequest.RaiseInventoryUpdateRequest(item, price, 1);
    // amount=1 表示购买，加1个物品到背包
}

// 出售
public void SellItem(ItemSO item, int price)
{
    InventoryUpdateRequest.RaiseInventoryUpdateRequest(item, -price, -1);
    // amount=-1 表示出售，从背包减1个物品
}
```

### 6.4 发布者2：QuestManager（任务奖励）

```csharp
// QuestManager.cs
[SerializeField] private InventorySlotsStatsSO QuestRewardRequest;  // ⭐ QuestRewardRequest 资产

private void RaiseRewardEvent(QuestSO quest)
{
    foreach (var reward in quest.rewards)
    {
        QuestRewardRequest.RaiseInventoryUpdateRequest(reward.item, 0, reward.quantity);
        // price=0 因为是奖励，不扣钱
    }
}
```

### 6.5 订阅者：InventoryManager

```csharp
// InventoryManager.cs
[SerializeField] private InventorySlotsStatsSO ShoppingRequest;     // ⭐ 同一个 ShoppingRequest
[SerializeField] private InventorySlotsStatsSO QuestRewardRequest;  // ⭐ 同一个 QuestRewardRequest
[SerializeField] private LootEventSO lootEvent;

private void OnEnable()
{
    lootEvent.LootEvent += OnItemLootedHandler;
    ShoppingRequest.InventoryUpdateRequestEvent += HandleShopping;
    QuestRewardRequest.InventoryUpdateRequestEvent += HandleQuestReward;
}
private void OnDisable()
{
    lootEvent.LootEvent -= OnItemLootedHandler;
    ShoppingRequest.InventoryUpdateRequestEvent -= HandleShopping;
    QuestRewardRequest.InventoryUpdateRequestEvent -= HandleQuestReward;
}

private void HandleShopping(ItemSO item, int price, int amount)
{
    if (amount > 0)
        TryPurchaseItem(item, price, amount);   // 购买：加物品扣钱
    else if (amount < 0)
        TrySellSelectedItem(item, price, -amount);  // 出售：减物品加钱
}

private void HandleQuestReward(ItemSO item, int price, int amount)
{
    UpdateInventorySlots(item, amount);  // 奖励：直接加物品
}
```

### 6.6 完整调用链（购买物品）

```
玩家点击商店购买按钮
    │
    ▼
ShopManager.BuyItem(item, price)
    │ InventoryUpdateRequest.RaiseInventoryUpdateRequest(item, price, 1)
    ▼
ShoppingRequest(InventorySlotsStatsSO).InventoryUpdateRequestEvent 广播
    │
    ▼
InventoryManager.HandleShopping(item, price, 1)
    │ amount > 0 → TryPurchaseItem(item, price, 1)
    │   ├── 检查金币够不够
    │   ├── 检查背包有没有空位
    │   ├── TryAddExact(item, 1) 放物品
    │   └── UpdateGold(price) 扣钱
    ▼
购买完成
```

### 6.7 🔍 在 Unity 里验证

| 步骤 | 操作 |
|------|------|
| 1 | `Assets/GameSO/Events/` 找两个 InventorySlotsStatsSO 资产：ShoppingRequest 和 QuestRewardRequest |
| 2 | 看 ShopManager 的 `InventoryUpdateRequest` 拖的是 ShoppingRequest |
| 3 | 看 InventoryManager 的 `ShoppingRequest` 拖的也是 ShoppingRequest |
| 4 | 看 QuestManager 的 `QuestRewardRequest` 拖的是另一个资产 |
| 5 | 运行游戏，在商店买一个物品，在 InventoryManager.HandleShopping 打断点 |
| 6 | 注意：断点会触发，但 HandleQuestReward 不会（因为是不同的 SO 资产） |

---

## 七、LoadQuestEventSO —— 任务列表加载

### 7.1 定义

```csharp
public class LoadQuestEventSO : ScriptableObject
{
    public event Action<List<QuestSO>> LoadQuestEvent;

    public void OnLoadQuestEventRaised(List<QuestSO> quests)
    {
        LoadQuestEvent?.Invoke(quests);
    }
}
```

### 7.2 发布者与订阅者

| 角色 | 脚本 | 行为 |
|------|------|------|
| 发布者 | `QuestBoardManager` | 玩家打开任务板时，把任务列表广播出去 |
| 订阅者 | `QuestManager` | 收到任务列表，刷新任务状态 |

```csharp
// QuestBoardManager.cs（发布者）
[SerializeField] private LoadQuestEventSO loadQuestEventSO;

public void OpenQuestBoard()
{
    List<QuestSO> quests = GetAvailableQuests();
    loadQuestEventSO.OnLoadQuestEventRaised(quests);
}

// QuestManager.cs（订阅者）
[SerializeField] private LoadQuestEventSO loadQuestEventSO;

private void OnEnable()
{
    loadQuestEventSO.LoadQuestEvent += OnReFreshQuestState;
}
private void OnDisable()
{
    loadQuestEventSO.LoadQuestEvent -= OnReFreshQuestState;
}

private void OnReFreshQuestState(List<QuestSO> quests)
{
    // 刷新任务 UI 状态
}
```

---

## 八、QuestOptionsEventSO —— 任务选项选择

### 8.1 定义

```csharp
public class QuestOptionsEventSO : ScriptableObject
{
    public event Action<MyEnums.QuestState> questOptionsEvent;

    public void OnQuestOptionsEventRaised(MyEnums.QuestState questState)
    {
        questOptionsEvent?.Invoke(questState);
    }
}
```

### 8.2 发布者与订阅者

| 角色 | 脚本 | 行为 |
|------|------|------|
| 发布者 | `QuestOptionsButton` | 玩家点击任务选项按钮 |
| 订阅者 | `QuestManager` | 处理选择的任务状态 |

```csharp
// QuestOptionsButton.cs（发布者）
[SerializeField] private QuestOptionsEventSO questOptionsEventSO;

public void OnButtonClick(MyEnums.QuestState state)
{
    questOptionsEventSO.OnQuestOptionsEventRaised(state);
}

// QuestManager.cs（订阅者）
[SerializeField] private QuestOptionsEventSO questOptionsEventSO;

private void OnEnable()
{
    questOptionsEventSO.questOptionsEvent += OnQuestOptionChose;
}
private void OnDisable()
{
    questOptionsEventSO.questOptionsEvent -= OnQuestOptionChose;
}

private void OnQuestOptionChose(MyEnums.QuestState questState)
{
    // 处理任务选择逻辑
}
```

---

## 九、ShopLoadEventSO / OpenSaveLoadPanelEventSO

这两个 SO 定义了但在当前代码中引用较少（可能是预留接口或通过 Inspector 配置使用）：

- `ShopLoadEventSO`：商店数据加载，传三个列表（消耗品、武器、防具）和头像目标 Transform
- `OpenSaveLoadPanelEventSO`：打开存档/读档面板，传面板类型枚举

---

## 十、全局事件网络图

```
                         ┌─────────────────────┐
                         │   SceneLoadEventSO   │
                         └──────────┬──────────┘
              发布：Teleport / SaveSystem / RetryManager
              订阅：SceneChanger / DataManager / UIManager
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │    VoidEventSO       │  (sceneLoadedEvent 实例)
                         └──────────┬──────────┘
              发布：SceneChanger
              订阅：DataManager

  ┌──────────────────┐      ┌──────────────────┐      ┌──────────────────┐
  │  ToggleCanvas    │      │   LootEventSO    │      │  DataSaveEventSO  │
  │  EventSO (×N)    │      └────────┬─────────┘      └────────┬─────────┘
  └────────┬─────────┘               │                         │
   发布：UIManager            发布：Loot               发布：DataManager
   订阅：各 CanvasManager     订阅：InventoryManager    订阅：SaveSystem

  ┌──────────────────────┐    ┌──────────────────────┐
  │ InventorySlotsStatsSO │    │  LoadQuestEventSO    │
  │  (ShoppingRequest)    │    └──────────┬───────────┘
  └──────────┬───────────┘          发布：QuestBoardManager
   发布：ShopManager                订阅：QuestManager
   订阅：InventoryManager

  ┌──────────────────────┐    ┌──────────────────────┐
  │ InventorySlotsStatsSO │    │ QuestOptionsEventSO  │
  │ (QuestRewardRequest)  │    └──────────┬───────────┘
  └──────────┬───────────┘          发布：QuestOptionsButton
   发布：QuestManager               订阅：QuestManager
   订阅：InventoryManager
```

---

## 十一、设计模式总结

### 11.1 同一个 SO 类，多个资产实例

`VoidEventSO`、`ToggleCanvasEventSO`、`InventorySlotsStatsSO` 都有**多个资产实例**，每个实例服务于不同的功能。这就像同一个"插座模板"，可以造很多个插座，每个插不同的电器。

**好处：** 不用为每个功能写一个 SO 类，复用代码。

### 11.2 Send / Receive 分区

DataManager 的 Inspector 里把字段分成 `[Header("Send")]` 和 `[Header("Receive")]`：
- Send：DataManager 作为发布者持有的 SO
- Receive：DataManager 作为订阅者持有的 SO

```csharp
[Header("Send")]
[SerializeField] private DataSaveEventSO dataSavedEvent;        // 我发布

[Header("Receive")]
[SerializeField] private SceneLoadEventSO sceneLoadEventSO;     // 我订阅
[SerializeField] private VoidEventSO sceneLoadedEvent;          // 我订阅
```

**好处：** 一眼看清这个脚本的通信方向。

### 11.3 事件配对的铁律

所有 SO 事件订阅都严格遵循：

```csharp
void OnEnable()
{
    someSO.SomeEvent += Handler;
}
void OnDisable()
{
    someSO.SomeEvent -= Handler;
}
```

因为 SO 事件内部还是 C# 委托，不 `-=` 会内存泄漏。

---

## 十二、常见坑与排查

### 坑1：拖错 SO 资产

**症状：** 发布者调了 Raise，但订阅者收不到。
**原因：** 两个脚本拖了不同的 `.asset` 文件。
**排查：** 对比发布者和订阅者 SO 字段的引用，看是不是同一个文件。

### 坑2：订阅者 OnEnable 没执行

**症状：** 收不到事件。
**原因：** 订阅者所在的 GameObject 没激活，或脚本没 enable。
**排查：** 检查 GameObject 的勾选框，看 OnEnable 有没有打日志。

### 坑3：发布者在订阅者之前触发

**症状：** 第一次收不到，后面能收到。
**原因：** 发布者在 Awake/Start 就触发了，订阅者还没 OnEnable。
**排查：** 用 `[DefaultExecutionOrder]` 调整执行顺序，或延迟触发。

### 坑4：没取消订阅导致重复

**症状：** 一次触发，回调执行多次。
**原因：** OnEnable 只 `+=` 没 `-=`，反复激活导致重复订阅。
**排查：** 检查 OnDisable 里有没有对应的 `-=`。
