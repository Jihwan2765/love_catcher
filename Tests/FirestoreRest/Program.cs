using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ClawMachine.Mechanics;
using UnityEngine.Networking;
static void Run(IEnumerator r){while(r.MoveNext())if(r.Current is IEnumerator nested)Run(nested);}
var auth=new BoothStaffAuth();
typeof(BoothStaffAuth).GetProperty("Instance",BindingFlags.Static|BindingFlags.Public).SetValue(null,auth);
var claims=Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"aud\":\"test-project\",\"boothStaff\":true,\"boothAdmin\":true}")).TrimEnd('=').Replace('+','-').Replace('/','_');
typeof(BoothStaffAuth).GetField("idToken",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(auth,"x."+claims+".x");
typeof(BoothStaffAuth).GetField("tokenExpiresAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(auth,1000f);
var service=FirebaseRESTService.Instance;
service.firebaseProjectId="test-project";
var requests=new List<string>();
const string playRound="0123456789abcdef0123456789abcdef";
string playReceipt=null;
var stats="{\"updateTime\":\"2026-01-01T00:00:00Z\",\"fields\":{\"totalDolls\":{\"integerValue\":\"1\"},\"totalLegendaryDolls\":{\"integerValue\":\"1\"},\"totalPlays\":{\"integerValue\":\"0\"},\"totalRevenue\":{\"integerValue\":\"0\"},\"totalRegistrations\":{\"integerValue\":\"0\"},\"totalSuccesses\":{\"integerValue\":\"0\"}}}";
var person="{\"name\":\"projects/test-project/databases/(default)/documents/Participants/candidate\",\"updateTime\":\"2026-01-01T00:00:00Z\",\"fields\":{\"insta\":{\"stringValue\":\"candidate\"},\"isPicked\":{\"booleanValue\":false}}}";
UnityWebRequest.Responder=req=>{
 if(req.method=="GET"&&req.url.EndsWith("GameState/stats"))return(200,stats);
 if(req.method=="GET"&&req.url.EndsWith("GameRounds/love_"+playRound)&&playReceipt!=null)return(200,playReceipt);
 if(req.method=="GET"&&req.url.EndsWith("ParticipantKeys/insta_candidate"))return(200,"{\"fields\":{\"participantKey\":{\"stringValue\":\"candidate\"}}}");
 if(req.method=="GET"&&req.url.EndsWith("ParticipantKeys/insta_stale"))return(200,"{\"fields\":{\"participantKey\":{\"stringValue\":\"missing\"}}}");
 if(req.method=="GET"&&req.url.EndsWith("Participants/candidate"))return(200,person);
 if(req.method=="GET")return(404,"");
 var body=Encoding.UTF8.GetString(req.uploadHandler.bytes);
 using(var json=JsonDocument.Parse(body))
 {
  if(req.url.EndsWith(":commit"))
  {
   var first=json.RootElement.GetProperty("writes")[0];
   if(first.TryGetProperty("update",out var update)&&
      update.GetProperty("name").GetString().EndsWith("GameRounds/love_"+playRound))
      playReceipt="{\"fields\":"+update.GetProperty("fields").GetRawText()+"}";
  }
 }
 requests.Add(body);
 if(req.url.EndsWith(":runQuery"))return(200,"[]");
 if(req.url.EndsWith(":runAggregationQuery"))return(200,"[{\"result\":{\"aggregateFields\":{\"count\":{\"integerValue\":\"2\"}}}}]");
 return(200,"{}");
};
bool registered=false,prize=false,profile=false,inventory=false,played=false;
Run(service.RegisterPlayer("Name","Candidate","Bio","여",0,ok=>registered=ok));
Run(service.CheckInstaIdExists("candidate",exists=>{if(exists!=true)throw new Exception("valid index rejected");}));
Run(service.CheckInstaIdExists("stale",exists=>{if(exists!=null)throw new Exception("stale index accepted");}));
Run(service.ClaimPrize("round1",false,ok=>prize=ok)); Run(service.CheckInstaIdExists("fresh",exists=>{if(exists!=false)throw new Exception("bad duplicate check");})); Run(service.GetRandomMatch("여",match=>{if(match.success)throw new Exception("unexpected match");}));
Run(service.ClaimProfile("round2",new MatchedProfileResponse{documentId="candidate",insta="candidate"},ok=>profile=ok));
Run(service.GetUnpickedCounts((male,female)=>{if(male!=2||female!=2)throw new Exception("bad aggregation: "+male+" "+female);}));
Run(service.UpdateTotalDolls(3,ok=>inventory=ok));
service.IncrementPlayCountAndRevenue(500,"candidate",playRound,(ok,_)=>played=ok);
service.IncrementPlayCountAndRevenue(500,"candidate",playRound,(ok,_)=>{if(!ok)throw new Exception("same play replay failed");});
service.IncrementPlayCountAndRevenue(1000,"candidate",playRound,(ok,_)=>{if(ok)throw new Exception("mismatched play replay accepted");});
if(!registered||!prize||!profile||!inventory||!played||requests.Count!=8)throw new Exception($"flows: {registered} {prize} {profile} {inventory} {played} {requests.Count}");
using(var play=JsonDocument.Parse(requests[^1]))
{
 var writes=play.RootElement.GetProperty("writes");
 if(writes.GetArrayLength()!=3 ||
    writes[2].GetProperty("transform").GetProperty("fieldTransforms")[0].GetProperty("fieldPath").GetString()!="attempts")
    throw new Exception("participant attempts was not committed with play receipt");
}
const string retryRound="fedcba9876543210fedcba9876543210";
var normalResponder=UnityWebRequest.Responder;
int interrupted=0;
UnityWebRequest.Responder=req=>{
 if(req.method=="POST"&&req.url.EndsWith(":commit")&&
    Encoding.UTF8.GetString(req.uploadHandler.bytes).Contains("GameRounds/love_"+retryRound)&&
    interrupted++==0)return(503,"");
 return normalResponder(req);
};
service.IncrementPlayCountAndRevenue(0,"candidate",retryRound,
    (ok,_)=>{if(ok)throw new Exception("unconfirmed play was accepted");});
service.IncrementPlayCountAndRevenue(0,"candidate",retryRound,
    (ok,_)=>{if(!ok)throw new Exception("same round retry failed");});
Console.WriteLine("Love index/registration/prize/profile/count/inventory/play+attempts JSON and replay checks passed");
