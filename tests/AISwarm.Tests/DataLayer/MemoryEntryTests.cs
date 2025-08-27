using System.ComponentModel.DataAnnotations;
using AISwarm.DataLayer.Entities;
using AISwarm.DataLayer;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
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

        [Fact]
        public void WhenCreatingMemoryEntryWithEmptyKey_ShouldFailValidation()
        {
            // Arrange
            var memoryEntry = new MemoryEntry
            {
                Id = "test-id",
                Namespace = "test-namespace",
                Key = "", // Empty key should fail validation
                Value = "test-value"
            };

            // Act & Assert
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(memoryEntry);
            var isValid = Validator.TryValidateObject(memoryEntry, validationContext, validationResults, validateAllProperties: true);

            // Assert - Should fail validation
            isValid.ShouldBeFalse();
            validationResults.ShouldContain(vr => vr.MemberNames.Contains("Key"));
        }

        [Fact]
        public void WhenCreatingMemoryEntryWithEmptyValue_ShouldFailValidation()
        {
            // Arrange
            var memoryEntry = new MemoryEntry
            {
                Id = "test-id",
                Namespace = "test-namespace",
                Key = "test-key",
                Value = "" // Empty value should fail validation
            };

            // Act & Assert
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(memoryEntry);
            var isValid = Validator.TryValidateObject(memoryEntry, validationContext, validationResults, validateAllProperties: true);

            // Assert - Should fail validation
            isValid.ShouldBeFalse();
            validationResults.ShouldContain(vr => vr.MemberNames.Contains("Value"));
        }
    }

    public class DatabaseIntegrationTests : MemoryEntryTests, IDisposable
    {
        private readonly CoordinationDbContext _dbContext;
        private readonly FakeTimeService _timeService;

        public DatabaseIntegrationTests()
        {
            _timeService = new FakeTimeService();
            var options = new DbContextOptionsBuilder<CoordinationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _dbContext = new CoordinationDbContext(options);
        }

        [Fact]
        public async Task WhenSavingMemoryEntryToDatabase_ShouldPersistSuccessfully()
        {
            // Arrange
            var memoryEntry = new MemoryEntry
            {
                Id = "test-id",
                Namespace = "test-namespace", 
                Key = "test-key",
                Value = "test-value"
            };

            // Act
            _dbContext.MemoryEntries.Add(memoryEntry);
            await _dbContext.SaveChangesAsync();

            // Assert - Check database directly
            var savedEntry = await _dbContext.MemoryEntries.FindAsync("test-id");
            savedEntry.ShouldNotBeNull();
            savedEntry.Namespace.ShouldBe("test-namespace");
            savedEntry.Key.ShouldBe("test-key");
            savedEntry.Value.ShouldBe("test-value");
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }
    }
}