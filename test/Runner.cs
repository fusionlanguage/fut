public class Runner
{
	public static int Main()
	{
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
