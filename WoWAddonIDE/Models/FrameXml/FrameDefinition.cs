// Models/FrameXml/FrameDefinition.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace WoWAddonIDE.Models.FrameXml
{
    /// <summary>WoW anchor point names.</summary>
    public static class AnchorPoints
    {
        public static readonly string[] All =
        {
            "TOPLEFT", "TOP", "TOPRIGHT",
            "LEFT", "CENTER", "RIGHT",
            "BOTTOMLEFT", "BOTTOM", "BOTTOMRIGHT"
        };
    }

    /// <summary>A single anchor point for a WoW frame.</summary>
    public class AnchorPoint
    {
        public string Point { get; set; } = "CENTER";
        public string RelativeTo { get; set; } = "";
        public string RelativePoint { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }

        public XElement ToXml()
        {
            var el = new XElement("Anchor", new XAttribute("point", Point));
            if (!string.IsNullOrWhiteSpace(RelativeTo))
                el.Add(new XAttribute("relativeTo", RelativeTo));
            if (!string.IsNullOrWhiteSpace(RelativePoint))
                el.Add(new XAttribute("relativePoint", RelativePoint));
            if (X != 0 || Y != 0)
                el.Add(new XElement("Offset", new XAttribute("x", X), new XAttribute("y", Y)));
            return el;
        }

        public static AnchorPoint FromXml(XElement el)
        {
            var a = new AnchorPoint
            {
                Point = el.Attribute("point")?.Value ?? "CENTER",
                RelativeTo = el.Attribute("relativeTo")?.Value ?? "",
                RelativePoint = el.Attribute("relativePoint")?.Value ?? ""
            };
            var offset = el.Element("Offset");
            if (offset != null)
            {
                int.TryParse(offset.Attribute("x")?.Value, out var x);
                int.TryParse(offset.Attribute("y")?.Value, out var y);
                a.X = x;
                a.Y = y;
            }
            return a;
        }
    }

    /// <summary>A script handler (OnLoad, OnClick, etc.) with Lua body.</summary>
    public class FrameScript
    {
        public string Handler { get; set; } = "OnLoad";
        public string Body { get; set; } = "";

        public XElement ToXml() =>
            new XElement(Handler, Body);

        public static FrameScript FromXml(XElement el) =>
            new FrameScript { Handler = el.Name.LocalName, Body = el.Value.Trim() };
    }

    /// <summary>Represents a WoW XML frame definition with properties, anchors, children, and scripts.</summary>
    public class FrameDefinition
    {
        public string FrameType { get; set; } = "Frame";
        public string Name { get; set; } = "";
        public string Parent { get; set; } = "";
        public string Inherits { get; set; } = "";
        public bool Hidden { get; set; }
        public bool EnableMouse { get; set; }
        public bool Movable { get; set; }
        public bool ClampedToScreen { get; set; }
        public int Width { get; set; } = 200;
        public int Height { get; set; } = 200;
        public int FrameStrata { get; set; } // 0 = default
        public string FrameLevel { get; set; } = "";

        // Type-specific
        public string Text { get; set; } = "";          // Button, FontString
        public string TexturePath { get; set; } = "";    // Texture
        public string Font { get; set; } = "";           // FontString
        public int FontSize { get; set; }                // FontString
        public string NormalTexture { get; set; } = "";   // Button
        public string PushedTexture { get; set; } = "";   // Button
        public string HighlightTexture { get; set; } = "";// Button
        public int MinValue { get; set; }                 // Slider/StatusBar
        public int MaxValue { get; set; } = 100;          // Slider/StatusBar
        public string Orientation { get; set; } = "HORIZONTAL"; // StatusBar/Slider

        public List<AnchorPoint> Anchors { get; set; } = new();
        public List<FrameDefinition> Children { get; set; } = new();
        public List<FrameScript> Scripts { get; set; } = new();

        /// <summary>Generate WoW-compatible XML.</summary>
        public XElement ToXml()
        {
            var el = new XElement(FrameType);

            if (!string.IsNullOrWhiteSpace(Name))
                el.Add(new XAttribute("name", Name));
            if (!string.IsNullOrWhiteSpace(Parent))
                el.Add(new XAttribute("parent", Parent));
            if (!string.IsNullOrWhiteSpace(Inherits))
                el.Add(new XAttribute("inherits", Inherits));
            if (Hidden)
                el.Add(new XAttribute("hidden", "true"));
            if (EnableMouse)
                el.Add(new XAttribute("enableMouse", "true"));
            if (Movable)
                el.Add(new XAttribute("movable", "true"));
            if (ClampedToScreen)
                el.Add(new XAttribute("clampedToScreen", "true"));

            // Size
            el.Add(new XElement("Size",
                new XAttribute("x", Width),
                new XAttribute("y", Height)));

            // Anchors
            if (Anchors.Count > 0)
            {
                var anchorsEl = new XElement("Anchors");
                foreach (var a in Anchors)
                    anchorsEl.Add(a.ToXml());
                el.Add(anchorsEl);
            }

            // Type-specific elements
            AddTypeSpecificXml(el);

            // Frames (children)
            if (Children.Count > 0)
            {
                var framesEl = new XElement("Frames");
                foreach (var child in Children)
                    framesEl.Add(child.ToXml());
                el.Add(framesEl);
            }

            // Scripts
            if (Scripts.Count > 0)
            {
                var scriptsEl = new XElement("Scripts");
                foreach (var s in Scripts)
                    scriptsEl.Add(s.ToXml());
                el.Add(scriptsEl);
            }

            return el;
        }

        private void AddTypeSpecificXml(XElement el)
        {
            switch (FrameType)
            {
                case "Button":
                    if (!string.IsNullOrWhiteSpace(NormalTexture))
                        el.Add(new XElement("NormalTexture", new XAttribute("file", NormalTexture)));
                    if (!string.IsNullOrWhiteSpace(PushedTexture))
                        el.Add(new XElement("PushedTexture", new XAttribute("file", PushedTexture)));
                    if (!string.IsNullOrWhiteSpace(HighlightTexture))
                        el.Add(new XElement("HighlightTexture", new XAttribute("file", HighlightTexture)));
                    if (!string.IsNullOrWhiteSpace(Text))
                        el.Add(new XElement("NormalText", new XAttribute("text", Text)));
                    break;

                case "FontString":
                    if (!string.IsNullOrWhiteSpace(Text))
                        el.Add(new XAttribute("text", Text));
                    if (!string.IsNullOrWhiteSpace(Font))
                        el.Add(new XAttribute("font", Font));
                    if (FontSize > 0)
                        el.Add(new XElement("FontHeight", new XAttribute("val", FontSize)));
                    break;

                case "Texture":
                    if (!string.IsNullOrWhiteSpace(TexturePath))
                        el.Add(new XAttribute("file", TexturePath));
                    break;

                case "StatusBar":
                case "Slider":
                    el.Add(new XAttribute("minValue", MinValue));
                    el.Add(new XAttribute("maxValue", MaxValue));
                    el.Add(new XAttribute("orientation", Orientation));
                    break;

                case "EditBox":
                    if (!string.IsNullOrWhiteSpace(Text))
                        el.Add(new XAttribute("text", Text));
                    break;
            }
        }

        /// <summary>Generate a complete Ui.xsd-compatible XML document.</summary>
        public string ToXmlDocument()
        {
            var ui = new XElement("Ui",
                new XAttribute("xmlns", "http://www.blizzard.com/wow/ui/"),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                ToXml());
            return new XDocument(new XDeclaration("1.0", "UTF-8", null), ui).ToString();
        }

        /// <summary>Parse a frame element from XML.</summary>
        public static FrameDefinition FromXml(XElement el)
        {
            var frame = new FrameDefinition
            {
                FrameType = el.Name.LocalName,
                Name = el.Attribute("name")?.Value ?? "",
                Parent = el.Attribute("parent")?.Value ?? "",
                Inherits = el.Attribute("inherits")?.Value ?? "",
                Hidden = el.Attribute("hidden")?.Value == "true",
                EnableMouse = el.Attribute("enableMouse")?.Value == "true",
                Movable = el.Attribute("movable")?.Value == "true",
                ClampedToScreen = el.Attribute("clampedToScreen")?.Value == "true",
                Text = el.Attribute("text")?.Value ?? "",
                TexturePath = el.Attribute("file")?.Value ?? "",
                Font = el.Attribute("font")?.Value ?? "",
            };

            // Size
            var size = el.Element("Size");
            if (size != null)
            {
                int.TryParse(size.Attribute("x")?.Value ?? size.Attribute("width")?.Value, out var w);
                int.TryParse(size.Attribute("y")?.Value ?? size.Attribute("height")?.Value, out var h);
                frame.Width = w > 0 ? w : 200;
                frame.Height = h > 0 ? h : 200;
            }

            // Anchors
            var anchors = el.Element("Anchors");
            if (anchors != null)
            {
                foreach (var a in anchors.Elements("Anchor"))
                    frame.Anchors.Add(AnchorPoint.FromXml(a));
            }

            // Children
            var frames = el.Element("Frames");
            if (frames != null)
            {
                foreach (var child in frames.Elements())
                    frame.Children.Add(FromXml(child));
            }

            // Layers (FontStrings, Textures)
            var layers = el.Element("Layers");
            if (layers != null)
            {
                foreach (var layer in layers.Elements("Layer"))
                {
                    foreach (var child in layer.Elements())
                        frame.Children.Add(FromXml(child));
                }
            }

            // Scripts
            var scripts = el.Element("Scripts");
            if (scripts != null)
            {
                foreach (var s in scripts.Elements())
                    frame.Scripts.Add(FrameScript.FromXml(s));
            }

            // Type-specific
            int.TryParse(el.Attribute("minValue")?.Value, out var minV);
            int.TryParse(el.Attribute("maxValue")?.Value, out var maxV);
            frame.MinValue = minV;
            frame.MaxValue = maxV > 0 ? maxV : 100;
            frame.Orientation = el.Attribute("orientation")?.Value ?? "HORIZONTAL";

            var normalTex = el.Element("NormalTexture");
            if (normalTex != null) frame.NormalTexture = normalTex.Attribute("file")?.Value ?? "";
            var pushedTex = el.Element("PushedTexture");
            if (pushedTex != null) frame.PushedTexture = pushedTex.Attribute("file")?.Value ?? "";
            var highlightTex = el.Element("HighlightTexture");
            if (highlightTex != null) frame.HighlightTexture = highlightTex.Attribute("file")?.Value ?? "";

            return frame;
        }

        /// <summary>Try to parse a full Ui XML document and extract frames.</summary>
        public static List<FrameDefinition> FromXmlDocument(string xml)
        {
            var result = new List<FrameDefinition>();
            try
            {
                var doc = XDocument.Parse(xml);
                var root = doc.Root;
                if (root == null) return result;

                foreach (var el in root.Elements())
                    result.Add(FromXml(el));
            }
            catch { /* malformed XML */ }
            return result;
        }
    }

    /// <summary>Schema metadata for a frame type's properties.</summary>
    public static class FrameTypeSchema
    {
        public static readonly string[] FrameTypes =
        {
            "Frame", "Button", "FontString", "Texture", "StatusBar",
            "Slider", "EditBox", "ScrollFrame", "GameTooltip", "CheckButton"
        };

        public static readonly string[] CommonScripts =
        {
            "OnLoad", "OnShow", "OnHide", "OnEvent", "OnUpdate",
            "OnClick", "OnEnter", "OnLeave", "OnMouseDown", "OnMouseUp",
            "OnDragStart", "OnDragStop", "OnValueChanged",
            "OnTextChanged", "OnEscapePressed", "OnEnterPressed"
        };

        public static string[] GetScriptsFor(string frameType) => frameType switch
        {
            "Button" or "CheckButton" => new[] { "OnClick", "OnLoad", "OnShow", "OnHide", "OnEnter", "OnLeave", "OnEvent" },
            "EditBox" => new[] { "OnTextChanged", "OnEnterPressed", "OnEscapePressed", "OnLoad", "OnShow", "OnHide" },
            "Slider" or "StatusBar" => new[] { "OnValueChanged", "OnLoad", "OnShow", "OnHide" },
            "ScrollFrame" => new[] { "OnScrollRangeChanged", "OnLoad", "OnShow", "OnHide" },
            _ => new[] { "OnLoad", "OnShow", "OnHide", "OnEvent", "OnUpdate", "OnEnter", "OnLeave" }
        };
    }
}
