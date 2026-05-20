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

The Pipeline Explorer view shows an error banner or a perpetual *Starting server…* state. The Mediator output panel contains lines like:

```
Server failed to start: ENOENT — DSoftStudio.Mediator.Server not found
Server exited with code 0xC0000135
Server failed to bind to port — address already in use
Server killed by host process (signal SIGABRT)
```

Pipeline Explorer ships a bundled .NET server that runs in the background for handler discovery and profiling event transport. If the server can't start, the entire feature surface is unavailable.

---

## 1. Antivirus or endpoint protection quarantined the binary

Corporate AV products (Defender for Endpoint, CrowdStrike, SentinelOne, Symantec) sometimes flag freshly-installed binaries as suspicious because they are not yet trusted by the reputation service. The binary is quarantined, removed, or refused execution.

**Symptom signature**

```
Server failed to start: ENOENT — DSoftStudio.Mediator.Server not found
```

…even though you just installed the extension.

**Check**

Browse to the extension's server directory and confirm the executable is present:

- **VS Code** — `<extension-install-path>/server/<rid>/DSoftStudio.Mediator.Server[.exe]`, where `<rid>` is `win-x64`, `linux-x64`, `osx-x64`, or `osx-arm64` matching your host. Typical extension paths:
  - Windows: `%USERPROFILE%\.vscode\extensions\dsoftstudio.dsoftstudio-mediator-<version>\`
  - macOS / Linux: `~/.vscode/extensions/dsoftstudio.dsoftstudio-mediator-<version>/`
- **Visual Studio** — `%LocalAppData%\Microsoft\VisualStudio\<instance>\Extensions\DSoftStudio\DSoftStudio Mediator Pipeline Explorer\<version>\server\DSoftStudio.Mediator.Server.exe`. The VSIX only ships Windows x64 binaries; there is no `<rid>` subdirectory.

If the executable is missing, AV is the most likely culprit.

> **Note**: the server also writes runtime state (lock file, IPC handles) to `%APPDATA%\DSoftStudio\Mediator\` on Windows, `~/.config/DSoftStudio/Mediator/` on Linux, and `~/Library/Application Support/DSoftStudio/Mediator/` on macOS. AV products rarely interfere with this state directory — they target executables — so the install directory above is the right place to look for quarantined binaries.

**Fix**

- Restore the binary from quarantine (your AV's console).
- Whitelist the extension install path so future updates aren't quarantined.
- Pipeline Explorer binaries are Authenticode-signed with the DSoftStudio code-signing certificate (thumbprint `BDE0B2F05B205A785A365BCDD918EB554CD6EDC4`). Some AVs accept the publisher as a whitelist key.

After restoring, reload the IDE.

---

## 2. The bundled binary is missing for your platform

The extension ships pre-built binaries for `win-x64`, `linux-x64`, `osx-x64`, and `osx-arm64`. If your platform doesn't match — for example, `linux-arm64` or `win-arm64` — the bundled binary won't run.

**Check**

```shell
# Detect your platform
uname -m   # macOS / Linux
echo $env:PROCESSOR_ARCHITECTURE   # Windows PowerShell
```

If the result is anything other than `x86_64` (= x64) or `arm64` on macOS, the bundled binary doesn't cover your architecture.

**Fix**

Pipeline Explorer 0.1.0 does not yet bundle Linux ARM64 or Windows ARM64. Two workarounds:

- **Run x64 via emulation** — Windows ARM64 transparently runs x64; macOS ARM64 transparently runs x64 via Rosetta 2.
- **Point at a self-published server** — build the server for your architecture and set the `mediator.serverPath` setting:

  ```jsonc
  {
    "mediator.serverPath": "/absolute/path/to/DSoftStudio.Mediator.Server"
  }
  ```

  Future releases will bundle additional architectures. Track [the architectures issue](https://github.com/DSoftStudio/Mediator.Enterprise/issues) for updates.

---

## 3. Missing native runtime dependency

The server is a self-contained .NET 8 publish, but on Linux it still depends on a recent glibc and `libstdc++`. On very old distributions (CentOS 7, Ubuntu 18.04, Amazon Linux 2) the system libraries are too old.

**Symptom signature**

```
Server exited with code 134 (SIGABRT)
Server failed: GLIBC_2.34 not found
```

**Check**

```shell
ldd --version          # glibc version on Linux
```

The server requires `glibc >= 2.31` and `libstdc++ >= 6.0.28`.

**Fix**

- Upgrade the host OS to a newer LTS (Ubuntu 22.04+, RHEL 9+, Debian 12+).
- Or, for short-term workarounds, use the Pipeline Explorer extension inside a devcontainer / WSL2 instance with a newer Linux.

On Windows and macOS this is rarely an issue — both ship modern enough runtimes by default.

---

## 4. Port collision

The server picks an ephemeral local port at startup. On hardened systems (corporate-managed laptops with strict outbound firewalls) the OS or a security product may block the bind.

**Symptom signature**

```
Server failed to bind to port — address already in use
Server failed: Permission denied (port allocation)
```

**Check**

```shell
# Windows
netstat -ano | findstr LISTEN | findstr 127.0.0.1

