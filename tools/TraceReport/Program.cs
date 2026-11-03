using System.Text.Json;
using Based.Spires2DamageMeter;
if(args.Length==4 && args[0]=="--release")
{
    var failures=ReleaseEvidence.Validate(File.ReadAllText(args[1]),args[2],args[3]);
    Console.WriteLine(JsonSerializer.Serialize(new{failures},new JsonSerializerOptions{WriteIndented=true}));
    return failures.Count>0?1:0;
}
if(args.Length==0){Console.Error.WriteLine("Usage: dotnet run --project tools/TraceReport -- file1.jsonl [file2.jsonl ...]. JSON report is written to stdout; files are read-only.");return 2;}
var report=new TraceReport();foreach(var path in args)
{
    try { report.Read(Path.GetFullPath(path),ReadShared(path)); }
    catch(Exception e) when(e is IOException or UnauthorizedAccessException) { report.ParseFailures.Add(path+": "+e.Message); }
}
Console.WriteLine(JsonSerializer.Serialize(new{report.Groups,report.ParseFailures,report.LegacyNumericalEventsExcluded,Notice="Observed consistency is not native correctness proof. Missing modifiers/scenarios remain not exercised."},new JsonSerializerOptions{WriteIndented=true}));
return report.ParseFailures.Count>0 || report.Groups.Any(g=>g.Failures.Count>0)?1:0;

static IEnumerable<string> ReadShared(string path)
{
    using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
    using var reader=new StreamReader(stream);
    while(reader.ReadLine() is { } line)yield return line;
}
