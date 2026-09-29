# Unity ARPG 项目面试进阶训练手册

适合正在学习 Unity、已经做出功能，但还不擅长解释原理和处理追问的你。

这份手册按“讲清一行代码 → 讲清一个功能 → 讲清系统协作 → 分析异常与改进”组织。你要练到能指出项目中的具体实现、解释设计原因，并用一个小实验验证自己的判断。重点是建立理解，不是背诵术语。

代码核对日期：2026 年 9 月 21 日。当前 ProjectVersion.txt 记录为 2022.3.62t7、团结编辑器 1.8.5；包配置包含 Input System 1.14.3、Addressables 1.22.3。README 中的版本描述与当前配置不同，面试介绍以实际使用的环境为准。

“当前实现”描述已经读到的代码；“建议改进”是练习方向，不代表项目已经实现。风险题是静态代码分析，是否在实际游玩中触发，需要按给出的步骤验证。文档中的代码练习不会自动修改游戏。

## 怎么使用这份手册

每题先只看“面试官问”，用自己的话回答 60～90 秒；卡住后看提示，再打开源码。最后核对参考答案，尝试回答追问。每次做 3～4 题即可，宁可把一个问题说清楚，也不要一次背完一章。

每题按 0～3 分自评：0 分是不会；1 分是会说术语；2 分是能结合代码解释；3 分是能举反例、完成验证并解释结果。每阶段四题至少 10 分，且没有 0 分，再进入下一阶段。这是自学标准，不代表公司的录用标准。

回答统一采用四步：**做什么 → 代码怎么走 → 为什么这样做 → 还有什么边界**。参考答案是表达骨架，请换成你自己的语言，不要把未完成的改进说成自己的成果。

| 阶段 | 题号 | 学习目标 |
|---|---|---|
| 一 | Q01～Q04 | 理解组件和生命周期 |
| 二 | Q05～Q08 | 解释输入和角色移动 |
| 三 | Q09～Q12 | 解释战斗时序和伤害 |
| 四 | Q13～Q16 | 理解 C# 契约和事件 |
| 五 | Q17～Q20 | 解释背包交易和任务 |
| 六 | Q21～Q24 | 解释对话 UI 和暂停 |
| 七 | Q25～Q28 | 解释存档和恢复 |
| 八 | Q29～Q32 | 解释场景加载和资源 |
| 九 | Q33～Q36 | 分析 A* 实现和性能 |
| 十 | Q37～Q40 | 完成排错和项目表达 |

## 第一阶段 从组件和生命周期开始

### Q01 角色为什么需要多个组件

**面试官问：** 你的玩家有 Transform、Rigidbody2D、Collider2D 和 PlayerMovement。它们分别负责什么？为什么一个脚本不能包办？

**先想一想：** 位置、物理模拟、碰撞形状、玩法规则，是不是同一件事？

**参考答案：** Transform 表达位置、旋转和缩放；Rigidbody2D 让对象参与二维刚体模拟；Collider2D 提供碰撞形状；PlayerMovement 根据输入和状态决定速度及动画。脚本调用这些组件提供的能力。分开以后，可以独立调碰撞体、替换外观或修改移动规则，不必自己重写物理引擎。

**继续追问：** 有 Collider2D 就一定收到 OnCollisionEnter2D 吗？答题要点：还需要检查刚体类型、碰撞层矩阵、Trigger 设置和碰撞回调条件，不能只看一个组件是否存在。

**动手验证：** 在玩家 Inspector 中定位四类组件，标出哪些位于根节点、哪些位于子节点。先记原值，再临时切换碰撞体的 Is Trigger，观察回调区别。

源码定位：PlayerMovement、Arrow，见文末 S01、S04。

### Q02 private 字段为什么能在 Inspector 中配置

**面试官问：** `[SerializeField] private Rigidbody2D rb;` 为什么既是 private，又能拖引用？和 public 有什么区别？

**先想一想：** C# 访问权限与 Unity 序列化是两套规则。

**参考答案：** private 限制其他类直接访问这个字段；SerializeField 告诉 Unity 对满足序列化规则的字段进行序列化，并通常在 Inspector 中显示。这样既能配置依赖，又不必开放任意赋值权限。public 字段不等于所有类型都能被 Unity 序列化；普通 C# 属性也不能简单等同于序列化字段。

**继续追问：** 拖错对象和完全没拖，哪个更容易发现？答题要点：空引用可能直接报错，错引用可能表现为“另一个对象动了”。需要验证引用的身份，而不只是加 null 判断。

**动手验证：** 临时清空 rb，观察实际报错位置；再通过 Inspector 恢复。解释为什么 GetComponent 适合同对象依赖，而 Inspector 引用更适合明确的跨节点依赖。

源码定位：PlayerMovement 的序列化字段，S01。

### Q03 Awake OnEnable Start 应该怎么分工

**面试官问：** 如果把事件订阅放在 Start，注销放在 OnDisable，重新启用后还收得到事件吗？

**先想一想：** 哪个方法只执行一次，哪个方法会随启用反复执行？

**参考答案：** 对正常启用的组件，Awake 用于实例初始化，OnEnable 在启用时执行，Start 在首次 Update 前执行一次。若 Start 订阅、OnDisable 注销，重新启用不会再执行 Start，订阅就丢了。本项目 PlayerMovement 在 OnEnable 订阅，在 OnDisable 注销，适合“仅启用期间监听”的需求。跨对象初始化顺序不能只靠猜测。

**继续追问：** 在其他对象的 OnEnable 里访问某单例，一定安全吗？答题要点：看实例是否已创建、激活及初始化；执行顺序属性不能替代明确的依赖准备过程。

**动手验证：** 给五个生命周期方法临时加日志，依次启用、禁用组件、禁用物体、重新启用。对照 Unity 的[生命周期文档](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html)。

源码定位：PlayerMovement、YSingleton，S01、S05。

### Q04 单例是否等于跨场景常驻

**面试官问：** YSingleton<T> 让 StatsManager.Instance 能随时访问，它是不是自动不会随场景销毁？

**先想一想：** 全局访问入口与对象寿命分别由谁控制？

**参考答案：** 当前 YSingleton 在 Awake 注册实例并销毁重复对象，在 OnDestroy 清空自己的静态引用。它没有调用 DontDestroyOnLoad。项目中的常驻需求依赖场景组织和对象所在场景是否被卸载，不能归功于 static。YSingleton<StatsManager> 和 YSingleton<DataManager> 是不同的封闭泛型类型，静态字段各自独立。

**继续追问：** 子类重写 Awake 却不调用基类，会怎样？答题要点：可能跳过单例注册，因此项目提供 OnSingletonInitialized 作为额外初始化入口。单例也会带来隐藏依赖和测试替换困难。

**动手验证：** 找到基类真正执行的 Destroy 和 OnDestroy；解释为何销毁一个重复实例时，不应把合法实例的 Instance 清空。

源码定位：YSingleton，S05。

## 第二阶段 把角色移动讲明白

### Q05 速度赋值为什么不乘 deltaTime

**面试官问：** `rb.velocity = new Vector2(horizontal, vertical) * speed;` 中输入为 (1, 0)，speed 为 5，会发生什么？要再乘 deltaTime 吗？

**先想一想：** 速度的单位和位移的单位分别是什么？

