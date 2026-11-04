using Godot;
using static Based.Spires2DamageMeter.MeterTooltipContent;
using static Godot.Control;
namespace Based.Spires2DamageMeter.Runtime;

internal static class MeterTooltipContentLogic
{
    public static Label Label(MeterTooltipContent self, string text, int size) { var l = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart }; if (size > 0) l.AddThemeFontSizeOverride("font_size", size); return l; }
    public static void Set(MeterTooltipContent self, Label label, string text, TooltipTone tone)
    {
        if (label.Text != text) label.Text = text;
        var key = tone is TooltipTone.Hp or TooltipTone.Block ? "bar_hp" : tone == TooltipTone.Bonus ? "bar_bonus" : "";
        var matching = key.Length > 0 && self.HasMeta(key);
        var colour = matching ? self.GetMeta(key).AsColor() : tone == TooltipTone.MutedBlock ? new Color(.6f, .6f, .6f) : MeterPalette.Colour(tone);
        label.AddThemeColorOverride("font_color", colour);
        // A subtle neutral outline keeps the dark bonus shades readable on the tooltip.
        label.AddThemeColorOverride("font_outline_color", new Color(1, 1, 1, .35f));
        label.AddThemeConstantOverride("outline_size", matching ? 1 : 0);
    }
    public static HBoxContainer Row(MeterTooltipContent self) { var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; self.content.AddChild(row); return row; }
    public static void Heading(MeterTooltipContent self, string text) { var label = self.Label(text); self.content.AddChild(label); self.textLabels.Add(label); }
    public static void _Ready(MeterTooltipContent self)
    {
        self.scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        self.content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        self.scroll.MouseFilter = MouseFilterEnum.Stop; self.scroll.FocusMode = FocusModeEnum.None; self.scroll.GetVScrollBar().FocusMode = FocusModeEnum.None; self.scroll.GetHScrollBar().FocusMode = FocusModeEnum.None; self.content.MouseFilter = MouseFilterEnum.Ignore;
        self.scroll.AddChild(self.content); self.AddChild(self.scroll); self.Refresh();
    }
    public static void Build(MeterTooltipContent self, TooltipCard? card)
    {
        foreach (var child in self.content.GetChildren()) { self.content.RemoveChild(child); child.QueueFree(); }
        self.stats.Clear(); self.sources.Clear(); self.headers.Clear(); self.textLabels.Clear();
        if (card == null) { self.plain = self.Label(); self.content.AddChild(self.plain); return; }
        self.title = self.Label("", 18); self.content.AddChild(self.title); self.scope = self.Label(); self.content.AddChild(self.scope);
        var summary = self.Row(); self.total = self.Label("", 22); self.total.SizeFlagsHorizontal = SizeFlags.ExpandFill; summary.AddChild(self.total); self.share = self.Label(); summary.AddChild(self.share);
        string section = "";
        foreach (var stat in card.Stats) { if (stat.Section != section) { self.content.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore }); self.Heading(section = stat.Section); } var row = self.Row(); var label = self.Label(stat.Label); label.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(label); var value = self.Label(); value.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(value); self.stats.Add((label, value)); }
        self.content.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore }); self.sourceHeading = self.Label("Sources"); self.content.AddChild(self.sourceHeading);
        var heading = self.Row(); foreach (var text in card.Headers) { var label = self.Label(text); heading.AddChild(label); self.headers.Add(label); }
        for (int i = 0; i < card.Sources.Count; i++) { var row = self.Row(); var name = self.Label(); row.AddChild(name); var values = new List<Label>(); for (int c = 1; c < card.Headers.Count; c++) { var label = self.Label(); label.HorizontalAlignment = HorizontalAlignment.Right; row.AddChild(label); values.Add(label); } self.sources.Add((row, name, values.ToArray())); }
        self.more = self.Label(); self.content.AddChild(self.more); self.content.AddChild(new HSeparator { MouseFilter = MouseFilterEnum.Ignore }); self.note = self.Label(); self.content.AddChild(self.note); self.warning = self.Label(); self.content.AddChild(self.warning);
    }
    public static void Refresh(MeterTooltipContent self)
    {
        if (!self.IsInsideTree()) return;
        self.width = Math.Max(self.HasMeta("interactive") ? 1 : 120, Math.Min(600, self.AvailableSize.X - 16)); var height = Math.Max(self.HasMeta("interactive") ? 1 : 120, Math.Min(700, self.AvailableSize.Y - 16)); self.CustomMinimumSize = new(self.width + 16, height + 16); self.Size = new(self.width + 16, height + 16); self.scroll.CustomMinimumSize = new(self.width, height); self.content.CustomMinimumSize = new(self.width, 0); self.MouseFilter = self.HasMeta("interactive") ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
        if (self.HasMeta("interactive")) { self.width = Math.Max(1, self.width - self.scroll.GetVScrollBar().GetCombinedMinimumSize().X); self.content.CustomMinimumSize = new Vector2(self.width,0); }
        var card = self.Model.Card; var key = card == null ? "plain" : string.Join("|", card.Stats.Select(s => s.Section + ":" + s.Label)) + "/" + string.Join("|", card.Headers) + "/" + card.Sources.Count;
        if (self.shape != key)
        {
            var scrollPosition = self.scroll.ScrollVertical; self.shape = key; self.Build(card);
            if (scrollPosition > 0 && self.HasMeta("interactive")) Hosting.RuntimeHost.Defer(() => { if (GodotObject.IsInstanceValid(self) && self.IsInsideTree() && self.shape == key) self.scroll.ScrollVertical = scrollPosition; });
        }
        var font = self.GetThemeDefaultFont(); var fontSize = self.GetThemeDefaultFontSize();
        if (card == null) { self.Set(self.plain, self.Model.Render()); self.plain.CustomMinimumSize = new(self.width, 0); return; }
        self.Set(self.title, card.Title); self.Set(self.scope, card.Scope); self.Set(self.total, card.Total.Text, card.Total.Tone); self.share.Visible = card.PartyShare.HasValue; self.Set(self.share, card.PartyShare.HasValue ? $"{card.PartyShare:0.#}% of party" : "");
        self.share.CustomMinimumSize = new(font.GetStringSize(self.share.Text, HorizontalAlignment.Left, -1, fontSize).X, 0);
        for (int i = 0; i < self.stats.Count; i++)
        {
            self.Set(self.stats[i].Value, card.Stats[i].Number.Text, card.Stats[i].Number.Tone);
            self.stats[i].Value.CustomMinimumSize = new(Math.Min(self.width * .45f, font.GetStringSize(self.stats[i].Value.Text, HorizontalAlignment.Left, -1, fontSize).X), 0);
        }
        self.Set(self.note, card.Note); self.Set(self.warning, card.Warning); self.warning.Visible = card.Warning.Length > 0;
        var numberWidth = Math.Max(45, self.headers.Skip(1).Select(l => font.GetStringSize(l.Text, HorizontalAlignment.Left, -1, fontSize).X + 8).DefaultIfEmpty(45).Max());
        foreach (var row in card.Sources.Take(12)) foreach (var value in row.Values) numberWidth = Math.Max(numberWidth, font.GetStringSize(value.Text, HorizontalAlignment.Left, -1, fontSize).X + 8);
        numberWidth = Math.Min(numberWidth, self.width * .65f / (self.headers.Count - 1));
        var nameWidth = Math.Max(20, self.width - (self.headers.Count - 1) * (numberWidth + 4));
        for (int i = 0; i < self.headers.Count; i++) { self.headers[i].CustomMinimumSize = new(i == 0 ? nameWidth : numberWidth, 0); self.headers[i].Size = new(i == 0 ? nameWidth : numberWidth, 0); self.headers[i].HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right; }
        for (int i = 0; i < self.sources.Count; i++)
        {
            var row = self.sources[i]; var source = card.Sources[i]; row.Row.Visible = true; self.Set(row.Name, source.Name); row.Name.CustomMinimumSize = new(nameWidth, 0); row.Name.Size = new(nameWidth, 0);
            for (int c = 0; c < row.Values.Length; c++)
            {
                self.Set(row.Values[c], source.Values[c].Text, source.Values[c].Tone); row.Values[c].CustomMinimumSize = new(numberWidth, 0); row.Values[c].Size = new(numberWidth, 0);
                int fitted = fontSize; while (fitted > 10 && font.GetStringSize(source.Values[c].Text, HorizontalAlignment.Left, -1, fitted).X > numberWidth) fitted--; row.Values[c].AddThemeFontSizeOverride("font_size", fitted);
            }
        }
        self.Set(self.more, card.Sources.Count == 0 ? "No matching damage recorded" : ""); self.more.Visible = self.more.Text.Length > 0;
        self.content.QueueSort();
    }
}
