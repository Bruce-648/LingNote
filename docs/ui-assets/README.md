# 设置图标生成记录

当前使用的图标文件位于 `src/LingNote/Assets/Settings/`。

- `imagegen-prompts.json`：初始12个透明底图标的生成提示词。
- `shortcut-icon-prompt.json`：当前快捷键单键帽图标的提示词。
- `icon-proportions-prompts.json`：当前麦克风、更新、存储三个重生成图标的提示词。

均使用内置image_gen工具。运行时按原始比例显示，并统一线宽、灰度和光学范围；原始PNG保留透明通道。
