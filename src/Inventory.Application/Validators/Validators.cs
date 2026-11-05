using FluentValidation;

namespace Inventory.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator() { RuleFor(x => x.Identifier).NotEmpty().MaximumLength(200); RuleFor(x => x.Password).NotEmpty().MaximumLength(200); }
}
public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() { RuleFor(x => x.RefreshToken).NotEmpty(); }
}
public class PasswordResetSubmissionRequestValidator : AbstractValidator<PasswordResetSubmissionRequest>
{
    public PasswordResetSubmissionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Nik).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
    }
}
public class PasswordResetProcessRequestValidator : AbstractValidator<PasswordResetProcessRequest>
{
    public PasswordResetProcessRequestValidator()
    {
        RuleFor(x => x.Action).Must(x => x is "process" or "reject");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(100)
            .When(x => x.Action == "process");
        RuleFor(x => x.RejectionReason).MaximumLength(500);
    }
}

public class EmployeeRequestValidator : AbstractValidator<EmployeeRequest>
{
    public EmployeeRequestValidator()
    {
        RuleFor(x => x.EmployeeName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmployeeNik).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EmployeePhone).MaximumLength(30);
        RuleFor(x => x.Position).MaximumLength(100);
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.BusinessUnit).MaximumLength(100);
    }
}

public class UserCreateRequestValidator : AbstractValidator<UserCreateRequest>
{
    public UserCreateRequestValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.EmployeeEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.ProfilePhotoUrl).MaximumLength(500);
    }
}
public class UserUpdateRequestValidator : AbstractValidator<UserUpdateRequest>
{
    public UserUpdateRequestValidator()
    {
        RuleFor(x => x.EmployeeEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).MinimumLength(8).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Password));
        RuleFor(x => x.ProfilePhotoUrl).MaximumLength(500);
    }
}
public class UserRolesRequestValidator : AbstractValidator<UserRolesRequest>
{
    public UserRolesRequestValidator() { RuleFor(x => x.RoleIds).NotNull().NotEmpty(); }
}

public class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator()
    {
        RuleFor(x => x.CatName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CatCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.CatPrefix).NotEmpty().MaximumLength(20);
    }
}
public class GroupRequestValidator : AbstractValidator<GroupRequest>
{
    public GroupRequestValidator()
    {
        RuleFor(x => x.CatId).GreaterThan(0);
        RuleFor(x => x.GroupName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GroupCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.GroupPrefix).NotEmpty().MaximumLength(20);
    }
}
public class TypeRequestValidator : AbstractValidator<TypeRequest>
{
    public TypeRequestValidator()
    {
        RuleFor(x => x.CatId).GreaterThan(0);
        RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.TypeName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TypeCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.TypePrefix).NotEmpty().MaximumLength(20);
    }
}
public class BrandRequestValidator : AbstractValidator<BrandRequest>
{
    public BrandRequestValidator()
    {
        RuleFor(x => x.TypeId).GreaterThan(0).When(x => x.TypeId.HasValue);
        RuleFor(x => x.BrandName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BrandCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.BrandPrefix).NotEmpty().MaximumLength(20);
    }
}
public class ModelRequestValidator : AbstractValidator<ModelRequest>
{
    public ModelRequestValidator()
    {
        RuleFor(x => x.CatId).GreaterThan(0); RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.TypeId).GreaterThan(0); RuleFor(x => x.BrandId).GreaterThan(0);
        RuleFor(x => x.ModelName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ModelCode).NotEmpty().MaximumLength(30);
    }
}
public class BusinessUnitRequestValidator : AbstractValidator<BusinessUnitRequest>
{
    public BusinessUnitRequestValidator()
    {
        RuleFor(x => x.BusUnitCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.BusUnitPrefix).NotEmpty().MaximumLength(20);
    }
}
public class LocationRequestValidator : AbstractValidator<LocationRequest>
{
    public LocationRequestValidator()
    {
        RuleFor(x => x.BusUnitId).GreaterThan(0);
        RuleFor(x => x.LocationName).NotEmpty().MaximumLength(150);
    }
}
public class AssetStatusRequestValidator : AbstractValidator<AssetStatusRequest>
{
    public AssetStatusRequestValidator()
    {
        RuleFor(x => x.StatusCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.StatusName).NotEmpty().MaximumLength(100);
    }
}
public class ConditionRequestValidator : AbstractValidator<ConditionRequest>
{
    public ConditionRequestValidator()
    {
        RuleFor(x => x.ConditionCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.ConditionName).NotEmpty().MaximumLength(100);
    }
}

