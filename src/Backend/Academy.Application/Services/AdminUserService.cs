using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class AdminUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public AdminUserService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);

        return users
            .OrderBy(x => x.FullName)
            .Select(x => new UserDto
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                Role = x.Role.ToString(),
                IsActive = x.IsActive
            })
            .ToList();
    }

    public async Task<UserDto> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role == UserRole.Owner)
            throw new InvalidOperationException("Owner accounts cannot be created here.");

        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = request.Role,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsActive = user.IsActive
        };
    }

    private static void EnsureManageable(User user)
    {
        if (user.Role == UserRole.Owner)
            throw new InvalidOperationException("Owner accounts are protected.");
    }

    public async Task ResetPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) throw new InvalidOperationException("Password must be at least 8 characters.");
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken) ?? throw new InvalidOperationException("User not found.");
        EnsureManageable(user); user.PasswordHash = _passwordHasher.Hash(password); user.UpdatedAtUtc = DateTimeOffset.UtcNow; _userRepository.Update(user); await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken) ?? throw new InvalidOperationException("User not found.");
        EnsureManageable(user); _userRepository.Remove(user); await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        EnsureManageable(user);

        user.IsActive = isActive;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}