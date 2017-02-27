using NUnit.Framework;
using static System.Math; // New C# 6.0 feature that allows one to import static methods and call them without their class name.

namespace HilbertTransformationTests
{

	[TestFixture]
	public class KendallTauCorrelationTests
	{
		public static int[] OneToTen = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

		#region Tau-a

		[Test]
		public void TauA_SameOrder()
		{
			var kendall = new KendallTauCorrelation<int, int>(
				(int value) => value,
				(int value) => value * 10
			);
			Assert.AreEqual(
				1.0,
				kendall.TauA(OneToTen),
				"Numbers that sort in the same order should be perfectly correlated."
			);
		}

		[Test]
		public void TauA_ReverseOrder()
		{
			var kendall = new KendallTauCorrelation<int, int>(
				(int value) => value,
				(int value) => value * -10
			);
			Assert.AreEqual(
				-1.0,
				kendall.TauA(OneToTen),
				"Numbers that sort in reverse order should be perfectly anti-correlated."
			);
		}

		[Test]
		public void TauA_OneSwap()
		{
			var reordered = new[] { 1, 2, 3, 5, 4, 6, 7, 8, 9, 10 };
			var kendall = new KendallTauCorrelation<int, int>(
				(int value) => value,
				(int value) => reordered[value - 1]
			);
			Assert.AreEqual(
				43.0 / 45.0,
				kendall.TauA(OneToTen),
				0.00001,
				"If a single number is out of place the sequences should be almost perfectly correlated."
			);
		}

		#endregion

		#region Tau-b

		[Test]
		public void TauB_SameOrder()
		{
			var kendall = new KendallTauCorrelation<int, int>(
				(int value) => value,
				(int value) => value * 10
			);
			Assert.AreEqual(
				1.0,
				kendall.TauB(OneToTen),
				"Numbers that sort in the same order should be perfectly correlated."
			);
		}

		[Test]
		public void TauB_ReverseOrder()
		{
			var kendall = new KendallTauCorrelation<int, int>(
				(int value) => value,
				(int value) => value * -10
			);
			Assert.AreEqual(
				-1.0,
				kendall.TauB(OneToTen),
				"Numbers that sort in reverse order should be perfectly anti-correlated."
			);
		}

		[Test]
		public void TauB_OneSwap_NoTies()
		{
			var reordered = new[] { 1, 2, 3, 5, 4, 6, 7, 8, 9, 10 };
			var kendall = new KendallTauCorrelation<int, int>(
				(int value) => value,
				(int value) => reordered[value - 1]
			);
			Assert.AreEqual(
				43.0 / 45.0,
				kendall.TauB(OneToTen),
				0.00001,
				"If a single number is out of place the sequences should be almost perfectly correlated."
			);
		}

		[Test]
		public void TauB_Ties()
		{
			var reordered = new[] { 1, 1, 1, 4, 5, 6, 7, 8, 9, 10 };
			var kendall = new KendallTauCorrelation<int, int>(
				(int value) => value,
				(int value) => reordered[value - 1]
			);
			Assert.AreEqual(
				42.0 / Sqrt(42.0 * 45.0),
				kendall.TauB(OneToTen),
				0.00001,
				"Adding a few ties should be almost perfectly correlated."
			);
		}

		#endregion
	}
}