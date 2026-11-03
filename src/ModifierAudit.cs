using System.Reflection;
using System.Reflection.Emit;
namespace Based.Spires2DamageMeter;

public static class ModifierAudit
{
    private static readonly Dictionary<ushort,OpCode> codes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
    public static bool InputIndependent(MethodInfo method)
        => cache.GetOrAdd(method,Analyze);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<MethodInfo,bool> cache=new();
    private static bool Analyze(MethodInfo method)
    {
        var parameter=Array.FindIndex(method.GetParameters(),p=>p.Name=="amount");
        if(parameter<0 && method.Name is "EnchantDamageAdditive" or "EnchantDamageMultiplicative")parameter=Array.FindIndex(method.GetParameters(),p=>p.ParameterType==typeof(decimal));
        // Damage caps have no incoming amount in this installed API; other unknown shapes fail closed.
        if(parameter<0)return method.Name=="ModifyDamageCap" && method.ReturnType==typeof(decimal) && method.GetParameters().Length==4 && method.GetParameters().All(p=>p.ParameterType!=typeof(decimal));
        var argument=parameter+(method.IsStatic?0:1); var body=method.GetMethodBody()?.GetILAsByteArray();if(body==null)return false;
        for(int i=0;i<body.Length;)
        {
            ushort value=body[i++];if(value==0xfe)value=(ushort)(0xfe00|body[i++]);var op=codes[value];
            var loaded=op.Name switch{"ldarg.0"=>0,"ldarg.1"=>1,"ldarg.2"=>2,"ldarg.3"=>3,"ldarg.s" or "ldarga.s"=>body[i],"ldarg" or "ldarga"=>BitConverter.ToUInt16(body,i),_=>-1};
            if(loaded==argument)return false;
            i+=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(body,i),_=>4};
        }
        return true;
    }
    public static IReadOnlyList<string> Dependencies(MethodInfo method)
    {
        var result=new SortedSet<string>(StringComparer.Ordinal);
        var body=method.GetMethodBody()?.GetILAsByteArray();if(body==null)return new[]{"unavailable IL"};
        for(int i=0;i<body.Length;)
        {
            ushort value=body[i++];if(value==0xfe)value=(ushort)(0xfe00|body[i++]);var op=codes[value];
            if(op.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok)
            {
                try { var member=method.Module.ResolveMember(BitConverter.ToInt32(body,i),method.DeclaringType?.GetGenericArguments(),method.GetGenericArguments());result.Add(member?.DeclaringType?.FullName+"."+member); }
                catch(ArgumentException){result.Add("unresolved token "+BitConverter.ToInt32(body,i));}
            }
            i+=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(body,i),_=>4};
        }
        return result.ToArray();
    }
}
