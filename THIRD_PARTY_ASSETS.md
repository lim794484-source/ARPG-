# 第三方素材说明

这个公开源码目录有意排除了不能随源码再次分发的原始素材。保留的 `.meta` 文件用于维持 Unity GUID；在本地把合法取得的对应文件放回原路径后，已有场景、动画和预制体引用可以继续使用。

## 未纳入 Git 的内容

| 路径 / 类型 | 原因 |
| --- | --- |
| `Materials/` | 原始素材包工作目录，不是运行时工程必需目录 |
| `Assets/Sprites/**/*.png` | 第三方或来源待确认的原始图像 |
| `Assets/Sprites/**/*.psd` | 第三方源文件 |
| `Assets/Sprites/**/*.jpg` | 第三方或来源待确认的图像 |
| `Assets/Sprites/**/*.ttf` | 字体文件，不能默认按项目 GPL 再分发 |
| `Assets/Sprites/UI/Fonts/*.asset` | 由本地字体生成、可能包含字形纹理的数据 |
| `Assets/Plugins/Newtonsoft.Json.dll` | 已由 `Packages/manifest.json` 中的包依赖替代 |

这些规则已经写入 `.gitignore`，防止本地补回素材后被误提交。

## Tiny Swords

- 作者：Pixel Frog
- 官方页面：[Tiny Swords](https://pixelfrog-assets.itch.io/tiny-swords)
- 使用方式：请从官方页面取得与你拥有许可相符的版本，只在本地工程中使用。
- 公开仓库处理：不提交其 PNG、PSD、Aseprite 或原始素材包。

官方页面的许可文本会更新，请在发布仓库或发行游戏前重新查看最新条款。若你能够证明当前项目使用的是官方另行提供的 CC0 旧版本，可以根据该版本随附的许可证，单独核对并恢复允许公开的文件；不要仅凭文件名推断许可。

## 本地恢复建议

1. 从合法来源下载或取回素材。
2. 按原工程路径恢复到 `Assets/Sprites/` 下对应目录，不要覆盖仓库内已有 `.meta` 文件。
3. 打开编辑器等待重新导入。
4. 检查 `InitialScene`、`PersistentScene`、`Scene1` 和 `Scene2` 中是否仍有 Missing 引用。
5. 通过 `git status --ignored` 确认这些本地素材处于 ignored 状态，再提交其他修改。

如果计划把完整可运行版本放到 GitHub，请先取得允许“随源代码公开再分发素材文件”的明确授权，或把第三方素材替换为自行创作 / 允许再分发的资源。
