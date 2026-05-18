using System.Collections.ObjectModel;
using Wfmgr.Contracts.Contouring;
using Wfmgr.Contracts.ExternalEvents;
using Wfmgr.Contracts.Monaco;
using Wfmgr.Contracts.Prescription;
using Wfmgr.Contracts.Sagas;
using Wfmgr.Contracts.Scheduling;

namespace Wfmgr.Contracts;

/// <summary>
/// Resolves a message type's <see cref="Type.FullName"/> back to its CLR <see cref="Type"/>.
/// Used by the outbox publisher to deserialize stored payloads into the correct typed
/// contract before publishing them on the bus.
/// </summary>
public static class KnownMessageTypes
{
    private static readonly IReadOnlyDictionary<string, Type> Map =
        new ReadOnlyDictionary<string, Type>(
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                [typeof(SendImagesToContourTool.V1).FullName!] = typeof(SendImagesToContourTool.V1),
                [typeof(QueryContourStatus.V1).FullName!]      = typeof(QueryContourStatus.V1),
                [typeof(SendToMonacoImport.V1).FullName!]      = typeof(SendToMonacoImport.V1),
                [typeof(QueryTreatmentProgress.V1).FullName!]  = typeof(QueryTreatmentProgress.V1),
                [typeof(GeneratePrescription.V1).FullName!]    = typeof(GeneratePrescription.V1),
                [typeof(IngestExternalEvent.V1).FullName!]     = typeof(IngestExternalEvent.V1),
                [typeof(SyncSchedule.V1).FullName!]            = typeof(SyncSchedule.V1),
                [typeof(StartContouringSaga.V1).FullName!]     = typeof(StartContouringSaga.V1),
                [typeof(ContourCompleted.V1).FullName!]        = typeof(ContourCompleted.V1),
                [typeof(MonacoImportAcked.V1).FullName!]       = typeof(MonacoImportAcked.V1),
            });

    public static bool TryResolve(string? typeFullName, out Type type)
    {
        if (!string.IsNullOrEmpty(typeFullName) && Map.TryGetValue(typeFullName, out var resolved))
        {
            type = resolved;
            return true;
        }
        type = typeof(object);
        return false;
    }
}
