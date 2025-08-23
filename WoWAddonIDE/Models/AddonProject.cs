using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WoWAddonIDE.Models
{
    public class AddonProject
    {
        public string Name { get; set; } = "";
        public string RootPath { get; set; } = "";
        public string TocPath { get; set; } = "";
        public string? InterfaceVersion { get; set; }
        public List<string> Files { get; set; } = new();

        public static AddonProject? LoadFromDirectory(string root)
        {
            if (!Directory.Exists(root)) return null;
            var toc = Directory.GetFiles(root, "*.toc", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (toc == null) return null;

            var proj = new AddonProject
            {
                RootPath = root,
                TocPath = toc,
                Name = Path.GetFileNameWithoutExtension(toc)
            };

            // gather files (all)
            proj.Files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories).ToList();

            // try read interface version
            var lines = File.ReadAllLines(toc);
            foreach (var l in lines)
            {
                var t = l.Trim();
                if (t.StartsWith("## Interface:", StringComparison.OrdinalIgnoreCase))
                {
                    proj.InterfaceVersion = t.Split(':', 2)[1].Trim();
                    break;
                }
            }
            return proj;
        }
    }
}
