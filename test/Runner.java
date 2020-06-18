import java.util.Locale;

public class Runner
{
	public static void main(String[] args)
	{
		Locale.setDefault(Locale.ROOT);
		if (Test.run()) {
			System.out.println("PASSED");
		}
		else {
			System.out.println("FAILED");
			System.exit(1);
		}
	}
}
