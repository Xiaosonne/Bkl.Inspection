using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class BladeFacilityNameCompare : IComparer<string>
{
	public int Compare(string x, string y)
	{
		try
		{
			if (Regex.IsMatch(x, @"(\d+)") && Regex.IsMatch(y, @"(\d+)"))
			{
				var mat1 = Regex.Matches(x, @"(\d+)");
				var mat2 = Regex.Matches(y, @"(\d+)");
				if (mat1.Count > 0 && mat1[mat1.Count - 1].Groups.Count > 0 && mat2.Count > 0 && mat2[mat2.Count - 1].Groups.Count > 0)
				{
					var val = mat1[mat1.Count-1].Groups[mat1[0].Groups.Count - 1].Value;
					var val2 = mat2[mat2.Count - 1].Groups[mat2[0].Groups.Count - 1].Value;
					if (int.TryParse(val, out var x1) && int.TryParse(val2, out var y1))
					{
						return x1 - y1;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());

		}
		return x.CompareTo(y);
	}
}
