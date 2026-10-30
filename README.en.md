<div align="center">
  <img src="src/LingNote/Assets/app-logo.png" alt="LingNote" width="76" />
  <h1>LingNote</h1>
  <p><a href="README.md">简体中文</a> · <strong>English</strong></p>
  <p><strong>Turn audio into notes you can revisit.</strong></p>
  <p>System audio recording · Local transcription · AI notes</p>
  <p>
    <a href="https://github.com/Bruce-648/LingNote/releases"><img src="assets/badges/version.svg" alt="Version 0.1.34" /></a>
    <img src="https://img.shields.io/badge/platform-Windows_11_x64-0078D4?style=flat-square" alt="Windows 11 x64" />
  </p>
  <p>
    <a href="https://github.com/Bruce-648/LingNote/releases/latest"><strong>Download</strong></a>
    · <a href="#quick-start">Quick start</a>
    · <a href="https://github.com/Bruce-648/LingNote/issues">Report an issue</a>
  </p>
</div>

---

**LingNote** is a lightweight AI recording and note-taking tool for Windows. It captures audio playing on your computer, transcribes it locally, and uses your configured AI endpoint to create notes you can edit, copy, and export.

Use it to capture and organize online courses, meeting audio, lectures, interviews, podcasts, and livestreams.

## Features

- **System audio recording**: Pause, resume, or stop at any time. Browse previous notes while recording continues in the background.
- **Floating controls**: Control recording while using other windows. Smoothly collapse the toolbar into a ghost bubble, then click it to expand.
- **Local transcription**: Uses the multilingual Whisper small model. Once the model is ready, speech recognition works offline.
- **AI notes**: Configure your own API endpoint, model, and API key to generate structured Markdown notes.
- **Note organization**: Compare notes with the original transcript, edit notes, play back recordings, and organize entries by topic and section.
- **Export and updates**: Copy notes, export Markdown, and check for updates within the app.

The little ghost reflects the app's state: it blinks while recording, stays still when paused, and gently floats while processing.

## Quick start

1. Download and install the latest version from **[Releases](https://github.com/Bruce-648/LingNote/releases/latest)**.
2. Open **Settings (设置) → Local speech model (本地语音模型)** to download or import the Whisper small model. The download is around **466 MB** and is only needed once.
3. In **Settings (设置) → AI settings (AI 设置)**, enter your API endpoint, model name, and API key. Save the settings and test the connection.
4. Play the content you want to capture and click **Start recording (开始录音)**. When you stop, LingNote completes transcription and organizes your notes, ready for review, editing, and export.

Supports **Windows 11 x64**. Recording and local transcription do not require an AI API key; AI note generation requires a connection to your configured endpoint.

## Usage notes

- The current version records **system playback audio**. It does not directly capture microphone input or record your screen. Your own microphone audio during an online meeting may therefore be missing from the recording.
- Use the floating controls when the main window is minimized. Stopping a recording automatically brings the main window back.
- Closing the main window hides the app in the system tray. Use the tray menu to reopen it or quit.
- Recordings are kept if processing fails. After checking your model or API settings, click **Continue processing (继续整理)** to retry.

## Data and privacy

Audio, transcripts, and notes are stored locally. Speech recognition runs on your computer; when generating AI notes, transcript text is sent to your configured endpoint. API keys are encrypted using Windows DPAPI for the current user.

Speech recognition and AI-generated notes can contain errors. Check important information against the original recording.

## Development and feedback

For source setup, testing, and packaging, see the **[development guide](docs/DEVELOPMENT.md)** (Chinese). See the **[release notes](assets/RELEASE-NOTES-0.1.34.md)** (Chinese) for version changes.

Bug reports and feature suggestions are welcome in **[Issues](https://github.com/Bruce-648/LingNote/issues)**.

Built with NAudio, Whisper.net, whisper.cpp, Markdig, and .NET. See [THIRD-PARTY-NOTICES](assets/THIRD-PARTY-NOTICES.txt) for third-party components and licenses.
