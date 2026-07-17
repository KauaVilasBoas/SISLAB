using System.Security.Cryptography;
using System.Text;

namespace SISLAB.Modules.Inventory.Infrastructure.ReadModels;

/// <summary>
/// Derives a stable, deterministic ledger-row id from an event id and a slice index, so a single movement
/// event that fans out into several costed rows (one per FEFO batch allocation — card [E4] #109) still gets
/// an idempotent primary key per row: reprocessing the same Outbox event re-derives the exact same ids and
/// hits <c>ON CONFLICT (id) DO NOTHING</c> instead of duplicating the movement.
/// </summary>
/// <remarks>
/// Index 0 returns the event id unchanged, so single-row movements (an entry, a transfer, or a consumption
/// that came from one batch) keep the original "row id == event id" identity. Higher indices hash the event
/// id together with the index (MD5 → 16 bytes → Guid) — MD5 is used purely as a fast, deterministic mixing
/// function here, not for any security purpose.
/// </remarks>
internal static class DeterministicRowId
{
    public static Guid ForSlice(Guid eventId, int index)
    {
        if (index == 0)
            return eventId;

        byte[] seed = Encoding.UTF8.GetBytes($"{eventId:N}:{index}");
        byte[] hash = MD5.HashData(seed);
        return new Guid(hash);
    }
}
