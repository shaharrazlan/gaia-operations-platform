using Gaia.Core.Errors;

namespace Gaia.Core.Tests.Errors;

public class ErrorTypesTests
{
    public static TheoryData<GaiaException, string> Cases => new()
    {
        { new ValidationException("bad", "fieldA"), ErrorCodes.Validation },
        { new ValidationException("bad formula", "formula", ErrorCodes.FormulaInvalid), ErrorCodes.FormulaInvalid },
        { new NotFoundException("missing"), ErrorCodes.OperationNotFound },
        { new DomainException("division by zero"), ErrorCodes.Domain },
        { new ExternalServiceException("down"), ErrorCodes.ExternalService },
        { new ExternalServiceException("slow", ErrorCodes.ExternalServiceTimeout), ErrorCodes.ExternalServiceTimeout },
        { new ConflictException("dup", ErrorCodes.DuplicateId), ErrorCodes.DuplicateId },
        { new ConflictException("stale", ErrorCodes.EditConflict), ErrorCodes.EditConflict },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void T8_each_exception_type_carries_its_error_code(GaiaException error, string expectedCode)
    {
        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public void T8_validation_error_names_the_field()
    {
        var error = new ValidationException("Field A is required.", "fieldA");

        Assert.Equal("fieldA", error.Field);
        Assert.Equal("Field A is required.", error.Message);
    }
}
