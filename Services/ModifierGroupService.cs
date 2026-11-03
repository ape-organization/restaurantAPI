using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.RequestsModels;

namespace PharmacyAPI.Services.Interfaces;

public interface IModifierGroupService
{
    Task<List<ModifierGroupDto>> GetModifierGroups(
        CancellationToken cancellationToken = default);

    Task<ModifierGroupDto?> GetModifierGroup(
        int id,
        CancellationToken cancellationToken = default);

    Task<ModifierGroupDto> CreateModifierGroup(
        ModifierGroupDto dto,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateModifierGroup(
        int id,
        ModifierGroupDto dto,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteModifierGroup(
        int id,
        CancellationToken cancellationToken = default);
}

public sealed class ModifierGroupService : IModifierGroupService
{
    private readonly ShoesDbContext _context;

    public ModifierGroupService(ShoesDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // GET ALL ACTIVE GROUPS
    // ============================================================

    public async Task<List<ModifierGroupDto>> GetModifierGroups(
        CancellationToken cancellationToken = default)
    {
        return await _context.ModifierGroups
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.Id)
            .Select(g => new ModifierGroupDto
            {
                Id = g.Id,
                NameAr = g.NameAr,
                NameEn = g.NameEn,
                Options = g.Options
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.Id)
                    .Select(o => new ModifierOptionDto
                    {
                        Id = o.Id,
                        NameAr = o.NameAr,
                        NameEn = o.NameEn,
                        Price = o.Price,
                        IsAvailable = o.IsAvailable,
                        ExtraProductId = o.ExtraProductId
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    // ============================================================
    // GET BY ID
    // ============================================================

    public async Task<ModifierGroupDto?> GetModifierGroup(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        return await _context.ModifierGroups
            .AsNoTracking()
            .Where(g =>
                g.Id == id &&
                g.IsActive)
            .Select(g => new ModifierGroupDto
            {
                Id = g.Id,
                NameAr = g.NameAr,
                NameEn = g.NameEn,
                Options = g.Options
                    .Where(o => o.IsActive)
                    .OrderBy(o => o.Id)
                    .Select(o => new ModifierOptionDto
                    {
                        Id = o.Id,
                        NameAr = o.NameAr,
                        NameEn = o.NameEn,
                        Price = o.Price,
                        IsAvailable = o.IsAvailable,
                        ExtraProductId = o.ExtraProductId
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ============================================================
    // CREATE
    // ============================================================

    public async Task<ModifierGroupDto> CreateModifierGroup(
        ModifierGroupDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        Normalize(dto);
        Validate(dto);

        await ValidateExtraProducts(
            dto.Options,
            cancellationToken);

        var group = new ModifierGroup
        {
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            IsActive = true
        };

        foreach (var optionDto in dto.Options)
        {
            group.Options.Add(new ModifierOption
            {
                NameAr = optionDto.NameAr,
                NameEn = optionDto.NameEn,
                Price = optionDto.Price,
                IsAvailable = optionDto.IsAvailable,
                ExtraProductId = optionDto.ExtraProductId,
                IsActive = true
            });
        }

        _context.ModifierGroups.Add(group);

        await _context.SaveChangesAsync(cancellationToken);

        return (await GetModifierGroup(
                   group.Id,
                   cancellationToken))!;
    }

    // ============================================================
    // UPDATE
    // ============================================================
    //
    // The sent option list replaces the current options:
    //
    // - Option with a known Id   -> updated
    // - Option without an Id     -> added
    // - Existing option not sent -> soft deleted
    //
    // Options are soft deleted so old orders keep their link.
    //
    public async Task<bool> UpdateModifierGroup(
        int id,
        ModifierGroupDto dto,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        ArgumentNullException.ThrowIfNull(dto);

        Normalize(dto);
        Validate(dto);

        await ValidateExtraProducts(
            dto.Options,
            cancellationToken);

        var group = await _context.ModifierGroups
            .Include(g => g.Options
                .Where(o => o.IsActive))
            .FirstOrDefaultAsync(
                g => g.Id == id &&
                     g.IsActive,
                cancellationToken);

        if (group is null)
            return false;

        group.NameAr = dto.NameAr;
        group.NameEn = dto.NameEn;

        var existingById =
            group.Options.ToDictionary(o => o.Id);

        var keptIds = new HashSet<int>();

        foreach (var optionDto in dto.Options)
        {
            // ----------------------------------------------------
            // EXISTING OPTION
            // ----------------------------------------------------

            if (optionDto.Id is > 0)
            {
                if (!existingById.TryGetValue(
                        optionDto.Id.Value,
                        out var existing))
                {
                    throw new ArgumentException(
                        $"Modifier option {optionDto.Id} does not belong to this group.");
                }

                existing.NameAr = optionDto.NameAr;
                existing.NameEn = optionDto.NameEn;
                existing.Price = optionDto.Price;
                existing.IsAvailable = optionDto.IsAvailable;
                existing.ExtraProductId = optionDto.ExtraProductId;

                keptIds.Add(existing.Id);

                continue;
            }

            // ----------------------------------------------------
            // NEW OPTION
            // ----------------------------------------------------

            group.Options.Add(new ModifierOption
            {
                NameAr = optionDto.NameAr,
                NameEn = optionDto.NameEn,
                Price = optionDto.Price,
                IsAvailable = optionDto.IsAvailable,
                ExtraProductId = optionDto.ExtraProductId,
                IsActive = true
            });
        }

        // --------------------------------------------------------
        // SOFT DELETE REMOVED OPTIONS
        // --------------------------------------------------------

        foreach (var existing in existingById.Values)
        {
            if (!keptIds.Contains(existing.Id))
            {
                existing.IsActive = false;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ============================================================
    // SOFT DELETE
    // ============================================================

    public async Task<bool> DeleteModifierGroup(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return false;

        var affected = await _context.ModifierGroups
            .Where(g =>
                g.Id == id &&
                g.IsActive)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    g => g.IsActive,
                    false),
                cancellationToken);

        return affected > 0;
    }

    // ============================================================
    // VALIDATION
    // ============================================================

    private static void Normalize(
        ModifierGroupDto dto)
    {
        dto.NameAr = dto.NameAr?.Trim() ?? string.Empty;
        dto.NameEn = dto.NameEn?.Trim() ?? string.Empty;
        dto.Options ??= [];

        foreach (var option in dto.Options)
        {
            option.NameAr = option.NameAr?.Trim() ?? string.Empty;
            option.NameEn = option.NameEn?.Trim() ?? string.Empty;

            if (option.ExtraProductId is <= 0)
                option.ExtraProductId = null;
        }
    }

    private static void Validate(
        ModifierGroupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NameEn))
        {
            throw new ArgumentException(
                "English group name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.NameAr))
        {
            throw new ArgumentException(
                "Arabic group name is required.");
        }

        if (dto.Options.Count == 0)
        {
            throw new ArgumentException(
                "A modifier group must have at least one option.");
        }

        foreach (var option in dto.Options)
        {
            if (string.IsNullOrWhiteSpace(option.NameEn) ||
                string.IsNullOrWhiteSpace(option.NameAr))
            {
                throw new ArgumentException(
                    "Each option needs an English and Arabic name.");
            }

            if (option.Price < 0)
            {
                throw new ArgumentException(
                    "Option price cannot be negative.");
            }
        }

        var duplicateIds = dto.Options
            .Where(o => o.Id is > 0)
            .GroupBy(o => o.Id)
            .Any(g => g.Count() > 1);

        if (duplicateIds)
        {
            throw new ArgumentException(
                "Duplicate modifier option detected.");
        }
    }

    private async Task ValidateExtraProducts(
        List<ModifierOptionDto> options,
        CancellationToken cancellationToken)
    {
        var productIds = options
            .Where(o => o.ExtraProductId.HasValue)
            .Select(o => o.ExtraProductId!.Value)
            .Distinct()
            .ToList();

        if (productIds.Count == 0)
            return;

        var found = await _context.Products
            .AsNoTracking()
            .CountAsync(
                p => productIds.Contains(p.Id) &&
                     !p.IsDeleted,
                cancellationToken);

        if (found != productIds.Count)
        {
            throw new ArgumentException(
                "One or more linked products do not exist.");
        }
    }
}
