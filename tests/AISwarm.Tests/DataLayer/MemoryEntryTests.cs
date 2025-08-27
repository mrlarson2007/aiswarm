using System.ComponentModel.DataAnnotations;
using AISwarm.DataLayer.Entities;
using Shouldly;

namespace AISwarm.Tests.DataLayer;

/// <summary>
/// Tests for MemoryEntry entity validation and behavior.
/// Following TDD approach - testing edge cases and validation first.
/// </summary>
public class MemoryEntryTests
{
    public class ValidationTests : MemoryEntryTests
    {
        [Fact]
        public void WhenCreatingMemoryEntryWithEmptyNamespace_ShouldFailValidation()
        {
            // Arrange
            var memoryEntry = new MemoryEntry
            {
                Id = "test-id",
                Namespace = "", // Empty namespace should fail validation
                Key = "test-key",
                Value = "test-value"
            };

            // Act & Assert
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(memoryEntry);
            var isValid = Validator.TryValidateObject(memoryEntry, validationContext, validationResults, validateAllProperties: true);

            // Assert - Should fail validation
            isValid.ShouldBeFalse();
            validationResults.ShouldContain(vr => vr.MemberNames.Contains("Namespace"));
        }
    }
}