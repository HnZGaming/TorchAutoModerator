using NLog;
using NLog.Config;
using NLog.Targets;
using Utils.Torch;

namespace Utils.General
{
    internal class FileLoggingConfigurator
    {
        public interface IConfig
        {
            bool SuppressWpfOutput { get; }
            bool EnableLoggingTrace { get; }
            string LogFilePath { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly FileTarget _target;
        readonly LoggingRule _rule;

        public FileLoggingConfigurator(string targetName, string namePattern, string defaultFilePath)
        {
            _target = new FileTarget
            {
                Name = targetName,
            };

            _rule = new LoggingRule
            {
                LoggerNamePattern = namePattern,
                Final = true,
            };

            // default config
            _target.FileName = defaultFilePath;
            _rule.Targets.Add(_target);
            _rule.Targets.Add(TorchUtils.GetWpfTarget());
            _rule.EnableLoggingForLevels(LogLevel.Info, LogLevel.Off);
        }

        public void Initialize()
        {
            LogManager.Configuration.AddTarget(_target);
            LogManager.Configuration.LoggingRules.Insert(0, _rule);
            LogManager.Configuration.Reload();
        }

        public void Reconfigure(IConfig config)
        {
            _target.FileName = config.LogFilePath;

            _rule.Targets.Clear();
            _rule.Targets.Add(_target);

            if (!config.SuppressWpfOutput)
            {
                _rule.Targets.Add(TorchUtils.GetWpfTarget());
            }

            var minLevel = config.EnableLoggingTrace ? LogLevel.Trace : LogLevel.Info;
            _rule.DisableLoggingForLevel(LogLevel.Trace);
            _rule.DisableLoggingForLevel(LogLevel.Debug);
            _rule.EnableLoggingForLevels(minLevel, LogLevel.Off);

            LogManager.ReconfigExistingLoggers();

            Log.Info($"Reconfigured; wpf={!config.SuppressWpfOutput}, minlevel={minLevel}");
        }
    }
}