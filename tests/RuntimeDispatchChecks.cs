using System.Reflection;
using System.Reflection.Emit;
using Based.Spires2DamageMeter.Hosting;
using Based.Spires2DamageMeter.Reload;

/// <summary>Inspect executable call sites/routes without executing game callbacks.</summary>
static class RuntimeDispatchChecks
{
    private sealed record Instruction(OpCode Code, object? Operand);
    private static readonly Dictionary<ushort, OpCode> opcodes = typeof(OpCodes).GetFields()
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(c => unchecked((ushort)c.Value));

    public static void Run(Type module, Action<bool, string> check)
    {
        var required = RequiredOperations(typeof(RuntimeHost).Assembly, typeof(RuntimeCalls).Assembly);
        var supported = Routes(module.GetMethod("Invoke")!);
        supported.UnionWith(Routes(module.Assembly.GetType("Based.Spires2DamageMeter.Runtime.Operations", true)!.GetMethod("Invoke")!));
        Validate(required, supported);
        check(required.Contains("Probe.HandleHook") && supported.Contains("Probe.HandleHook"), "compiled host and runtime both declare combat hook dispatch");
        check(required.Count > 100, $"all {required.Count} compiled host/contract operation keys have executable runtime routes");
        var dispatcher = module.Assembly.GetType("Based.Spires2DamageMeter.Runtime.Operations", true)!.GetMethod("Invoke")!;
        try
        {
            dispatcher.Invoke(null, ["Fixture.UnknownOperation", Array.Empty<object?>()]);
            throw new Exception("Unknown operation unexpectedly accepted");
        }
        catch (TargetInvocationException e) when (e.InnerException is MissingMethodException missing && missing.Message == "Fixture.UnknownOperation")
        {
            check(true, "unknown runtime operations remain explicit dispatch failures");
        }
        var incomplete = Routes(typeof(RuntimeDispatchChecks).GetMethod(nameof(IncompleteInvoke), BindingFlags.NonPublic | BindingFlags.Static)!);
        try
        {
            Validate(new HashSet<string> { "Probe.HandleHook" }, incomplete);
            throw new Exception("Missing-route fixture unexpectedly passed");
        }
        catch (InvalidDataException e)
        {
            check(e.Message.Contains("Probe.HandleHook"), "missing combat route fails dispatch coverage before packaging");
        }
        try
        {
            ReadRequired(typeof(RuntimeDispatchChecks).GetMethod(nameof(DynamicBoundary), BindingFlags.NonPublic | BindingFlags.Static)!);
            throw new Exception("Dynamic boundary unexpectedly passed");
        }
        catch (InvalidDataException)
        {
            check(true, "unverifiable dynamic operation keys fail dispatch coverage explicitly");
        }
        try
        {
            ReadRequired(typeof(RuntimeDispatchChecks).GetMethod(nameof(MisleadingBoundary), BindingFlags.NonPublic | BindingFlags.Static)!);
            throw new Exception("Literal argument was mistaken for an operation key");
        }
        catch (InvalidDataException) { check(true, "literal arguments cannot disguise a dynamic operation key"); }
    }

    private static object? IncompleteInvoke(string operation, object?[] values)
    {
        if (operation == "Probe.HookPrefix") return null;
        throw new MissingMethodException(operation);
    }
    private static object? DynamicBoundary(string operation) => RuntimeCalls.Call(operation);
    private static object? MisleadingBoundary(string operation) => RuntimeCalls.Call(operation, "Probe.HandleHook");

