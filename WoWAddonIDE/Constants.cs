namespace WoWAddonIDE
{
    /// <summary>
    /// Centralized constants used throughout the IDE.
    /// </summary>
    public static class Constants
    {
        // -------- Application --------
        public const string AppName = "WoWAddonIDE";
        public const string SettingsFileName = "settings.json";
        public const string RecentProjectsFileName = "recent-projects.json";
        public const string CrashLogFileName = "crash.log";
        public const string SecureTokenKey = "github_token";

        // -------- File Extensions --------
        public const string LuaExtension = ".lua";
        public const string XmlExtension = ".xml";
        public const string TocExtension = ".toc";
        public const string JsonExtension = ".json";
        public const string MarkdownExtension = ".md";
        public const string TextExtension = ".txt";

        /// <summary>Extensions watched for external file changes.</summary>
        public static readonly string[] WatchedExtensions =
            { LuaExtension, XmlExtension, TocExtension, TextExtension, MarkdownExtension };

        /// <summary>Extensions considered "code" for find-in-files filtering.</summary>
        public static readonly string[] CodeExtensions =
            { LuaExtension, XmlExtension, TocExtension };

        // -------- TOC Metadata Keys --------
        public const string TocInterfacePrefix = "## Interface:";
        public const string TocTitlePrefix = "## Title:";
        public const string TocAuthorPrefix = "## Author:";
        public const string TocVersionPrefix = "## Version:";
        public const string TocNotesPrefix = "## Notes:";
        public const string DefaultInterfaceVersion = "110005";

        // -------- Hidden Folders (excluded from project tree) --------
        public static readonly string[] HiddenFolders = { ".git", ".vs" };

        // -------- Build Excludes (default) --------
        public const string DefaultPackageExcludes =
            ".psd .xcf .zip .7z .rar .git .vs bin obj .pdb .user .cache .tmp";

        // -------- Git --------
        public const string DefaultRemoteName = "origin";
        public const string DefaultCommitMessage = "Update";
        public const string DefaultGitUserName = "WoWAddonIDE";
        public const string DefaultGitEmail = "noreply@localhost";

        // -------- Formatter --------
        public const string DefaultStyluaArgs =
            "--search-parent-directories --stdin-filepath \"{file}\" -";
        public const string StyluaExeName = "stylua.exe";

        // -------- Paths --------
        public static string AppDataDir =>
            System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                AppName);

        public static string SettingsPath =>
            System.IO.Path.Combine(AppDataDir, SettingsFileName);

        // -------- Lua Comment Prefix --------
        public const string LuaCommentPrefix = "--";

        // -------- UI --------
        public const string DefaultEditorFont = "Consolas";
        public const double DefaultEditorFontSize = 14;
        public const double MinEditorFontSize = 8;
        public const double MaxEditorFontSize = 40;
    }
}
