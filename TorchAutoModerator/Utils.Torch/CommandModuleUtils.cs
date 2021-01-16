using System;
using System.Linq;
using System.Threading.Tasks;
using Torch.Commands;
using Utils.General;
using VRageMath;

namespace Utils.Torch
{
    public static class CommandModuleUtils
    {
        public static void CatchAndReport(this CommandModule self, Action f)
        {
            try
            {
                f();
            }
            catch (Exception e)
            {
                ReportGenerator.LogAndRespond(self, e, m => self.Context.Respond(m, Color.Red));
            }
        }

        public static async void CatchAndReport(this CommandModule self, Func<Task> f)
        {
            try
            {
                await f();
            }
            catch (Exception e)
            {
                ReportGenerator.LogAndRespond(self, e, m => self.Context.Respond(m, Color.Red));
            }
        }

        public static void GetOrSetProperty(this CommandModule self, object config, string propertyName)
        {
            var properties = config.GetType().GetProperties();
            var property = properties.First(p => p.Name == propertyName);

            if (self.Context.Args.TryGetFirst(out var arg))
            {
                var value = Parse(config.GetType(), arg);
                property.SetValue(config, value);
                self.Context.Respond($"New value set: {arg}");
            }
            else
            {
                var value = property.GetValue(config);
                self.Context.Respond($"> {value}");
            }
        }

        static object Parse(Type type, string value)
        {
            if (type == typeof(string)) return value;
            if (type == typeof(bool)) return bool.Parse(value);
            if (type == typeof(int)) return int.Parse(value);
            if (type == typeof(float)) return float.Parse(value);
            if (type == typeof(double)) return double.Parse(value);
            if (type == typeof(long)) return long.Parse(value);
            if (type == typeof(ulong)) return ulong.Parse(value);
            throw new ArgumentException($"unsupported type: {type}");
        }
    }
}