using System.Data;

namespace Inventory.Application.Services;

public class PasswordResetService(IAppDbContext db, IPasswordHasher hasher, ICurrentUser current)
{
    private const string SubmissionMessage =
        "If the submitted details match an active Member account, the request has been recorded for Admin review.";

    public async Task<PasswordResetSubmissionDto> SubmitAsync(
        PasswordResetSubmissionRequest request,
        CancellationToken ct)
    {
        var nik = request.Nik.Trim();
        var email = request.Email.Trim();
        var account = await db.Users.AsNoTracking()
            .Include(user => user.Employee)
            .FirstOrDefaultAsync(
                user => user.IsActive &&
                        user.Employee.EmployeeNik == nik &&
                        user.EmployeeEmail == email,
                ct);

        if (account is not null &&
            Same(account.EmployeeName, request.Name) &&
            Same(account.Employee.EmployeePhone, request.Phone) &&
            await HasMemberRoleAsync(account.UserId, ct))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);
            var hasPendingRequest = await db.PasswordResetRequests.AnyAsync(
                item => item.UserId == account.UserId &&
                        item.Status == PasswordResetRequestStatus.Pending,
                ct);
            if (!hasPendingRequest)
            {
                db.PasswordResetRequests.Add(new TrPasswordResetRequest
                {
                    RequestId = $"PRR-{Guid.NewGuid():N}",
                    UserId = account.UserId,
                    MemberName = account.EmployeeName,
                    Nik = account.Employee.EmployeeNik,
                    Email = account.EmployeeEmail,
                    Phone = account.Employee.EmployeePhone!,
                    RequestDate = DateTime.UtcNow,
                    Status = PasswordResetRequestStatus.Pending
                });
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }

        return new PasswordResetSubmissionDto(SubmissionMessage);
    }

    public async Task<PasswordResetRequestsDto> ListAsync(CancellationToken ct)
    {
        var requests = await db.PasswordResetRequests.AsNoTracking()
            .Include(item => item.ProcessedBy)
            .OrderByDescending(item => item.RequestDate)
            .Take(100)
            .ToListAsync(ct);
        return new PasswordResetRequestsDto(requests.Select(ToDto).ToList());
    }

    public async Task<PasswordResetRequestDto> ProcessAsync(
        int id,
        PasswordResetProcessRequest request,
        CancellationToken ct)
    {
        var adminId = current.UserId
            ?? throw new AppException(401, "Unauthorized", "Authenticated user is required.");
        var resetRequest = await db.PasswordResetRequests
            .Include(item => item.User)
            .Include(item => item.ProcessedBy)
            .FirstOrDefaultAsync(item => item.PasswordResetRequestId == id, ct)
            ?? throw new NotFoundException($"Password reset request {id}");

        if (resetRequest.Status != PasswordResetRequestStatus.Pending)
            throw new ConflictException("This request has already been resolved.");

        if (request.Action == "process")
        {
            if (!resetRequest.User.IsActive || !await HasMemberRoleAsync(resetRequest.UserId, ct))
                throw new ConflictException("Only an active Member account can be reset through this request.");
            if (string.IsNullOrWhiteSpace(request.NewPassword) ||
                request.NewPassword.Length < 8 ||
                request.NewPassword.Length > 100)
                throw new ValidationFailedException("newPassword", "The new password must contain 8 to 100 characters.");
            resetRequest.User.EmployeePassword = hasher.Hash(request.NewPassword);
            resetRequest.User.UpdatedAt = DateTime.UtcNow;
            resetRequest.Status = PasswordResetRequestStatus.Processed;
            resetRequest.RejectionReason = null;
        }
        else if (request.Action == "reject")
        {
            resetRequest.Status = PasswordResetRequestStatus.Rejected;
            resetRequest.RejectionReason = string.IsNullOrWhiteSpace(request.RejectionReason)
                ? "Information could not be verified."
                : request.RejectionReason.Trim();
        }
        else
        {
            throw new ValidationFailedException("action", "Action must be 'process' or 'reject'.");
        }

        resetRequest.ProcessedByUserId = adminId;
        resetRequest.ProcessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (resetRequest.ProcessedBy is null)
            await db.Entry(resetRequest).Reference(item => item.ProcessedBy).LoadAsync(ct);
        return ToDto(resetRequest);
    }

    private Task<bool> HasMemberRoleAsync(int userId, CancellationToken ct) =>
        (from modelRole in db.ModelRoles
         join role in db.Roles on modelRole.RoleId equals role.RoleId
         where modelRole.ModelId == userId && role.RoleName == RoleNames.Member
         select modelRole.ModelId).AnyAsync(ct);

    private static bool Same(string? left, string right) =>
        string.Equals(left?.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static PasswordResetRequestDto ToDto(TrPasswordResetRequest request) =>
        new(
            request.PasswordResetRequestId,
            request.RequestId,
            request.UserId,
            request.MemberName,
            request.Nik,
            request.Email,
            request.Phone,
            request.RequestDate,
            request.Status.ToString(),
            request.ProcessedBy?.EmployeeName,
            request.ProcessedAt,
            request.RejectionReason);
}
