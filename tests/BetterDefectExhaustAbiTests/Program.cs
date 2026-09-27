using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
string modPath=Path.GetFullPath(args[0]), refs=Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving += (_, name) => {
 string path=Path.Combine(refs, name.Name+".dll");
 return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(path):null;
};
void Require(bool ok,string message) { if(!ok)throw new Exception(message); }
using(var pe=new PEReader(File.OpenRead(modPath))) {
 var metadata=pe.GetMetadataReader();
 foreach(var handle in metadata.MemberReferences) {
  var member=metadata.GetMemberReference(handle);
  if(metadata.GetString(member.Name)!="Exhaust" || member.Parent.Kind!=HandleKind.TypeReference)continue;
  var type=metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
  Require(metadata.GetString(type.Name)!="CardCmd", "Direct return-type-dependent CardCmd.Exhaust MemberRef remains");
 }
}
var mod=AssemblyLoadContext.Default.LoadFromAssemblyPath(modPath);
var bd=mod.GetType("BetterDefect.Cards.Bd",true)!;
var native=(MethodInfo)bd.GetField("ExhaustCardMethod",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
Require(native.GetParameters().Length==4,"Native Exhaust parameter shape changed");
Require(typeof(Task).IsAssignableFrom(native.ReturnType),"Native Exhaust return is not awaitable Task");
Require(native.DeclaringType!.FullName=="MegaCrit.Sts2.Core.Commands.CardCmd","Wrong Exhaust selected");
Require(bd.GetMethod("Exhaust",BindingFlags.Public|BindingFlags.Static)!.ReturnType==typeof(Task),"Bridge must return Task");
var focus=mod.GetType("BetterDefect.Cards.BdBarrageTemporaryFocusPower",true)!;
Require(focus.BaseType!.Name=="TemporaryFocusPower","Barrage temporary focus must use native turn-end cleanup");
Console.WriteLine($"PASS {Path.GetFileName(modPath)} against {refs}: cached Exhaust resolves {native.ReturnType}, no direct ABI call, Barrage turn-end power present.");
