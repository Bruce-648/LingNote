<div align="center">
  <img src="src/LingNote/Assets/app-logo.png" alt="LingNote" width="76" />
  <h1>LingNote</h1>
  <p><strong>简体中文</strong> · <a href="README.en.md">English</a></p>
  <p><strong>让声音，成为随时可查的笔记。</strong></p>
  <p>系统录音 · 本地转写 · AI 整理</p>
  <p>
    <a href="https://github.com/Bruce-648/LingNote/releases"><img src="assets/badges/version.svg" alt="版本 0.1.34" /></a>
    <img src="https://img.shields.io/badge/platform-Windows_11_x64-0078D4?style=flat-square" alt="Windows 11 x64" />
  </p>
  <p>
    <a href="https://github.com/Bruce-648/LingNote/releases/latest"><strong>下载最新版</strong></a>
    · <a href="#快速开始">快速开始</a>
    · <a href="https://github.com/Bruce-648/LingNote/issues">反馈问题</a>
  </p>
</div>

---

**LingNote** 是一款轻量的 Windows AI 录音与笔记工具。它记录电脑中播放的声音，在本地完成语音转写，再通过你配置的 AI 接口整理成可以编辑、复制和导出的笔记。

适用于线上课程、会议回顾、讲座、访谈、播客与直播等内容的记录与整理。

## 功能特性

- **系统录音**：随时暂停、继续和结束，后台录音时也能查看以前的记录。
- **悬浮控制**：切换窗口后通过悬浮条控制录音，可平滑收起为幽灵悬浮球，单击展开。
- **本地转写**：使用 Whisper small 多语言模型，模型就绪后可离线识别。
- **AI 整理**：支持自定义接口地址、模型和 API Key，生成结构清晰的 Markdown 笔记。
- **笔记管理**：对照原始转写、编辑笔记、回放录音，按主题和分区整理记录。
- **导出与更新**：支持复制、Markdown 导出，以及应用内检查更新。

小幽灵会跟随工作状态变化：录音时眨眼，暂停时静止，整理时轻轻飘动。

## 快速开始

1. 从 **[Releases](https://github.com/Bruce-648/LingNote/releases/latest)** 下载并安装最新版。
2. 在 **设置 → 本地语音模型** 中下载或导入 Whisper small 模型，约 **466 MB**，只需准备一次。
3. 在 **设置 → AI 设置** 中填写接口地址、模型名称和 API Key，保存并测试连接。
4. 播放需要记录的内容，点击「开始录音」。结束后自动完成转写与笔记整理，即可查看、编辑和导出。

支持 **Windows 11 x64**。录音与本地转写不需要 AI API Key；自动整理笔记需要连接你配置的 AI 接口。

## 使用说明

- 当前版本录制**系统播放声音**，不直接采集麦克风，也不录制屏幕。线上会议中自己的麦克风发言不一定会被包含。
- 最小化后可以通过悬浮条继续控制录音，点击结束会自动返回主窗口。
- 关闭主窗口会隐藏到系统托盘，可从托盘菜单重新打开或退出。
- 处理失败时录音仍会保留，检查模型或接口配置后可点击「继续整理」。

## 数据与隐私

音频、转写和笔记保存在本地。语音转写由本机模型完成；生成 AI 笔记时，转写文字会发送给你配置的接口。API Key 使用 Windows 当前用户的 DPAPI 加密保存。

语音识别和 AI 整理可能出现错误，重要信息请结合原始录音核对。

## 开发与反馈

源码运行、测试与打包步骤见 **[开发文档](docs/DEVELOPMENT.md)**。版本变化见 **[更新说明](assets/RELEASE-NOTES-0.1.34.md)**。

遇到问题或有功能建议，欢迎提交 **[Issue](https://github.com/Bruce-648/LingNote/issues)**。

感谢 NAudio、Whisper.net、whisper.cpp、Markdig 及 .NET 生态提供的基础能力。第三方组件与许可见 [THIRD-PARTY-NOTICES](assets/THIRD-PARTY-NOTICES.txt)。
