using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Clustering
{
	/// <summary>
	/// Record start and stop times of named events, then later format all timings in a single message.
	/// </summary>
	public class Timer
	{
		public static readonly Timer Instance = new Timer();

		public static void Start(string eventName)
		{
			Instance.StartEvent(eventName);
		}

		public static void Stop(string eventName)
		{
			Instance.StopEvent(eventName);
		}

		/// <summary>
		/// Stop all timers, compose a message with all timings, write it to the log,
		/// and Reset the Timer instance.
		/// </summary>
		/// <returns>The log.</returns>
		public static string Log()
		{
			var msg = Instance.FinalTimes();
			Logger.Info(msg);
			Instance.Reset();
			return msg;
		}

		Dictionary<string, Stopwatch> Timers { get; set; }

		public Dictionary<string, double> SecondsPerEvent { get; set; }

		List<string> OrderedEvents { get; set; }

		public Timer()
		{
			Reset();
		}

		public void StartEvent(string eventName) 
		{
			var timer = new Stopwatch();
			timer.Start();
			Timers[eventName] = timer;
			OrderedEvents.Add(eventName);
		}

		public void StopEvent(string eventName)
		{
			Stopwatch timer = null;
			if (Timers.TryGetValue(eventName, out timer)) 
			{
				timer.Stop();
				var seconds = timer.ElapsedMilliseconds / 1000.0;
				SecondsPerEvent[eventName] = seconds;
			}
		}

		public void StopAll()
		{
			foreach (var eventName in OrderedEvents)
			{
				if (!SecondsPerEvent.ContainsKey(eventName))
					StopEvent(eventName);
			}
		}

		public void Reset()
		{
			Timers = new Dictionary<string, Stopwatch>();
			SecondsPerEvent = new Dictionary<string, double>();
			OrderedEvents = new List<string>();
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendLine("Event times (sec):");
			foreach (var eventName in OrderedEvents)
			{
				double seconds = 0;
				if (SecondsPerEvent.TryGetValue(eventName, out seconds))
					sb.AppendLine($"    {eventName} = {seconds}");
				else
					sb.AppendLine($"    {eventName} = running...");
			}
			return sb.ToString();
		}

		public string FinalTimes()
		{
			StopAll();
			return ToString();
		}
	}
}
