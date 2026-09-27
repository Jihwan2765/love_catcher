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
int allRequests=0;
const string playRound="0123456789abcdef0123456789abcdef";
const string candyRound="abcdef0123456789abcdef0123456789";
string playReceipt=null;
string candyReceipt=null;
var stats="{\"updateTime\":\"2026-01-01T00:00:00Z\",\"fields\":{\"totalDolls\":{\"integerValue\":\"1\"},\"totalLegendaryDolls\":{\"integerValue\":\"1\"},\"totalPlays\":{\"integerValue\":\"0\"},\"totalRevenue\":{\"integerValue\":\"0\"},\"totalRegistrations\":{\"integerValue\":\"0\"},\"totalSuccesses\":{\"integerValue\":\"0\"}}}";
var person="{\"name\":\"projects/test-project/databases/(default)/documents/Participants/candidate\",\"updateTime\":\"2026-01-01T00:00:00Z\",\"fields\":{\"insta\":{\"stringValue\":\"candidate\"},\"isPicked\":{\"booleanValue\":false}}}";
UnityWebRequest.Responder=req=>{
 allRequests++;
 if(req.method=="GET"&&req.url.EndsWith("GameState/stats"))return(200,stats);
 if(req.method=="GET"&&req.url.EndsWith("GameRounds/love_"+playRound)&&playReceipt!=null)return(200,playReceipt);
 if(req.method=="GET"&&req.url.EndsWith("GameRounds/love_candy_"+candyRound)&&candyReceipt!=null)return(200,candyReceipt);
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
   if(first.TryGetProperty("update",out update)&&
      update.GetProperty("name").GetString().EndsWith("GameRounds/love_candy_"+candyRound))
      candyReceipt="{\"fields\":"+update.GetProperty("fields").GetRawText()+"}";
  }
 }
 requests.Add(body);
 if(req.url.EndsWith(":runQuery"))return(200,"[]");
 if(req.url.EndsWith(":runAggregationQuery"))return(200,"[{\"result\":{\"aggregateFields\":{\"count\":{\"integerValue\":\"2\"}}}}]");
 return(200,"{}");
};
bool registered=false,prize=false,profile=false,candy=false,inventory=false,played=false;
Run(service.RegisterPlayer("Name","Candidate","Bio","여",0,ok=>registered=ok));
Run(service.CheckInstaIdExists("candidate",exists=>{if(exists!=true)throw new Exception("valid index rejected");}));
Run(service.CheckInstaIdExists("stale",exists=>{if(exists!=null)throw new Exception("stale index accepted");}));
if (!FirebaseRESTService.TryNormalizeInstaId(" @Candidate ",out var normalized) || normalized!="candidate" ||
    FirebaseRESTService.TryNormalizeInstaId("bad handle!",out _))
    throw new Exception("Instagram handle validation changed");
