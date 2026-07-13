namespace UniPM.Api.Features.Auth;

public static class AuthRoleCatalog
{
    public const string Admin = "Admin";
    public const string Gsd = "GSD";
    public const string Inspector = "Inspector";
    public const string Supervisor = "Supervisor";
    public const string DepartmentHead = "DepartmentHead";

    public static IReadOnlyList<string> Values { get; } =
        [Admin, Gsd, Inspector, Supervisor, DepartmentHead];
}