**参考答案：** 代码把刚体线速度设为 (5, 0)，即向世界坐标正 X 方向每秒 5 个单位。无阻挡、无其他影响且持续维持该速度时，一秒约移动 5 个单位。速度赋值不再乘 deltaTime；若自己累计位置，才通常用速度乘经过的时间得到位移。碰撞、阻尼和其他脚本仍可能影响实际运动。

**继续追问：** 如果改为 `transform.position += direction * speed` 呢？答题要点：每帧固定增加位移会受帧率影响，而且直接改 Transform 与刚体模拟需要协调。

**动手验证：** 分别记录 30 FPS 和 120 FPS 下约一秒的位移，再解释差异来源。参考 [Rigidbody2D.velocity](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rigidbody2D-velocity.html)。

源码定位：PlayerMovement.SetMovement，S01。

### Q06 为什么斜向移动不一定更快

**面试官问：** 同时按 W 和 D，速度一定是单方向的 √2 倍吗？

**先想一想：** 先确认传入的是 (1, 1)，还是已经归一化后的向量。

**参考答案：** 只有输入向量为 (1, 1)、且后面直接乘速度等条件成立时，长度才会放大到 √2。当前 GameInput 的 Move 使用未显式设置 mode 的 2DVector，按 Input System 默认 DigitalNormalized 模式，斜向输入会归一化。Joystick 也会把长度大于 1 的输入归一化。因此不能只看到 SetMovement 没写 normalized 就认定当前游戏有斜向加速。

**继续追问：** 为什么不能把所有摇杆输入都 normalized？答题要点：小幅度摇杆输入会被放大成满速；ClampMagnitude(input, 1) 能保留小于 1 的力度。

**动手验证：** 输出实际输入的 x、y 和 magnitude，分别测试键盘斜向、摇杆半推和满推。核对 [2D Vector Composite 的模式说明](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/ActionBindings.html)。

源码定位：PlayerMovement、Joystick、GameInput.inputactions，S01、S06。

### Q07 Update 和 FixedUpdate 怎么配合

**面试官问：** 你目前在 Update 调用移动逻辑并写刚体速度，这样一定错吗？你会怎么整理？

**先想一想：** 一帧渲染中，物理步可能发生几次？按键瞬间与持续移动是否一样？

**参考答案：** 不能说一定不能工作。当前代码在 Update 中读取输入、转换状态并设置速度；帧率与物理步不同，会影响输入和物理更新的时序。建议在符合 Input System 更新模式的阶段采集输入，缓存持续方向和一次性动作请求，再在 FixedUpdate 统一应用刚体运动。状态机、击退和动作锁也必须统一，不能只搬一行速度代码。

**继续追问：** 把 WasPressedThisFrame 直接搬进 FixedUpdate 就好吗？答题要点：必须匹配输入更新模式；可缓存按下请求并只消费一次，避免遗漏或重复处理。

**动手验证：** 在低帧率和不同 fixedDeltaTime 下短按攻击键，记录输入请求次数、消费次数及动画次数。对照[执行顺序说明](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html)。

源码定位：PlayerMovement.Update、MovementSM，S01。

### Q08 击退为什么会受到移动速度影响

**面试官问：** 同一个敌人推你，升级移动速度后击退也变强了。你先查哪里？

**先想一想：** 击退函数已经算出了速度，接收它的函数是否还会再乘一次速度？

**参考答案：** 当前 KnockBack 计算 `direction * force`，然后把它交给 SetMovement。SetMovement 又乘 GetSpeed，所以最终赋值为 direction × force × 移动速度。若 force 的设计含义就是击退速度，这里有额外缩放。建议把普通移动的“输入转速度”和底层“应用速度”分开，明确参数单位；如果想用冲量，再单独考虑 AddForce 与质量。

**继续追问：** 攻击时受击，为什么可能无法及时恢复？答题要点：同时跟踪 playerState、canBeInterrupted、动作结束事件与击退协程，找哪个写入最后生效。

**动手验证：** 保持 force 不变，把移动速度改为原来的两倍，记录 KnockBack 后立即赋给 rb.velocity 的数值。先验证放大关系，再设计修正。

源码定位：PlayerMovement.KnockBack、SetMovement、KnockBackCounter，S01。

## 第三阶段 理解战斗时序

### Q09 一次挥剑从按键到伤害经过哪些步骤

**面试官问：** 按下 J 后，敌人为什么会在挥剑时受伤，而不是按键瞬间受伤？

**先想一想：** 输入决定开始动作，谁决定命中的时刻？

**参考答案：** MovementSM 检查近战输入、武器激活状态和冷却，切到 Attacking；攻击状态停止移动并锁定打断。动画事件经 PlayerAnimationEventRelay 转发给 PlayerCombat.DealDamage，在攻击点周围查询碰撞体，再调用 IDamageable.TakeDamage。动作结束时通过 SO 事件回到 Idle 并重置冷却。具体命中帧取决于动画资源中的事件配置。

**继续追问：** 修改动画时长后伤害会自动正确吗？答题要点：还要检查命中事件位置、过渡是否跳过事件，以及同一事件是否被重复调用。

**动手验证：** 在 Animator、Animation 窗口和三个回调处对照时间顺序，画出“按键 → 攻击状态 → 命中 → 结束”四个时间点。

源码定位：PlayerMovement、PlayerCombat、PlayerAnimationEventRelay，S01～S03。

### Q10 动画结束事件丢失会怎样

**面试官问：** 玩家挥剑后再也不能移动，你会优先怀疑什么？

**先想一想：** 哪行锁住了输入，哪条路径负责解锁？

**参考答案：** HandleAttackingState 把 canBeInterrupted 设为 false，Update 随后会提前返回。正常恢复依赖 FinishCombat 广播、OnActionFinished 接收。如果动画被打断、事件函数名称不匹配、Relay 未转发或两端拖了不同 SO，解锁路径可能不执行。应记录动作状态和事件链，逐段定位，不能只在 Update 中强行解锁。

**继续追问：** 加一个固定 0.5 秒计时器兜底就够了吗？答题要点：动作速度、武器切换和取消会改变时序；更稳妥的方向是统一动作退出清理，并用动作编号拒绝旧回调。

**动手验证：** 在临时动画副本里移除结束事件，验证锁死条件；恢复后模拟中途切换装备，检查旧动作回调是否影响新动作。

源码定位：PlayerMovement.OnActionFinished、PlayerCombat.FinishCombat、Relay，S01～S03。

### Q11 为什么一个敌人可能被一次挥剑伤害两次

**面试官问：** 敌人有身体和头部两个碰撞体，OverlapCircleAll 返回的是什么？

**先想一想：** 一个 Collider2D 和一个逻辑受伤对象是一一对应的吗？

**参考答案：** 查询返回碰撞体数组。当前代码对每个结果 GetComponent<IDamageable> 并调用伤害。如果多个碰撞体都能找到同一个逻辑目标，可能重复伤害；若伤害组件只在父节点，当前 GetComponent 又可能找不到。建议先定义受击盒如何关联目标，再按目标身份做本次攻击去重，明确命中窗口内允许几次伤害。

**继续追问：** 每次查询创建 HashSet 就没有代价了吗？答题要点：去重也有分配和维护成本，可以按攻击周期复用集合；先正确，再测频率和开销。

**动手验证：** 给一个敌人添加子碰撞体，分别测试伤害组件在根节点和子节点的情况；记录碰撞体数量与实际扣血次数。

源码定位：PlayerCombat.DealDamage，S02。

### Q12 弓箭如何改成对象池

