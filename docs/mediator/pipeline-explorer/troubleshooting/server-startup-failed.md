---
layout: default
title: "Server startup failed - Pipeline Explorer"
description: "The bundled Pipeline Explorer server failed to start. Causes and resolutions."
---
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudioBgWhite.svg">
    <source media="(prefers-color-scheme: light)" srcset="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg">
    <img alt="DSoftStudio Mediator" src="https://raw.githubusercontent.com/DSoftStudio/Mediator/main/assets/images/DSoftStudio.svg" height="120">
  </picture>
</p>

[← Back to Troubleshooting](index.md)

# Server startup failed

## Symptom

The Pipeline Explorer view shows an error, or stays on *Starting server…* and never loads. Pipeline Explorer runs a small bundled background server for handler discovery and profiling; when it can't start, the tree and profiler stay unavailable.

Work through the causes below in order.

---

## 1. Antivirus or endpoint protection quarantined the server

Security products sometimes quarantine freshly-installed binaries before they are widely trusted. If the bundled server file is removed or blocked from running, the extension can't launch it.

**Check**

Open the extension's installation folder and confirm the server files are present:

- **VS Code** — the extension's folder under your user `.vscode/extensions` directory.
- **Visual Studio** — the extension's folder under your Visual Studio installation's extensions directory.

If the server files are missing right after a successful install, quarantine is the likely cause.

**Fix**

- Restore the file from your security product's quarantine.
- Add the extension's installation folder to its allow-list so future updates aren't quarantined.
- Reload the IDE.

---

## 2. The server can't run on your platform

The bundled server is a native binary matched to your operating system and CPU architecture. If it won't start and you're on an uncommon platform, capture the output (see [Capturing logs](#capturing-logs) below) so we can help.

---

## 3. A leftover server process or stale lock

Pipeline Explorer keeps a single background server per machine and tracks it with a lock. If a previous session left a server running, or the lock points at a process that is no longer alive, the new session can fail to connect.

**Fix**

- **VS Code** — run **Mediator: Stop Server** from the Command Palette to stop any lingering server, then reload the window. It respawns automatically on the next refresh.
- **Visual Studio** — close and reopen the tool window.
- If it still won't start, restart the machine to clear any stale state, then reopen your solution.

---

## 4. Very old Linux system libraries

On Linux, the server needs reasonably current system libraries. Very old distributions may not have them, and the server exits immediately on launch.

**Fix**

- Use a current LTS release of your distribution.
- Or run the extension inside a devcontainer / WSL2 image based on a current Linux.

On Windows and macOS this is rarely an issue.

---

## Capturing logs

If you want to look closer or report the problem, capture the full output:

- **VS Code** — open the **Output** panel (`Ctrl+Shift+U`) and select **Mediator** from the dropdown.
- **Visual Studio** — **View → Output**, then select **Mediator** in the dropdown.

Reload the IDE so the log captures startup from the beginning, reproduce the failure, then copy the **Mediator** output. You can [open an issue](https://github.com/DSoftStudio/Mediator.Enterprise/issues) with that output plus your IDE and extension version — no project source is needed.

---

[← Back to Troubleshooting](index.md)
