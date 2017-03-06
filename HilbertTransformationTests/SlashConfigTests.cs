using System;
using Clustering;
using NUnit.Framework;

namespace HilbertTransformationTests
{
	[TestFixture]
	public class SlashConfigTests
	{
		[Test]
		public void SlashConfigSerialization()
		{
			var config = new SlashConfig("data.csv", "clustered.csv", "id", "category");
			config.Output.LogFile = "slash-log.txt";
			var asYAML = config.ToString();
			Console.WriteLine(asYAML);

			var configRoundTrip = SlashConfig.Deserialize(asYAML);

			Assert.AreEqual(config, configRoundTrip, "Deserialized object does not match.");
		}
	}
}
