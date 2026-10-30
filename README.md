<div align="center">
  <img src="src/LingNote/Assets/app-logo.png" alt="LingNote 幽灵 Logo" width="76" />
  <h1>LingNote</h1>
  <p><strong>让声音，成为随时可查的笔记。</strong></p>
  <p>系统录音 · 本地转写 · AI 整理</p>
  <p>
    <a href="https://github.com/Bruce-648/LingNote/releases"><img src="assets/badges/version.svg" alt="版本 0.1.33" /></a>
    <img src="https://img.shields.io/badge/platform-Windows_11_x64-0078D4?style=flat-square" alt="Windows 11 x64" />
    <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square" alt=".NET 10" />
    <img src="assets/badges/local-whisper.svg" alt="Whisper 本地转写" />
  </p>
  <p>
    <a href="https://github.com/Bruce-648/LingNote/releases/latest"><strong>下载最新版</strong></a>
    · <a href="#快速开始">快速开始</a>
    · <a href="#功能特性">功能特性</a>
    · <a href="#开发与构建">开发与构建</a>
    · <a href="https://github.com/Bruce-648/LingNote/issues">反馈问题</a>
  </p>
</div>

---

**LingNote** 是一款轻量的 Windows AI 录音与笔记工具。它将电脑中播放的声音保存下来，通过本地 Whisper 模型转写为文字，再调用你配置的 AI 接口，整理成可以编辑、复制和导出的 Markdown 笔记。

无论是线上课程、会议回顾、讲座听记，还是访谈与直播内容整理，都可以在专心听的同时，让 LingNote 帮你留下记录。

## 功能特性

| 功能 | 你可以做什么 |
| --- | --- |
| **系统声音录制** | 录制电脑当前默认播放设备的声音，随时暂停、继续或结束。 |
| **悬浮录音控制** | 切换窗口或最小化后，通过悬浮条控制录音；也可以平滑收起成小幽灵悬浮球，单击重新展开。 |
| **本地语音转写** | 使用 Whisper small 多语言模型，在录音期间后台分段转写，结束后补全剩余内容。模型就绪后可离线识别。 |
| **自定义 AI 整理** | 填写自己的接口地址、模型名称和 API Key，使用兼容 Chat Completions 的接口生成笔记，支持流式输出。 |
| **笔记与转写对照** | 在笔记、原始转写之间切换，编辑笔记、复制内容或导出为 `.md`；录音期间也能查看其他记录。 |
| **主题与分区管理** | 按主题和自定义分区整理记录，移动录音或整个主题，展开或收起较长的列表。 |
| **录音回放与版本恢复** | 回放保存的录音片段；已有历史版本时，可恢复上一版笔记。 |
| **应用更新** | 在设置中检查 GitHub 新版本，查看更新说明、下载安装包，校验后安装并重启。 |

小幽灵也会跟随工作状态变化：录音时眨眼，暂停时静止；整理内容时轻轻飘动，配合逐点出现的加载提示。

## 适用场景

- **课程与讲座**：边听边记录，课后整理知识点，回看原始转写。
- **会议回顾**：把电脑中播放的会议内容整理成便于查阅的记录。
- **访谈与播客**：保存重要表达，通过文字回顾长音频内容。
- **直播与视频**：记录分享内容，将零散信息整理为个人笔记。

> 当前版本采集的是**系统播放声音**，不直接录制麦克风，也不录制屏幕画面。录制线上会议时，自己的麦克风发言不一定会被包含。

## 快速开始

### 1. 安装应用

