using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

/// <summary>
/// 场景切换管理器
/// 负责管理场景的加载、卸载和过渡动画
/// 使用单例模式，通过事件响应场景切换请求
/// </summary>
public class SceneChanger : YSingleton<SceneChanger>
{

    /// <summary>玩家初始位置</summary>
    [SerializeField] private Vector3 initialPosition = Vector3.zero;

    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private GameSceneSO initScene;
    [SerializeField] private GameObject player;
    [SerializeField] private CanvasGroup fadeCanva;

    /// <summary>过渡动画播放器数组</summary>
    ///     
    [Header("Events")] [SerializeField] private SceneLoadEventSO loadEventSO;

    [SerializeField] private VoidEventSO sceneLoadedEvent;
    [SerializeField] private Animator[] transitionImagesDuringFade;
    [SerializeField] private Object[] objectsToUnableWhileMenuOrReset;
    private GameSceneSO sceneToLoad;

    private GameSceneSO currentScene;

    /// <summary>已加载的场景对象</summary>
    private Scene loadedScene;

    /// <summary>玩家新位置</summary>
    private Vector3 newPosition;

    /// <summary>是否需要淡入淡出</summary>
    private bool isToFade;

    private bool isInitialScene = true;

    private Coroutine _fadeCoroutine;

    /// <summary>
    /// 获取当前活动场景
    /// </summary>
    /// <returns>当前场景对象</returns>
    public Scene GetCurrentScene()
    {
        return loadedScene != null ? loadedScene : SceneManager.GetActiveScene();
    }

    // 供存档系统在切场前读取当前场景 SO。
    public GameSceneSO GetCurrentGameScene()
    {
        return currentScene;
    }

    /// <summary>
    /// 唤醒时初始化单例并加载首个场景
    /// </summary>
    protected override void OnSingletonInitialized()
    {
        EnsureCircleMaskSprite();
        sceneToLoad = initScene;
        SetPlayerPostion(initialPosition);
        LoadScene(sceneToLoad);
    }

    /// <summary>
    /// 启用时订阅场景加载事件
    /// </summary>
    private void OnEnable()
    {
        loadEventSO.LoadRequestEvent += OnLoadRequestEvent;
    }

    /// <summary>
    /// 禁用时取消订阅场景加载事件
    /// </summary>
    private void OnDisable()
    {
        loadEventSO.LoadRequestEvent -= OnLoadRequestEvent;
    }

    /// <summary>
    /// 播放过渡动画
    /// 直接控制 FadeCanvas 的 CanvasGroup.alpha，让背景白屏和中间头像作为整体同步淡入淡出。
    /// </summary>
    /// <param name="name">动画状态名称（FadeIn/FadeOut）</param>
    private void PlayLoadingAnimation(string name)
    {
        bool fadeIn = name == "FadeIn";

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeCanvasGroup(fadeIn ? 0f : 1f, fadeIn ? 1f : 0f, fadeDuration));

