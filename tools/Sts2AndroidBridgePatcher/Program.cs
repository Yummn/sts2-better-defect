using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 2)
    throw new ArgumentException("usage: input-sts2.dll output-sts2.dll");

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
using var asm = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters
{
    ReadWrite = false,
    AssemblyResolver = resolver
});
var module = asm.MainModule;

TypeDefinition RequireType(string fullName) =>
    module.GetType(fullName) ?? throw new InvalidOperationException($"type not found: {fullName}");

var cardModel = RequireType("MegaCrit.Sts2.Core.Models.CardModel");
var choiceContext = RequireType("MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext");
var cardPlay = RequireType("MegaCrit.Sts2.Core.Entities.Cards.CardPlay");
var taskType = module.ImportReference(typeof(Task));

var onPlay = cardModel.Methods.Single(m =>
    m.Name == "OnPlay" &&
    m.Parameters.Count == 2 &&
    m.Parameters[0].ParameterType.FullName == choiceContext.FullName &&
    m.Parameters[1].ParameterType.FullName == cardPlay.FullName);

const string bridgeName = "MegaCrit.Sts2.Core.Modding.AndroidCardPlayBridge";
if (module.GetType(bridgeName) is not null)
    throw new InvalidOperationException($"{bridgeName} already exists");

var bridge = new TypeDefinition(
    "MegaCrit.Sts2.Core.Modding",
    "AndroidCardPlayBridge",
    TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
    module.TypeSystem.Object);
module.Types.Add(bridge);

// Do not use Func<CardModel, ..., Task> here. Android v103's AOT image does
// not contain that exact generic Invoke instantiation, so the first played
// card fails with MissingMethodException. A MethodInfo field plus the
// non-generic MethodBase.Invoke API works on the stock AOT runtime.
var methodInfoType = module.ImportReference(typeof(System.Reflection.MethodInfo));

var handlerField = new FieldDefinition(
    "Handler",
    FieldAttributes.Public | FieldAttributes.Static,
    methodInfoType);
bridge.Fields.Add(handlerField);

var dispatch = new MethodDefinition(
    "Dispatch",
    MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
    taskType);
dispatch.Parameters.Add(new ParameterDefinition("card", ParameterAttributes.None, cardModel));
dispatch.Parameters.Add(new ParameterDefinition("choiceContext", ParameterAttributes.None, choiceContext));
dispatch.Parameters.Add(new ParameterDefinition("cardPlay", ParameterAttributes.None, cardPlay));
bridge.Methods.Add(dispatch);

var objectType = module.TypeSystem.Object;
var invoke = module.ImportReference(typeof(System.Reflection.MethodBase).GetMethod(
    "Invoke",
    [typeof(object), typeof(object[])])
    ?? throw new MissingMethodException(typeof(System.Reflection.MethodBase).FullName, "Invoke"));

var dil = dispatch.Body.GetILProcessor();
var fallback = dil.Create(OpCodes.Ldarg_0);
var handlerReturnedNull = dil.Create(OpCodes.Pop);
var returnHandledTask = dil.Create(OpCodes.Ret);
dil.Append(dil.Create(OpCodes.Ldsfld, handlerField));
dil.Append(dil.Create(OpCodes.Brfalse_S, fallback));
dil.Append(dil.Create(OpCodes.Ldsfld, handlerField));
dil.Append(dil.Create(OpCodes.Ldnull));
dil.Append(dil.Create(OpCodes.Ldc_I4_3));
dil.Append(dil.Create(OpCodes.Newarr, objectType));
dil.Append(dil.Create(OpCodes.Dup));
dil.Append(dil.Create(OpCodes.Ldc_I4_0));
dil.Append(dil.Create(OpCodes.Ldarg_0));
dil.Append(dil.Create(OpCodes.Stelem_Ref));
dil.Append(dil.Create(OpCodes.Dup));
dil.Append(dil.Create(OpCodes.Ldc_I4_1));
dil.Append(dil.Create(OpCodes.Ldarg_1));
dil.Append(dil.Create(OpCodes.Stelem_Ref));
dil.Append(dil.Create(OpCodes.Dup));
dil.Append(dil.Create(OpCodes.Ldc_I4_2));
dil.Append(dil.Create(OpCodes.Ldarg_2));
dil.Append(dil.Create(OpCodes.Stelem_Ref));
dil.Append(dil.Create(OpCodes.Callvirt, invoke));
dil.Append(dil.Create(OpCodes.Castclass, taskType));
dil.Append(dil.Create(OpCodes.Dup));
dil.Append(dil.Create(OpCodes.Brfalse_S, handlerReturnedNull));
dil.Append(returnHandledTask);
dil.Append(handlerReturnedNull);
dil.Append(fallback);
dil.Append(dil.Create(OpCodes.Ldarg_1));
dil.Append(dil.Create(OpCodes.Ldarg_2));
dil.Append(dil.Create(OpCodes.Callvirt, onPlay));
dil.Append(dil.Create(OpCodes.Ret));
dispatch.Body.MaxStackSize = 8;

var stateMachine = cardModel.NestedTypes.Single(t =>
    t.Name.StartsWith("<OnPlayWrapper>d__", StringComparison.Ordinal));
var moveNext = stateMachine.Methods.Single(m => m.Name == "MoveNext" && m.Parameters.Count == 0);
var callSites = moveNext.Body.Instructions.Where(i =>
    (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
    i.Operand is MethodReference mr &&
    mr.Name == onPlay.Name &&
    mr.DeclaringType.FullName == cardModel.FullName &&
    mr.Parameters.Count == 2 &&
    mr.Parameters[0].ParameterType.FullName == choiceContext.FullName &&
    mr.Parameters[1].ParameterType.FullName == cardPlay.FullName).ToList();
if (callSites.Count != 1)
    throw new InvalidOperationException($"expected one CardModel.OnPlay call, found {callSites.Count}");
callSites[0].OpCode = OpCodes.Call;
callSites[0].Operand = dispatch;

// The compatibility router's original field order initializes its target map
// before its auto-property MethodInfo fields, producing an empty map. Move
// only the CreateSupportedTargetMap assignment to the end of the type cctor.
var router = RequireType("MegaCrit.Sts2.Core.Modding.HarmonyAndroidCompatRouter");
var targetMapField = router.Fields.Single(f => f.Name == "_supportedTargets");
var cctor = router.Methods.Single(m => m.IsConstructor && m.IsStatic);
var targetStore = cctor.Body.Instructions.Single(i =>
    i.OpCode == OpCodes.Stsfld &&
    i.Operand is FieldReference fr &&
    fr.Name == targetMapField.Name &&
    fr.DeclaringType.FullName == router.FullName);
var targetCreate = targetStore.Previous ??
    throw new InvalidOperationException("supported target map initializer call missing");
if (targetCreate.OpCode != OpCodes.Call ||
    targetCreate.Operand is not MethodReference createRef ||
    createRef.Name != "CreateSupportedTargetMap")
    throw new InvalidOperationException("unexpected supported target map initializer shape");
var ret = cctor.Body.Instructions.Last(i => i.OpCode == OpCodes.Ret);
var cil = cctor.Body.GetILProcessor();
cil.Remove(targetCreate);
cil.Remove(targetStore);
cil.InsertBefore(ret, targetCreate);
cil.InsertBefore(ret, targetStore);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
asm.Write(args[1]);
Console.WriteLine($"patched Android card-play bridge and router initialization: {args[1]}");
