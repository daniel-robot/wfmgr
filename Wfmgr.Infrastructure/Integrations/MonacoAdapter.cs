using System.Text;
using Wfmgr.Application.Integrations;
using Microsoft.Extensions.Configuration;

namespace Wfmgr.Infrastructure.Integrations;

public class MonacoAdapter : IMonacoAdapter
{
    private readonly string _dropRoot;

    public MonacoAdapter(IConfiguration configuration)
    {
        _dropRoot = configuration["Monaco:DropRoot"]
            ?? throw new InvalidOperationException("Configuration key Monaco:DropRoot is required.");
    }

    public Task SendToMonacoImportAsync(Guid caseId, string payloadJson, CancellationToken ct)
    {
        return DropImportAsync(caseId, payloadJson, ct);
    }

    public async Task DropImportAsync(Guid caseId, string payloadJson, CancellationToken ct)
    {
        var caseFolder = Path.Combine(_dropRoot, caseId.ToString("N"));
        Directory.CreateDirectory(caseFolder);

        var manifestPath = Path.Combine(caseFolder, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, payloadJson, Encoding.UTF8, ct);

        var triggerPath = Path.Combine(caseFolder, "READY.trigger");
        await File.WriteAllTextAsync(triggerPath, DateTimeOffset.UtcNow.ToString("O"), Encoding.UTF8, ct);
    }
}