        foreach (Animator transitionImage in transitionImagesDuringFade)
        {
            if (transitionImage != null)
            {
                transitionImage.Play(name);
            }
        }
    }

    /// <summary>
    /// 线性插值 CanvasGroup 的 alpha
    /// </summary>
    private IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        if (fadeCanva == null) yield break;

        fadeCanva.alpha = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fadeCanva.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        fadeCanva.alpha = to;
    }

    /// <summary>
    /// 设置玩家位置
    /// </summary>
    /// <param name="newPosition">新位置坐标</param>
    private void SetPlayerPostion(Vector3 newPosition)
    {
        player.GetComponent<Transform>().position = newPosition;
    }

    /// <summary>
    /// 场景加载请求事件回调
    /// </summary>
    /// <param name="scene">目标场景</param>
    /// <param name="newPosition">玩家新位置</param>
    /// <param name="isToFade">是否显示过渡动画</param>
    private void OnLoadRequestEvent(GameSceneSO scene, Vector3 newPosition, bool isToFade)
    {
        ForbidInput();
        TimeManager.Instance.PauseGame();
        sceneToLoad = scene;

        StatsManager.Instance.Respawn(); //回血


        this.newPosition = newPosition == Vector3.zero ? sceneToLoad.initialPosition : newPosition;
        //如果传入位置为零向量，则使用场景预设的初始位置
        this.isToFade = isToFade;
        if (isToFade && !isInitialScene)
        {
            PlayLoadingAnimation("FadeIn");
        }

        StartCoroutine(UnloadCurrentScene(sceneToLoad)); //卸载当前场景
    }

    /// <summary>
    /// 卸载当前场景协程
    /// 等待淡入动画完成后卸载旧场景，然后加载新场景
    /// </summary>
    /// <param name="sceneToLoad">要加载的目标场景</param>
    private IEnumerator UnloadCurrentScene(GameSceneSO sceneToLoad)
    {
        yield return new WaitForSecondsRealtime(fadeDuration);

        if (currentScene != null)
            yield return currentScene.sceneReference.UnLoadScene();
        LoadScene(sceneToLoad);
        SetPlayerPostion(newPosition);
    }

    /// <summary>
    /// 异步加载场景
    /// 使用 Addressables 加载场景，以 additive 模式添加
    /// </summary>
    /// <param name="sceneToLoad">要加载的场景</param>
    private void LoadScene(GameSceneSO sceneToLoad)
    {
        if (sceneToLoad.sceneType == MyEnums.SceneType.Menu)
        {
            SetObjects(false);
        }

        else if (sceneToLoad.sceneType == MyEnums.SceneType.Location)
        {
            SetObjects(true);
        }

        if (sceneToLoad != null)
        {
            var loadingOption = sceneToLoad.sceneReference.LoadSceneAsync(LoadSceneMode.Additive);
            loadingOption.Completed += OnLoadCompleted;
        }
    }

    private void SetObjects(bool state)
    {
        foreach (Object obj in objectsToUnableWhileMenuOrReset)
        {
            if (obj is GameObject go)
            {
                go.SetActive(state);
            }
        }
    }

    /// <summary>
    /// 场景加载完成回调
    /// 更新当前场景引用，播放淡出动画
    /// </summary>
    /// <param name="handle">异步操作句柄</param>
    private void OnLoadCompleted(AsyncOperationHandle<SceneInstance> handle)
    {
        currentScene = sceneToLoad;
        loadedScene = handle.Result.Scene;
        if (isToFade && !isInitialScene)
        {
            PlayLoadingAnimation("FadeOut");
        }

        isInitialScene = false;
        sceneLoadedEvent?.OnEventRaised();
        AllowInput();
        TimeManager.Instance.ForceResumeGame();
    }

    private void ForbidInput()
    {
        if (player == null) return;
        var movement = player.GetComponentInChildren<PlayerMovement>(true);
        if (movement != null) movement.enabled = false;
    }

    private void AllowInput()
    {
        if (player == null) return;
        var movement = player.GetComponentInChildren<PlayerMovement>(true);
        if (movement != null) movement.enabled = true;
    }

    /// <summary>
    /// 把 FadeCanvas 下的 CircleMask 的 Image 设为 Filled Radial 360，形成圆形遮罩。
    /// 不生成任何运行时纹理，使用 Unity 内置白色 Sprite，保持像素清晰。
    /// </summary>
    private void EnsureCircleMaskSprite()
    {
        if (fadeCanva == null) return;

        var circleMaskGO = fadeCanva.transform.Find("CircleMask");
        if (circleMaskGO == null) return;

        var img = circleMaskGO.GetComponent<Image>();
        if (img == null) return;

        // 用内置白色 Sprite + Filled Radial 360 实现圆形
        if (img.sprite == null)
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillAmount = 1f;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.color = Color.white;

        // 确保 Mask 存在
        if (circleMaskGO.GetComponent<Mask>() == null)
            circleMaskGO.gameObject.AddComponent<Mask>();
    }
}
