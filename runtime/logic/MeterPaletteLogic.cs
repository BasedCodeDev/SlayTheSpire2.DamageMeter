using Godot;
using static Based.Spires2DamageMeter.MeterPalette;
using static Godot.Control;
namespace Based.Spires2DamageMeter.Runtime; internal static class MeterPaletteLogic { public static Color Colour(TooltipTone tone) => tone switch { TooltipTone.Hp => new(1f, .45f, .5f), TooltipTone.Block => new(.35f, .7f, 1f), TooltipTone.Bonus => new(1f, .65f, .3f), TooltipTone.MutedBlock => new(.5f, .6f, .7f), _ => Colors.White }; }