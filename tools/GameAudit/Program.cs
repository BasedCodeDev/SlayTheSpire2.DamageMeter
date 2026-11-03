using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
var dir=Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving+=(_,n)=>File.Exists(Path.Combine(dir,n.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,n.Name+".dll")):null;
var game=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir,"sts2.dll"));
if(args.Length>1 && args[1]=="--manifest")
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{gameVersion="v0.107.1",assemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(game.Location))),scope="Effective AbstractModel callbacks including inherited implementations; direct IL dependencies only",entries=Based.Spires2DamageMeter.ModifierManifest.Build(game.GetType("MegaCrit.Sts2.Core.Models.AbstractModel")!)},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    return;
}
if(args[1]=="--chain-contracts")
{
 foreach(var t in game.GetTypes())foreach(var m in t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>m.Name is "ModifyWeakMultiplier" or "ModifyVulnerableMultiplier"))
 Console.WriteLine($"  [\"{t.FullName}.{m.Name}\"]=new(\"{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(m.GetMethodBody()!.GetILAsByteArray()!))}\",\"multiplier_chain_snapshot\"),");
 return;
}
if(args[1]=="--additive-metadata")
{
 var tag=game.GetType("MegaCrit.Sts2.Core.Entities.Cards.CardTag")!;Console.WriteLine("Tag5="+Enum.GetName(tag,5));
 var phantom=game.GetType("MegaCrit.Sts2.Core.Models.Powers.PhantomBladesPower")!;
 foreach(var m in phantom.GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(m=>m.Name.Contains("b__")))Console.WriteLine(m.Name+" "+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(m.GetMethodBody()!.GetILAsByteArray()!)));
 return;
}
if(args[1]=="--attack-metadata")
{
 Console.WriteLine("Pile5="+Enum.GetName(game.GetType("MegaCrit.Sts2.Core.Entities.Cards.PileType")!,5));
 Console.WriteLine("Card1="+Enum.GetName(game.GetType("MegaCrit.Sts2.Core.Entities.Cards.CardType")!,1));
 var t=game.GetType("MegaCrit.Sts2.Core.Models.Powers.LethalityPower")!;
 foreach(var m in t.GetMethods(BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>m.Name.Contains("b__")))Console.WriteLine(m.Name+" "+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(m.GetMethodBody()!.GetILAsByteArray()!)));
 return;
}
var opcodes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
foreach(var type in game.GetTypes().Where(t=>t.FullName==args[1] || (args[1]=="powers" && t.Namespace=="MegaCrit.Sts2.Core.Models.Powers")))
foreach(var m in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(m=>m.Name.Contains(args[2])))
{
 Console.WriteLine(type.Name+" "+m);
 var body=m.GetMethodBody()?.GetILAsByteArray(); if(body==null)continue;
 for(int i=0;i<body.Length;)
 {
  var offset=i; ushort code=body[i++]; if(code==0xfe)code=(ushort)(0xfe00|body[i++]); var op=opcodes[code];
  int size=op.OperandType switch { OperandType.InlineNone=>0, OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(body,i),_=>4};
  string operand="";
  if(size==4 && op.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)try{operand=m.Module.ResolveMember(BitConverter.ToInt32(body,i),type.GetGenericArguments(),m.GetGenericArguments())?.ToString()??"";}catch{}
  else if(op.OperandType==OperandType.InlineString)operand=m.Module.ResolveString(BitConverter.ToInt32(body,i));
  else if(size>0)operand=Convert.ToHexString(body.AsSpan(i,size));
  Console.WriteLine($" {offset:x4} {op.Name} {operand}");i+=size;
 }
}
