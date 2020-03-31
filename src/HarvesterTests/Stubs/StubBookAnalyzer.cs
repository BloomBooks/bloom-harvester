using BloomHarvester;

namespace BloomHarvesterTests.Stubs
{

	class StubBookAnalyzer : IBookAnalyzer
	{
		internal int NextGetBookComputedLevelResult { get; set; }

		public int GetBookComputedLevel()
		{
			return NextGetBookComputedLevelResult;
		}
	}
}
