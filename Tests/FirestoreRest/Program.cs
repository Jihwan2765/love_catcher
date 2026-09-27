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
Run(service.RegisterPlayer("Name","FreshUser","Bio","여",0,ok=>registered=ok));
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
if(!registered||!prize||!profile||!candy||!inventory||!played||requests.Count!=10)throw new Exception($"flows: {registered} {prize} {profile} {candy} {inventory} {played} {requests.Count}");
bool checkedRegistration=false,checkedProfile=false,checkedCandy=false;
foreach(var requestBody in requests)
{
 if(requestBody.Contains("ParticipantKeys/insta_freshuser"))
 {
  using var registration=JsonDocument.Parse(requestBody);
  var writes=registration.RootElement.GetProperty("writes");
  var participant=writes[1].GetProperty("update");
  if(writes.GetArrayLength()!=3 ||
     participant.GetProperty("name").GetString()!="projects/test-project/databases/(default)/documents/Participants/insta_freshuser" ||
     participant.TryGetProperty("updateTime",out _) ||
     participant.GetProperty("fields").TryGetProperty("instaId",out _) ||
     participant.GetProperty("fields").GetProperty("insta").GetProperty("stringValue").GetString()!="freshuser")
   throw new Exception("registration write contains response-only fields or the wrong participant");
  checkedRegistration=true;
 }
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
if(!checkedRegistration||!checkedProfile||!checkedCandy)throw new Exception("registration or reward writes were not verified");
using(var play=JsonDocument.Parse(requests[^1]))
{
 var writes=play.RootElement.GetProperty("writes");
 if(writes.GetArrayLength()!=3 ||
    writes[2].GetProperty("transform").GetProperty("fieldTransforms")[0].GetProperty("fieldPath").GetString()!="attempts")
    throw new Exception("participant attempts was not committed with play receipt");
}
var updateResponder=UnityWebRequest.Responder;
string patchUrl=null;
UnityWebRequest.Responder=req=>{
 if(req.method=="PATCH")patchUrl=req.url;
 return updateResponder(req);
};
bool pickedSaved=false,participantSaved=false;
Run(service.UpdatePickedStatus("candidate",true,ok=>pickedSaved=ok));
using(var picked=JsonDocument.Parse(requests[^1]))
{
 if(!pickedSaved || !patchUrl.Contains("?updateMask.fieldPaths=isPicked&currentDocument.updateTime=") ||
    picked.RootElement.GetProperty("fields").GetProperty("isPicked").GetProperty("booleanValue").GetBoolean()!=true)
  throw new Exception("picked status patch must update only isPicked");
}
Run(service.UpdateParticipantFullData(new ParticipantData{documentId="candidate",name="Name",insta="candidate",bio="Bio",gender="여",isPicked=true,attempts=2},ok=>participantSaved=ok));
using(var participantPatch=JsonDocument.Parse(requests[^1]))
{
 var fields=participantPatch.RootElement.GetProperty("fields");
 if(!participantSaved || !patchUrl.Contains("updateMask.fieldPaths=isPicked") ||
    participantPatch.RootElement.TryGetProperty("updateTime",out _) ||
    participantPatch.RootElement.TryGetProperty("name",out _) ||
    fields.TryGetProperty("instaId",out _) ||
    fields.GetProperty("isPicked").GetProperty("booleanValue").GetBoolean()!=true)
  throw new Exception("participant patch contains response-only fields or omits the update mask");
}
UnityWebRequest.Responder=updateResponder;
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
bool orphanIndexExists=true,orphanClaimExists=true;
int orphanCleanupCommits=0,orphanRegistrationCommits=0;
UnityWebRequest.Responder=req=>{
 if(req.method=="GET"&&req.url.EndsWith("ParticipantKeys/insta_orphan"))
  return orphanIndexExists?(200,"{\"updateTime\":\"2026-01-01T00:00:01Z\",\"fields\":{\"participantKey\":{\"stringValue\":\"insta_orphan\"}}}"):(404,"");
 if(req.method=="GET"&&req.url.EndsWith("Participants/insta_orphan"))return(404,"");
 if(req.method=="GET"&&req.url.EndsWith("ProfileClaims/insta_orphan"))
  return orphanClaimExists?(200,"{\"updateTime\":\"2026-01-01T00:00:02Z\",\"fields\":{\"targetKey\":{\"stringValue\":\"insta_orphan\"}}}"):(404,"");
 if(req.method=="GET"&&req.url.Contains("/Participants?pageSize=300"))
  return(200,"{\"documents\":[{\"name\":\"projects/test-project/databases/(default)/documents/Participants/other\",\"fields\":{\"insta\":{\"stringValue\":\"other\"}}}]}");
 if(req.method=="POST"&&req.url.EndsWith(":commit")&&
    Encoding.UTF8.GetString(req.uploadHandler.bytes).Contains("ParticipantKeys/insta_orphan"))
 {
  using var json=JsonDocument.Parse(req.uploadHandler.bytes);
  var writes=json.RootElement.GetProperty("writes");
  if(writes[0].TryGetProperty("delete",out _))
  {
   if(writes.GetArrayLength()!=2 ||
      !writes[1].GetProperty("delete").GetString().EndsWith("ProfileClaims/insta_orphan") ||
      writes[0].GetProperty("currentDocument").GetProperty("updateTime").GetString()!="2026-01-01T00:00:01Z")
    throw new Exception("orphan cleanup was not limited to the versioned index and claim");
   orphanCleanupCommits++; orphanIndexExists=false; orphanClaimExists=false;
  }
  else
  {
   if(writes.GetArrayLength()!=3 ||
      !writes[1].GetProperty("update").GetProperty("name").GetString()!.EndsWith("Participants/insta_orphan"))
    throw new Exception("orphan replacement did not create the participant");
   orphanRegistrationCommits++; orphanIndexExists=true;
  }
  return(200,"{}");
 }
 return normalResponder(req);
};
ParticipantRegistrationResult orphanResult=ParticipantRegistrationResult.Failed;
Run(service.RegisterPlayer("Name","orphan","Bio","여",0,null,true,result=>orphanResult=result));
if(orphanResult!=ParticipantRegistrationResult.Created || orphanCleanupCommits!=1 ||
   orphanRegistrationCommits!=1 || orphanClaimExists)
 throw new Exception("admin registration did not repair orphaned links before creating participant");
int conflictingWrites=0;
UnityWebRequest.Responder=req=>{
 if(req.method=="GET"&&req.url.EndsWith("ParticipantKeys/insta_occupied"))
  return(200,"{\"updateTime\":\"2026-01-01T00:00:01Z\",\"fields\":{\"participantKey\":{\"stringValue\":\"insta_occupied\"}}}");
 if(req.method=="GET"&&req.url.EndsWith("Participants/insta_occupied"))
  return(200,"{\"fields\":{\"insta\":{\"stringValue\":\"someone_else\"}}}");
 if(req.method=="POST"&&req.url.EndsWith(":commit")){conflictingWrites++;return(200,"{}");}
 return normalResponder(req);
};
ParticipantRegistrationResult conflictResult=ParticipantRegistrationResult.Failed;
Run(service.RegisterPlayer("Name","occupied","Bio","여",0,null,true,result=>conflictResult=result));
if(conflictResult!=ParticipantRegistrationResult.IndexConflict || conflictingWrites!=0)
 throw new Exception("registration overwrote an index whose participant still exists");
int shadowWrites=0;
UnityWebRequest.Responder=req=>{
 if(req.method=="GET"&&req.url.EndsWith("ParticipantKeys/insta_shadow"))
  return(200,"{\"updateTime\":\"2026-01-01T00:00:01Z\",\"fields\":{\"participantKey\":{\"stringValue\":\"missing_shadow\"}}}");
 if(req.method=="GET"&&req.url.EndsWith("Participants/missing_shadow"))return(404,"");
 if(req.method=="GET"&&req.url.Contains("/Participants?pageSize=300"))
  return(200,"{\"documents\":[{\"name\":\"projects/test-project/databases/(default)/documents/Participants/another\",\"fields\":{\"instaId\":{\"stringValue\":\"@shadow\"}}}]}");
 if(req.method=="POST"&&req.url.EndsWith(":commit")){shadowWrites++;return(200,"{}");}
 return normalResponder(req);
};
ParticipantRegistrationResult shadowResult=ParticipantRegistrationResult.Failed;
Run(service.RegisterPlayer("Name","shadow","Bio","여",0,null,true,result=>shadowResult=result));
if(shadowResult!=ParticipantRegistrationResult.IndexConflict || shadowWrites!=0)
 throw new Exception("orphan repair ignored another participant with the same handle");
int participantCountRequests=0;
UnityWebRequest.Responder=req=>{
 if(req.method=="POST"&&req.url.EndsWith(":runAggregationQuery"))
 {
  participantCountRequests++;
  using var query=JsonDocument.Parse(req.uploadHandler.bytes);
  var structured=query.RootElement.GetProperty("structuredAggregationQuery").GetProperty("structuredQuery");
  if(structured.GetProperty("from")[0].GetProperty("collectionId").GetString()!="Participants" ||
     structured.TryGetProperty("where",out _))
   throw new Exception("participant count query did not count the entire collection");
  return(200,"[{\"result\":{\"aggregateFields\":{\"count\":{\"integerValue\":\"3\"}}}}]");
 }
 return normalResponder(req);
};
int? participantCount=null;
Run(service.GetParticipantCount(value=>participantCount=value));
if(participantCount!=3 || participantCountRequests!=1)
 throw new Exception("participant count did not use Firebase collection aggregation");
UnityWebRequest.Responder=req=>{
 if(req.method=="POST"&&req.url.EndsWith(":runAggregationQuery"))return(503,"");
 return normalResponder(req);
};
participantCount=0;
Run(service.GetParticipantCount(value=>participantCount=value));
if(participantCount.HasValue)
 throw new Exception("failed participant count returned a stale total");
UnityWebRequest.Responder=req=>req.method=="POST"&&req.url.EndsWith(":runAggregationQuery")
 ?(200,"[{\"result\":{\"aggregateFields\":{\"count\":{\"integerValue\":\"0\"}}}}]") : normalResponder(req);
Run(service.GetParticipantCount(value=>participantCount=value));
if(participantCount!=0)
 throw new Exception("empty participant collection was not counted as zero");
const string lockedRound="aabbccddeeff00112233445566778899";
int profileResets=0;
bool profileClaimExists=true;
string profileClaimTarget="insta_hyuna.in.blanket";
bool currentPicked=false;
string pickedPatchUrl=null;
string candidates="[{\"document\":{\"name\":\"projects/test-project/databases/(default)/documents/Participants/insta_hyuna.in.blanket\",\"fields\":{\"name\":{\"stringValue\":\"최현아\"},\"insta\":{\"stringValue\":\"hyuna.in.blanket\"}}}},{\"document\":{\"name\":\"projects/test-project/databases/(default)/documents/Participants/insta_ddong2\",\"fields\":{\"name\":{\"stringValue\":\"똥2\"},\"insta\":{\"stringValue\":\"ddong2\"}}}}]";
UnityWebRequest.Responder=req=>{
 if(req.method=="POST"&&req.url.EndsWith(":runQuery"))
 {
  using var query=JsonDocument.Parse(req.uploadHandler.bytes);
  if(query.RootElement.GetProperty("structuredQuery").TryGetProperty("limit",out _))
   throw new Exception("match query still truncates candidates before checking claims");
  return(200,candidates);
 }
 if(req.method=="GET"&&req.url.EndsWith("ProfileClaims/insta_hyuna.in.blanket"))
  return profileClaimExists?(200,"{\"updateTime\":\"2026-01-01T00:00:01Z\",\"fields\":{\"targetKey\":{\"stringValue\":\""+profileClaimTarget+"\"}}}"):(404,"");
 if(req.method=="GET"&&req.url.EndsWith("ProfileClaims/insta_ddong2"))return(404,"");
 if(req.method=="GET"&&req.url.EndsWith("Participants/insta_hyuna.in.blanket"))
  return(200,"{\"updateTime\":\"2026-01-01T00:00:00Z\",\"fields\":{\"insta\":{\"stringValue\":\"hyuna.in.blanket\"},\"isPicked\":{\"booleanValue\":"+(currentPicked?"true":"false")+"}}}");
 if(req.method=="PATCH"&&req.url.Contains("Participants/insta_hyuna.in.blanket"))
 { pickedPatchUrl=req.url; return(200,"{}"); }
 if(req.method=="POST"&&req.url.EndsWith(":commit"))
 {
  using var reset=JsonDocument.Parse(req.uploadHandler.bytes);
  var writes=reset.RootElement.GetProperty("writes");
  if(writes.GetArrayLength()!=2 ||
     !writes[0].GetProperty("update").GetProperty("name").GetString()!.EndsWith("Participants/insta_hyuna.in.blanket") ||
     !writes[1].GetProperty("delete").GetString()!.EndsWith("ProfileClaims/insta_hyuna.in.blanket") ||
     writes[0].GetProperty("currentDocument").GetProperty("updateTime").GetString()!="2026-01-01T00:00:00Z" ||
     writes[1].GetProperty("currentDocument").GetProperty("updateTime").GetString()!="2026-01-01T00:00:01Z")
   throw new Exception("developer reset did not atomically update picked state and release the claim");
  profileClaimExists=false;
  currentPicked=false;
  profileResets++;
  return(200,"{}");
 }
 return normalResponder(req);
};
MatchedProfileResponse eligibleMatch=default;
Run(service.GetRandomMatch("여",match=>eligibleMatch=match));
if(eligibleMatch.documentId!="insta_ddong2")
 throw new Exception("a profile with an existing claim was selected again");
Run(service.GetRandomMatch("여",match=>eligibleMatch=match,new HashSet<string>{"insta_ddong2"}));
if(eligibleMatch.success || !eligibleMatch.querySucceeded)
 throw new Exception("locked profiles were reported as available after excluding the valid candidate");
var lockedCandidate=new MatchedProfileResponse{documentId="insta_hyuna.in.blanket",insta="hyuna.in.blanket"};
ProfileClaimResult claimResult=ProfileClaimResult.Failed;
Run(service.ClaimProfile(lockedRound,lockedCandidate,null,result=>claimResult=result));
if(claimResult!=ProfileClaimResult.CandidateUnavailable || profileResets!=0)
 throw new Exception("a previously claimed profile was retried instead of rejected");
ParticipantUpdateResult editResult=ParticipantUpdateResult.Failed;
Run(service.UpdatePickedStatus("insta_hyuna.in.blanket",false,null,result=>editResult=result));
if(editResult!=ParticipantUpdateResult.Saved || profileResets!=1 || profileClaimExists || pickedPatchUrl!=null)
 throw new Exception("developer mode did not release the claim with isPicked");
Run(service.GetRandomMatch("여",match=>eligibleMatch=match));
if(eligibleMatch.documentId!="insta_hyuna.in.blanket")
 throw new Exception("reset participant was not eligible for matching again");
profileClaimExists=true;
Run(service.UpdateParticipantFullData(new ParticipantData{documentId="insta_hyuna.in.blanket",name="최현아",insta="hyuna.in.blanket",gender="여",isPicked=false},null,result=>editResult=result));
if(editResult!=ParticipantUpdateResult.ProfileClaimLocked || profileResets!=1)
 throw new Exception("editing an already inconsistent participant silently released the claim");
currentPicked=true;
Run(service.UpdateParticipantFullData(new ParticipantData{documentId="insta_hyuna.in.blanket",name="최현아",insta="hyuna.in.blanket",gender="여",isPicked=false},null,result=>editResult=result));
if(editResult!=ParticipantUpdateResult.Saved || profileResets!=2 || profileClaimExists || pickedPatchUrl!=null)
 throw new Exception("full participant edit did not release the claim atomically");
Run(service.UpdatePickedStatus("insta_hyuna.in.blanket",true,null,result=>editResult=result));
if(editResult!=ParticipantUpdateResult.Saved ||
   pickedPatchUrl==null || !pickedPatchUrl.Contains("currentDocument.updateTime="))
 throw new Exception("developer mode could not mark a participant picked");
profileClaimExists=true;
profileClaimTarget="someone_else";
Run(service.UpdatePickedStatus("insta_hyuna.in.blanket",false,null,result=>editResult=result));
if(editResult!=ParticipantUpdateResult.ProfileClaimLocked || profileResets!=2)
 throw new Exception("developer mode deleted another participant's claim");
int concurrentClaimReads=0,concurrentClaimCommits=0;
UnityWebRequest.Responder=req=>{
 if(req.method=="GET"&&req.url.EndsWith("MatchResults/love_"+lockedRound))return(404,"");
 if(req.method=="GET"&&req.url.EndsWith("Participants/candidate"))return(200,person);
 if(req.method=="GET"&&req.url.EndsWith("ProfileClaims/insta_candidate"))
  return ++concurrentClaimReads==1?(404,""):(200,"{\"fields\":{\"targetKey\":{\"stringValue\":\"candidate\"}}}");
 if(req.method=="POST"&&req.url.EndsWith(":commit"))
 { concurrentClaimCommits++; return(412,""); }
 return normalResponder(req);
};
Run(service.ClaimProfile(lockedRound,new MatchedProfileResponse{documentId="candidate",insta="candidate"},
 null,result=>claimResult=result));
if(claimResult!=ProfileClaimResult.CandidateUnavailable || concurrentClaimCommits!=1 || concurrentClaimReads!=2)
 throw new Exception("a concurrent profile claim was not classified for another candidate retry");
Console.WriteLine("Love registration/prize/profile/candy/count/inventory/play/delete/orphan recovery/profile reset checks passed");
