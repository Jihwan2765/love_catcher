using System;
using System.Collections;
using System.Text.Json;
namespace UnityEngine {
 public class Object { protected static void DontDestroyOnLoad(object x){} protected static void Destroy(object x){} protected static T FindObjectOfType<T>() where T:class=>null; protected static T FindFirstObjectByType<T>() where T:class=>null; }
 public class MonoBehaviour:Object { public object gameObject=new(); public Coroutine StartCoroutine(IEnumerator r){ Run(r); return null; } private static void Run(IEnumerator r){ while(r.MoveNext()) if(r.Current is IEnumerator nested) Run(nested); } }
 public class Coroutine {}
 public class GameObject { public GameObject(string name){} public T AddComponent<T>() where T:new()=>new T(); }
 public class TextAsset { public string text; }
 public class HeaderAttribute:Attribute { public HeaderAttribute(string s){} }
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type){} }
 public enum RuntimeInitializeLoadType { BeforeSceneLoad }
 public static class Resources { public static T Load<T>(string name) where T:class=>null; }
 public static class JsonUtility { static readonly JsonSerializerOptions o=new(){IncludeFields=true}; public static string ToJson(object x)=>JsonSerializer.Serialize(x,o); public static T FromJson<T>(string s)=>JsonSerializer.Deserialize<T>(s,o); }
 public static class Debug { public static void Log(object x){} public static void LogWarning(object x){System.Console.WriteLine(x);} public static void LogError(object x){} }
 public static class Time { public static float realtimeSinceStartup; }
 public static class Mathf { public static float Max(float a,float b)=>Math.Max(a,b); public static float Min(float a,float b)=>Math.Min(a,b); public static int Max(int a,int b)=>Math.Max(a,b); public static int Min(int a,int b)=>Math.Min(a,b); }
 public class WaitForSecondsRealtime { public WaitForSecondsRealtime(float x){} }
 public static class Screen { public static int width=1920,height=1080; }
 public struct Rect { public Rect(float x,float y,float w,float h){} }
 public static class GUI { public static bool enabled; public static void Box(Rect r,string s){} public static void Label(Rect r,string s){} public static string TextField(Rect r,string s)=>s; public static string PasswordField(Rect r,string s,char c)=>s; public static bool Button(Rect r,string s)=>false; }
}
namespace UnityEngine.SceneManagement { public static class SceneManager {} }
namespace UnityEngine.Networking {
 public class UploadHandlerRaw { public byte[] bytes; public UploadHandlerRaw(byte[] b){bytes=b;} }
 public class DownloadHandlerBuffer { public string text; }
 public class UnityWebRequest:IDisposable { public enum Result { InProgress,Success,ConnectionError,ProtocolError,DataProcessingError } public Result result; public static Func<UnityWebRequest,(long,string)> Responder; public string url,method; public UnityWebRequest(string url,string method){this.url=url;this.method=method;} public static UnityWebRequest Get(string url)=>new(url,"GET"){downloadHandler=new DownloadHandlerBuffer()}; public UploadHandlerRaw uploadHandler; public DownloadHandlerBuffer downloadHandler; public long responseCode; public string error; public int timeout; public void SetRequestHeader(string k,string v){} public object SendWebRequest(){ var reply=Responder==null?(0L,""):Responder(this); responseCode=reply.Item1; downloadHandler.text=reply.Item2; result=responseCode==200?Result.Success:Result.ProtocolError; return null; } public void Dispose(){} }
}
