using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace UniPM.Api.Data;

internal static class DatabaseConstraintViolation
{
    internal static bool IsUniqueConstraint(Exception exception)
    {
        var sqlException = exception.GetBaseException() as SqlException;
        return sqlException?.Number is 2601 or 2627;
    }

    internal static bool IsDeadlock(Exception exception)
    {
        var sqlException = exception.GetBaseException() as SqlException;
        return sqlException?.Number == 1205;
    }
}
