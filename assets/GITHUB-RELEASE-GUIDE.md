# GitHub 发布流程

发布仓库：https://github.com/Bruce-648/LingNote
仓库公开，只发布安装包、校验文件和更新说明。不要上传本地项目目录或个人数据。

## 首次发布 0.1.33
1. 创建公开仓库 Bruce-648/LingNote，并勾选 Add a README file。
2. 打开 https://github.com/Bruce-648/LingNote/releases/new 。
3. 新建标签 `v0.1.33`，发布标题填写 `LingNote 0.1.33`。
4. 将本目录 RELEASE-NOTES-0.1.33.md 的内容复制到更新说明。
5. 上传这两个文件：
   - artifacts/LingNote-0.1.33-Setup.exe
   - artifacts/LingNote-0.1.33-Setup.exe.sha256
6. 不勾选预发布，确认附件上传完成后 Publish release，并将本版设为最新版本。
7. 安装本版后，在「设置 → 应用更新」点击检查更新，应显示「当前已是最新版本」。

## 后续发布
- 提升项目和安装脚本的版本号，运行 tools/build.ps1。
- 每个正式 Release 的标签应为 v加版本号，例如 v0.1.34。
- 安装包必须命名为 LingNote-版本号-Setup.exe，对应校验附件为相同文件名加 .sha256。
- 上传完整附件后再公开 Release；开发中的版本可以先保存为草稿。
- 在说明里写清本次变化，使用者将在程序中看到这些说明。

更新功能只读取该公开仓库，不使用 GitHub 登录凭据。安装包不包含录音数据库、API 密钥或语音模型。
