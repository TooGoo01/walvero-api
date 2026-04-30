using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class DeviceToken : EntityBase, IEntity
    {
        public ApplicationUser? User { get; set; }
        /// <summary>
        /// AspNetUsers.Id-ə işarə edən FK. Guest istifadəçi üçün <c>null</c> olur
        /// (login olmamış istifadəçilərin də FCM bildirişlərini ala bilməsi üçün).
        /// Login zamanı eyni token yenidən qeydiyyatdan keçəndə UserId set olunur.
        /// </summary>
        public int? UserId { get; set; }
        public string Token { get; set; }
        public string Platform { get; set; } // IOS / ANDROID
    }
}
