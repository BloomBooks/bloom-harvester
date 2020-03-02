using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.Parse
{
	internal class BookUpdateOperation : UpdateOperation
	{
		internal BookUpdateOperation()
		{
			UpdateFieldWithString("updateSource", "bloomHarvester");
		}

		override internal void Clear()
		{
			base.Clear();
			UpdateFieldWithString("updateSource", "bloomHarvester");
		}
	}
}
