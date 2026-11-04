using Godot;
using MegaCrit.Sts2.Core.Runs;
using static Based.Spires2DamageMeter.MeterBar;
using static Godot.Control;
namespace Based.Spires2DamageMeter.Runtime;

internal static class MeterBarLogic
{
    public static void _Draw(MeterBar self)
    {
        self.trackStyle ??= CreateStyle(new(.3f, .34f, .4f, .16f), 6, 0);
        if (self.hpStyle == null || self.hpStyle.BgColor != WithAlpha(self.HpColour, .94f)) self.hpStyle = CreateStyle(WithAlpha(self.HpColour, .94f), 6, 5);
        if (self.ampStyle == null || self.ampStyle.BgColor != WithAlpha(self.AmpColour, .94f)) self.ampStyle = CreateStyle(WithAlpha(self.AmpColour, .94f), 6, 5);
        self.glossStyle ??= CreateStyle(new(1, 1, 1, .11f), 5, 0);
        var barRect = new Rect2(0, 3, self.Size.X, Math.Max(0, self.Size.Y - 6));
        self.DrawStyleBox(self.trackStyle, barRect);
        var frame = BarAnimations.Frame(self);
        if (frame.End > 0)
        {
            var hp = self.Size.X * (float)frame.Base;
            var amp = self.Size.X * (float)(frame.End - frame.Base);
            if (hp > 0) self.DrawStyleBox(self.hpStyle, new Rect2(0, 3, hp, barRect.Size.Y));
            if (amp > 0) self.DrawStyleBox(self.ampStyle, new Rect2(hp, 3, amp, barRect.Size.Y));
            var filledWidth = hp + amp;
            if (filledWidth > 12) self.DrawStyleBox(self.glossStyle, new Rect2(2, 4, filledWidth - 4, Math.Max(0, barRect.Size.Y * .32f)));
            var pulse = BarAnimations.Pulse(self);
            if (pulse > 0 && filledWidth > 8) self.DrawRect(new Rect2(4, 5, filledWidth - 8, Math.Max(0,barRect.Size.Y - 4)),new Color(1,1,1,pulse));
        }
        self.DrawRowText();
    }
    public static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
    public static StyleBoxFlat CreateStyle(Color color, int radius, int glowSize)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color,
            ShadowColor = new Color(color.R, color.G, color.B, .3f),
            ShadowSize = glowSize
        };
        style.SetCornerRadiusAll(radius);
        return style;
    }
    public static void DrawRowText(MeterBar self)
    {
        if (self.Size.X <= 0) return;
        var font = self.GetThemeDefaultFont(); var fontSize = Math.Clamp(self.GetThemeDefaultFontSize(), 10, 15);
        var damage = BarAnimations.Text(self);
        while (fontSize > 10 && font.GetStringSize(damage, HorizontalAlignment.Left, -1, fontSize).X + 8 > self.Size.X) fontSize--;
        var damageWidth = font.GetStringSize(damage, HorizontalAlignment.Left, -1, fontSize).X;
        var damageX = (self.Size.X - damageWidth) / 2;
        var y = (self.Size.Y - font.GetHeight(fontSize)) / 2 + font.GetAscent(fontSize);
        if (damageWidth + 8 > self.Size.X) return;
        var originalName = self.ShowPlayerName ? self.PlayerName : "";
        var name = originalName;
        var reservedWidth = Math.Max(damageWidth, font.GetStringSize(BarAnimations.Text(self, true), HorizontalAlignment.Left,-1,fontSize).X);
        reservedWidth = Math.Max(reservedWidth, font.GetStringSize(BarAnimations.StartText(self),HorizontalAlignment.Left,-1,fontSize).X);
        var nameWidth = Math.Max(0, (self.Size.X - reservedWidth) / 2 - 12);
        while (name.Length > 0 && font.GetStringSize(name, HorizontalAlignment.Left, -1, fontSize).X > nameWidth)
            name = name[..^1];
        if (name != originalName)
        {
            while (name.Length > 0 && font.GetStringSize(name + "\u2026", HorizontalAlignment.Left, -1, fontSize).X > nameWidth)
                name = name[..^1];
            if (name.Length > 0) name += "\u2026";
        }
        if (name.Length > 0) self.DrawOutlined(font, new Vector2(4, y), name, fontSize);
        self.DrawOutlined(font, new Vector2(damageX, y), damage, fontSize);
    }
    public static void DrawOutlined(MeterBar self, Font font, Vector2 position, string text, int fontSize)
    {
        self.DrawStringOutline(font, position, text, HorizontalAlignment.Left, -1, fontSize, 3, Colors.Black);
        self.DrawString(font, position, text, HorizontalAlignment.Left, -1, fontSize, Colors.White);
    }
}
