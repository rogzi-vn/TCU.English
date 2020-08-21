﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TCU.English.Models
{
    public class UserTypeUser : BaseEntity
    {
        public int UserId { get; set; }
        public int UserTypeId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(UserTypeId))]
        public virtual UserType UserType { get; set; }

    }
}
