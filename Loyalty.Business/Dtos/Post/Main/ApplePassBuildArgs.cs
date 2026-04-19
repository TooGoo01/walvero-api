using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed record ApplePassBuildArgs(
      string SerialNumber,
      string AuthToken,
      string OrganizationName,
      string CustomerFullName,
      string BarcodeValue,
      decimal CurrentPoints,
      string? TierName,
      string? BackgroundColor,
      string? LogoText,
      string? IconPath,
      string? LogoPath,
      string? StripPath
  );
}
