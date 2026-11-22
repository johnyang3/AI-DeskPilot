# DeskPilot

**English** | [简体中文](README.zh-CN.md)

**English documentation | Current version: 0.1.0 | Platform: Windows 10/11 x64**

DeskPilot is a compact Windows desktop utility for batch archive extraction, reversible desktop organization, cleanup-candidate scanning, AI-assisted computer management, and verified updates through GitHub Releases.

## Features

- **Batch extraction**: Supports ZIP, 7Z, RAR, TAR, and GZ/TGZ. Each archive is extracted into a separate directory without overwriting existing files, and archive paths are checked for path traversal.
- **Desktop organization**: Organizes only regular files in the top level of the current user's desktop into Documents, Images, Videos, Audio, Archives, and Other. Shortcuts, applications, scripts, hidden files, system files, and protected paths are skipped by default.
- **Undo organization**: Every operation is recorded locally, and the most recent desktop organization can be undone.
- **Cleanup-candidate scanning**: Finds current-user temporary items unchanged for more than seven days, installed programs matching custom rules, and extension directories for Chrome, Edge, and some 360 browsers.
- **360-related item detection**: Lists candidates based on program names, publishers, extension manifests, and domain rules. The user decides whether to keep each item.
- **File protection**: Protected files, directories, and their children cannot be recycled by cleanup tools and are skipped during desktop organization.
- **AI-assisted management**: Uses an OpenAI-compatible API and allow-listed tools to inspect system information, list desktop files, scan cleanup candidates, and—after confirmation—organize, extract, recycle files, or launch registered uninstallers.
- **Automatic updates**: Checks the latest stable GitHub release, verifies the package's SHA-256 hash, and, after confirmation, backs up the existing app, replaces its files, and restarts it.
- **UI language**: Follows the Windows display language automatically. This release includes Simplified Chinese and English; other languages currently fall back to English.

> A cleanup candidate only means that an item matched a manual-review rule. It does not mean the program or extension is malicious. DeskPilot never uninstalls programs or deletes files solely because they appeared in scan results.

## Download and run

1. Download `DeskPilot-win-x64.zip` from the GitHub Releases page.
2. Extract the entire archive into a regular folder.
3. Run `DeskPilot.exe`.

The release is a self-contained, single-file Windows x64 application and does not require a separate .NET runtime.

Windows SmartScreen may display a warning when you first run the app. The project does not currently have a code-signing certificate. Only use packages from this project's Releases page, and verify that the SHA-256 hash matches `DeskPilot-win-x64.zip.sha256`.

## Usage

### 1. Batch extraction

1. Open the **Archives** tab.
2. Click **Add archives** and select one or more files.
3. Click **Choose output** and select a destination.
4. Click **Extract**, review the operation, and confirm.

Each archive is extracted into its own subdirectory named after the archive. If that directory already exists, DeskPilot creates a new directory with a numeric suffix instead of overwriting files.

### 2. Desktop organization

1. Open the **Desktop** tab.
2. Click **Preview organization** to review the proposed sources and destinations.
3. Click **Organize now** and confirm to move the files.
4. To restore them, click **Undo last organization**.

Organized files are placed in `DeskPilot整理` on the desktop by default. Only files are processed; desktop folders are not moved.

### 3. Cleanup assistant

1. Open the **Cleanup** tab and click **Scan candidates**.
2. Review each candidate's name, type, path, and match reason.
3. Select the items you want to process and click **Remove/uninstall selected**.
4. Read the confirmation prompt before deciding whether to continue.

Regular files and extension directories are sent to the Windows Recycle Bin. Installed programs are handled through their registered uninstallers. Close the relevant browser before removing a third-party browser extension. DeskPilot does not forcibly terminate browsers or other processes.

### 4. AI assistant

1. Open the **AI assistant** tab.
2. Enter an OpenAI-compatible chat completions endpoint, for example:

   `https://api.openai.com/v1/chat/completions`

3. Enter a model name supported by your provider and your API key.
4. Click **Save settings securely**. The key is encrypted for the current Windows user with DPAPI and is not stored as plaintext.
5. Enter a request such as “List my desktop files” or “Scan for 360-related extension candidates.” Click **Send** or press Ctrl+Enter.

The AI can only use the tools exposed by DeskPilot; it has no arbitrary command-line or Shell execution capability. Read-only operations may run automatically. Extraction, organization, file recycling, and program uninstallation always show a local confirmation dialog, and cancelling prevents the operation.

API requests send the current conversation and necessary tool results to your configured provider. Do not enter passwords, government ID numbers, or other sensitive information in the conversation.

### 5. Settings and updates

- **Protected paths**: Enter one full file or directory path per line, then click **Save protection rules**.
- **Check for updates**: Click **Check & install updates**. When a new version is found, DeskPilot asks for confirmation before downloading, verifying, and installing it.
- **Open repository**: Click **Open GitHub repository** to view the source code and release history.

## Customize cleanup-candidate rules

After the first scan, edit:

`%APPDATA%\DeskPilot\cleanup-rules.json`

Default content:

```json
{
  "ProgramKeywords": ["360", "2345", "hao123"],
  "ExtensionKeywords": ["360.cn", "360safe.com", "so.com", "haosou.com", "360浏览器", "360安全"]
}
```

- `ProgramKeywords`: Matches installed-program names or publishers.
- `ExtensionKeywords`: Matches relevant text or domains in browser extension manifests.

These rules are manual-review conditions, not a virus-signature database. Internationalized `__MSG_*__` names in extension manifests may not be fully resolved; scanning relies on manifest text and visible domains.

## Build locally

### Requirements

- Windows 10/11 x64
- .NET 8 SDK

### Run tests

```powershell
dotnet test tests/DeskPilot.Tests/DeskPilot.Tests.csproj -c Release
```

### Publish the Windows x64 app

```powershell
dotnet publish src/DeskPilot/DeskPilot.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
```

Build output is written to `artifacts/win-x64`.

## Automated releases and updates

Pushing to `main`, or manually running the `build-and-release` workflow, causes GitHub Actions to run tests, build the app, generate the ZIP package and SHA-256 file, and update the `v0.1.0` release.

When releasing a new version, update all of the following:

1. `<Version>` in `src/DeskPilot/DeskPilot.csproj`.
2. `tag_name` and `name` in `.github/workflows/release.yml`.
3. Push the corresponding new version tag.

## Project structure

```text
src/DeskPilot/              Application source code
tests/DeskPilot.Tests/      Automated tests
.github/workflows/          Build and release workflows
artifacts/                  Local build output
README.md                   English documentation
README.zh-CN.md             Chinese documentation
LICENSE                     MIT License
```

## Privacy and local data

Settings, the encrypted API key, organization history, and cleanup-candidate rules are stored in:

`%APPDATA%\DeskPilot`

DeskPilot contains no telemetry. GitHub update checks do not include your API key, and AI requests are sent only to your configured endpoint.

## Current limitations

- This release supports Windows x64 only.
- Multi-volume archives are not currently supported.
- Firefox extensions are not currently scanned.
- Third-party application self-protection is not automatically bypassed.
- The app is not code-signed, so SmartScreen may display a warning on first launch.
- Windows display languages other than Chinese and English currently fall back to English.

## License

This project is licensed under the MIT License. See `LICENSE` for details.
