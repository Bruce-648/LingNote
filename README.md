# LingNote

当前版本：**0.1.33**。Windows 11 x64 网课录音、实时转写与 AI 学习笔记工具。

## 打开程序

双击根目录的 **LingNote.lnk**，或运行 **app\LingNote.exe**。以后继续使用这个固定入口。

## 项目目录

| 目录或文件 | 内容 |
| --- | --- |
| app/ | 最新版程序与运行库（语音模型在安装后下载） |
| Data/ | 当前录音数据库、音频、笔记、加密 API 设置与数据备份 |
| library-location.json | 当前数据目录及录音库身份标识 |
| src/LingNote/ | 当前 C# / WPF 源码 |
| tests/LingNote.Tests/ | 自动化检查，使用隔离测试数据 |
| assets/ | 当前品牌源文件、模型、测试音频与许可 |
| tools/ | 当前构建、安装包和素材处理工具 |
| .packages/ | 离线构建使用的 NuGet 依赖缓存 |
| artifacts/ | 最新安装包输出目录 |

**更新只替换 app 中的程序文件。请保留 Data 和 library-location.json。**
本机数据固定在 E:\LingNote\Data，旧数据的恢复备份也保留在 Data 的备份目录内。

## 使用

1. 在「设置」填写接口地址、模型名称和 API Key，测试连接后保存。
2. 首次使用在设置顶部点击「下载模型」（约 466 MB）；下载完成后可以离线转写。
3. 播放网课，点击「开始录音」。切换窗口或最小化后，可用悬浮条暂停、继续、结束或返回。
4. 结束后，本地 Whisper small 模型完成转写，再调用配置的兼容 Chat Completions 接口整理笔记。
5. 笔记可以复制、编辑和导出 Markdown；原始转写可单独查看，录音片段可以回放。

没有 API Key 时仍可先录音和本地转写，之后配置接口再整理。系统播放设备的其他声音也会被录入，不采集麦克风或屏幕画面。转写和笔记可能有识别错误，原音频保留供核对。音频留在本地，整理时只发送转写文字；API Key 用当前 Windows 用户的 DPAPI 加密。

## 记录与分区

- 「主题」「未分区」及自定义分区同级显示，可以折叠展开。
- 「未分区」右侧菜单可切换时间排列、按天分组或新建分区。
- 录音可以移至主题或分区；整个主题也可以移至分区。
- 分区支持编辑名称、全部标为已读、删除录音和移除分区。
- 「删除录音」确认后删除分区内的独立录音和主题内录音，保留分区与主题。
- 「移除分区」保留录音和笔记，将主题移回「主题」、独立录音移回「未分区」。

## 开发与构建

需要 .NET 10 SDK；依赖 NAudio、Whisper.net、Microsoft.Data.Sqlite 和 Markdig。

```powershell
dotnet restore src/LingNote --configfile NuGet.Config
dotnet build src/LingNote --no-restore
dotnet run --project tests/LingNote.Tests --no-restore
```

可选的真实音频与本地模型检查：

```powershell
dotnet run --project tests/LingNote.Tests --no-restore -- --audio --asr
```

完整发布及安装包构建：

```powershell
powershell -ExecutionPolicy Bypass -File tools/build.ps1 -SkipRestore
```

程序发布到固定的 app/，安装包输出到 artifacts/。分发安装包不包含本机的数据目录绑定或启动诊断文件，在目标 Windows 用户的数据目录保存数据。
模型来源和校验值见 assets/model-manifest.json，许可见 assets/THIRD-PARTY-NOTICES.txt 和 app/licenses/。

## 应用更新

0.1.33 起可在「设置 → 应用更新」检查 GitHub 新版本，支持下载进度、取消、完整性校验和安装重启。发布流程见 assets/GITHUB-RELEASE-GUIDE.md。
