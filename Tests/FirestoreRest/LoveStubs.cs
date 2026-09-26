namespace ClawMachine.Utils { public static class EnvLoader { public static string Get(string key)=>""; } }
namespace UnityEngine {
 public static class PlayerPrefs { public static int GetInt(string key,int fallback=0)=>fallback; public static void SetInt(string key,int value){} public static void Save(){} }
 public static class Random { public static int Range(int start,int end)=>start; }
 public class WaitUntil { public WaitUntil(System.Func<bool> condition){} }
}
namespace UnityEngine { public class TooltipAttribute:System.Attribute { public TooltipAttribute(string text){} } }
