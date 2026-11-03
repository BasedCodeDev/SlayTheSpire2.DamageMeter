namespace Based.Spires2DamageMeter;
public enum MultiplierOperation { Add, DoubleDeviation }
public sealed record MultiplierStep(string Id,string Model,MultiplierOperation Operation,decimal Value,bool Status,bool Eligible,IReadOnlyList<Share> Providers,decimal Input,decimal Output)
{
 public string EligibilityReason {get;init;}="captured";
 public decimal Apply(decimal input)=>!Eligible?input:Operation==MultiplierOperation.Add?input+Value:2*input-1;
}
public sealed record MultiplierChain(string Root,bool Eligible,decimal Seed,IReadOnlyList<MultiplierStep> Steps)
{
 public string Family {get;init;}="Vulnerable";
 public string EligibilityReason {get;init;}="captured";
 public decimal Evaluate(ISet<string>? excluded=null)
 {
  if(!Eligible)return 1;
  var value=excluded?.Contains(Root)==true?1:Seed;
  foreach(var s in Steps)if(excluded?.Contains(s.Id)!=true)value=s.Apply(value);
  return value;
 }
 public bool Reconciles(decimal output)
 {
  var allowed=Family=="Weak"?new[]{"PaperKrane","DebilitatePower"}:new[]{"PaperPhrog","CrueltyPower","DebilitatePower"};
  var last=-1;var value=Eligible?Seed:1;
  foreach(var s in Steps){var rank=Array.IndexOf(allowed,s.Model);if(rank<=last||s.Input!=value||s.Apply(value)!=s.Output)return false;last=rank;value=s.Output;}
  return value==output&& (Eligible||Steps.Count==0);
 }
 public static bool RootEligible(bool vulnerable,bool targetOwner,bool dealerOwner,bool powered)=>powered&&(vulnerable?targetOwner:dealerOwner);
 public static bool ChildEligible(string model,bool weak,bool targetOwner,bool dealerOwner,bool powered)=>powered&&(model switch{"CrueltyPower" or "PaperPhrog"=>!targetOwner,"PaperKrane"=>targetOwner,"DebilitatePower"=>weak?dealerOwner:targetOwner,_=>throw new ArgumentException("Unknown child")});
}
