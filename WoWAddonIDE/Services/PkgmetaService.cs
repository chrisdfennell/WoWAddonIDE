using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WoWAddonIDE.Services
{
    /// <summary>
    /// Handles .pkgmeta file generation, parsing, and keyword substitution
    /// for CurseForge/Wago/WoWInterface BigWigs packager format.
    /// </summary>
    public static class PkgmetaService
    {
        /// <summary>Well-known external libraries that can be embedded.</summary>
        public static readonly (string Name, string Url, string Tag)[] KnownLibraries =
        {
            ("LibStub",                 "https://repos.curseforge.com/wow/libstub/trunk",               "libs/LibStub"),
            ("CallbackHandler-1.0",     "https://repos.curseforge.com/wow/callbackhandler/trunk/CallbackHandler-1.0", "libs/CallbackHandler-1.0"),
            ("AceAddon-3.0",            "https://repos.curseforge.com/wow/ace3/trunk/AceAddon-3.0",    "libs/AceAddon-3.0"),
            ("AceConsole-3.0",          "https://repos.curseforge.com/wow/ace3/trunk/AceConsole-3.0",  "libs/AceConsole-3.0"),
            ("AceEvent-3.0",            "https://repos.curseforge.com/wow/ace3/trunk/AceEvent-3.0",    "libs/AceEvent-3.0"),
            ("AceDB-3.0",              "https://repos.curseforge.com/wow/ace3/trunk/AceDB-3.0",        "libs/AceDB-3.0"),
            ("AceDBOptions-3.0",       "https://repos.curseforge.com/wow/ace3/trunk/AceDBOptions-3.0", "libs/AceDBOptions-3.0"),
            ("AceConfig-3.0",          "https://repos.curseforge.com/wow/ace3/trunk/AceConfig-3.0",    "libs/AceConfig-3.0"),
            ("AceGUI-3.0",             "https://repos.curseforge.com/wow/ace3/trunk/AceGUI-3.0",       "libs/AceGUI-3.0"),
            ("AceLocale-3.0",          "https://repos.curseforge.com/wow/ace3/trunk/AceLocale-3.0",    "libs/AceLocale-3.0"),
            ("AceTimer-3.0",           "https://repos.curseforge.com/wow/ace3/trunk/AceTimer-3.0",     "libs/AceTimer-3.0"),
            ("AceComm-3.0",            "https://repos.curseforge.com/wow/ace3/trunk/AceComm-3.0",      "libs/AceComm-3.0"),
            ("AceSerializer-3.0",      "https://repos.curseforge.com/wow/ace3/trunk/AceSerializer-3.0","libs/AceSerializer-3.0"),
            ("LibDataBroker-1.1",      "https://repos.curseforge.com/wow/libdatabroker-1-1/trunk",     "libs/LibDataBroker-1.1"),
            ("LibDBIcon-1.0",          "https://repos.curseforge.com/wow/libdbicon-1-0/trunk/LibDBIcon-1.0", "libs/LibDBIcon-1.0"),
        };

        /// <summary>
        /// Keyword tokens that the BigWigs packager replaces at package time.
        /// </summary>
        public static readonly (string Token, string Description)[] KeywordTokens =
        {
            ("@project-version@",       "Replaced with the tag name (e.g. v1.2.0)"),
            ("@project-revision@",      "Replaced with the VCS revision/hash"),
            ("@project-date-iso@",      "ISO-8601 date of last commit"),
            ("@project-date-integer@",  "Integer date (YYYYMMDDHHMMSS)"),
            ("@project-abbreviated-hash@", "Short git hash"),
            ("@project-hash@",          "Full git commit hash"),
            ("@project-timestamp@",     "Unix timestamp of last commit"),
        };

        /// <summary>Generates a default .pkgmeta file.</summary>
        public static string GenerateDefault(string addonName, IEnumerable<string>? selectedLibs = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("package-as: " + addonName);
            sb.AppendLine();
            sb.AppendLine("enable-nolib-creation: yes");
            sb.AppendLine();

            // Ignore patterns
            sb.AppendLine("ignore:");
            sb.AppendLine("  - .git");
            sb.AppendLine("  - .vs");
            sb.AppendLine("  - \"*.psd\"");
            sb.AppendLine("  - \"*.xcf\"");
            sb.AppendLine("  - README.md");
            sb.AppendLine("  - CHANGELOG.md");
            sb.AppendLine();

            // Externals
            var libs = selectedLibs?.ToList();
            if (libs != null && libs.Count > 0)
            {
                sb.AppendLine("externals:");
                foreach (var libName in libs)
                {
                    var known = KnownLibraries.FirstOrDefault(l =>
                        l.Name.Equals(libName, StringComparison.OrdinalIgnoreCase));
                    if (known != default)
                    {
                        sb.AppendLine($"  {known.Tag}:");
                        sb.AppendLine($"    url: {known.Url}");
                    }
                }
                sb.AppendLine();
            }

            // Manual changelog
            sb.AppendLine("manual-changelog:");
            sb.AppendLine("  filename: CHANGELOG.md");
            sb.AppendLine("  markup-type: markdown");

            return sb.ToString();
        }

        /// <summary>Parses an existing .pkgmeta and returns its sections.</summary>
        public static PkgmetaInfo Parse(string filePath)
        {
            var info = new PkgmetaInfo();
            if (!File.Exists(filePath)) return info;

            var lines = File.ReadAllLines(filePath);
            string? currentSection = null;
            string? currentExtKey = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();

                // Top-level key
                if (!line.StartsWith(" ") && !line.StartsWith("\t") && line.Contains(':'))
                {
                    var colonIdx = line.IndexOf(':');
                    var key = line.Substring(0, colonIdx).Trim();
                    var val = line.Substring(colonIdx + 1).Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "package-as":
                            info.PackageAs = val;
                            currentSection = null;
                            break;
                        case "enable-nolib-creation":
                            info.EnableNoLibCreation = val.Equals("yes", StringComparison.OrdinalIgnoreCase);
                            currentSection = null;
                            break;
                        case "ignore":
                            currentSection = "ignore";
                            break;
                        case "externals":
                            currentSection = "externals";
                            break;
                        case "manual-changelog":
                            currentSection = "changelog";
                            break;
                        default:
                            currentSection = null;
                            break;
                    }
                    continue;
                }

                // Section items
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (currentSection == "ignore" && trimmed.StartsWith("- "))
                {
                    info.IgnorePatterns.Add(trimmed.Substring(2).Trim().Trim('"'));
                }
                else if (currentSection == "externals")
                {
                    if (trimmed.EndsWith(":") && !trimmed.StartsWith("url:") && !trimmed.StartsWith("tag:"))
                    {
                        currentExtKey = trimmed.TrimEnd(':').Trim();
                        info.Externals[currentExtKey] = "";
                    }
                    else if (currentExtKey != null && trimmed.StartsWith("url:"))
                    {
                        info.Externals[currentExtKey] = trimmed.Substring(4).Trim();
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// Applies keyword substitution to all files in a directory (for local preview).
        /// Uses git info if available, otherwise uses fallback values.
        /// </summary>
        public static int ApplyKeywordSubstitution(string directory, string version, string? gitHash = null)
        {
            int count = 0;
            var now = DateTime.UtcNow;
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["@project-version@"] = version,
                ["@project-revision@"] = gitHash ?? "local",
                ["@project-date-iso@"] = now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["@project-date-integer@"] = now.ToString("yyyyMMddHHmmss"),
                ["@project-abbreviated-hash@"] = (gitHash?.Length >= 7 ? gitHash.Substring(0, 7) : gitHash) ?? "local",
                ["@project-hash@"] = gitHash ?? "local",
                ["@project-timestamp@"] = new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
            };

            foreach (var file in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".lua" && ext != ".toc" && ext != ".xml" && ext != ".txt" && ext != ".md")
                    continue;

                var text = File.ReadAllText(file);
                bool changed = false;

                foreach (var (token, replacement) in replacements)
                {
                    if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        text = Regex.Replace(text, Regex.Escape(token), replacement, RegexOptions.IgnoreCase);
                        changed = true;
                    }
                }

                if (changed)
                {
                    File.WriteAllText(file, text);
                    count++;
                }
            }

            return count;
        }
    }

    public class PkgmetaInfo
    {
        public string PackageAs { get; set; } = "";
        public bool EnableNoLibCreation { get; set; }
        public List<string> IgnorePatterns { get; set; } = new();
        public Dictionary<string, string> Externals { get; set; } = new();
    }
}
