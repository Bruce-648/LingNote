<div align="center">
  <img src="src/LingNote/Assets/app-logo.png" alt="LingNote" width="76" />
  <h1>LingNote</h1>
  <p><a href="README.md">简体中文</a> · <strong>English</strong></p>
  <p><strong>Turn audio into notes you can revisit.</strong></p>
  <p>System audio recording · Local transcription · AI notes</p>
  <p>
    <img src="assets/badges/version.svg" alt="Version 0.2.0" />
    <img src="https://img.shields.io/badge/platform-Windows_11_x64-0078D4?style=flat-square" alt="Windows 11 x64" />
  </p>
  <p>
    <a href="https://github.com/Bruce-648/LingNote/releases"><strong>Releases</strong></a>
    · <a href="#quick-start">Quick start</a>
    · <a href="assets/RELEASE-NOTES-0.2.0.md">0.2.0 release notes</a>
    · <a href="https://github.com/Bruce-648/LingNote/issues">Report an issue</a>
  </p>
</div>

---

**LingNote** is a lightweight Windows desktop app for recording audio and creating AI notes. Capture audio playing on your computer, transcribe it locally with Whisper, and use your configured AI endpoint to create notes you can edit, copy, and export.

Use it for online courses, lectures, meeting reviews, interviews, podcasts, and livestreams. The current source version is **0.2.0**. The app's interface is primarily in Simplified Chinese; Chinese labels below help you find the corresponding controls.

## Screenshots

![Side-by-side Classroom Notes and Refined Notes](docs/images/notes.png)

<details>
<summary>View recording and settings screens</summary>

**Recording controls**

![Recording screen](docs/images/recording.png)

**Settings**

![Settings screen](docs/images/settings.png)

</details>

Screenshots show the actual 0.2.0 WPF interface with isolated demo data.

## Features

- **System audio recording**: Pause, resume, and stop recording while continuing to browse previous notes.
- **Independent floating controls**: Control recording while other windows are active or the main window is minimized. Keep the controls on top, drag them, or collapse them into a ghost bubble.
- **Local transcription**: Uses the multilingual Whisper small model. Transcription works offline once the model is ready, and the original transcript and timestamps are retained.
- **Classroom Notes and Refined Notes**: Read both side by side, edit and autosave them independently, copy or export either version, regenerate notes, and restore previous versions.
- **Topic organization**: Expand and collapse folder-style topics, and organize recordings into topics, unassigned entries, and custom sections.
- **Configurable AI connection**: Set your endpoint, model, and API key, then test the connection. Advanced settings include note mode, thinking mode, streaming, and text chunk length.
- **Playback, export, and updates**: Play back recording segments, export Markdown, and check for updates within the app.

## Quick start

1. Get a published installer from **[Releases](https://github.com/Bruce-648/LingNote/releases)**. If you already have a local build, use `artifacts/0.2.0/LingNote-0.2.0-Setup.exe` or run `app/LingNote.exe` directly.
2. Open **Settings (设置) → Local speech model (本地语音模型)** to download or import the multilingual Whisper small model, around **466 MB**. The installer does not include the model; it only needs to be prepared once.
3. Under **Settings (设置) → AI connection (AI 连接)**, enter an OpenAI Chat Completions-compatible endpoint, model name, and API key. Click **Save (保存)** and **Test connection (测试连接)**.
4. Play the content you want to capture. Click **New recording (新录音)**, then **Start recording (开始录音)**.
5. Click **Stop and organize (结束并整理)**, wait for transcription and note generation, then review, edit, or export the result.

Supports **Windows 11 x64**. The installer includes the .NET runtime dependencies; normal use does not require the .NET SDK. Recording and local transcription do not need an API key. AI note generation requires a network connection and a configured service, whose fees depend on your provider.

## Two note modes

Choose a mode under **Settings (设置) → Advanced settings (高级设置) → Note mode (笔记模式)**. Changes apply to new recordings; each recording retains its own mode.

| Mode | What happens after recording |
| --- | --- |
| Classroom mode (课堂模式, default) | Generates Classroom Notes first. Generate Refined Notes in the right-hand pane when needed. |
| Refined mode (精编模式) | Generates Refined Notes directly from the transcript and displays them in a single pane, while retaining the audio and original transcript. |

**Classroom Notes (课堂笔记)** organize the recording's content while removing repetition and irrelevant narration. **Refined Notes (精编笔记)** revise structure, detail, and wording around the topic, and may add missing core knowledge. They can also be shorter. Refinement makes additional requests to your configured AI service.

Edit note text directly to autosave changes; the original transcript is read-only. In the two-pane view, resize the panes by dragging the divider and scroll each pane independently. Successful regeneration retains the previous version; failed processing preserves existing content.

## Usage notes

- LingNote captures audio from the **default system playback device**. It does not directly record your microphone or screen. Your own microphone speech in an online meeting may be missing, while audio from other applications may also be captured.
- Use the floating controls when switching windows or minimizing the app. Stopping a recording returns to the main window. Closing the main window hides the app in the system tray; use the tray menu to reopen it or quit.
- Start another recording while an earlier one is being processed. Recordings and processing results are saved separately.
- Audio is retained if processing fails. Check the speech model or AI configuration, then retry using the note pane's continue, regenerate, or refine action.
- Available thinking options depend on the endpoint and model. The setting is disabled for unsupported models.

## Data and privacy

Audio, transcripts, notes, and settings are stored locally. Installed copies use `%LOCALAPPDATA%\LingNote` by default; existing library bindings continue to use their bound directory. Open **Settings (设置) → Advanced settings (高级设置) → Open data folder (打开数据文件夹)** to find the actual location. Keep and back up this folder when upgrading or moving your data.

Speech transcription runs on your computer. AI generation and refinement send the relevant transcripts, notes, and text context to your configured service. API keys are encrypted with **Windows DPAPI** for the current Windows user. Enter the key again when moving to a different computer or Windows user.

Speech recognition and AI output can contain errors. Refinement uses model knowledge; the app does not automatically retrieve textbooks or independently fact-check results. Verify important terms, numbers, definitions, and conclusions against the recording and reliable sources. Markdown supports tables, lists, code, and readable representations of common formulas; full typesetting for complex LaTeX is not currently supported.

## Development and project structure

Built with **C# / WPF / .NET 10**, NAudio, Whisper.net, Microsoft.Data.Sqlite, and Markdig.

```text
LingNote/
├─ src/LingNote/          # App source, icons, and fonts
├─ tests/LingNote.Tests/  # Automated checks
├─ assets/               # Assets, licenses, model metadata, and release notes
├─ docs/                 # Documentation and screenshots
├─ tools/                # Build and installer scripts
├─ app/                  # Local runnable build (not committed)
└─ artifacts/0.2.0/       # Current installer and validation output (not committed)
```

See the **[development guide](docs/DEVELOPMENT.md)** for source setup, dependency restore, testing, and packaging. The **[0.2.0 delivery notes](docs/RELEASE-0.2.0.md)** cover local outputs and validation; the **[release notes](assets/RELEASE-NOTES-0.2.0.md)** describe version changes. These documents are in Chinese.

## Feedback and acknowledgments

Submit bugs and suggestions through **[Issues](https://github.com/Bruce-648/LingNote/issues)**. Include the version, reproduction steps, and screenshots with sensitive information removed. Do not submit API keys or private recordings.

Thanks to NAudio, Whisper.net, whisper.cpp, Markdig, and the .NET ecosystem. See **[THIRD-PARTY-NOTICES](assets/THIRD-PARTY-NOTICES.txt)** for third-party components and licenses.
