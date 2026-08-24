using ConvertPdf.Extensions;
using ConvertPdf.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddLogging();
builder.Services.AddHttpLogging();
builder.Services.AddSingleton(_ => new SemaphoreSlim(1, 1));
builder.Services.AddSingleton(_ => new AllowedExtensions());
WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.UseHttpLogging();

app.MapPost("api/convert", async ([FromServices] SemaphoreSlim semaphore, [FromServices] ILogger<Program> logger, [FromServices] AllowedExtensions allowedExtensions, IFormFile file) =>
{
    if (file == null)
    {
        return Results.BadRequest("File not found");
    }

    string fileExtension = file.GetFileExtension();

    if (allowedExtensions.NoContains(fileExtension))
    {
        return Results.BadRequest("Extension Not Accepted.");
    }

    string inputDirectory = Path.Combine(Path.GetTempPath(), "input");
    string outputDirectory = Path.Combine(Path.GetTempPath(), "output");
    string fileName = $"{Guid.NewGuid()}{fileExtension}";
    string inputPath = Path.Combine(inputDirectory, fileName);
    string outputPath = Path.Combine(outputDirectory, Path.ChangeExtension(fileName, ".pdf"));

    await semaphore.WaitAsync();

    logger.LogInformation("Initialize Process Convert To PDF {Date}", DateTime.Now);

    try
    {
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(outputDirectory);

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
            logger.LogError("Error converting {FileExtension} to PDF. ExitCode: {ExitCode}. Error: {Error}", fileExtension, process.ExitCode, error);

            return Results.Problem($"Erro ao converter {fileExtension} para PDF: {error}");
        }

        byte[] pdf = await File.ReadAllBytesAsync(outputPath);

        logger.LogInformation("Finalize Process Convert To PDF {Date}", DateTime.Now);

        return Results.File(pdf, "application/pdf", "documento.pdf");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error converting {FileExtension} to PDF", fileExtension);
        throw;
    }
    finally
    {
        if (File.Exists(inputPath))
        {
            File.Delete(inputPath);
        }

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        semaphore.Release();
    }
})
.DisableAntiforgery();

app.Run();