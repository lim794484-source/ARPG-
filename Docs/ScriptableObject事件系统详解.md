# ScriptableObject 事件系统详解 —— 两种事件系统的区别

> 本文档用最通俗的语言，讲清楚本项目为什么用 ScriptableObject 做事件，它和你熟悉的普通 C# 事件到底有什么不同。

---

## 📌 先记住一句话

> **ScriptableObject 事件 = "公共传声筒"，谁都能说话、谁都能听，且传声筒本身不会因为场景切换而消失。**
>
> **普通 C# 事件 = "两个人私下打电话"，必须知道对方的号码（引用），且一挂断（场景卸载）就断了。**

---

## 一、本项目里的"两种事件系统"

这个项目里其实有**两套事件机制**在同时工作：

| 类型 | 代表例子 | 本质 |
|------|---------|------|
| ① ScriptableObject 事件 | `SceneLoadEventSO`、`ToggleCanvasEventSO`、`VoidEventSO` | 用 ScriptableObject 资产做中转 |
| ② 普通 C# 事件 | `EnemyHealth.OnDefeated`、`ExpManager.OnLevelUp` | 用 C# 的 `event Action<...>` |

下面逐个拆解。

---

## 二、普通 C# 事件（你熟悉的那种）

### 2.1 长什么样？

```csharp
// EnemyHealth.cs
public class EnemyHealth : MonoBehaviour
{
    // 声明一个静态事件
    public delegate void MonsterDefeated(int exp);
    public static event MonsterDefeated OnDefeated;

    void Die()
    {
        OnDefeated?.Invoke(expReward);  // 触发事件
    }
}
```

```csharp
// ExpManager.cs（订阅者）
public class ExpManager : MonoBehaviour
{
    void OnEnable()
    {
        EnemyHealth.OnDefeated += GainExp;  // 订阅
    }
    void OnDisable()
    {
        EnemyHealth.OnDefeated -= GainExp;  // 取消订阅
    }

    void GainExp(int exp) { ... }
}
```

### 2.2 通俗理解

想象 `EnemyHealth` 和 `ExpManager` 是两个人：

- `EnemyHealth` 说："我死的时候会打个电话通知你"
- `ExpManager` 说："好，我把我的号码给你"（订阅）
- 敌人死了 → `EnemyHealth` 打电话 → `ExpManager` 接到电话，加经验

### 2.3 核心特点

| 特点 | 说明 |
|------|------|
| **直接耦合** | 订阅者必须**知道发布者是谁**（`EnemyHealth.OnDefeated`） |
| **同一场景** | 两边都得在运行时存在，订阅才有效 |
| **场景切换会断** | 如果 `EnemyHealth` 随场景卸载了，事件就没了 |
| **一对一/一对多** | 一个事件可以有多个订阅者，但都得直接引用 |

### 2.4 它的问题

> ❌ **跨场景通信会断！**
>
> 比如 `Teleport`（在 Scene1）想通知 `SceneChanger`（在 PersistentScene）切场景。
> - 如果用普通事件，`SceneChanger` 要订阅 `Teleport` 的事件
> - 但 `Teleport` 随 Scene1 卸载时，`SceneChanger` 订阅的事件也没了
> - 下次加载 Scene2，新的 `Teleport` 又要重新订阅，非常麻烦

---

## 三、ScriptableObject 事件（项目的精妙设计）

### 3.1 长什么样？

```csharp
// SceneLoadEventSO.cs（一个 ScriptableObject 文件）
public class SceneLoadEventSO : ScriptableObject
{
    public event Action<GameSceneSO, Vector3, bool> LoadRequestEvent;

    public void RaiseLoadRequestEvent(GameSceneSO scene, Vector3 position, bool isToFade)
    {
        LoadRequestEvent?.Invoke(scene, position, isToFade);
    }
}
```

```csharp
// Teleport.cs（发布者，在 Scene1 里）
public class Teleport : MonoBehaviour
{
    [SerializeField] private SceneLoadEventSO loadEventSO;  // ⭐ 拖一个 SO 资产进来

    private void OnTriggerEnter2D(Collider2D other)
    {
        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, newPosition, true);
    }
}
```

```csharp
// SceneChanger.cs（订阅者，在 PersistentScene 里）
public class SceneChanger : MonoBehaviour
{
    [SerializeField] private SceneLoadEventSO loadEventSO;  // ⭐ 拖同一个 SO 资产进来

    void OnEnable()
    {
        loadEventSO.LoadRequestEvent += OnLoadRequestEvent;  // 订阅
    }
    void OnDisable()
    {
        loadEventSO.LoadRequestEvent -= OnLoadRequestEvent;
    }
}
```

### 3.2 通俗理解

`SceneLoadEventSO` 这个资产是一个**公共传声筒**：

