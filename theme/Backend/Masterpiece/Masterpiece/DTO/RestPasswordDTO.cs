﻿using System.ComponentModel.DataAnnotations;

namespace Masterpiece.DTO
{
    public class RestPasswordDTO
    {
        [Required]
        public string Password { get; set; } = null!;

        [Required]

        public string NewPassword { get; set; } = null!;
        [Required]

        public string ConfirmPassword { get; set; } = null!;
    }
}
