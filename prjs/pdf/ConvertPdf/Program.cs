using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddLogging();
builder.Services.AddHttpLogging();
builder.Services.AddSingleton(_ => new SemaphoreSlim(1, 1));
WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.UseHttpLogging();
app.MapPost("api/convert", async (SemaphoreSlim semaphore, [FromServices] ILogger<Program> logger, IFormFile file) =>
{
    await semaphore.WaitAsync();
    logger.LogInformation($"Initialize Process Convert Docx To PDF {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}");
    try
    {
        string inputDirectory = Path.Combine(Path.GetTempPath(), "input");
        string outputDirectory = Path.Combine(Path.GetTempPath(), "output");

        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(outputDirectory);

        string fileName = $"{Guid.NewGuid()}.docx";
        string inputPath = Path.Combine(inputDirectory, fileName);
        string outputPath = Path.Combine(outputDirectory, Path.ChangeExtension(fileName, ".pdf"));

        await using (FileStream stream = File.Create(inputPath))
        {
            await file.CopyToAsync(stream);
        }

        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = "libreoffice",
            Arguments = $"--headless --convert-to pdf --outdir \"{outputDirectory}\" \"{inputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(processInfo)!;

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            return Results.Problem($"Erro ao converter DOCX para PDF: {error}");
        }

        byte[] pdf = await File.ReadAllBytesAsync(outputPath);

        File.Delete(inputPath);
        File.Delete(outputPath);

        logger.LogInformation(@$"Finalize Process Convert Docx To PDF {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}");
        return Results.File(pdf, "application/pdf", "documento.pdf");
    }
    catch(Exception ex)
    {
        logger.LogInformation($"Error: {ex.Message} + {ex.InnerException}");
        throw;
    }
    finally
    {
        semaphore.Release();
    }
})
.DisableAntiforgery();
app.Run();