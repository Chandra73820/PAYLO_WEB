using System.ComponentModel.DataAnnotations;

namespace PAYLO_WEB.Models
{

    public class AdminLoginViewModel
    {
        [Required]
        public string? DistributorId { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string? PassKey { get; set; }

        public string? UserType { get; set; }
        public string? LoginFrom { get; set; }
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
        public string? SessionID { get; set; }
    }

    public class ApiSettings
    {
        public string ApiKeyHeaderName { get; set; }
        public string GlobalApiKey { get; set; }
    }
    public class AssociateLoginViewModel
    {
        [Required]
        public string DistributorId { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        public string? UserType { get; set; }
        public string? LoginFrom { get; set; }
        public string? Action { get; set; }
    }
    public class FranchiseLoginViewModel
    {
        [Required]
        public string FranchiseId { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        public string? UserType { get; set; }
        public string? LoginFrom { get; set; }
        public string? Action { get; set; }
    }
}

