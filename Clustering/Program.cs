using System;
namespace Clustering
{
	/// <summary>
	/// Entry point for the "SLASH" console application.
	/// </summary>
	public class Program
	{
		/// <summary>
		/// Program entry point.
		/// </summary>
		/// <param name="args">The command-line arguments.
		/// See SlashCommand.HelpMessage for information about the command line arguments.
		/// </param>
		static void Main(string[] args)
		{
			var slashCommand = new SlashCommand(args);
			slashCommand.Execute();
		}
	}
}
