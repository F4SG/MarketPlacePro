using System.ComponentModel.DataAnnotations;
using MarketPlacePro.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MarketPlacePro.Controllers;

internal static class Persistencia
{
    public static async Task<bool> GuardarAsync(Controller controller, MarketPlaceProContext context,
        Func<Task>? operacion = null)
    {
        try
        {
            if (operacion == null) await context.SaveChangesAsync();
            else await operacion();
            return true;
        }
        catch (ValidationException ex)
        {
            controller.ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            controller.ModelState.AddModelError(string.Empty, "El registro cambió o ya no existe. Vuelva al listado e intente nuevamente.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            controller.ModelState.AddModelError(string.Empty, "Ya existe un registro con esos datos. Revise los valores e intente nuevamente.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            controller.ModelState.AddModelError(string.Empty, "No se puede guardar la operación porque existen registros relacionados o un valor no permitido.");
        }
        return false;
    }
}
