using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WoWAddonIDE.Services
{
    public static class TocParser
    {
        public static string GenerateDefaultToc(string addonName, string interfaceVersion)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## Interface: {interfaceVersion}");
            sb.AppendLine($"## Title: {addonName}");
            sb.AppendLine("## Author: You");
            sb.AppendLine("## Version: 1.0.0");
            sb.AppendLine("## Notes: Created with WoW Addon IDE");
            sb.AppendLine();
            // conventionally include main files by name; caller can append more
            sb.AppendLine($"{addonName}.lua"); // optional if you create it
            sb.AppendLine("Main.lua");
            return sb.ToString();
        }

        /// <summary>
        /// Generates flavor-specific TOC files (e.g. MyAddon_Mainline.toc, MyAddon_Vanilla.toc).
        /// Uses the base TOC as a template, replacing only the Interface version.
        /// </summary>
        public static List<string> GenerateFlavorTocs(
            string projectRoot,
            string addonName,
            string baseTocPath,
            IEnumerable<(string Suffix, string InterfaceVersion)> flavors)
        {
            var created = new List<string>();

            // Read base TOC as template (or generate a default if missing)
            string[] baseLines;
            if (File.Exists(baseTocPath))
                baseLines = File.ReadAllLines(baseTocPath);
            else
                baseLines = GenerateDefaultToc(addonName, Constants.DefaultInterfaceVersion)
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var (suffix, iface) in flavors)
            {
                if (string.IsNullOrWhiteSpace(iface)) continue;

                var flavorFileName = $"{addonName}_{suffix}.toc";
                var flavorPath = Path.Combine(projectRoot, flavorFileName);

                var lines = new List<string>();
                bool replacedInterface = false;

                foreach (var line in baseLines)
                {
                    if (line.TrimStart().StartsWith("## Interface", StringComparison.OrdinalIgnoreCase)
                        && !replacedInterface)
                    {
                        lines.Add($"## Interface: {iface}");
                        replacedInterface = true;
                    }
                    else
                    {
                        lines.Add(line);
                    }
                }

                if (!replacedInterface)
                    lines.Insert(0, $"## Interface: {iface}");

                File.WriteAllLines(flavorPath, lines);
                created.Add(flavorPath);
            }

            return created;
        }

        /// <summary>
        /// Discovers all TOC files for an addon (base + flavor-specific).
        /// Returns tuples of (flavorLabel, tocPath). The base TOC gets label "Base".
        /// </summary>
        public static List<(string Label, string Path)> DiscoverTocFiles(string projectRoot, string addonName)
        {
            var result = new List<(string Label, string Path)>();

            // Base TOC: AddonName.toc
            var baseToc = System.IO.Path.Combine(projectRoot, $"{addonName}.toc");
            if (File.Exists(baseToc))
                result.Add(("Base", baseToc));

            // Flavor TOCs: AddonName_Suffix.toc
            foreach (var (suffix, displayName, _) in Constants.WowFlavors)
            {
                var flavorPath = System.IO.Path.Combine(projectRoot, $"{addonName}_{suffix}.toc");
                if (File.Exists(flavorPath))
                    result.Add((displayName, flavorPath));
            }

            // Also pick up any other _*.toc files we didn't expect
            foreach (var tocFile in Directory.GetFiles(projectRoot, "*.toc", SearchOption.TopDirectoryOnly))
            {
                if (!result.Any(r => r.Path.Equals(tocFile, StringComparison.OrdinalIgnoreCase)))
                    result.Add((System.IO.Path.GetFileNameWithoutExtension(tocFile), tocFile));
            }

            return result;
        }
    }
}
