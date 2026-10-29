using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;

namespace HsClassifier.Application.Interfaces;

public interface IVersionManager
{
    Task<Guid> CreateVersionAsync(string name, string btkiEdition, string regulationNumber,
        DateTime effectiveFrom, DateTime? effectiveTo, string createdBy);
    Task<NomenclatureVersion?> GetVersionAsync(Guid versionId);
    Task<NomenclatureVersion?> GetActiveVersionAsync(DateTime effectiveDate);
    Task<IReadOnlyList<NomenclatureVersion>> GetAllVersionsAsync();
    Task<bool> TransitionStateAsync(Guid versionId, ImportState newState);
    Task<bool> ActivateVersionAsync(Guid versionId);
    Task<bool> DeactivateVersionAsync(Guid versionId);
}