- 它不挂在任何 GameObject 上，是一个独立的 `.asset` 文件
- `Teleport` 和 `SceneChanger` 都在 Inspector 里把**同一个**传声筒拖进自己的字段
- `Teleport` 对着传声筒说话 → 传声筒转给所有听着的人（`SceneChanger`）

```
  Teleport（在 Scene1）          SceneLoadEventSO.asset           SceneChanger（在 PersistentScene）
  ┌──────────────┐            ┌──────────────────┐            ┌──────────────────┐
  │ 拖了这个 SO   │──引用────▶│  传声筒          │◀────引用───│  拖了同一个 SO    │
  │ 调用 Raise   │──说话────▶│  内部有个 event   │──广播─────▶│  订阅了这个 event │
  └──────────────┘            └──────────────────┘            └──────────────────┘
```

### 3.3 核心特点

| 特点 | 说明 |
|------|------|
| **完全解耦** | 发布者和订阅者**互相不知道对方的存在**，只认 SO 资产 |
| **跨场景** | SO 是资产文件，不随场景销毁，通信永不中断 |
| **可视化** | 在 Inspector 里拖资产就能连线，不用写代码 |
| **可调试** | 选中 SO 资产能看到谁引用了它 |

### 3.4 为什么能跨场景？

这是最关键的一点：

| 对象 | 生命周期 | 场景切换时 |
|------|---------|-----------|
| `Teleport`（MonoBehaviour） | 随 Scene1 存在 | Scene1 卸载 → 销毁 |
| `SceneChanger`（MonoBehaviour） | 在 PersistentScene | 不销毁 |
| `SceneLoadEventSO`（资产） | **永远存在** | ✅ 不销毁 |

> 💡 ScriptableObject 资产存在于硬盘上的 `.asset` 文件里，加载进内存后只要还有引用就不会被卸载。所以场景切换时，传声筒还在，新的发布者（Scene2 的 Teleport）继续对它说话，老的订阅者（SceneChanger）继续能听到。

---

## 四、两种事件系统的区别（对比表）

| 维度 | 普通 C# 事件 | ScriptableObject 事件 |
|------|-------------|----------------------|
| **存储位置** | 脚本里的字段 | 独立的 `.asset` 资产文件 |
| **耦合度** | 高（必须知道对方类名） | 低（只认资产） |
| **跨场景** | ❌ 会断 | ✅ 不会断 |
| **可视化连线** | ❌ 不行 | ✅ Inspector 拖资产 |
| **运行时创建** | 代码里 `+=` 订阅 | Inspector 配置好 |
| **适合场景** | 同场景内紧耦合通信 | 跨场景/解耦通信 |
| **调试难度** | 需打断点看调用链 | 看 SO 资产引用即可 |
| **性能** | 直接调用，快 | 多一层 SO 中转，可忽略 |

---

## 五、项目里谁用了哪种？

### 5.1 用 ScriptableObject 事件的（跨场景/解耦）

| SO 事件 | 发布者 | 订阅者 | 为什么用 SO |
|---------|--------|--------|------------|
| `SceneLoadEventSO` | Teleport | SceneChanger | Teleport 在内容场景，SceneChanger 在常驻场景 |
| `ToggleCanvasEventSO` | UIManager | 各 CanvasManager | UI 画布分布在不同对象 |
| `VoidEventSO` | 各种 | 各种 | 通用空事件，灵活 |
| `LootEventSO` | Loot | InventoryManager | Loot 在内容场景，背包在常驻场景 |
| `DataSaveEventSO` | DataManager | SaveSystem | 统一存档触发 |
| `InventorySlotsStatsSO` | ShopManager | InventoryManager | 商店和背包解耦 |

### 5.2 用普通 C# 事件的（同场景/紧耦合）

| 事件 | 发布者 | 订阅者 | 为什么不用 SO |
|------|--------|--------|--------------|
| `EnemyHealth.OnDefeated` | EnemyHealth | ExpManager | 都在常驻/内容场景，直接引用方便 |
| `ExpManager.OnLevelUp` | ExpManager | SkillTreeManager | 都是常驻管理器，紧耦合没问题 |
| `SkillSlot.OnAbilityPointSpent` | SkillSlot | SkillManager/SkillTreeManager | UI 内部交互，不需要跨场景 |

> 💡 **设计原则**：
> - **跨场景** → 必须用 ScriptableObject 事件
> - **同场景且紧耦合** → 用普通 C# 事件更简单
> - **可解耦的** → 优先 ScriptableObject 事件

---

## 六、🔍 在 Unity 里验证两种事件

### 验证1：ScriptableObject 事件（传声筒）

