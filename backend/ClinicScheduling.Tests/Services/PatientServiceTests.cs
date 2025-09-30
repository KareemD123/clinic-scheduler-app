using Xunit;
using Moq;
using FluentAssertions;
using ClinicScheduling.Application.Services;
using ClinicScheduling.Application.DTOs.Patients;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Domain.Interfaces;

namespace ClinicScheduling.Tests.Services;

public class PatientServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IRepository<Patient>> _mockPatientRepo;
    private readonly PatientService _patientService;

    public PatientServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPatientRepo = new Mock<IRepository<Patient>>();
        _mockUnitOfWork.Setup(u => u.Patients).Returns(_mockPatientRepo.Object);
        _patientService = new PatientService(_mockUnitOfWork.Object);
    }

    [Fact]
    public async Task CreatePatient_ValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new CreatePatientRequest
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Email = "john@example.com",
            Phone = "555-0100",
            Address = new AddressDto
            {
                Street = "123 Main St",
                City = "Springfield",
                State = "IL",
                ZipCode = "62701"
            }
        };

        var expectedPatient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Email = request.Email,
            Phone = request.Phone,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockPatientRepo.Setup(r => r.AddAsync(It.IsAny<Patient>()))
            .ReturnsAsync(expectedPatient);

        // Act
        var result = await _patientService.CreatePatientAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.FirstName.Should().Be("John");
        result.Data.LastName.Should().Be("Doe");
        _mockPatientRepo.Verify(r => r.AddAsync(It.IsAny<Patient>()), Times.Once);
    }

    [Fact]
    public async Task GetPatientById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _mockPatientRepo.Setup(r => r.GetByIdAsync(nonExistentId))
            .ReturnsAsync((Patient?)null);

        // Act
        var result = await _patientService.GetPatientByIdAsync(nonExistentId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("PATIENT_NOT_FOUND");
        result.Data.Should().BeNull();
    }
}
