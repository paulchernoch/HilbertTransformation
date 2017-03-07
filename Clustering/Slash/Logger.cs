using System;
using System.IO;

namespace Clustering
{
	/// <summary>
	/// For writing log messages.
	/// </summary>
	public class Logger
	{
		public static readonly Logger Instance = new Logger();

		public bool WriteToStandardOut { get; set; } = false;
		public bool WriteToStandardError { get; set; } = false;
		public bool WriteToFile { get; set; } = false;
		public string Level { get; set; } = "info";
		public string LogFile { get; set; } = "slash.log";

		public bool ShouldIgnore(string messageLevel)
		{
			if (!WriteToStandardOut && !WriteToStandardError && !WriteToFile) return false;
			if (Level.Equals("debug")) return false;
			if (messageLevel.Equals("debug")) return true;
			if (Level.Equals("info")) return false;
			if (messageLevel.Equals("info")) return true;
			if (Level.Equals("warn")) return false;
			if (messageLevel.Equals("warn")) return true;
			if (Level.Equals("error")) return false;
			return true; // All messages ignored.
		}

		bool IsErrorOrWarning(string messageLevel)
		{
			return messageLevel.Equals("error") || messageLevel.Equals("warn");
		}

		bool Write(string message, string messageLevel)
		{
			messageLevel = messageLevel.ToLowerInvariant();
			var msgToWrite = $"{messageLevel.ToUpperInvariant()}: message\n";
			if (ShouldIgnore(messageLevel))
				return false;
			lock(this)
			{
				if (WriteToStandardError && IsErrorOrWarning(messageLevel))
					Console.Error.Write(msgToWrite);
				else if (WriteToStandardOut)
					Console.Out.Write(msgToWrite);
				if (WriteToFile)
					File.AppendAllText(LogFile, msgToWrite);
			}
			return true;
		}

		public bool LogDebug(string message) { return Write(message, "debug"); }
		public bool LogInfo(string message) { return Write(message, "info"); }
		public bool LogWarn(string message) { return Write(message, "warn"); }
		public bool LogError(string message) { return Write(message, "error"); }

		public static bool Debug(string message) { return Instance.LogDebug(message); }
		public static bool Info(string message) { return Instance.LogInfo(message); }
		public static bool Warn(string message) { return Instance.LogWarn(message); }
		public static bool Error(string message) { return Instance.LogError(message); }

		public static void SetupForTests(string logFileName = "slash-test.log")
		{
			Logger.Instance.LogFile = logFileName;
			Logger.Instance.Level = "info";
			Logger.Instance.WriteToFile = true;
			Logger.Instance.WriteToStandardOut = true;
		}

	}
}