| 步骤 | 操作 | 观察 |
|------|------|------|
| 1️⃣ | `Project` → `Assets/GameSO/Events/SceneLoadEventSO.asset` | 这就是传声筒资产 |
| 2️⃣ | 打开 `Scene1`，找 `Teleport` 对象 | Inspector 里 `loadEventSO` 字段拖了这个资产 |
| 3️⃣ | 打开 `PersistentScene`，找 `SceneManager` | SceneChanger 的 `loadEventSO` 字段也拖了**同一个**资产 |
| 4️⃣ | 运行游戏，走进传送点 | Console 里能看到 SceneChanger 收到了请求 |

**关键：** 你把同一个 `.asset` 文件拖给两个不同脚本的字段，它们就"连上"了。这就是 SO 事件的精髓。

### 验证2：普通 C# 事件（打电话）

| 步骤 | 操作 | 观察 |
|------|------|------|
| 1️⃣ | 打开 `EnemyHealth.cs` | 看到 `public static event MonsterDefeated OnDefeated;` |
| 2️⃣ | 打开 `ExpManager.cs` | 看到 `EnemyHealth.OnDefeated += GainExp;` |
| 3️⃣ | 注意：这里没有 SO 资产 | 直接用类名 `EnemyHealth` 引用 |
| 4️⃣ | 杀一个敌人 | Console 看到 ExpManager 加了经验 |

---

## 七、为什么说这是"最精妙的设计"？

### 7.1 解决了 Unity 的一个老痛点

Unity 里跨场景通信一直很麻烦：
- `DontDestroyOnLoad` 会让对象乱飘
- `static` 变量场景切换后状态混乱
- 直接引用会在场景卸载时报空引用

ScriptableObject 事件完美解决了这些问题：
- ✅ 不需要 `DontDestroyOnLoad`
- ✅ 状态存在资产里，不依赖场景
- ✅ 引用永远有效

### 7.2 解耦带来的好处

假设你要加一个"切场景时播放音效"的功能：

**用普通事件的做法：**
```csharp
// 要改 Teleport 代码
public event Action OnSceneChange;  // 加一个事件
// 还要让 SoundManager 找到 Teleport 并订阅... 麻烦！
```

**用 ScriptableObject 事件的做法：**
```csharp
// SoundManager 里拖同一个 SceneLoadEventSO 进来
loadEventSO.LoadRequestEvent += PlaySound;
// ✅ 不用改 Teleport 一行代码！
```

> 💡 **开闭原则**：对扩展开放，对修改关闭。SO 事件让你加新功能时不用改旧代码。

### 7.3 数据和逻辑分离

SO 事件把"通信通道"（数据）和"通信内容"（逻辑）分开：
- SO 资产 = 通道（配置）
- 脚本 = 谁在通道里说话/听话（逻辑）

配置改了不用重新编译，逻辑改了不影响配置。

---

## 八、常见疑问

### Q1：SO 事件会不会泄漏内存？

不会。SO 资产加载进内存后，只要有脚本引用它就存在。没有引用了 Unity 会自动卸载。本项目里因为管理器一直引用着，所以一直在内存里，但这正是我们想要的。

### Q2：SO 事件和普通事件性能差多少？

几乎可以忽略。SO 事件只是多了一层"资产中转"，本质还是 C# 的 `event Action`，调用效率一样。

### Q3：我可以用一个 SO 事件传任意参数吗？

可以。SO 里的 `event Action<...>` 想传什么参数就传什么。本项目里 `VoidEventSO` 不传参数，`SceneLoadEventSO` 传 3 个参数。

### Q4：如果两个脚本拖了不同的 SO 资产会怎样？

那就**通信不上**！就像两个人拿了不同的传声筒，各说各的。必须拖**同一个** `.asset` 文件才能连上。这是新手最容易犯的错。

---

## 九、总结

| 对比项 | 普通 C# 事件 | ScriptableObject 事件 |
|--------|-------------|----------------------|
| **一句话** | 私下打电话 | 公共传声筒 |
| **耦合度** | 高 | 低 |
| **跨场景** | ❌ | ✅ |
| **可视化** | ❌ | ✅ |
| **适用** | 同场景紧耦合 | 跨场景/解耦 |
| **本项目** | 敌人死亡、升级 | 场景切换、UI、拾取 |

> 🎯 **记忆口诀**：跨场景找 SO，同场景用 event。SO 是传声筒，event 是打电话。

---

## 十、动手练习（可选）

1. 找一个 `ToggleCanvasEventSO` 资产，看看哪些脚本引用了它
2. 尝试新建一个 `VoidEventSO`，让两个脚本通过它通信
3. 对比：如果用普通事件实现同样的功能，代码会有什么不同？

通过这个练习，你会真正理解为什么项目作者选择了 ScriptableObject 事件系统。
