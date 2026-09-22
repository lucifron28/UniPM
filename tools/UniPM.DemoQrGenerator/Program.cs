using UniPM.DemoQrGenerator;

var outputDirectory = DemoQrArguments.ParseOutputDirectory(args);
var result = await DemoQrWriter.WriteAsync(outputDirectory);
Console.WriteLine($"Generated {result.PngCount} demo QR codes and {result.IndexPath}.");
