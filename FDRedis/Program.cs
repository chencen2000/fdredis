using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel.Web;
using System.Collections.Specialized;
using System.Web;
using StackExchange.Redis;
using System.Configuration;
using System.Diagnostics;

namespace FDRedis
{
    [ServiceContract]
    public interface IFDRedis
    {
        [OperationContract]
        [WebGet(UriTemplate = "hello?name={name}")]
        Stream SayHello(string name);
        [OperationContract]
        [WebGet(UriTemplate = "/db/{id}/{*key}")]
        Stream handleGetKeyWithId(string id, string key);
        [OperationContract]
        [WebGet(UriTemplate = "/db/{*key}")]
        Stream handleGetKey(string key);
        [OperationContract]
        [WebInvoke(Method ="POST", UriTemplate = "/db/{*id}")]
        Stream handlePostKeyValue(string id, Stream request);
    }

    class Program : IFDRedis
    {
        static String TAG = "FDRedis";
        static void logIt(string msg)
        {
            System.Diagnostics.Trace.WriteLine($"[{TAG}]: {msg}");
        }
        static void Main(string[] args)
        {
            System.Configuration.Install.InstallContext _args = new System.Configuration.Install.InstallContext(null, args);
            if (_args.IsParameterTrue("debug"))
            {
                System.Console.WriteLine("wait for debugger, press any key to continue...");
                System.Console.ReadKey();
            }
            if (_args.IsParameterTrue("start-server"))
            {
                bool own = false;
                System.Threading.EventWaitHandle evt = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, TAG, out own);
                if (own)
                {
                    start_server(evt, _args.Parameters);
                }
                else
                {
                    // server already running.
                }
            }
            else if (_args.IsParameterTrue("kill-server"))
            {
                try
                {
                    System.Threading.EventWaitHandle evt = System.Threading.EventWaitHandle.OpenExisting(TAG);
                    evt.Set();
                    evt.Close();
                }
                catch (Exception) { }
            }
            else
            {
                test();
            }
        }

        static void start_server(System.Threading.EventWaitHandle quit, System.Collections.Specialized.StringDictionary args)
        {
            int port = getFreePort();
            try
            {
                Uri baseAddress = new Uri(string.Format("http://localhost:{0}/", port));
                WebServiceHost svcHost = new WebServiceHost(typeof(Program), baseAddress);
                WebHttpBinding b = new WebHttpBinding();
                b.Name = "FDRedis";
                b.HostNameComparisonMode = HostNameComparisonMode.Exact;
                svcHost.AddServiceEndpoint(typeof(IFDRedis), b, "");
                svcHost.Open();
                logIt($"WebService is running at http://localhost:{port}/");
                System.Console.WriteLine($"WebService is running at http://localhost:{port}/");
                quit.WaitOne();
                logIt("Service is going to terminated.");
                svcHost.Close();
            }
            catch (Exception) {  }
        }
        static void test()
        {
#if !true
            try
            {
                ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost");
                IDatabase db = redis.GetDatabase(1);
                //if (!db.KeyExists("test"))
                //    db.StringSet("test", "123");
                var value = db.StringGet("test");
                var eps = redis.GetEndPoints();
                var server = redis.GetServer(eps[0]);
                var keys = server.Keys(pattern: "*");
                foreach (var k in keys)
                {

                }
                //server.Shutdown();
            }
            catch (Exception) { }
#else
            System.Configuration.Configuration config = System.Configuration.ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            string s = config.AppSettings.Settings["redis"]?.Value;
            string app = System.IO.Path.GetFullPath(s);
            var v = config.AppSettings.Settings["path"];
#endif
        }

        static int getFreePort(int port = 0)
        {
            int ret = -1;
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            while (ret <= 0)
            {
                try
                {
                    IPEndPoint ipe = new IPEndPoint(IPAddress.Loopback, port);
                    s.Bind(ipe);
                    ret = ((IPEndPoint)s.LocalEndPoint).Port;
                }
                catch (Exception)
                {
                    port = 0;
                }
                finally
                {
                    s.Close();
                }
            }
            return ret;
        }
        #region access redis server
        static string get_redis_root()
        {
            string ret = "";
            System.Configuration.Configuration config = System.Configuration.ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            string s = config.AppSettings.Settings["redispath"]?.Value;
            s = System.IO.Path.GetFullPath(s);
            //s = System.IO.Path.Combine(s, "redis-server.exe");
            if (System.IO.Directory.Exists(s))
                ret = s;
            return ret;
        }
        static ConnectionMultiplexer start_redis_server()
        {
            ConnectionMultiplexer redis = null;
            string exe = System.IO.Path.Combine(get_redis_root(), "redis-server.exe");
            int port = getFreePort(6379);
            Process p = new Process();
            p.StartInfo.FileName= System.IO.Path.Combine(get_redis_root(), "redis-server.exe");

            return redis;
        }
        #endregion
        #region web service handler
        public Stream SayHello(string name)
        {
            Stream ret = null;
            try
            {
#if !true
                Dictionary<string, object> dic = new Dictionary<string, object>();
                dic.Add("function", "SayHello");
                dic.Add("name", name);
                System.ServiceModel.Web.WebOperationContext op = System.ServiceModel.Web.WebOperationContext.Current;
                var jss = new System.Web.Script.Serialization.JavaScriptSerializer();
                string s = jss.Serialize(dic);
                ret = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(s));
#else
                StringBuilder sb = new StringBuilder();
                sb.Append("SayHello: ");
                sb.Append(name);
                sb.AppendLine();
                sb.Append("OK");
                sb.AppendLine();
                ret = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
#endif
            }
            catch (Exception) { }
            return ret;
        }
        public Stream handleGetKeyWithId(string id, string key)
        {
            Stream ret = null;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"id={id}");
            sb.AppendLine($"{key}=value");
            ret = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
            return ret;
        }
        public Stream handleGetKey(string key)
        {
            return handleGetKeyWithId("0", key);
        }
        public Stream handlePostKeyValue(string id, Stream request)
        {
            System.ServiceModel.OperationContext op = System.ServiceModel.OperationContext.Current;
            System.ServiceModel.Web.WebOperationContext webop = System.ServiceModel.Web.WebOperationContext.Current;
            string content_type = webop.IncomingRequest.Headers["Content-Type"];
            string s = "OK";
            using (StreamReader sr = new StreamReader(request))
            {
                s = sr.ReadToEnd();
            }
            if (content_type == "application/x-www-form-urlencoded")
            {
                NameValueCollection kvp;
                kvp = HttpUtility.ParseQueryString(s);
            }
            else if (content_type == "application/json")
            {

            }
            else if (content_type == "plain/text")
            {

            }
            else
            {

            }
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"{s}{System.Environment.NewLine}"));
        }
        #endregion
    }
}
