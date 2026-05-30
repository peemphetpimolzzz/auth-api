namespace AuthApi.Api.Domain;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string User = "User";
}

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
