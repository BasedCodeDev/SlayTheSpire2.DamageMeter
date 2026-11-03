using System.Reflection;
using System.Security.Cryptography;
namespace Based.Spires2DamageMeter;
public sealed record ModifierContract(string Hash,string Strategy);
public static class ModifierContracts
{
 public static IReadOnlyList<string> MissingStateAccessors(Assembly game)
 {
  var missing=new List<string>();
  const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
  foreach(var name in new[]{"TheBoot","TungstenRod","BeatingRemnant"})
  {
   var type=game.GetType("MegaCrit.Sts2.Core.Models.Relics."+name);
   var vars=type?.GetProperty("DynamicVars",flags)?.PropertyType;
   var item=vars?.GetProperty("Item",new[]{typeof(string)});
   if(item?.PropertyType.GetProperty("BaseValue",flags)==null)missing.Add(name+" dynamic state accessor");
   if(name=="BeatingRemnant"&&type?.GetProperty("DamageReceivedThisTurn",flags)==null)missing.Add(name+" turn budget");
  }
  foreach(var name in new[]{"WeakPower","VulnerablePower"})
  {
   var vars=game.GetType("MegaCrit.Sts2.Core.Models.Powers."+name)?.GetProperty("DynamicVars",flags)?.PropertyType;
   if(vars?.GetProperty("Item",new[]{typeof(string)})?.PropertyType.GetProperty("BaseValue",flags)==null)missing.Add(name+" multiplier seed accessor");
  }
  var shell=game.GetType("MegaCrit.Sts2.Core.Models.Powers.HardenedShellPower");
  if(shell?.GetNestedType("Data",BindingFlags.Public|BindingFlags.NonPublic)?.GetField("damageReceivedThisTurn",flags)==null)missing.Add("HardenedShell turn budget");
  return missing;
 }
 public static readonly IReadOnlyDictionary<string,ModifierContract> Entries=new Dictionary<string,ModifierContract>
 {
  ["MegaCrit.Sts2.Core.Models.Relics.PaperKrane.ModifyWeakMultiplier"]=new("F13679EB8A2AB8584CC2CD65C20AA54245B6D87DFFA4C80152FABD6110469381","multiplier_chain_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.PaperPhrog.ModifyVulnerableMultiplier"]=new("830866694AA2EC76109F957018557FFA06CFB9D28E6FB804C289818B4F061D44","multiplier_chain_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.CrueltyPower.ModifyVulnerableMultiplier"]=new("C3C151C144D9F74471CF2B170F3CD1C1B44CBD66A744A7101788140829A7E152","multiplier_chain_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.DebilitatePower.ModifyVulnerableMultiplier"]=new("52E4DF8A1A21D872FB6B350136856153C8FE77972EDBC85E0ABD595186AD51A5","multiplier_chain_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.DebilitatePower.ModifyWeakMultiplier"]=new("B4B9D1A41A849E51EFF612DA079925BB647FF73F31194883BC58F14EE13C34E9","multiplier_chain_snapshot"),

  ["MegaCrit.Sts2.Core.Models.AbstractModel.ModifyDamageAdditive"]=new("75CEF9C6DAB579F822993D013203B8FE1A3B2719F56A7F50511D6BE47CAEE57E","audited_default"),
  ["MegaCrit.Sts2.Core.Models.AbstractModel.ModifyDamageCap"]=new("DE90B1D072A06D3770965738AA877482E53A0557AEA74B6E6FECF9BE4E283962","audited_default"),
  ["MegaCrit.Sts2.Core.Models.AbstractModel.ModifyDamageMultiplicative"]=new("0B5D7388002E757187C875C40275DBA5DF3A23E2805A2DADE830B669B318220D","audited_default"),
  ["MegaCrit.Sts2.Core.Models.AbstractModel.ModifyHpLostAfterOsty"]=new("F027062EC4624F5B91851DD4C9641248E4094C2EE638D64D77B03FDD88DA7E43","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.AbstractModel.ModifyHpLostAfterOstyLate"]=new("F027062EC4624F5B91851DD4C9641248E4094C2EE638D64D77B03FDD88DA7E43","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.AbstractModel.ModifyHpLostBeforeOsty"]=new("F027062EC4624F5B91851DD4C9641248E4094C2EE638D64D77B03FDD88DA7E43","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.AbstractModel.ModifyHpLostBeforeOstyLate"]=new("F027062EC4624F5B91851DD4C9641248E4094C2EE638D64D77B03FDD88DA7E43","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.AbstractModel.ModifyUnblockedDamageTarget"]=new("1E96E7625241E9E19A5636AF81D0857A06ADC75F90D1CE457C4CAFECA1525F3A","incoming_only_routing"),
  ["MegaCrit.Sts2.Core.Models.EnchantmentModel.EnchantDamageAdditive"]=new("75CEF9C6DAB579F822993D013203B8FE1A3B2719F56A7F50511D6BE47CAEE57E","audited_default"),
  ["MegaCrit.Sts2.Core.Models.EnchantmentModel.EnchantDamageMultiplicative"]=new("0B5D7388002E757187C875C40275DBA5DF3A23E2805A2DADE830B669B318220D","audited_default"),
  ["MegaCrit.Sts2.Core.Models.Enchantments.Corrupted.EnchantDamageMultiplicative"]=new("5AEE2B5A4F2E3B4325D4FA713FFE4DD91B3C606952F035FC7DA021EE2CB91908","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Enchantments.Inky.EnchantDamageAdditive"]=new("F660D8AA2C14AE7C324AC8D5E04E6702A95A4E52E0C1C1734E77B7A6E08F57B3","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Enchantments.Instinct.EnchantDamageMultiplicative"]=new("39E49EAE223524288C0B365740184C1DDFF713D45A644EF69FD20DCF001AC24C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Enchantments.Momentum.EnchantDamageAdditive"]=new("24BB10FED990241B396989D6E026F1BAE468C6453E4E0AE8C8F01576096A1808","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Enchantments.Sharp.EnchantDamageAdditive"]=new("E3D9CEF4CBB8A6F3CCC135C695038331E484B9392F1E0D405B210832DA6F97D6","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Enchantments.TezcatarasEmber.EnchantDamageAdditive"]=new("F660D8AA2C14AE7C324AC8D5E04E6702A95A4E52E0C1C1734E77B7A6E08F57B3","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Enchantments.Vigorous.EnchantDamageAdditive"]=new("73FEAD8F10F712B42254F2DC583209F521B959D31E3F2EB59591D13401E5FB69","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.AccuracyPower.ModifyDamageAdditive"]=new("6FCC3CA518AF80B3E1E0E4A3A174E827EE84B147FDFABA8527920CFFF3BCDA8C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.BufferPower.ModifyHpLostAfterOstyLate"]=new("1701B5877D9240FE16E250492B4C5ACBC4944BCBE4CB722CF88C3E7C61C53122","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.CalcifyPower.ModifyDamageAdditive"]=new("D6A88E4F9D521AF0AFFFDA8D5A91A16D6CD70E4E9A66F87176616B2821963DB7","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.ColossusPower.ModifyDamageMultiplicative"]=new("5E3774779763EC1E4C60833834F1B170AEBDC1DB36B41EA2E3CA2DA565731F24","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.ConquerorPower.ModifyDamageMultiplicative"]=new("3D1AE19EE81E38F983D749F44A1927C312EA0AC32A520D09EC82BF610D76BAA7","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.CoveredPower.ModifyDamageMultiplicative"]=new("99D256B4431B8851A70B38BDED5C1DE3DA7CCFA87118EA316437F28F19368743","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.DiamondDiademPower.ModifyDamageMultiplicative"]=new("BD89402F9D58AC458037B84DCB04015AC850ED135CD9F6F37DC48B62CC8CEE3C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.DieForYouPower.ModifyUnblockedDamageTarget"]=new("3A9C6109BCBBA6242E2C19C4BF2F4990F5CD43093351E4BF3A46B57D467EEA10","incoming_only_routing"),
  ["MegaCrit.Sts2.Core.Models.Powers.DoubleDamagePower.ModifyDamageMultiplicative"]=new("3A4259E5212E3EF9C47F5A33166F60E48855A070494C1FB07AC84CB31459D88C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.FlankingPower.ModifyDamageMultiplicative"]=new("63B9FCF2B9FC44B4F7A062D0BD4758B52CCA1D535EA1D6DD9A783FF7193A592C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.FlutterPower.ModifyDamageMultiplicative"]=new("B71D090C2CFB82F344754C79CBA1D029AED806D692B093E8956573AB5CA24074","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.GigantificationPower.ModifyDamageMultiplicative"]=new("FF27FEE0765BD89CB518C9E07C5890CD9DB0B0E8BCC7080A38C3DA6DCEBF7840","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.GuardedPower.ModifyDamageMultiplicative"]=new("BD89402F9D58AC458037B84DCB04015AC850ED135CD9F6F37DC48B62CC8CEE3C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.HangPower.ModifyDamageMultiplicative"]=new("F6BAF48B5299C837E0BD5CDBE7D10147969525399F98D56AD691C4769C698ED1","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.HardToKillPower.ModifyDamageCap"]=new("BA967754C20167022F91657B3A4E8BEA81CF0B189A4C9E21B8D42C64B1BEB603","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.HardenedShellPower.ModifyHpLostBeforeOstyLate"]=new("7574C69A65F0874D62CBB3A4D4CB4E58F13BE3FAD00A70362C4270A9EB964967","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.IntangiblePower.ModifyDamageCap"]=new("886C3D15BAAF777506253F2FD6055080B930120E5583C4EEC3F4AD81675C53DE","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.IntangiblePower.ModifyHpLostAfterOsty"]=new("ADE66A430EA257681939460A33BC20BBA007D3EB0B928D3A25EE37893601CBFA","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.InterceptPower.ModifyDamageMultiplicative"]=new("4F88BB324C725176060024D0A532E6ED01A1C0C46D74E902EA9A080794A8D96E","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.KnockdownPower.ModifyDamageMultiplicative"]=new("63B9FCF2B9FC44B4F7A062D0BD4758B52CCA1D535EA1D6DD9A783FF7193A592C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.LeadershipPower.ModifyDamageAdditive"]=new("9A767A3A0694C40C091C6617CFD26188C654F3A02C1D80C5483B91F2D458CB7B","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.LethalityPower.ModifyDamageMultiplicative"]=new("7D5DF9E1367FAEE52B6CE40DB2721CAC4C8D94DE58D0FA0496370F1224ECEC6F","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.Mocks.MockRevivePower.ModifyDamageMultiplicative"]=new("13239967CB3FD14165EE6D1CDDCC4B0F19C9E819A1CA004C17C74E97CFE00AD8","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.PhantomBladesPower.ModifyDamageAdditive"]=new("79B47950072D74F3EE68455477D3DE9FBAB051097693F05BFBA0B2A9144B0D8C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.ShrinkPower.ModifyDamageMultiplicative"]=new("0180DE5146C9C580AC81738F914C111314178131B6F53B2CC1F5530B6251E3D5","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.SlipperyPower.ModifyHpLostAfterOsty"]=new("A7FB63FC30F7C10230406FA7008AC4E1957DF128E72CE1526504BAA4F286A8DB","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.SlowPower.ModifyDamageMultiplicative"]=new("9BD9BAA58758F150D1F2F8A8D21679B8B394853A468D88BC1EA81ADF528C6806","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.SoarPower.ModifyDamageMultiplicative"]=new("B71D090C2CFB82F344754C79CBA1D029AED806D692B093E8956573AB5CA24074","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.StrengthPower.ModifyDamageAdditive"]=new("0F677F415A6BF82C64D5A201ED393D799BF7C17095974F5D3330204E5D066734","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.SurroundedPower.ModifyDamageMultiplicative"]=new("A165575268B00D0D4563F4C65E59781BC9BF454DE3111027A39BD1F7DF2F743C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.TaintedPower.ModifyDamageAdditive"]=new("65D459F31E3E9878429B9F912CFB97B9DA76D70371E5D30A6EB2841399CEEBE9","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.TankPower.ModifyDamageMultiplicative"]=new("AF59F63D8687A1074BCC76E369452E354D9E8EB03B8F1B3D0C952078B4919D26","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.TrackingPower.ModifyDamageMultiplicative"]=new("4DEE1A63697970F490CF866F15AEE1BE37D867BC930491727751C4E5981AD848","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.VigorPower.ModifyDamageAdditive"]=new("5DDB2734379E09A82C6FC0FD2F609C9E5013E81AF7CD38E9A4C12D40A6C4D6F5","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.VulnerablePower.ModifyDamageMultiplicative"]=new("2AA0443834CDB912CA3CDAD4D4A34DBE7CA61E86D10CD0942AD9DD70738039CA","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Powers.WeakPower.ModifyDamageMultiplicative"]=new("4A2D7E631A487E3E811F5701804AD79BAD031E60860A8783D4097D1D674D129C","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.BeatingRemnant.ModifyHpLostAfterOsty"]=new("E65F62045DCDDE4B50CE415D40ED20F7A1B64BAB185102C9F9D57816B61249D1","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.FakeStrikeDummy.ModifyDamageAdditive"]=new("94C62A1FF64F618D32CE1F440B04E643B34336D9289CC2B231D4C7BC07AD7F0A","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.MiniatureCannon.ModifyDamageAdditive"]=new("3C5895B80EFFB61B00BF0D904CF12DBAD9D873A7A20A749F591D01BF6BB51593","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.MysticLighter.ModifyDamageAdditive"]=new("0FBFE384F37BFA7D665B635B9837E35569848095D32E9615B47B00F37D2FFBBC","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.PenNib.ModifyDamageMultiplicative"]=new("437A52CA286C2A947D66228DF996516C0AEE5602927B6BECAEBFA515804A23FA","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.StrikeDummy.ModifyDamageAdditive"]=new("94C62A1FF64F618D32CE1F440B04E643B34336D9289CC2B231D4C7BC07AD7F0A","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.TheBoot.ModifyHpLostAfterOstyLate"]=new("B3672FFFB27AEEDE83027F8A8F25353C23C76F0C93AA2792A02144E9CEF5BDA9","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.TungstenRod.ModifyHpLostAfterOsty"]=new("BF80866BB94FC14A4A8D84785AD7A3EC68FFD5A7F8B1699C9CDA8DB8A226ECE5","ordered_hp_loss_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.UndyingSigil.ModifyDamageMultiplicative"]=new("9D330DC802144CE7E7BFDA3874836D163AE6F083DA13688768B77E057614D647","frozen_numeric_snapshot"),
  ["MegaCrit.Sts2.Core.Models.Relics.VitruvianMinion.ModifyDamageMultiplicative"]=new("BE15DBC955818471792B00551EB391C49EBE7AF89607CAFCC310FA6E13D3E8CD","frozen_numeric_snapshot"),
 };
 private static readonly System.Collections.Concurrent.ConcurrentDictionary<MethodInfo,bool> cache=new();
 public static bool Supported(MethodInfo m)=>cache.GetOrAdd(m,method=>method.Module.ModuleVersionId.ToString()==HpLossAdapterAudit.ModuleId&&Entries.TryGetValue(method.DeclaringType!.FullName+"."+method.Name,out var c)&&Convert.ToHexString(SHA256.HashData(method.GetMethodBody()!.GetILAsByteArray()!))==c.Hash);
}
