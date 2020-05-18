using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SIL.IO;

namespace BloomHarvester.IO
{
	interface IFileIO
	{
		/// <summary>
		/// Returns true if the path exists
		/// </summary>
		bool Exists(string path);

		/// <summary>
		/// Opens a text file, reads all lines of the file into a string array, and then closes the file.
		/// </summary>
		string[] ReadAllLines(string path);
	}

	/// <summary>
	/// This class is an adapter that makes SIL.IO.RobustFile implement an interface,
	/// so that it can be mocked out for unit tests.
	/// This means our tests can specify the specific IO results they want
	/// </summary>
	internal class FileIO : IFileIO
	{
		public bool Exists(string path) => RobustFile.Exists(path);

		public string[] ReadAllLines(string path) => RobustFile.ReadAllLines(path);

		// ENHANCE: Add more and more of RobustFile's functionality to the wrapper, as it is needed by the unit tests
	}
}
