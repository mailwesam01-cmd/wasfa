using Wasfa.Domain.Model;

namespace Wasfa.Safety.Contracts;

public interface ISafetyRule
{
    string Id { get; }
    string Name { get; }
    IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed);
}

public interface ISafetyEngine
{
    Task<IReadOnlyList<SafetyFinding>> EvaluatePrescriptionAsync(PatientContext patient, ProposedMedication proposed, CancellationToken ct = default);
}