**面试官问：** 当前每次 Shoot 都 Instantiate，Arrow.Start 中延迟 Destroy。箭很多时，你会怎么改？

**先想一想：** 复用后的箭保留了上一次的哪些状态？

**参考答案：** 当前箭的发射、碰撞附着和销毁是一条单次生命周期。建议改为获取箭时显式初始化，命中或超时后归还；重置父节点、贴图、刚体类型、速度、碰撞状态、方向和本次伤害。寿命倒计时必须每次发射重新开始，不能依赖只执行一次的 Start。旧的延迟销毁或协程必须取消，避免回收后再次借出的箭被旧任务处理。

**继续追问：** 池容量设得越大越好吗？答题要点：池会保留内存；根据峰值并发数、预热成本和超额策略决定。当前项目尚未实现箭池。

**动手验证：** 列出 Arrow.AttachToTarget 修改的每个字段，把它们变成“借出时重置清单”。以后实现时连续复用同一支箭三次检查残留。

源码定位：PlayerBow.Shoot、Arrow，S04。性能背景见 Unity 的[分配与复用建议](https://docs.unity3d.com/2022.3/Documentation/Manual/performance-garbage-collection-best-practices.html)。

## 第四阶段 理解契约和事件

### Q13 为什么用 IDamageable 和 ISaveable

**面试官问：** 为什么不直接 GetComponent<EnemyHealth>，为什么存档不把所有类型写成一串 if？

**先想一想：** 调用方真正需要知道对象的类型，还是只需要知道它能做什么？

**参考答案：** IDamageable 描述“能承受伤害”的能力，ISaveable 描述“能写入和恢复 Data”的能力。调用方依赖契约，敌人、可破坏物等可以各自实现逻辑。抽象类适合共享基类行为，如 YSingleton；接口适合让不同类型提供同一能力。本项目 ISaveable 还有默认方法实现，不能机械回答“接口绝对不能有方法体”。

**继续追问：** 用了接口就完全解耦了吗？答题要点：ISaveable 默认注册方法仍直接访问 DataManager.Instance，接口减少了一部分类型依赖，没有消除所有依赖。

**动手验证：** 纸上设计一个可受伤木箱，列出它需要实现的成员；解释为什么 PlayerCombat 理论上无需新增木箱专用分支。

源码定位：IDamageable、ISaveable、YSingleton，S05、S07。

### Q14 SO 配置为什么与运行时状态分开

**面试官问：** QuestSO 已经有 currentAmount 字段，为什么 QuestManager 还用字典存进度？

**先想一想：** 两个系统引用同一个 SO 时，修改的是几份数据？

**参考答案：** SO 资产常被共享，QuestSO 适合存任务目标和奖励等配置；不同存档、不同角色的进度应由独立运行时数据表示。当前 QuestManager 用 QuestProgressData 保存状态和目标进度，QuestObjective.currentAmount 是遗留字段。不要把“SO 可以承载数据”误解为“运行时修改会自动变成玩家磁盘存档”。

**继续追问：** SO 一定永不销毁吗？答题要点：SO 也有生命周期和引用管理，不能把资产、运行时创建实例和内存常驻混成一个概念。

**动手验证：** 找两个引用同一 QuestSO 的位置，说明如果直接修改资产上的目标数量会影响谁。参考 [ScriptableObject 官方说明](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ScriptableObject.html)。

源码定位：QuestSO、QuestManager.QuestProgressData，S08。

### Q15 SO 事件是不是异步消息队列

**面试官问：** VoidEventSO.OnEventRaised 被调用后，订阅者是在下一帧才执行吗？

**先想一想：** 看函数体，而不是根据“事件”这个名字判断。

**参考答案：** 当前实现只是 `VoidEvent?.Invoke()`，是普通 C# 委托同步调用；处理器会在触发调用返回前依次执行。SO 提供可以通过 Inspector 共享引用的事件载体，并没有自动提供排队、历史重放、跨线程或异步能力。没有订阅者时调用直接结束；之后再订阅不会收到此前事件。

**继续追问：** 一个订阅者抛异常，后面的订阅者一定能执行吗？答题要点：普通多播委托不会自动隔离异常；是否逐个捕获、记录失败或中止流程，应按业务语义设计。

**动手验证：** 触发前打印 A，两个订阅者打印 B、C，触发后打印 D；观察顺序，再用受控异常验证传播。恢复代码后再继续练习。

源码定位：VoidEventSO，S09。

### Q16 为什么事件要成对订阅注销

**面试官问：** 多切几次场景，一次挥剑触发多次结束回调，你如何排查？

**先想一想：** 是否反复 +=，却没有在相应时机 -=？

**参考答案：** 本项目常用 OnEnable 订阅、OnDisable 注销，使监听期跟组件启用期一致。遗漏注销可能保留订阅对象引用或产生重复调用；具体后果取决于发布者、订阅者的寿命。还需确认订阅和注销使用的是同一个 SO 实例及同一个处理器。匿名 lambda 若没有保存委托引用，重新写一个看似相同的 lambda 通常不能注销原来的订阅。

**继续追问：** 如果对象禁用时也必须接收消息呢？答题要点：调整监听生命周期，或把数据处理放到常驻服务；不能无条件套用 OnEnable/OnDisable。

**动手验证：** 连续开关一个组件五次，每次只触发一次事件，核对回调次数；再检查 Inspector 中发布端与订阅端的事件资产是否相同。

源码定位：PlayerMovement、DataManager、VoidEventSO，S01、S09、S14。

## 第五阶段 从背包到任务的一致性

### Q17 买东西为什么要先检查容量

**面试官问：** 购买 10 个药水，背包只能放下 6 个，应该扣多少钱？当前代码怎么处理？

**先想一想：** 先定义业务规则，再看实现能否保持金币和物品一致。

**参考答案：** 当前普通物品购买采用全收或不收：TryPurchaseItem 检查参数和金币，再通过 TryAddExact 预检总容量并放入全部数量，成功后才扣钱。若放入异常，有移除已加入数量的补救。注意 price 在这个接口里是本次扣款金额，函数内部不会自动乘 amount，调用方必须明确总价语义。

**继续追问：** 这就是数据库事务吗？答题要点：不是；这是当前主线程同步流程下的业务一致性设计，没有完整隔离、持久化提交和逐槽回滚保证。按数量移除的补救也未必恢复原来的槽位分布。

**动手验证：** 测金币不足、容量刚好、容量差一个、负价格、批量购买五种情况；失败时同时核对金币和库存。

源码定位：InventoryManager.TryPurchaseItem、TryAddExact，S10。

### Q18 物品历史记录的是累计拾取还是当前持有

**面试官问：** 我捡过 5 个药水，喝掉 2 个，要求上交 5 个的任务能完成吗？

**先想一想：** 不要相信 ItemHistoryManager 这个名字，检查所有写入时的正负号。

**参考答案：** 当前 RecordItem 接受正负增量，使用、出售和丢弃会减少记录；读档时普通物品记录由背包重新构建。这更接近当前库存的镜像，而不是永不减少的累计拾取数。上交任务最后还会检查并扣除真实库存，因此只有 3 个药水不能交 5 个。若要做“曾累计捡到 5 个”，应维护独立的累计计数。

**继续追问：** 背包数量和历史字典会不会不一致？答题要点：存在多份可变数据就需要检查所有写入入口；金币、经验等特殊分支还应单独定义语义，不能笼统说字典永远等于背包。

**动手验证：** 依次拾取、使用、出售、丢弃和读档，比较 GetItemQuantity 与历史计数。发现不一致先定位漏记入口。

源码定位：InventoryManager.RecordItemHistory、RebuildItemHistory；ItemHistoryManager，S10、S11。

### Q19 为什么任务进度不能只用一个 bool

**面试官问：** Idle、Accepted、IsToComplete、Completed 和 Decline 分别有什么意义？

**先想一想：** “条件满足”与“领取奖励”是不是同一个时刻？

**参考答案：** 项目用状态区分未接取、已接取、待提交、已完成与拒绝；目标数量保存在 QuestProgressData。条件满足不意味着已经消耗物品或发奖。当前刷新任务板时会检查目标并推进部分状态，提交时再次刷新目标与验证库存。不能说它已经实现了所有目标变化的实时事件推送；地点目标也不能仅凭 SO 有字段就宣称逻辑齐全。

**继续追问：** 进入待提交后把物品卖了怎么办？答题要点：提交时重新验证；界面显示也应根据最新状态刷新，避免只相信先前缓存。

**动手验证：** 达成物品条件后先打开任务板，再卖掉物品后提交。观察 UI 提示、库存和奖励，解释三个状态是否一致。

源码定位：QuestManager.OnReFreshQuestState、UpdateObjectiveProgress、OnQuestOptionChose，S08。

### Q20 同一任务重复提交如何防止扣物和发奖两次

**面试官问：** 当前判断 previousState 不是 Completed 才发奖，是否已经完全解决重复提交？

**先想一想：** 扣物品发生在状态检查前，还是后？

**参考答案：** 当前 QuestStateChanged 防止从 Completed 再设置 Completed 时直接重复发奖，但提交入口先调用 IsQuestObjDone 和 TryConsumeQuestItems。IsQuestObjDone 对已完成任务直接返回 true，所以若重复完成请求仍能进入、且库存充足，可能再次扣物而不发奖。另外，状态入口缺少统一的合法迁移校验，不能只靠 UI 禁用保护业务。

**继续追问：** 任务两个目标分别要同一种药水 3 个和 4 个，检查各自达成就够吗？答题要点：需要合并成总需求 7 个；当前 TryConsumeQuestItems 已做合并。

**动手验证：** 设计“已完成任务再次收到提交请求”和“合计需求大于库存”两组测试。建议先校验允许的来源状态，再扣物、转状态、发奖，并考虑中途失败的恢复。

源码定位：QuestManager.OnQuestOptionChose、IsQuestObjDone、TryConsumeQuestItems，S08。

## 第六阶段 对话 UI 和暂停

### Q21 为什么对话按钮可能越点越多次触发

**面试官问：** ShowChoices 用 AddListener 绑定按钮，反复显示选项时会发生什么？

**先想一想：** 每次添加之前，旧监听是否已经清掉？

**参考答案：** DialogManager 在 DisableButtons 中移除运行时监听，再在 ShowChoices 为各按钮绑定目标对话。要逐条检查显示路径：ShowChoices 本身不会先清理；如果同一节点重复进入 ShowChoices，可能重复 AddListener。循环中当前用局部 nextDialog 绑定对应目标，可避免直接捕获共享循环索引造成选项错乱，但闭包正确不等于监听数量正确。

**继续追问：** RemoveAllListeners 会删掉 Inspector 里的所有持久监听吗？答题要点：不能这样理解；还应明确按钮监听由谁拥有，避免把其他模块添加的运行时监听一起清掉。

**动手验证：** 到达有选项的节点后多次尝试推进，再点一个选项，记录 OnOptionSelected 的执行次数。建议统一“清理 → 绑定 → 显示”的入口。

源码定位：DialogManager.AdvanceDialog、ShowChoices、DisableButtons，S12。

### Q22 UI 透明了为什么还可能挡住点击

**面试官问：** 只把 CanvasGroup.alpha 设为 0，就算关闭面板了吗？

**先想一想：** 看不见、不能交互、不拦截射线，是三个不同状态。

**参考答案：** alpha 控制透明度，interactable 控制组内可交互组件的交互，blocksRaycasts 影响 UI 射线阻挡。项目的 ICanvasManager 和 DialogManager 会一起设置三项，避免透明面板继续挡点击。CanvasGroup 隐藏不会自动停掉 GameObject 上脚本的 Update 或协程，是否停逻辑要另外设计。

**继续追问：** SetActive(false) 和隐藏 CanvasGroup 可以随意替换吗？答题要点：不行；前者会改变对象启用状态和相关生命周期，影响监听与协程等行为。

**动手验证：** 在按钮上方放一个透明面板，分别切换三个属性；用 UI 射线结果确认是哪一个对象阻挡点击。

源码定位：ICanvasManager.SetCanvaState、DialogManager.SetDialogCanvas，S12、S13。

### Q23 ESC 为什么应该关闭最上面的面板

**面试官问：** 先开背包，再开任务，点一下背包把它置顶，然后按 ESC，应该关谁？

**先想一想：** 打开顺序、视觉顺序和焦点顺序是否同步？

**参考答案：** 通常应关闭当前最上层、获得焦点的面板。CanvasFocusStack 用 LinkedList 维护打开顺序，并处理焦点变化、关闭请求和 sortingOrder 刷新。它是纯 C# 类，实际显示由回调交给外部。好处是顺序规则可独立测试；仍须保证面板把真实开关结果回报给它，不能只有“发出请求”却认为显示一定成功。

**继续追问：** LinkedList 按枚举值删除就是 O(1) 吗？答题要点：先查找值仍为 O(n)；已有节点引用时删除才是 O(1)。面板很少时不必为理论复杂度过度设计。

**动手验证：** 按题目顺序操作，再测重复关闭、关闭不存在面板、清空所有面板；核对当前焦点和显示顺序。

源码定位：CanvasFocusStack.HandleFocus、ReportState、HandleESCOrCloseTop，S13。

### Q24 两个系统都暂停游戏应该怎么恢复

**面试官问：** 菜单与场景加载同时要求暂停，菜单关闭后是否应该立即恢复？

**先想一想：** 一个 bool 能不能表达两个尚未结束的暂停原因？

**参考答案：** 应等所有需要暂停的原因都解除再恢复。当前 TimeManager 有 pauseCount，但 PauseGame 没有递增它，ResumeGame 却递减，所以不能宣称当前实现了正确的引用计数暂停。建议使用成对申请和释放，或返回可释放的暂停令牌，以便识别拥有者、避免重复释放并清理异常流程。

**继续追问：** timeScale 为 0 时 UI 还能更新吗？答题要点：不代表所有代码停止；缩放时间等待会停住，需独立运行的过渡可使用 unscaledDeltaTime 或 WaitForSecondsRealtime。项目淡入淡出就是这种用法。

**动手验证：** 依次模拟 A 暂停、B 暂停、A 恢复、B 恢复，预期前三步仍暂停、最后恢复。再用缩放与非缩放等待作对比。

源码定位：TimeManager、SceneChanger.FadeCanvasGroup，S15、S17。参阅 [Unity 协程说明](https://docs.unity3d.com/2022.3/Documentation/Manual/Coroutines.html)。

## 第七阶段 存档不仅是写 JSON

### Q25 DataManager 和 SaveSystem 为什么分开

**面试官问：** 请从点击保存开始，解释数据如何从对象变成磁盘文件。

**先想一想：** 收集状态、描述数据、序列化、文件读写，各是哪一层的责任？

**参考答案：** ISaveable 让背包、任务等对象提供 SaveData 和 LoadData；DataManager 管理注册表并收集状态到 Data；SaveSystem 封装 SaveInfo 与 Data，通过 Newtonsoft.Json 序列化并写入 persistentDataPath。手动保存需要先 PrepareManualSaveData 采集最新数据，不能把 WriteSave 当成自动抓取全场状态的入口。

**继续追问：** 在遍历注册表时为何用 ToList？答题要点：获得成员快照，避免回调过程中原列表增删使枚举失效；它只是浅复制，不会冻结对象状态，也不自动提供线程安全。

**动手验证：** 用临时测试档保存金币变化前后两次，比较 JSON。再解释“采集前写盘”为什么可能写到旧值。

源码定位：ISaveable、DataManager.PrepareManualSaveData、SaveSystem.WriteSave、Data，S07、S14、S16。

### Q26 为什么物品资源名不是永久可靠的存档 ID

**面试官问：** 把药水资源从 Potion 改名为 HealthPotion，旧存档还能恢复吗？

**先想一想：** 数据里存的是资产引用、显示名、资源名，还是独立 ID？

**参考答案：** 当前背包存 item.name，再从 knownItems 按名字恢复；任务也用资源名构造键。改名或同名资源会让匹配失效或产生歧义。资源名比会本地化的显示名称方便，但不是不可变身份。建议使用创建后稳定且唯一的业务 ID，并提供旧键到新键的迁移映射。GetInstanceID 也不适合作跨运行持久化身份。

**继续追问：** 任务目标按列表序号保存，交换两个目标会怎样？答题要点：进度可能对应错目标；独立 objectiveId 和存档版本迁移更可靠。

**动手验证：** 只在备份/临时资产上做改名和目标重排实验，观察旧数据如何匹配。区分“Unity 资产引用依赖 .meta GUID”与“业务存档自己用字符串键”。

源码定位：InventoryManager.SaveData、LoadData；QuestManager.CreateStatus、ApplySavedProgress，S08、S10。

### Q27 读档为什么还会触发自动存档问题

**面试官问：** 从当前游戏读入旧存档时，为什么恢复后的背包或位置可能被当前场景覆盖？

**先想一想：** 读档与普通切场景是否使用同一个加载事件？

**参考答案：** LoadSave 先把存档装入 DataManager，再发场景加载请求；普通加载请求同时会触发 DataManager.OnAutoSave。如果不区分原因，它会重新收集即将离开的当前场景，污染刚读入的快照。当前 IsLoadingSaveRequest 在同步请求期间标记读档，让 OnAutoSave 跳过采集，并用 finally 清理标记。

**继续追问：** finally 执行时场景已经异步加载完成了吗？答题要点：没有。标记覆盖的是同步事件分发窗口，不能作为整个加载操作的进行中状态。若以后改为排队事件，应随请求携带 LoadReason。

**动手验证：** 存下位置 A 与金币 10，再走到 B、改变金币后读取 A 的档，跟踪 LoadFromData、OnAutoSave、OnLoadCompleted 的顺序和数据。

源码定位：SaveSystem.LoadSave、DataManager.OnAutoSave，S14、S16。

### Q28 坏档和旧档分别怎么处理

**面试官问：** 最新档 JSON 截断了，与旧档缺少 inventorySlots 字段，是同一类问题吗？

**先想一想：** 格式损坏、结构缺失、语义不合法，应该分别检查。

**参考答案：** 当前 GetLatestLoadableSavePath 会倒序寻找可解析、含角色数据且场景可定位的档，允许跳过部分坏档。Data 的默认集合与各 LoadData 的校验兼容部分缺字段旧档，但不代表覆盖全部迁移问题。WriteSave 目前直接写最终文件，没有完整的临时文件提交策略。建议增加版本号、迁移步骤、数值与 ID 校验，并采用经目标平台验证的替换/备份写入方案。

**继续追问：** 加校验和就能防作弊吗？答题要点：普通校验和用于发现损坏，不等于可信认证；也不能解决“字段合法但业务含义错了”。

**动手验证：** 复制一份测试档，分别截断内容、删除新增字段、改成不存在的场景 ID，记录回退行为；不要破坏唯一的真实存档。

源码定位：SaveSystem.IsLoadableSaveFile、GetLatestLoadableSavePath、WriteSave，S16。

## 第八阶段 场景切换和异步流程

### Q29 请讲清项目的场景组织

**面试官问：** InitialScene、PersistentScene 和游戏内容场景各自有什么职责？

**先想一想：** 哪些对象应跟着地点变化，哪些对象应持续存在？

**参考答案：** InitialLoad 在启动时加载 persistentScene；SceneChanger 再用 Additive 方式加载菜单或地点内容场景，切换时卸载旧内容场景。这样常驻服务和内容可以分别管理。不是所有加载都用了 Additive：InitialLoad 的调用没有显式传这个模式，不能把后续场景加载策略套到整个启动流程上。

**继续追问：** 场景加载完成就自动成为 Active Scene 吗？答题要点：Additive 加载与设置活动场景是不同操作；应明确新生成对象归属哪个场景。项目丢弃物品时显式 MoveGameObjectToScene，就是相关例子。

**动手验证：** 从启动场景进入游戏，观察 Hierarchy 的场景分组；生成一个丢弃物，记录其 scene，再切场景看其生命周期。

源码定位：InitialLoad、SceneChanger.LoadScene、InventoryManager.DropLoot，S10、S17、S18。参阅 [Addressables 场景加载](https://docs.unity3d.com/Packages/com.unity.addressables@1.22/manual/LoadingScenes.html)。

### Q30 异步加载是不是另开一个线程执行我的代码

**面试官问：** LoadSceneAsync 和协程为什么不会把整个游戏卡在等待上？协程里可以放心执行十秒计算吗？

**先想一想：** “等待期间交回控制权”和“计算被放到其他线程”并不相同。

**参考答案：** 异步 API 让调用方发起操作后可以等待完成通知；协程在 yield 处挂起，之后按条件继续。普通 Unity 协程中的同步代码仍在主线程执行，十秒大循环会阻塞主线程。不能把所有引擎内部资源加载工作都概括为单线程或多线程；关键是知道自己的代码在哪里运行，以及哪些 Unity API 只能在主线程访问。

**继续追问：** 场景异步加载就完全没有卡顿吗？答题要点：资源处理、激活、Awake 和 OnEnable 中的大量初始化仍可能产生主线程尖峰，应实测各阶段耗时。

**动手验证：** 在小测试场景中比较分帧处理与一次处理一批数据的 Profiler 时间线，避免在正式场景中故意阻塞十秒。

源码定位：SceneChanger.UnloadCurrentScene、LoadScene，S17。参阅 [Unity 协程文档](https://docs.unity3d.com/2022.3/Documentation/Manual/Coroutines.html)。

### Q31 连续收到两次加载请求会怎样

**面试官问：** 玩家短时间触发两个传送点，或加载失败，当前流程一定能恢复吗？

**先想一想：** 回调使用自己的请求数据，还是一个后来可以被覆盖的成员字段？

**参考答案：** SceneChanger 用共享 sceneToLoad、newPosition 和 isToFade 保存请求，当前入口没有明确的加载中拦截。重复请求可能启动多个协程并覆盖这些字段。OnLoadCompleted 又直接用 handle.Result 和 sceneToLoad 更新状态，没有先检查操作成功与否。建议建立加载状态，采用明确的拒绝、排队或替换策略，让回调绑定请求身份，并集中处理失败时的输入、暂停和遮罩恢复。

**继续追问：** 只加 isLoading=true 就解决了吗？答题要点：每个成功、失败、取消和禁用出口都要释放状态；否则只是把重入问题换成永久锁住。

**动手验证：** 在测试环境连续发两次不同目标请求，再使用无效引用模拟失败；核对当前场景、玩家位置、输入状态、timeScale 和遮罩。

源码定位：SceneChanger.OnLoadRequestEvent、UnloadCurrentScene、OnLoadCompleted，S17。

### Q32 编辑器能加载为什么打包后失败

**面试官问：** 用 Addressables 的场景在编辑器里正常，构建后找不到，如何排查？

**先想一想：** 编辑器直接读资产与玩家程序读构建产物，走的是不是同一条路径？

**参考答案：** 先记录实际 key、句柄状态和异常，再检查场景引用是否有效、是否在正确的组、内容是否为目标平台构建，以及 catalog、构建路径和加载路径是否一致。编辑器的资产数据库模式可能掩盖内容构建缺失。最后在真实构建环境验证启动入口到目标场景的完整链路，不能只验证一个 AssetReference 在 Inspector 中非空。

**继续追问：** Addressables 等于已经实现热更新吗？答题要点：它提供资源寻址和加载等能力；远程内容、版本发布、更新流程与失败回退需要额外配置和实现。普通 Addressables 资源更新也不等于 C# 代码热更新。

**动手验证：** 画出“入口场景 → 常驻场景 → 菜单 → 地点”的依赖图，给每一跳记录引用、构建产物和成功/失败日志。

源码定位：InitialLoad、SceneChanger、GameSceneSO，S17、S18。官方依据见 [Addressables 场景加载说明](https://docs.unity3d.com/Packages/com.unity.addressables@1.22/manual/LoadingScenes.html)。

## 第九阶段 从 A 星原理追到实现细节

### Q33 A 星中的 g h f 分别是什么

**面试官问：** 为什么你会把一个节点加入 open 集合？为什么选择 f 最小的节点？

**先想一想：** 已经走了多少，与预计还要走多少，需要分开保存。

**参考答案：** g 是起点到当前节点已知的路径代价，h 是当前节点到目标的估计代价，f=g+h。每次从 open 中选择最低 f 的候选扩展；发现更小的 g 时更新节点及父节点，找到终点后沿父节点回溯。项目用 Dictionary 保存 open、HashSet 保存 closed、Stack 返回路径。启发式与边代价的关系影响搜索效率和最优性，不能只背公式。

**继续追问：** h=0 会怎样？答题要点：在非负边权等标准前提下，按累计代价搜索，退化为 Dijkstra 的搜索策略；通常会探索更多节点。

**动手验证：** 在 5×5 网格纸上设置起终点和障碍，手算前三次展开的 g、h、f，并画出父节点方向。不要跳过代价相同时如何选取的约定。

源码定位：AStarPathFinder.FindPath、AddNodeToOpen、RetracePath，S19。

### Q34 你的 CalDistance 真的是欧氏距离吗

**面试官问：** 当前函数先算 dx+dy，然后返回 1 或 1.414。距离十格和距离两格有什么不同？

**先想一想：** 同一个函数被拿去算相邻边代价，又被拿去估算到终点的距离。

**参考答案：** 当前 CalDistance 对曼哈顿差值大于等于 2 的情况都返回 1.414，否则返回 1。它用于相邻直走/斜走时大致符合边代价，但用来估算任意远终点时区分能力很弱；同一点还会返回 1，而不是 0。因此不能把当前实现介绍成标准欧氏距离，也不能直接套用标准启发式保证最优性的证明。

**继续追问：** 八方向、直走代价 1、斜走代价约 1.414 时怎么写更合适？答题要点：可用八方向距离 `max(dx,dy) + (D2-1)*min(dx,dy)`，D2 与实际斜边代价一致。四方向才常用曼哈顿距离；不能混用成本模型。

**动手验证：** 列出 (0,0) 到 (0,0)、(1,0)、(1,1)、(10,0) 的当前返回值；与建议公式比较。改进验证应比较路径总代价和展开节点数，而不仅是“能走到”。

源码定位：PathFinderDetails.CalDistance，S20。

### Q35 斜着走是否会穿过墙角

**面试官问：** 斜向目标格能走，就代表角色一定能安全通过吗？

**先想一想：** 两个相邻侧格和角色碰撞体宽度会影响通行。

**参考答案：** 当前 CanWalkDiagonally 在两个侧格都不可走时才拒绝，因此只有一侧阻挡时仍允许斜走。这是否符合需求要按角色宽度和障碍规则判断；严格禁止切角可在任一侧阻挡时拒绝。函数还直接用字典索引访问侧格，未在此处检查键是否存在；若地图边缘或稀疏网格缺格，会有异常风险。

**继续追问：** 把 && 改成 || 就全部修好了吗？答题要点：还要检查不存在的格子如何处理、世界坐标转换、网格尺寸与角色安全边距。算法通路不自动等于物理碰撞体能通过。

**动手验证：** 分别布置双侧墙、单侧墙、地图缺角三种情况，比较预期与实际路线；用 Gizmos 同时画网格路径和碰撞体范围。

源码定位：AStarPathFinder.CanWalkDiagonally、AStarNodeManager，S19、S21。

### Q36 已有重寻路冷却为什么还可能掉帧

**面试官问：** MovementController 有 0.5 秒冷却，敌人目标不可达时应该最多每秒算两次吧？

**先想一想：** 冷却判断覆盖了所有 FindWay 入口吗？

**参考答案：** 当前冷却用于已有有效路径时、目标移动后的重算分支；没有有效路径时 GetPosToGo 会直接尝试 FindWay。如果外部每帧调用，不可达状态仍可能每帧搜索。日志每两秒输出一次只减少日志，不等于减少算法计算。建议给失败重试也加调度或退避，并按帧预算分摊多个角色的请求。

**继续追问：** open 是 Dictionary，选择最小 f 就是 O(1) 吗？答题要点：SearchCheapestCost 遍历全部 open，是 O(n)；可考虑最小堆，但需要处理节点代价下降、重复入堆及旧条目失效。当前代码还会在展开时创建方向数组，需测实际分配。

**动手验证：** 让 1、10、50 个敌人的目标不可达，记录每帧寻路次数、展开节点数、CPU 时间与 GC Alloc。先证实瓶颈再改数据结构。

源码定位：MovementController.GetPosToGo、FindWay；AStarPathFinder.SearchCheapestCost，S19、S22。

## 第十阶段 像开发者一样排错和表达

### Q37 Vector3.zero 为什么不适合表示所有失败

**面试官问：** 为什么 NPC 不愿意走向世界原点？为什么存档位置 (0,0,0) 可能变成默认出生点？

**先想一想：** 零向量既可能是合法坐标，也被代码当作了特殊标记。

**参考答案：** MovementController 无路径时返回 Vector3.zero；SceneChanger 收到零向量时改用默认出生点。这样合法数据与“失败/未指定”混在一起，调用者无法可靠区分。建议用 `TryGetWaypoint(out Vector3 point)` 的 bool 表达成功，或者带明确状态的结果类型；出生点请求用独立标志或可空值表达是否指定。

**继续追问：** 换一个特别大的坐标当失败值呢？答题要点：仍依赖魔法值，扩大世界范围后还可能碰撞。接口应该明确表达状态，而不是靠猜数值。

**动手验证：** 构造合法原点目标与无路径目标，要求两个结果可以被调用方区分；再测试保存和恢复原点位置。

源码定位：MovementController.GetPosToGo、SceneChanger.OnLoadRequestEvent，S17、S22。

### Q38 只用 StartsWith 能保证删除路径位于存档目录吗

**面试官问：** 当前 DeleteSave 会把路径规范化后做前缀检查，这样足够吗？

**先想一想：** `C:\Game\SaveBackup\a.json` 是否也以 `C:\Game\Save` 开头？

**参考答案：** 仅比较字符串前缀没有确认目录边界，相似名称的相邻目录也能匹配。建议使用目录边界明确的规范化比较或安全的相对路径校验，同时按目标平台考虑大小写、分隔符和链接策略。Path.GetFullPath 本身也可能因输入无效抛异常，应纳入错误处理。这里是在审查当前校验范围，不代表已经发生了误删。

**继续追问：** UI 只列出存档文件，就可以忽略底层校验吗？答题要点：底层入口可能被其他代码调用，应自己守住输入契约，但无需把单机存档夸大成已实现完整安全沙箱。

**动手验证：** 先只编写不执行删除的路径判断用例：真实子文件、相邻目录、包含 .. 的路径、空字符串。禁止拿真实文件试删。

源码定位：SaveSystem.DeleteSave，S16。

### Q39 性能优化之前应该测什么

**面试官问：** 50 个敌人同屏后掉帧，你会先对象池、改算法还是合批？

**先想一想：** 先区分 CPU、GPU、分配、物理和加载卡顿，才能决定改哪里。

**参考答案：** 建立固定场景与相同操作路径，在目标设备或接近实际构建条件下采样帧时间。CPU 时间线看寻路、脚本、物理与 UI；结合渲染指标区分 GPU/绘制压力；GC Alloc 看分配来源。项目可重点观察无路径重算、OverlapCircleAll、箭的创建销毁和高频路径数组转换，但这些只是候选，不能未测就断言谁最慢。

**继续追问：** 把所有 ToList 都删掉可以吗？答题要点：有些是为遍历稳定性而做的低频快照；先看频率和正确性目的。不能用节省少量分配换来集合修改异常。

**动手验证：** 每次只改一个因素，记录相同负载下的典型帧、最慢帧、分配字节和寻路次数；优化后重跑同样流程。不填写没有实测的提升百分比。

源码定位：PlayerCombat、PlayerBow、DataManager、MovementController，S02、S04、S14、S22。参阅 Unity 的[垃圾回收优化建议](https://docs.unity3d.com/2022.3/Documentation/Manual/performance-garbage-collection-best-practices.html)。

### Q40 如何介绍项目并接住质疑

**面试官问：** 用两分钟介绍你的项目。选一个你理解最深的系统，再说一个当前不足和改进办法。

**先想一想：** 功能清单只能说明做过什么，调用链和取舍才能说明你理解了什么。

**参考答案骨架：** “这是一个 2D 俯视角 ARPG 学习项目，包含近远程战斗、背包商店、任务对话和存档。我重点学习了……。以存档为例，各对象通过 ISaveable 提供状态，DataManager 汇总，SaveSystem 写文件；读档复用了场景事件，因此用请求标记避免自动保存覆盖快照。当前仍有……边界，我会先用……复现，再做……改进，用……验证。”

**继续追问：** 哪部分是参考教程或借助工具完成的？答题要点：如实说明来源与自己的工作范围；区分“跟着做出来”“读懂并修改过”“独立设计且验证过”。不会的部分说明你的定位路径，不编造实现经历。

**动手验证：** 录音两分钟，检查每个术语能否展开成具体方法和数据；删去“完全解耦”“永不出错”“性能很好”等没有证据的说法。再任选 Q20、Q27 或 Q36 接受连续追问。

## 六个值得优先准备的项目追问

以下问题可用于面试前最后一次回顾。它们在前面的题目中已经给出了分析与验证方法。

| 问题 | 当前代码证据 | 你应该能讲出的边界 |
|---|---|---|
| 击退与移动速度耦合 | KnockBack 调用再次乘 GetSpeed 的 SetMovement | 参数单位与最终速度，见 Q08 |
| 重复提交可能再次扣物 | 已完成目标判断返回 true，随后尝试扣物 | 业务入口幂等，见 Q20 |
| 暂停计数不成对 | PauseGame 未增加 pauseCount | 多个暂停拥有者，见 Q24 |
| 读档期间再次触发加载事件 | IsLoadingSaveRequest 控制同步自动保存入口 | 请求窗口与异步完成不同，见 Q27 |
| 启发式没有体现远距离 | CalDistance 只有 1 与 1.414 两种结果 | 估价函数与边代价，见 Q34 |
| 无路时仍可能高频搜索 | 无有效路径分支不检查重建冷却 | 日志限频不等于计算限频，见 Q36 |

## 十四次练习安排

建议每次 45～60 分钟：10 分钟复述，20 分钟读代码，15 分钟验证，最后 5 分钟记录错题。如果题目仍讲不清，就重复本次，不必硬追进度。

| 次数 | 学习内容 | 本次产出 |
|---|---|---|
| 1 | Q01～Q04 | 玩家组件图与生命周期日志 |
| 2 | Q05～Q08 | 输入长度、速度和击退的实测记录 |
| 3 | Q09～Q12 | 一次近战与射箭的调用链 |
| 4 | Q13～Q16 | 事件收发实验与订阅检查清单 |
| 5 | Q17～Q20 | 购买、交任务失败条件矩阵 |
| 6 | Q21～Q24 | 按钮监听、焦点和多来源暂停实验 |
| 7 | Q25～Q28 | 保存与恢复的数据流图 |
| 8 | Q29～Q32 | 场景切换成功与失败流程图 |
| 9 | Q33～Q36 | 手算网格与寻路计数记录 |
| 10 | Q37～Q40 | 接口反例与两分钟项目介绍 |
| 11 | 重做所有 0～1 分题 | 用自己的话重写答案 |
| 12 | 完成下面的模拟面试 A | 录音并标注卡住的问题 |
| 13 | 完成下面的模拟面试 B | 排查步骤与改进验收条件 |
| 14 | 抽 8 题闭卷复述 | 总结还不能证明的结论 |

## 模拟面试 A 基础与功能链路

总计约 25 分钟，先不看答案。基础回答正确后，再进入追问。

1. 2 分钟介绍项目，指出一个你亲自验证过的功能。对应 Q40。
2. 解释速度赋值为什么不乘 deltaTime。继续追问键盘斜向为什么不一定更快。对应 Q05、Q06。
3. 从按 J 开始讲完一次近战。继续追问动画结束事件丢失如何定位。对应 Q09、Q10。
4. 解释 SO 事件的实际调用方式。继续追问不同 SO 实例和重复订阅。对应 Q15、Q16。
5. 解释买物品时的容量和扣款。继续追问失败后哪些数据应保持不变。对应 Q17。

每项 0～4 分：概念正确 1 分、结合代码 1 分、说明一个边界 1 分、提出可观察的验证 1 分。达到 16 分且没有概念性错误，再练 B。

## 模拟面试 B 系统异常与取舍

总计约 35 分钟。每题先说你会观察什么，再说可能原因，最后提出修改与回归验证。

1. 读档之后出现当前背包覆盖旧档，分析事件时序。对应 Q25、Q27。
2. 重复提交一个已完成任务，奖励没重复但物品变少，定位入口保护。对应 Q20。
3. 场景加载失败后黑屏且不能动，列出必须恢复的状态。对应 Q24、Q31。
4. 目标不可达时 50 个敌人掉帧，但日志两秒才一条，解释为何不矛盾。对应 Q36、Q39。
5. 手算当前 CalDistance，再说明换启发式和改 open 集合各解决什么问题。对应 Q33～Q36。

每项仍为 0～4 分。不要仅回答“加空判断”“上对象池”“用事件解耦”。要求说清条件、位置、预期和副作用。17 分以上说明这组问题已能较完整表达，仍需按投递岗位补充渲染、网络或平台等专项知识。

## 三个进阶实作题

这些是后续练习，不是项目已完成的改动。每次只做一题，并保留修改前的复现场景。

**实作一 让路径查询明确表达成功与失败**

把“返回 Vector3.zero 表示失败”改成清晰的结果契约。验收：合法原点可达时能返回；不可达时明确失败；已有调用方全部处理失败；暂停和目标变化不造成错误移动。先完成 Q37 再做。

**实作二 让任务提交可以安全重复调用**

集中校验来源状态，合并物品需求，再执行扣除、状态提交和奖励发放。验收：连续提交同一任务只扣一次、只发一次；库存不足全部不扣；同物品多目标合计正确；读档恢复完成状态不发奖。另行说明奖励发放异常时如何处理，不能把单线程误认为不会失败。先完成 Q17～Q20 再做。

**实作三 限制失败寻路的重试频率**

为无路径状态增加可观察的重试节奏，并避免所有角色同帧重试。验收：不可达时每秒调用次数有明确上限；目标或障碍变化后能在定义的延迟内恢复；成功路径行为不退化；相同场景改前改后有 Profiler 对比。先完成 Q36、Q39 再做。

## 错题记录模板

每次只记录具体缺口，避免写“Unity 基础不牢”这种难以执行的总结。

```text
题号与日期：
我第一次怎么回答：
不准确的那一句：
实际源码位置与行为：
我做的验证与结果：
修正后的 60 秒回答：
仍待验证的条件：
下次复习日期：
自评 0～3 分：
```

## 源码导航

这些链接指向本项目实际文件。方法名用于定位具体逻辑；代码以后修改时，应重新核对答案。

| 编号 | 文件与重点 |
|---|---|
| S01 | [PlayerMovement](D:/my_arpg-master/Assets/Scripts/Gameplay/Player/PlayerMovement.cs) 输入、状态、移动、击退 |
| S02 | [PlayerCombat](D:/my_arpg-master/Assets/Scripts/Gameplay/Player/PlayerCombat.cs) 近战查询与动作结束 |
| S03 | [PlayerAnimationEventRelay](D:/my_arpg-master/Assets/Scripts/Gameplay/Player/PlayerAnimationEventRelay.cs) 动画事件转发 |
| S04 | [PlayerBow](D:/my_arpg-master/Assets/Scripts/Gameplay/Player/PlayerBow.cs) 与 [Arrow](D:/my_arpg-master/Assets/Scripts/Gameplay/Player/Arrow.cs) 射击生命周期 |
| S05 | [YSingleton](D:/my_arpg-master/Assets/Scripts/Contracts/YSingleton.cs) 单例初始化与清理 |
| S06 | [Joystick](D:/my_arpg-master/Assets/Scripts/UI/Joystick.cs) 与 [输入配置](D:/my_arpg-master/Assets/Settings/Input/GameInput.inputactions) 输入归一化 |
| S07 | [IDamageable](D:/my_arpg-master/Assets/Scripts/Contracts/IDamageable.cs) 与 [ISaveable](D:/my_arpg-master/Assets/Scripts/Contracts/ISaveable.cs) 能力契约 |
| S08 | [QuestSO](D:/my_arpg-master/Assets/Scripts/SO/QuestSO.cs) 与 [QuestManager](D:/my_arpg-master/Assets/Scripts/Gameplay/Quest/QuestManager.cs) 任务配置、状态与提交 |
| S09 | [VoidEventSO](D:/my_arpg-master/Assets/Scripts/SO/Events/VoidEventSO.cs) 同步事件载体 |
| S10 | [InventoryManager](D:/my_arpg-master/Assets/Scripts/Inventory/Items/InventoryManager.cs) 背包、交易、恢复 |
| S11 | [ItemHistoryManager](D:/my_arpg-master/Assets/Scripts/Gameplay/Dialog/HistoryManager/ItemHistoryManager.cs) 数量记录 |
| S12 | [DialogManager](D:/my_arpg-master/Assets/Scripts/Gameplay/Dialog/DialogManager.cs) 对话推进与按钮监听 |
| S13 | [CanvasFocusStack](D:/my_arpg-master/Assets/Scripts/Gameplay/CanvasManagers/CanvasFocusStack.cs) 与 [ICanvasManager](D:/my_arpg-master/Assets/Scripts/Contracts/ICanvasManager.cs) 焦点和真实显示状态 |
| S14 | [DataManager](D:/my_arpg-master/Assets/Scripts/Gameplay/Save/DataManager.cs) 采集、恢复及自动保存 |
| S15 | [TimeManager](D:/my_arpg-master/Assets/Scripts/Gameplay/Player/TimeManager.cs) 暂停状态 |
| S16 | [SaveSystem](D:/my_arpg-master/Assets/Scripts/Gameplay/Save/SaveSystem.cs) 与 [Data](D:/my_arpg-master/Assets/Scripts/Gameplay/Save/Data.cs) 持久化格式与文件读写 |
| S17 | [SceneChanger](D:/my_arpg-master/Assets/Scripts/Scene/SceneChanger.cs) 异步切场景与恢复 |
| S18 | [InitialLoad](D:/my_arpg-master/Assets/Scripts/InitialLoad.cs) 与 [GameSceneSO](D:/my_arpg-master/Assets/Scripts/SO/GameSceneSO.cs) 启动入口和场景描述 |
| S19 | [AStarPathFinder](<D:/my_arpg-master/Assets/Scripts/Gameplay/A Star/AStarPathFinder.cs>) 搜索与通行判断 |
| S20 | [PathFinderDetails](<D:/my_arpg-master/Assets/Scripts/Gameplay/A Star/PathFinderDetails.cs>) 代价与父节点 |
| S21 | [AStarNodeManager](<D:/my_arpg-master/Assets/Scripts/Gameplay/A Star/AStarNodeManager.cs>) 网格数据 |
| S22 | [MovementController](<D:/my_arpg-master/Assets/Scripts/Gameplay/A Star/MovementController.cs>) 路径消费与重试 |

## 面试前最后核对

- 能否在不打开答案的情况下讲完“输入 → 状态 → 动画 → 伤害 → 结束”？
- 能否解释一个自己实际复现过的问题，而不只是重复代码注释？
- 能否区分当前实现、计划改进和已验证结果？
- 能否从存档 ID、请求时序、业务幂等和路径状态各举一个反例？
- 简历上写的优化效果是否有测量条件和数据？没有就描述实现，不填写数字。
- 不确定时能否明确说出需要查看哪个文件、哪项配置和什么运行日志？
