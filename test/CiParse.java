import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;

public class CiParse
{
	public static void main(String[] args) throws IOException
	{
		CiSystem system = CiSystem.new_();
		CiParser parser = new CiParser();
		parser.program = new CiProgram();
		parser.program.parent = system;
		parser.program.system = system;
		for (String inputFilename : args) {
			byte[] input = Files.readAllBytes(Path.of(inputFilename));
			parser.parse(inputFilename, input, input.length);
		}
		System.out.println("PASSED");
	}
}
