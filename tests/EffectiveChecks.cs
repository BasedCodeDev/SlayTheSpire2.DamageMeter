using Based.Spires2DamageMeter;
static class EffectiveChecks{
 public static void Run(Action<bool,string> check){
 var e=new EffectiveDamageLedger();
 DamageCredit C(string id,string who,decimal hp,decimal block,bool bonus=false)=>new(id,who,"Direct",new("card","strike","Strike"),null,hp,block,bonus);
 void Hit(string id,string target,string who,decimal hp,decimal block,bool qualifies)=>e.Add(id,target,new[]{C(id,who,hp,block)},qualifies);
 Hit("1","x","A",0,20,false);check(e.Credits.Count==0&&e.PendingFor("A")==20,"retained shield contributions wait across turns");
 Hit("2","y","B",3,0,true);check(e.PendingFor("A")==20,"different target cannot release pending block");
 Hit("3","x","B",6,12,true);check(e.Ledger().Rows()["A"].Block==20&&e.Ledger().Rows()["B"].Hp==9,"breakthrough releases earlier teammate credit");
 Hit("3","x","B",6,12,true);check(e.Ledger().Rows()["B"].Block==12,"effective events deduplicate");
 e.Clear();Hit("4","x","A",0,10,false);Hit("5","x","B",6,0,true);check(e.Ledger().Rows()["A"].Block==10,"exact break accepts later HP hit");
 e.Clear();Hit("6","x","A",0,10,false);e.Discard("x");Hit("7","x","B",6,0,true);check(!e.Ledger().Rows().ContainsKey("A"),"expired or replaced shields discard old credit");
 e.Clear();Hit("8","x","A",0,10,false);Hit("9","x","B",6,0,false);check(e.PendingFor("A")==10&&e.Ledger().Rows()["B"].Hp==6,"bypass HP counts without releasing shield");
 e.Add("bonus","x",new[]{C("bonus","Provider",0,3,true)},false);Hit("10","x","B",1,0,true);check(e.Ledger().Rows()["Provider"].AmplifiedBlock==3,"pending amplification retains provider ownership");
 e.Clear();Hit("11","x","A",0,4,false);var json=System.Text.Json.JsonSerializer.Serialize(e.Snapshot());var restored=new EffectiveDamageLedger();restored.Merge(System.Text.Json.JsonSerializer.Deserialize<EffectiveSnapshot>(json)!);check(restored.PendingFor("A")==4,"pending effective snapshot survives JSON restore");restored.Finish();check(restored.Pending.Count==0,"combat end drops pending contributions");
 var dir=Path.Combine(Path.GetTempPath(),"effective-check-"+Guid.NewGuid().ToString("N"));
 try{var run=new RunIdentity("p","m","start","seed","A");var store=new RunStatisticsStore(dir);store.Completed["f"]=new("f",new(),new()){Effective=e.Snapshot()};store.Save(run,"h");store.Restore(run,"h");check(store.Completed["f"].Effective!.Pending["x"].Sum(c=>c.Block)==4,"checkpoint six preserves effective pending state");
 var path=Directory.GetFiles(dir,"*.json",SearchOption.AllDirectories).Single();var old=File.ReadAllText(path).Replace("\"Schema\":7","\"Schema\":5");File.WriteAllText(path,old);store.Restore(run,"h");check(store.Completed.ContainsKey("f"),"legacy schema five remains readable");
 }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
 e.Add("missing",null,new[]{C("missing","A",2,5)},null);check(e.Incomplete&&e.Ledger().Rows()["A"].Hp==2,"missing lifecycle preserves HP and marks partial");
 }
}
