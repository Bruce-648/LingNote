# 开发与构建

[返回项目首页](../README.md)

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

Inno Setup 编译器和 Visual C++ 安装器仅在本地打包时使用，不随源码仓库提交。请自行准备 `tools/Inno/ISCC.exe` 与 `assets/vc_redist.x64.exe`。可分别从 [Inno Setup 官网](https://jrsoftware.org/isinfo.php) 与 [微软官方运行库下载地址](https://aka.ms/vs/17/release/vc_redist.x64.exe) 获取。

仓库中的 `tools/build.ps1` 负责发布应用、整理运行依赖并生成安装包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build.ps1
```

构建需使用 `tools/Inno/ISCC.exe` 和 `assets/vc_redist.x64.exe`。发布程序位于 `app/`，安装包和 `.sha256` 校验文件位于 `artifacts/`。安装包不包含语音模型、个人数据库或 API 配置。

GitHub 发布步骤见 **[发布指南](../assets/GITHUB-RELEASE-GUIDE.md)**，版本记录见 **[0.1.33 更新说明](../assets/RELEASE-NOTES-0.1.33.md)**。

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
