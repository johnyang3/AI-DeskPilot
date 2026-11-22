using System.Globalization;

namespace DeskPilot.Services;

public static class Loc
{
    public static bool IsChinese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string[]> Text = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AppTitle"] = ["DeskPilot 桌面管家", "DeskPilot"],
        ["Archives"] = ["批量解压", "Archives"],
        ["Desktop"] = ["桌面整理", "Desktop"],
        ["Cleanup"] = ["清理助手", "Cleanup"],
        ["Assistant"] = ["AI 管理", "AI assistant"],
        ["Settings"] = ["设置与更新", "Settings & updates"],
        ["AddArchives"] = ["添加压缩包", "Add archives"],
        ["RemoveSelected"] = ["移除所选项", "Remove selected"],
        ["ChooseOutput"] = ["选择输出目录", "Choose output"],
        ["StartExtract"] = ["开始解压", "Extract"],
        ["Output"] = ["输出目录", "Output"],
        ["NoArchives"] = ["请添加 ZIP、7Z、RAR、TAR 或 GZ 压缩包。", "Add ZIP, 7Z, RAR, TAR or GZ archives."],
        ["PreviewOrganize"] = ["预览整理", "Preview organization"],
        ["ApplyOrganize"] = ["执行整理", "Organize now"],
        ["UndoOrganize"] = ["撤销上次整理", "Undo last organization"],
        ["AddProtection"] = ["保护所选文件/文件夹", "Protect file/folder"],
        ["OrganizeHint"] = ["仅移动普通文件，跳过快捷方式、程序、系统文件和受保护路径；目标位于桌面的 DeskPilot整理 文件夹。", "Only regular files are moved. Shortcuts, apps, system files and protected paths are skipped. Destination: DeskPilot整理 on the desktop."],
        ["Scan"] = ["扫描清理候选", "Scan candidates"],
        ["CleanSelected"] = ["移除/卸载所选", "Remove/uninstall selected"],
        ["CleanupHint"] = ["规则匹配只表示需要人工复核，不代表项目恶意。文件送入回收站；程序调用其官方卸载入口；浏览器扩展需先关闭浏览器。", "A rule match is not a malware verdict. Files go to Recycle Bin; apps use their registered uninstaller. Close browsers before removing extensions."],
        ["Name"] = ["名称", "Name"],
        ["Kind"] = ["类型", "Type"],
        ["Reason"] = ["匹配原因", "Reason"],
        ["Path"] = ["路径", "Path"],
        ["Size"] = ["大小", "Size"],
        ["Endpoint"] = ["OpenAI 兼容接口", "OpenAI-compatible endpoint"],
        ["Model"] = ["模型", "Model"],
        ["ApiKey"] = ["API 密钥", "API key"],
        ["SaveAi"] = ["加密保存设置", "Save settings securely"],
        ["Send"] = ["发送", "Send"],
        ["AiHint"] = ["只读操作自动执行；解压、整理、回收文件和卸载均需确认。不会开放任意命令执行。密钥使用 Windows DPAPI 加密。", "Read-only actions run automatically; extraction, organization, recycling and uninstall always require confirmation. No arbitrary shell access. Keys use Windows DPAPI."],
        ["AiPlaceholder"] = ["例如：列出桌面文件，或扫描 360 相关扩展候选", "Example: list desktop files or scan for 360-related extension candidates"],
        ["ProtectedPaths"] = ["受保护路径（每行一个）", "Protected paths (one per line)"],
        ["SaveProtection"] = ["保存保护规则", "Save protection rules"],
        ["CheckUpdate"] = ["检查并安装更新", "Check & install updates"],
        ["OpenRepo"] = ["打开 GitHub 仓库", "Open GitHub repository"],
        ["Version"] = ["版本", "Version"],
        ["Language"] = ["界面语言", "UI language"],
        ["Ready"] = ["就绪", "Ready"],
        ["ConfirmTitle"] = ["确认操作", "Confirm operation"],
        ["Cancel"] = ["取消", "Cancel"],
        ["Confirm"] = ["确认执行", "Confirm"],
        ["Close"] = ["关闭", "Close"],
        ["Error"] = ["错误", "Error"],
        ["Done"] = ["完成", "Done"],
        ["NoUpdate"] = ["当前已是最新版本。", "You are using the latest version."],
        ["UpdateAvailable"] = ["发现新版本 {0}。是否下载、校验并安装？程序将重启。", "Version {0} is available. Download, verify and install? The app will restart."],
        ["UpdateFailed"] = ["更新失败：{0}", "Update failed: {0}"],
        ["SettingsSaved"] = ["设置已保存。", "Settings saved."],
        ["MissingApi"] = ["请填写接口、模型和 API 密钥。", "Enter the endpoint, model and API key."],
        ["AiWelcome"] = ["你好！我可以查看系统与桌面信息、扫描清理候选，并在你确认后整理、解压或回收文件。", "Hello! I can inspect the system and desktop, scan cleanup candidates, and organize, extract or recycle files after your confirmation."],
        ["ConfirmMoves"] = ["将移动 {0} 个文件。是否继续？\n\n{1}", "Move {0} files?\n\n{1}"],
        ["ConfirmExtract"] = ["将解压 {0} 个压缩包到：\n{1}\n\n是否继续？", "Extract {0} archives to:\n{1}\n\nContinue?"],
        ["ConfirmCleanup"] = ["将处理 {0} 个候选项。文件/扩展目录进入回收站，程序会启动其卸载器。是否继续？", "Process {0} candidates? Files/extension folders go to Recycle Bin; apps launch their uninstallers. Continue?"],
        ["ConfirmRecycle"] = ["是否将以下 {0} 项送入回收站？\n\n{1}", "Send these {0} items to Recycle Bin?\n\n{1}"],
        ["NothingSelected"] = ["没有选中项目。", "No items selected."],
        ["NoMoves"] = ["没有需要整理的文件。", "No files need organizing."],
        ["NoUndo"] = ["没有可撤销的整理记录。", "No organization history is available."],
        ["ScanComplete"] = ["扫描完成，共 {0} 个候选。", "Scan complete: {0} candidates."],
        ["ExtractComplete"] = ["解压完成：成功 {0} 个，失败 {1} 个。", "Extraction complete: {0} succeeded, {1} failed."],
        ["MoveComplete"] = ["已整理 {0} 个文件，可随时撤销。", "Organized {0} files. You can undo this operation."],
        ["UndoComplete"] = ["已恢复 {0} 个文件。", "Restored {0} files."],
        ["Protected"] = ["路径受保护：{0}", "Protected path: {0}"],
        ["UnsafePath"] = ["路径超出目标目录，已跳过。", "Unsafe archive entry was skipped."],
        ["OperationCancelled"] = ["用户取消了操作。", "The user cancelled the operation."],
        ["NoCandidate"] = ["没有找到匹配的清理候选。", "No matching cleanup candidate was found."],
        ["UninstallStarted"] = ["已启动卸载器：{0}", "Uninstaller started: {0}"],
        ["UpdateStarted"] = ["更新已就绪，程序即将重启。", "Update ready. The app will now restart."]
    };

    public static string T(string key) => Text.TryGetValue(key, out var values) ? (IsChinese ? values[0] : values[1]) : key;
    public static string F(string key, params object[] args) => string.Format(T(key), args);
}
