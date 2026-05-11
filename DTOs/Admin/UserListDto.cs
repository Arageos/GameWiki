namespace GameWiki.DTOs.Admin
{
    public class UserListDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string RoleName { get; set; }
        public bool IsBanned { get; set; }
    }
}