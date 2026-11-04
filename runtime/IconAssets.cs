using Godot;

namespace Based.Spires2DamageMeter.Runtime;

internal static class IconAssets
{
    private static readonly Dictionary<string, ImageTexture> textures = new();

    public static ImageTexture Get(string name)
    {
        if (textures.TryGetValue(name, out var texture)) return texture;
        using var stream = typeof(IconAssets).Assembly.GetManifestResourceStream("MeterIcon." + name + ".png")
            ?? throw new InvalidOperationException("Missing meter icon: " + name);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        using var source = new Image();
        if (source.LoadPngFromBuffer(bytes.ToArray()) != Error.Ok)
            throw new InvalidOperationException("Invalid meter icon: " + name);
        // Render the original transparent asset at a consistent UI size without stretching.
        source.Convert(Image.Format.Rgba8);
        var pixels = source.GetData();
        var width = source.GetWidth(); var height = source.GetHeight();
        var left = width; var top = height; var right = -1; var bottom = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (pixels[(y * width + x) * 4 + 3] >= 64)
                { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        if (right < left) throw new InvalidOperationException("Empty meter icon: " + name);
        // Ignore near-invisible generated edge haze when measuring the displayed silhouette.
        using var cropped = source.GetRegion(new Rect2I(left, top, right - left + 1, bottom - top + 1));
        var scale = 20d / Math.Max(cropped.GetWidth(), cropped.GetHeight());
        cropped.Resize(Math.Max(1, (int)Math.Round(cropped.GetWidth() * scale)),
            Math.Max(1, (int)Math.Round(cropped.GetHeight() * scale)), Image.Interpolation.Lanczos);
        using var canvas = Image.CreateEmpty(20, 20, false, Image.Format.Rgba8);
        canvas.BlitRect(cropped, new Rect2I(0, 0, cropped.GetWidth(), cropped.GetHeight()),
            new Vector2I((20 - cropped.GetWidth()) / 2, (20 - cropped.GetHeight()) / 2));
        texture = ImageTexture.CreateFromImage(canvas);
        texture.ResourceName = name;
        textures.Add(name, texture);
        return texture;
    }

    public static void Apply(Button button, string name)
    {
        button.Text = "";
        button.Icon = Get(name);
    }

    public static void Dispose()
    {
        foreach (var texture in textures.Values) texture.Dispose();
        textures.Clear();
    }
}
