namespace BloomHarvester.Parse.Model
{
	public enum HarvestState
	{
		Unknown,
		New, // set by parse code when the user uploads a book
		Updated, // set by parse code when the user re-uploads a book
		InProgress,
		Done,
		Failed,
		Aborted
	}
}
