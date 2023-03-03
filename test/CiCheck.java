import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;

public class CiCheck
{
	public static void main(String[] args) throws IOException
	{
		final CiSystem system = CiSystem.new_();
		final CiConsoleParser parser = new CiConsoleParser();
		parser.program = new CiProgram();
		parser.program.parent = system;
		parser.program.system = system;
		for (String inputFilename : args) {
			byte[] input = Files.readAllBytes(Path.of(inputFilename));
			parser.parse(inputFilename, input, input.length);
		}
		final CiSema sema = new CiSema();
		sema.process(parser.program);
		System.out.println("PASSED");
	}
}
