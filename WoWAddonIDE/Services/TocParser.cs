using System;
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
    }
}