前往 **[Releases](https://github.com/Bruce-648/LingNote/releases/latest)**，下载 `LingNote-版本号-Setup.exe` 并安装。

- 支持 **Windows 11 x64**。
- 安装包包含应用所需的 .NET 运行环境，普通使用者无需安装 .NET SDK。
- 安装包不包含语音模型，首次使用时再下载或导入。

### 2. 准备语音模型

打开 **设置 → 本地语音模型**，点击「下载模型」，等待下载与校验完成。也可以通过「导入模型」选择已下载的官方 Whisper small 多语言 GGML 模型。

模型文件约 **466 MB**，只需准备一次。下载完成后，本地语音转写不依赖网络；取消或下载失败时可以重试。

模型来源与 SHA-256 校验信息见 [model-manifest.json](assets/model-manifest.json)。

### 3. 配置 AI 接口

在 **设置 → AI 设置** 中填写：

| 配置项 | 填写内容 |
| --- | --- |
| 接口地址 | 服务商提供的兼容接口地址，例如 `https://your-provider.example/v1`。 |
| 模型 | 该服务实际支持的模型名称。 |
| API Key | 你自己的服务密钥。 |

点击「保存」，再用「测试连接」确认配置可用。可在高级设置中调整流式生成与每段整理字数。

**录音和本地转写不需要 AI API Key。** 自动整理笔记需要连接已配置的 AI 接口。

### 4. 开始记录

1. 播放需要记录的内容，点击「新录音 → 开始录音」。
2. 切换到其他窗口，或最小化 LingNote，通过悬浮条暂停、继续和结束录音。
3. 点击「结束」，主窗口自动弹出，完成转写并整理笔记。
4. 查看笔记与原始转写，按需编辑、复制或导出 Markdown。

若处理失败，已保存的录音仍保留，可以检查模型或 API 配置后点击「继续整理」。

## 数据与隐私

| 内容 | 处理方式 |
| --- | --- |
| **原始音频** | 保存于本地，本地转写流程不会将音频上传到 AI 接口。 |
| **语音转写** | 由本机 Whisper 模型生成；进行 AI 整理时，转写文字会发送给你配置的接口。 |
| **笔记与录音记录** | 使用本地 SQLite 数据库保存。 |
| **API Key** | 使用当前 Windows 用户的 DPAPI 加密保存，不随源码提交。 |

安装版默认使用 `%LOCALAPPDATA%\LingNote` 保存用户数据；如果应用目录存在 `library-location.json`，则按其中绑定的目录读取原有录音库。

备份时请保留完整数据目录；迁移使用目录绑定的版本时，也请保留 `library-location.json`。语音识别和 AI 整理可能出现错误，重要信息请结合录音核对。

## 常见问题

<details>
<summary><strong>录音时可以查看以前的笔记吗？</strong></summary>

可以。录音与当前查看的记录相互独立，切换其他笔记或转写页面不会暂停录音。点击结束后，会回到本次录音进行整理。

</details>

<details>
<summary><strong>没有声音或没有识别到讲话怎么办？</strong></summary>

请确认内容正在通过系统默认播放设备输出，并检查录音片段中是否有实际讲话。系统播放设备的其他声音也可能一起被录入。当前版本不直接采集麦克风。

</details>

<details>
<summary><strong>可以完全离线使用吗？</strong></summary>

已准备好模型后，可以离线录音和语音转写。AI 整理需要连接你配置的接口；下载语音模型、检查和下载 GitHub 更新需要联网。

</details>

<details>
<summary><strong>点击窗口关闭按钮后，为什么程序还在运行？</strong></summary>

关闭主窗口会隐藏到系统托盘。可点击托盘图标重新打开，也可通过托盘菜单退出。录音期间隐藏窗口不会结束录音。

</details>

<details>
<summary><strong>如何更新应用？</strong></summary>

打开「设置 → 应用更新」检查新版本。下载完成并通过校验后，点击「安装并重启」。应用也提供启动时自动检查更新的开关。

请在录音与整理任务结束后安装更新。更新保留原有录音、笔记、API 配置和已下载的模型。

</details>

<details>
<summary><strong>为什么源码仓库里没有语音模型和安装包？</strong></summary>

语音模型在安装后通过设置下载或导入，不随 Git 提交。安装包及校验文件通过 GitHub Releases 分发；下载源码压缩包不能替代下载安装包。

</details>

## 开发与构建

### 技术栈

| 部分 | 实现 |
| --- | --- |
| 桌面界面 | C#、WPF、.NET 10 |
| 音频采集 | NAudio / WASAPI Loopback |
| 本地转写 | Whisper.net / whisper.cpp |
| 数据保存 | Microsoft.Data.Sqlite |
| Markdown 显示 | Markdig |
| Windows 安装包 | Inno Setup |

### 从源码运行

安装 **.NET 10 SDK**，在 Windows 上执行：

```powershell
git clone https://github.com/Bruce-648/LingNote.git
cd LingNote

dotnet restore src/LingNote -r win-x64 --configfile NuGet.Config
dotnet run --project src/LingNote -r win-x64 --no-restore
```

首次运行后，在设置中下载或导入模型。开发运行与安装版一样，需要自行配置 AI 接口。

### 运行检查

```powershell
dotnet restore tests/LingNote.Tests -r win-x64 --configfile NuGet.Config
dotnet run --project tests/LingNote.Tests -r win-x64 --no-restore
dotnet run --project tests/LingNote.Tests -r win-x64 --no-restore -- --ui-check
```

检查使用隔离数据目录。可选的 `--audio --asr` 检查还需要实际播放设备，以及本地的模型和测试音频文件。

### 构建安装包

仓库中的 `tools/build.ps1` 负责发布应用、整理运行依赖并生成安装包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build.ps1
```

构建需使用 `tools/Inno/ISCC.exe` 和 `assets/vc_redist.x64.exe`。发布程序位于 `app/`，安装包和 `.sha256` 校验文件位于 `artifacts/`。安装包不包含语音模型、个人数据库或 API 配置。

GitHub 发布步骤见 **[发布指南](assets/GITHUB-RELEASE-GUIDE.md)**，版本记录见 **[0.1.33 更新说明](assets/RELEASE-NOTES-0.1.33.md)**。

### 目录结构

```text
LingNote/
├─ src/LingNote/             # 应用源码、图标与字体
├─ tests/LingNote.Tests/     # 自动化检查
├─ assets/                  # 品牌素材、许可与模型校验信息
├─ tools/                   # 构建与安装脚本
├─ app/                     # 本地发布输出，不提交
├─ artifacts/               # 安装包输出，不提交
└─ README.md
```

## 反馈与致谢

遇到问题或有功能建议，欢迎提交 **[Issue](https://github.com/Bruce-648/LingNote/issues)**。描述时请附上应用版本、复现步骤和必要的截图，隐去 API Key 等私人信息。

感谢 [NAudio](https://github.com/naudio/NAudio)、[Whisper.net](https://github.com/sandrohanea/whisper.net)、[whisper.cpp](https://github.com/ggml-org/whisper.cpp)、[Markdig](https://github.com/xoofx/markdig) 及 .NET 生态提供的基础能力。第三方组件与许可信息见 [THIRD-PARTY-NOTICES](assets/THIRD-PARTY-NOTICES.txt)。

---

<div align="center">
  <sub>听见，记录，再回顾。</sub>
</div>