public class AssetRequestValidator : AbstractValidator<AssetRequest>
{
    public AssetRequestValidator()
    {
        RuleFor(x => x.CatId).GreaterThan(0); RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.TypeId).GreaterThan(0); RuleFor(x => x.BrandId).GreaterThan(0);
        RuleFor(x => x.AssetName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AssetType).IsInEnum();
        RuleFor(x => x.StatusId).GreaterThan(0);
        RuleFor(x => x.AssetPrefix).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).When(x => x.Quantity.HasValue);
        RuleFor(x => x.UnitOfMeasure).MaximumLength(30);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.AssetTag).MaximumLength(100);
        RuleFor(x => x.PurchaseOrderNumber).MaximumLength(100);
        RuleFor(x => x.SapMaterialNumber).MaximumLength(100);
        RuleFor(x => x.AssetValue).GreaterThanOrEqualTo(0).When(x => x.AssetValue.HasValue);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.BusinessUnit).MaximumLength(100);
    }
}
public class BulkBusinessUnitRequestValidator : AbstractValidator<BulkBusinessUnitRequest>
{
    public BulkBusinessUnitRequestValidator()
    {
        RuleFor(x => x.AssetIds).NotEmpty().Must(l => l.Count <= 1000).WithMessage("A maximum of 1000 assets per request is allowed.");
        RuleFor(x => x.BusUnitId).GreaterThan(0);
        RuleFor(x => x.BusinessUnit).MaximumLength(100);
    }
}

public class GoodItemRequestValidator : AbstractValidator<GoodItemRequest>
{
    public GoodItemRequestValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.ReferenceNumber).MaximumLength(100);
        RuleFor(x => x.AssetInformation).MaximumLength(500);
        RuleFor(x => x.Condition).MaximumLength(100);
        RuleFor(x => x.Remarks).MaximumLength(500);
        RuleFor(x => x.Specifications).MaximumLength(500);
        RuleFor(x => x.RequestJustification).MaximumLength(500);
        RuleFor(x => x.RequiredDuration).MaximumLength(100);
        RuleFor(x => x.IntendedPurpose).MaximumLength(500);
    }
}
public class GoodCreateRequestValidator : AbstractValidator<GoodCreateRequest>
{
    public GoodCreateRequestValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GoodName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TransactionType).IsInEnum();
        RuleFor(x => x.BusinessUnit).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RequestDate).NotEmpty();
        RuleFor(x => x.FormIssueDate).NotEmpty();
        RuleFor(x => x.PersonName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PersonNik).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PersonDepartment).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DestinationBusinessUnit).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RequestNumber).MaximumLength(100);
        RuleFor(x => x.ApproverName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.InventoryNotes).MaximumLength(1000);
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Items).SetValidator(new GoodItemRequestValidator());
    }
}
public class GoodStatusRequestValidator : AbstractValidator<GoodStatusRequest>
{
    public GoodStatusRequestValidator() { RuleFor(x => x.Status).IsInEnum(); RuleFor(x => x.Notes).MaximumLength(500); }
}

public class AuditCreateRequestValidator : AbstractValidator<AuditCreateRequest>
{
    public AuditCreateRequestValidator()
    {
        RuleFor(x => x.StockOpnameName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StockOpnameType).IsInEnum();
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.AuditDate).NotEmpty();
        RuleFor(x => x.BusinessUnit).MaximumLength(100);
        RuleFor(x => x.StockOpnameStatus).Must(s => StockOpnameStatuses.All.Contains(s!, StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.StockOpnameStatus)).WithMessage("Status must be Draft, InProgress or Completed.");
    }
}
public class AuditUpdateRequestValidator : AbstractValidator<AuditUpdateRequest>
{
    public AuditUpdateRequestValidator()
    {
        RuleFor(x => x.StockOpnameName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AuditDate).NotEmpty();
        RuleFor(x => x.BusinessUnit).MaximumLength(100);
        RuleFor(x => x.StockOpnameStatus).NotEmpty().Must(s => StockOpnameStatuses.All.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status must be Draft, InProgress or Completed.");
    }
}
public class AuditItemRequestValidator : AbstractValidator<AuditItemRequest>
{
    public AuditItemRequestValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.PhysicalQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Finding).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
public class AuditItemsRequestValidator : AbstractValidator<AuditItemsRequest>
{
    public AuditItemsRequestValidator()
    {
        RuleFor(x => x.Items).NotNull();
        RuleForEach(x => x.Items).SetValidator(new AuditItemRequestValidator());
        RuleFor(x => x.Items).Must(items => items.Select(i => i.AssetId).Distinct().Count() == items.Count)
            .When(x => x.Items != null).WithMessage("An asset can appear only once per audit.");
    }
}
