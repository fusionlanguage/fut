using System.Globalization;

public class Runner
{
	public static int Main()
	{
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		if (Test.Run()) {
			System.Console.WriteLine("PASSED");
			return 0;
		}
		else {
			System.Console.WriteLine("FAILED");
			return 1;
		}
	}
}
