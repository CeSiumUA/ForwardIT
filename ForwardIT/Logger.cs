using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace ForwardIT
{
    public class Logger
    {
        public Logger()
        {

        }
        public async Task LogExceptionAsync(Exception exception)
        {
            var excLog = new { Date = DateTime.Now, StackTrace = exception.StackTrace, Message = exception.ToString() };
            string log = Newtonsoft.Json.JsonConvert.SerializeObject(excLog, Newtonsoft.Json.Formatting.Indented);
            await LogAsync(log);
        }
        public async Task LogAsync(string Log)
        {
            Directory.CreateDirectory("conf");
            using(StreamWriter sw = new StreamWriter(@"conf\appLogs.json", true))
            {
                await sw.WriteLineAsync(Environment.NewLine);
                await sw.WriteLineAsync("----------------------------------------------------------------------------");
                await sw.WriteLineAsync(Log);
            }
        }
    }
}