# macOS / Linux
lsof -iTCP -sTCP:LISTEN -n -P | grep 127.0.0.1
```

If the port range used by the server is fully occupied, this is the cause.

**Fix**

- Close other Pipeline Explorer instances or other IDEs that might have lingering server processes.
- Run `Mediator: Stop Server` from the Command Palette (VS Code) to force-kill any zombie server, then reload the IDE.
- If the issue persists, restart the host — this clears all stale port allocations.

---

## 5. Permission denied on the temp directory

The server uses the system temp directory (`%TEMP%` / `/tmp`) for ephemeral state. If the temp directory is not writable by the IDE process — common in some sandboxed CI / remote-development setups — startup fails.

**Symptom signature**

```
Server failed: cannot create directory '<temp>/dsoftstudio-mediator': Permission denied
```

**Check**

```shell
# Windows
echo $env:TEMP
icacls $env:TEMP

# macOS / Linux
echo $TMPDIR
ls -ld /tmp
```

The IDE's effective user needs read + write + execute on the temp directory.

**Fix**

- Set `TMPDIR` (macOS / Linux) or `TEMP` (Windows) to a writable location before launching the IDE.
- If you are running inside a container, ensure the temp volume is writable by the user running the IDE.

---

## 6. `mediator.serverPath` override pointing nowhere

If you set the `mediator.serverPath` setting to a custom binary path, the extension uses that exact path instead of the bundled one. A typo, missing executable bit, or wrong architecture causes immediate failure.

**Symptom signature**

```
Server failed to start: ENOENT — <path-you-configured> not found
Server failed: not executable
```

**Check**

Open your settings (User or Workspace) and find `mediator.serverPath`. Verify the path resolves to an actual file:

```shell
ls -la /path/from/setting
file /path/from/setting   # should report ELF/Mach-O/PE executable
```

**Fix**

- Clear the setting (set to empty string) to fall back to the bundled server.
- Or fix the path — make sure the file exists, is the right architecture, and (on macOS / Linux) has the executable bit:

  ```shell
  chmod +x /path/from/setting
  ```

After fixing, reload the IDE.

---

## Capturing logs for a bug report

If none of the above resolve the issue and you want to file a report, capture the full output:

- **VS Code** — open the **Output** panel (`Ctrl+Shift+U`), select **Mediator** from the dropdown, copy everything.
- **Visual Studio** — **View → Output**, select **Mediator** in the dropdown, copy everything.

Reload the IDE so the logs are captured from startup, then exercise the failure again to ensure the relevant lines are in the panel before copying.

---

## Still stuck?

Open an issue at the [GitHub repository](https://github.com/DSoftStudio/Mediator.Enterprise/issues) with:

- Your OS and architecture (`uname -a` or `systeminfo`)
- Your IDE and extension version
- The full Mediator output panel contents (with verbose logging if possible)
- Any antivirus / endpoint protection that runs on the machine

---

[← Back to Troubleshooting](index.md)