int beforeUnauthenticatedLookup=allRequests;
typeof(BoothStaffAuth).GetField("idToken",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(auth,null);
Run(service.CheckInstaIdExists("candidate",exists=>{if(exists!=null)throw new Exception("unauthenticated lookup accepted");}));
if(allRequests!=beforeUnauthenticatedLookup)throw new Exception("unauthenticated lookup sent a request");
typeof(BoothStaffAuth).GetField("idToken",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(auth,"x."+claims+".x");
Run(service.ClaimPrize("round1",false,ok=>prize=ok)); Run(service.CheckInstaIdExists("fresh",exists=>{if(exists!=false)throw new Exception("bad duplicate check");})); Run(service.GetRandomMatch("여",match=>{if(match.success||!match.querySucceeded)throw new Exception("empty match pool was treated as query failure");}));
Run(service.ClaimProfile("round2",new MatchedProfileResponse{documentId="candidate",insta="candidate"},ok=>profile=ok));
Run(service.ClaimCandy(candyRound,ok=>candy=ok));
Run(service.ClaimCandy(candyRound,ok=>{if(!ok)throw new Exception("same candy result replay failed");}));
Run(service.GetUnpickedCounts((male,female)=>{if(male!=2||female!=2)throw new Exception("bad aggregation: "+male+" "+female);}));
Run(service.UpdateTotalDolls(3,ok=>inventory=ok));
service.IncrementPlayCountAndRevenue(500,"candidate",playRound,(ok,_)=>played=ok);
service.IncrementPlayCountAndRevenue(500,"candidate",playRound,(ok,_)=>{if(!ok)throw new Exception("same play replay failed");});
service.IncrementPlayCountAndRevenue(1000,"candidate",playRound,(ok,_)=>{if(ok)throw new Exception("mismatched play replay accepted");});
if(!registered||!prize||!profile||!candy||!inventory||!played||requests.Count!=9)throw new Exception($"flows: {registered} {prize} {profile} {candy} {inventory} {played} {requests.Count}");
bool checkedProfile=false,checkedCandy=false;
foreach(var requestBody in requests)
{
 if(requestBody.Contains("MatchResults/love_round2"))
 {
  using var match=JsonDocument.Parse(requestBody);
  var writes=match.RootElement.GetProperty("writes");
  if(writes.GetArrayLength()!=4||writes[3].GetProperty("transform").GetProperty("fieldTransforms")[0].GetProperty("fieldPath").GetString()!="totalSuccesses")
   throw new Exception("profile claim does not commit success count atomically");
  checkedProfile=true;
 }
 if(requestBody.Contains("GameRounds/love_candy_"+candyRound))
 {
  using var consolation=JsonDocument.Parse(requestBody);
  var writes=consolation.RootElement.GetProperty("writes");
  if(writes.GetArrayLength()!=2||writes[1].GetProperty("transform").GetProperty("fieldTransforms")[0].GetProperty("fieldPath").GetString()!="totalSuccesses")
   throw new Exception("candy claim does not commit success count atomically");
  checkedCandy=true;
 }
}
if(!checkedProfile||!checkedCandy)throw new Exception("reward receipts were not written");
using(var play=JsonDocument.Parse(requests[^1]))
{
 var writes=play.RootElement.GetProperty("writes");
 if(writes.GetArrayLength()!=3 ||
    writes[2].GetProperty("transform").GetProperty("fieldTransforms")[0].GetProperty("fieldPath").GetString()!="attempts")
    throw new Exception("participant attempts was not committed with play receipt");
}
const string guestRound="00112233445566778899aabbccddeeff";
string guestReceipt=null;
int guestCommits=0;
var registeredResponder=UnityWebRequest.Responder;
UnityWebRequest.Responder=req=>{
 if(req.method=="GET"&&req.url.EndsWith("GameRounds/love_"+guestRound))
  return guestReceipt==null?(404,""):(200,guestReceipt);
 if(req.method=="POST"&&req.url.EndsWith(":commit")&&
    Encoding.UTF8.GetString(req.uploadHandler.bytes).Contains("GameRounds/love_"+guestRound))
 {
  guestCommits++;
  using var json=JsonDocument.Parse(req.uploadHandler.bytes);
  var writes=json.RootElement.GetProperty("writes");
  if(writes.GetArrayLength()!=2 ||
     writes[0].GetProperty("update").GetProperty("fields").GetProperty("kind").GetProperty("stringValue").GetString()!="love_guest" ||
     writes[1].GetProperty("transform").GetProperty("fieldTransforms")[0].GetProperty("fieldPath").GetString()!="totalPlays")
   throw new Exception("guest play must write only receipt and shared statistics");
  guestReceipt="{\"fields\":"+writes[0].GetProperty("update").GetProperty("fields").GetRawText()+"}";
  return(503,""); // 커밋은 성공했지만 응답만 유실된 경우
 }
 if(req.url.Contains("ParticipantKeys/")||req.url.Contains("Participants/"))
  throw new Exception("guest play accessed participant data");
 return registeredResponder(req);
};
service.IncrementPlayCountAndRevenue(500,"",guestRound,(ok,_)=>{if(!ok)throw new Exception("guest play failed");});
service.IncrementPlayCountAndRevenue(500,"",guestRound,(ok,_)=>{if(!ok)throw new Exception("guest play replay failed");});
service.IncrementPlayCountAndRevenue(1000,"",guestRound,(ok,_)=>{if(ok)throw new Exception("guest round conflict accepted");});
if(guestCommits!=1)throw new Exception("guest play committed more than once");
UnityWebRequest.Responder=registeredResponder;
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
const string lostCandyRound="123456789abcdef0123456789abcdef0";
string lostCandyReceipt=null;
int lostCandyCommits=0;
UnityWebRequest.Responder=req=>{
 if(req.method=="GET"&&req.url.EndsWith("GameRounds/love_candy_"+lostCandyRound))
  return lostCandyReceipt==null?(404,""):(200,lostCandyReceipt);
 if(req.method=="POST"&&req.url.EndsWith(":commit")&&
    Encoding.UTF8.GetString(req.uploadHandler.bytes).Contains("GameRounds/love_candy_"+lostCandyRound))
 {
  lostCandyCommits++;
  using var json=JsonDocument.Parse(req.uploadHandler.bytes);
  lostCandyReceipt="{\"fields\":"+json.RootElement.GetProperty("writes")[0].GetProperty("update").GetProperty("fields").GetRawText()+"}";
  return(503,"");
 }
 return normalResponder(req);
};
Run(service.ClaimCandy(lostCandyRound,ok=>{if(!ok)throw new Exception("lost candy commit response was not recovered");}));
if(lostCandyCommits!=1)throw new Exception("candy result was committed more than once");
UnityWebRequest.Responder=req=>req.url.EndsWith(":runQuery")?(503,""):normalResponder(req);
Run(service.GetRandomMatch("여",match=>{if(match.success||match.querySucceeded)throw new Exception("failed match query was treated as empty pool");}));
int deleteCommits=0;
UnityWebRequest.Responder=req=>{
 if(req.method=="GET"&&req.url.EndsWith("Participants/to_delete"))
  return(200,"{\"updateTime\":\"2026-01-01T00:00:00Z\",\"fields\":{\"insta\":{\"stringValue\":\"to_delete\"}}}");
 if(req.method=="GET"&&req.url.EndsWith("ParticipantKeys/insta_to_delete"))
  return(200,"{\"updateTime\":\"2026-01-01T00:00:01Z\",\"fields\":{\"participantKey\":{\"stringValue\":\"to_delete\"}}}");
 if(req.method=="GET"&&req.url.EndsWith("ProfileClaims/insta_to_delete"))
  return(200,"{\"updateTime\":\"2026-01-01T00:00:02Z\",\"fields\":{\"targetKey\":{\"stringValue\":\"to_delete\"}}}");
 if(req.method=="POST"&&req.url.EndsWith(":commit")&&
    Encoding.UTF8.GetString(req.uploadHandler.bytes).Contains("Participants/to_delete"))
 {
  deleteCommits++;
  using var json=JsonDocument.Parse(req.uploadHandler.bytes);
  var writes=json.RootElement.GetProperty("writes");
  if(writes.GetArrayLength()!=3 ||
     !writes[0].GetProperty("delete").GetString().EndsWith("Participants/to_delete") ||
     !writes[1].GetProperty("delete").GetString().EndsWith("ParticipantKeys/insta_to_delete") ||
     !writes[2].GetProperty("delete").GetString().EndsWith("ProfileClaims/insta_to_delete") ||
     writes[1].GetProperty("currentDocument").GetProperty("updateTime").GetString()!="2026-01-01T00:00:01Z")
   throw new Exception("admin deletion must remove participant, index and claim in one guarded commit");
  return(200,"{}");
 }
 return normalResponder(req);
};
Run(service.DeleteParticipant("to_delete",ok=>{if(!ok)throw new Exception("linked participant delete failed");}));
if(deleteCommits!=1)throw new Exception("linked participant delete did not commit once");
bool lostDeleteApplied=false;
UnityWebRequest.Responder=req=>{
 if(req.method=="GET"&&req.url.EndsWith("Participants/lost_delete"))
  return lostDeleteApplied?(404,""):(200,"{\"updateTime\":\"2026-01-01T00:00:00Z\",\"fields\":{\"insta\":{\"stringValue\":\"lost_delete\"}}}");
 if(req.method=="GET"&&req.url.EndsWith("ParticipantKeys/insta_lost_delete"))
  return lostDeleteApplied?(404,""):(200,"{\"updateTime\":\"2026-01-01T00:00:01Z\",\"fields\":{\"participantKey\":{\"stringValue\":\"lost_delete\"}}}");
 if(req.method=="GET"&&req.url.EndsWith("ProfileClaims/insta_lost_delete"))return(404,"");
 if(req.method=="POST"&&req.url.EndsWith(":commit")&&
    Encoding.UTF8.GetString(req.uploadHandler.bytes).Contains("Participants/lost_delete"))
 { lostDeleteApplied=true; return(503,""); }
 return normalResponder(req);
};
Run(service.DeleteParticipant("lost_delete",ok=>{if(!ok)throw new Exception("lost delete response was not recovered");}));
Console.WriteLine("Love registration/prize/profile/candy/count/inventory/play/delete JSON and replay checks passed");