    private static void Validate(HashSet<string> required, HashSet<string> supported)
    {
        var missing = required.Except(supported).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0) throw new InvalidDataException("Missing runtime dispatch routes: " + string.Join(", ", missing));
    }

    private static HashSet<string> RequiredOperations(params Assembly[] assemblies)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in assemblies.Distinct().SelectMany(a => a.GetTypes()).Where(t => t != typeof(RuntimeCalls)))
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
                keys.UnionWith(ReadRequired(method));
        }
        return keys;
    }

    private static string[] ReadRequired(MethodBase method)
    {
        var body = Read(method);
        var calls = body.Where(i => i.Operand is MethodBase m && m.DeclaringType == typeof(RuntimeCalls) && m.Name == nameof(RuntimeCalls.Call)).ToArray();
        if (calls.Length == 0) return [];
        // Current bridges each make one dispatch with one literal key. Fail closed
        // when their shape changes, rather than guessing a dynamic operation name.
        var literals = body.Where(i => i.Code == OpCodes.Ldstr).Select(i => (string)i.Operand!).ToArray();
        if (calls.Length != 1 || literals.Length != 1)
            throw new InvalidDataException("Cannot verify dispatch operation in " + method.DeclaringType + "." + method.Name);
        // Track the evaluation stack to verify the literal is the operation
        // argument itself, rather than a string contained in its argument array.
        var stack = new List<string?>();
        foreach (var instruction in body)
        {
            if (instruction.Code.FlowControl == FlowControl.Return) { stack.Clear(); continue; }
            if (instruction.Code == OpCodes.Dup)
            {
                if (stack.Count == 0) throw new InvalidDataException("Cannot verify stack in " + method);
                stack.Add(stack[^1]); continue;
            }
            if (instruction.Operand is MethodBase called && instruction.Code.FlowControl == FlowControl.Call)
            {
                var count = called.GetParameters().Length + (!called.IsStatic && instruction.Code != OpCodes.Newobj ? 1 : 0);
                if (called.DeclaringType == typeof(RuntimeCalls) && called.Name == nameof(RuntimeCalls.Call))
                {
                    if (stack.Count < count || stack[^count] != literals[0])
                        throw new InvalidDataException("Nonliteral dispatch operation in " + method);
                    return literals;
                }
                Pop(count);
                if (instruction.Code == OpCodes.Newobj || called is MethodInfo info && info.ReturnType != typeof(void)) stack.Add(null);
                continue;
            }
            var pop = instruction.Code.StackBehaviourPop switch
            {
                StackBehaviour.Pop0 => 0,
                StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
                StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8 or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8 or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
                StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_pop1 or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8 or StackBehaviour.Popref_popi_popr4 or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref => 3,
                _ => throw new InvalidDataException("Unsupported dispatch IL " + instruction.Code + " in " + method)
            };
            Pop(pop);
            if (instruction.Code.StackBehaviourPush != StackBehaviour.Push0)
                stack.Add(instruction.Code == OpCodes.Ldstr ? (string)instruction.Operand! : null);
        }
        throw new InvalidDataException("Dispatch call not reached in " + method);

        void Pop(int count)
        {
            if (stack.Count < count) throw new InvalidDataException("Cannot verify dispatch stack in " + method);
            stack.RemoveRange(stack.Count - count, count);
        }
    }

    private static HashSet<string> Routes(MethodInfo method)
    {
        var body = Read(method);
        var argument = method.IsStatic ? 0 : 1;
        var aliases = new HashSet<int>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        bool IsOperation(Instruction instruction) => Argument(instruction) == argument || Local(instruction, false) is int l && aliases.Contains(l);
        for (var i = 1; i < body.Count; i++)
            if (IsOperation(body[i - 1]) && Local(body[i], true) is int local) aliases.Add(local);
        for (var i = 1; i + 1 < body.Count; i++)
        {
            if (body[i].Code != OpCodes.Ldstr || body[i + 1].Operand is not MethodInfo comparison ||
                comparison.DeclaringType != typeof(string) || comparison.Name != "op_Equality") continue;
            if (!IsOperation(body[i - 1]))
                throw new InvalidDataException("Cannot verify runtime route comparison in " + method);
            keys.Add((string)body[i].Operand!);
        }
        if (keys.Count == 0) throw new InvalidDataException("No verifiable runtime routes in " + method);
        return keys;
    }

    private static int? Argument(Instruction i) => i.Code.Value switch
    {
        0x02 => 0, 0x03 => 1, 0x04 => 2, 0x05 => 3,
        0x0e or unchecked((short)0xfe09) => Convert.ToInt32(i.Operand), _ => null
    };
    private static int? Local(Instruction i, bool store)
    {
        if (store)
            return i.Code.Value switch { 0x0a => 0, 0x0b => 1, 0x0c => 2, 0x0d => 3, 0x13 or unchecked((short)0xfe0e) => Convert.ToInt32(i.Operand), _ => null };
        return i.Code.Value switch { 0x06 => 0, 0x07 => 1, 0x08 => 2, 0x09 => 3, 0x11 or unchecked((short)0xfe0c) => Convert.ToInt32(i.Operand), _ => null };
    }

    private static List<Instruction> Read(MethodBase method)
    {
        var bytes = method.GetMethodBody()?.GetILAsByteArray();
        var result = new List<Instruction>();
        if (bytes == null) return result;
        for (var offset = 0; offset < bytes.Length;)
        {
            ushort code = bytes[offset++];
            if (code == 0xfe) code = (ushort)(0xfe00 | bytes[offset++]);
            var op = opcodes[code];
            var size = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset), _ => 4
            };
            object? operand = null;
            if (op.OperandType == OperandType.InlineString) operand = method.Module.ResolveString(BitConverter.ToInt32(bytes, offset));
            else if (op.OperandType == OperandType.InlineMethod)
                operand = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset), method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
            else if (op.OperandType == OperandType.ShortInlineVar) operand = (int)bytes[offset];
            else if (op.OperandType == OperandType.InlineVar) operand = (int)BitConverter.ToUInt16(bytes, offset);
            result.Add(new(op, operand));
            offset += size;
        }
        return result;
    }
}
