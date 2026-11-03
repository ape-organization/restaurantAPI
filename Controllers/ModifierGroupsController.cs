using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyAPI.Models.RequestsModels;
using PharmacyAPI.Services.Interfaces;

namespace PharmacyAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModifierGroupsController : ControllerBase
    {
        private readonly IModifierGroupService _modifierGroupService;

        public ModifierGroupsController(
            IModifierGroupService modifierGroupService)
        {
            _modifierGroupService = modifierGroupService;
        }


        // ============================================================
        // GET: api/modifiergroups
        // ============================================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<ModifierGroupDto>>> GetModifierGroups(
            CancellationToken cancellationToken)
        {
            try
            {
                var groups = await _modifierGroupService
                    .GetModifierGroups(cancellationToken);

                return Ok(groups);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }


        // ============================================================
        // GET: api/modifiergroups/5
        // ============================================================

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<ActionResult<ModifierGroupDto>> GetModifierGroup(
            int id,
            CancellationToken cancellationToken)
        {
            try
            {
                var group = await _modifierGroupService
                    .GetModifierGroup(id, cancellationToken);

                if (group == null)
                    return NotFound();

                return Ok(group);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }


        // ============================================================
        // POST: api/modifiergroups
        // ============================================================

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ModifierGroupDto>> CreateModifierGroup(
            [FromBody] ModifierGroupDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var group = await _modifierGroupService
                    .CreateModifierGroup(dto, cancellationToken);

                return CreatedAtAction(
                    nameof(GetModifierGroup),
                    new { id = group.Id },
                    group);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }


        // ============================================================
        // PUT: api/modifiergroups/5
        // ============================================================

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateModifierGroup(
            int id,
            [FromBody] ModifierGroupDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _modifierGroupService
                    .UpdateModifierGroup(
                        id,
                        dto,
                        cancellationToken);

                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }


        // ============================================================
        // DELETE: api/modifiergroups/5
        // ============================================================

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteModifierGroup(
            int id,
            CancellationToken cancellationToken)
        {
            var deleted = await _modifierGroupService
                .DeleteModifierGroup(
                    id,
                    cancellationToken);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
